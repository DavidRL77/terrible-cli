using System.CommandLine;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using TerribleDialogue;
using TerribleDialogue.Data;
using TerribleDialogueConsole.SoundPlayer;
using TerribleDialogueConsole.View;
using ConsoleView.View;
using ConsoleView.Input;

namespace TerribleDialogueConsole
{
    internal class App
    {
        private readonly ISoundPlayer musicPlayer;
        private readonly ISoundPlayer sfxPlayer;
        private readonly ViewStack viewStack = new ViewStack();
        private readonly DialoguePanel dialoguePanel;
        private readonly ConsoleKeybind[] keybinds;
        private readonly IInputHandler<ConsoleKeyInfo> inputHandler;

        private readonly DialogueManager dialogueManager;
        private DialogueEngine currentEngine;

        // Used to locate Music and Sfx files
        private string baseDirectory;

        public App(ISoundPlayer musicPlayer, ISoundPlayer sfxPlayer)
        {
            this.musicPlayer = musicPlayer;
            this.sfxPlayer = sfxPlayer;
            
            baseDirectory = AppContext.BaseDirectory;
            keybinds = [
                new(ConsoleKey.S, ConsoleModifiers.Alt, JumpSet),
                new(ConsoleKey.N, ConsoleModifiers.Alt, JumpNode),
                new(ConsoleKey.Escape, ConsoleModifiers.None, GoBack),
                new(ConsoleKey.Q, ConsoleModifiers.Alt, Quit),
                new(ConsoleKey.F1, ConsoleModifiers.None, ShowKeybinds)
                ];

            inputHandler = new KeybindConsoleInputHandler(true, keybinds);
            dialoguePanel = new DialoguePanel(AdvanceDialogue);

            dialogueManager = new DialogueManager();

            dialogueManager.OnLine += DialogueManager_OnLine;
            dialogueManager.OnChoices += DialogueManager_OnChoices;
            dialogueManager.OnStop += DialogueManager_OnStop;
            dialogueManager.OnEnd += DialogueManager_OnEnd;
            dialogueManager.AddCallHandler("play", PlayCallHandler);
            dialogueManager.AddCallHandler("stop", StopCallHandler);
            dialogueManager.AddCallHandler("screen", ScreenCallHandler);
            dialogueManager.AddCallHandler("wait", WaitCallHandler);
        }

        public void Run(DialogueEngine engine, string baseDirectory)
        {
            this.baseDirectory = baseDirectory;

            Console.Clear();
            Console.OutputEncoding = Encoding.UTF8;

            if(engine.IsDialogueOver)
            {
                Console.Clear();
                Console.Write($"Dialogue is over.");
                Console.ReadLine();
                Console.Clear();
                return;
            }

            currentEngine = engine;

            viewStack.Push(dialoguePanel);
            // BUG: The second time the same dialogue is opened, both START and ON LINE will be called, duplicating the lines and prompts in the dialogue panel
            dialogueManager.BeginDialogue(engine);

            while(viewStack.Count > 0)
            {
                if(viewStack.CurrentView is IInputListener<ConsoleKeyInfo> listener 
                && listener.CanHandleInput() 
                && inputHandler.TryGetInput(out ConsoleKeyInfo input))
                {
                    listener.OnInput(input);
                }
                    
            }
        }

        private void JumpSet()
        {
            string[] sets = currentEngine.DialogueObject.Sets.Keys.ToArray();
            ShowJumpMenu(sets, "Set to jump to:", currentEngine.SetSet);
        }

        private void JumpNode()
        {
            string[] nodes = currentEngine.DialogueObject.Sets[currentEngine.CurrentSetId].Nodes.Keys.ToArray(); // holy shit
            ShowJumpMenu(nodes, "Node to jump to:", currentEngine.SetNode);
        }

        private void ShowJumpMenu(string[] options, string prompt, Action<string> callback)
        {
            viewStack.Push(
                new ConsolePanel(
                    new ConsoleText(prompt, ConsoleColor.White, ConsoleColor.Black),
                    new ConsoleMenu<string>(options, (self, index, option) =>
                        {
                            callback.Invoke(option);
                            ResetView();
                            AdvanceDialogue();
                        }, ConsoleColor.Gray)
                )
            );
        }

        private void GoBack()
        {
            if(viewStack.Count > 1)
                viewStack.Pop();
            else
                dialogueManager.EndDialogue();
        }

        private void Quit()
        {
            dialogueManager.EndDialogue();
        }

        private void ShowKeybinds()
        {
            ConsolePanel panel = new ConsolePanel();
            foreach(ConsoleKeybind keybind in keybinds)
            {
                if(keybind.Modifiers != ConsoleModifiers.None)
                {
                    panel.AddElement(new ConsoleText(Enum.GetName(keybind.Modifiers) + "+", ConsoleColor.Cyan, ConsoleColor.Black, false));
                }
                panel.AddElement(new ConsoleText(Enum.GetName(keybind.Key) + ": ", ConsoleColor.Cyan, ConsoleColor.Black, false));
                panel.AddElement(new ConsoleText(keybind.Action.GetMethodInfo().Name, ConsoleColor.White, ConsoleColor.Black, true));
            }

            panel.AddElement(ConsoleKeyPrompt.UntilKeyPressed(ConsoleKey.Enter, GoBack));

            viewStack.Push(panel);
        }

        private void ResetView()
        {
            viewStack.Clear();
            dialoguePanel.Clear();
            viewStack.Push(dialoguePanel);
        }

        private void AdvanceDialogue()
        {
            if(!dialogueManager.InDialogue)
                return;

            dialogueManager.Next();
        }

        #region DIALOGUE_CALLBACKS
        private void DialogueManager_OnLine(LineData lineData)
        {
            // TEMP FIX
            if(!dialoguePanel.Visible)
                return;

            bool newline = lineData.Tags.GetValueOrDefault("display", "newline") == "newline";
            bool block = lineData.Tags.GetValueOrDefault("block", "yes") == "yes";
            string[] splitLines = lineData.Text.Split("<br>");

            ConsoleColor color = ColorByName(lineData.Tags.GetValueOrDefault("color", "white"));

            dialoguePanel.AddText(new ConsoleText(lineData.Text, color, Console.BackgroundColor, newline));   

            if(block)
                dialoguePanel.ShowPrompt();
            else 
                AdvanceDialogue();
        }


        private void DialogueManager_OnChoices(string[] choices)
        {
            dialoguePanel.AddText(new ConsoleMenu<string>(choices, (self, index, option) =>
            {
                dialoguePanel.RemoveText(self);
                dialoguePanel.AddText(new ConsoleText("> " + option, ConsoleColor.Gray));
                dialogueManager.AddChoice(index);
                AdvanceDialogue();
            }, ConsoleColor.Gray));
        }

        private void DialogueManager_OnStop()
        {
            dialogueManager.EndDialogue();
        }

        private void DialogueManager_OnEnd()
        {
            musicPlayer.Stop();
            currentEngine = null;
            viewStack.Clear();
            dialoguePanel.Clear();
        }
        #endregion

        #region DIALOGUE_CALL_HANDLERS
        private void ScreenCallHandler(CallData callData)
        {
            string action = callData.Args.Get<string>(0);

            if(action == "clear")
                dialoguePanel.Clear();
        }

        private void PlayCallHandler(CallData callData)
        {
            string channel = callData.Args.GetOrDefault<string>(0);
            string audioFile = callData.Args.GetOrDefault<string>(1);

            if(channel == null || audioFile == null)
                return;

            ISoundPlayer soundPlayer;
            bool loop;
            string folder;
            switch(channel)
            {
                case "sfx":
                    folder = "Sfx";
                    soundPlayer = sfxPlayer;
                    loop = false;
                    break;
                case "music":
                    folder = "Music";
                    soundPlayer = musicPlayer;
                    loop = true;
                    break;
                default:
                    return;
            }


            string filePath = Path.Combine(baseDirectory, folder, audioFile);
            if(!File.Exists(filePath))
            {
                Console.Error.WriteLine($"File not found: '{filePath}'");
                return;
            }

            if(loop)
                soundPlayer.PlayLooping(filePath);
            else
                soundPlayer.Play(filePath);
        }

        private void StopCallHandler(CallData callData)
        {
            string channel = callData.Args.GetOrDefault<string>(0);

            if(channel == null)
                return;

            ISoundPlayer soundPlayer = channel switch
            {
                "sfx" => sfxPlayer,
                "music" => musicPlayer,
                _ => null
            };
            soundPlayer.Stop();
        }

        private void WaitCallHandler(CallData callData)
        {
            float seconds = callData.Args.GetOrDefault<float>(0);
            Thread.Sleep((int)(seconds * 1000));
        }
        #endregion


        private static ConsoleColor ColorByName(string name)
        {
            if(Enum.TryParse(name, true, out ConsoleColor color))
            {
                return color;
            }
            else
            {
                return ConsoleColor.White;
            }
        }
    }
}
