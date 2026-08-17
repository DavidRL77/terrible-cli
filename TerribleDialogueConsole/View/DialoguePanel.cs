using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConsoleView.View;

namespace TerribleDialogueConsole.View
{
    /// <summary>
    /// A panel consisting of two elements: list of text, and a prompt that when shown, blocks until the user presses enter,
    /// then removes itself from the panel. 
    /// </summary>
    internal class DialoguePanel : AbstractViewElement, IInputListener<ConsoleKeyInfo>
    {        
        private readonly ConsolePanel mainPanel;
        private readonly ConsolePanel textPanel;
        private readonly ConsoleKeyPrompt prompt;

        public DialoguePanel(Action onPromptComplete)
        {
            textPanel = new ConsolePanel();
            prompt = ConsoleKeyPrompt.UntilKeyPressed(ConsoleKey.Enter, () =>
            {
                mainPanel.RemoveElement(prompt);
                onPromptComplete.Invoke();
            });

            mainPanel = new ConsolePanel(textPanel);
        }

        protected override void OnHide()
        {
            mainPanel.Hide();
        }

        protected override void OnShow()
        {
            mainPanel.Show();
        }

        public void AddText(IViewElement element)
        {
            textPanel.AddElement(element);
        }

        public void RemoveText(IViewElement element)
        {
            textPanel.RemoveElement(element);
        }

        public void ShowPrompt()
        {
            if(!mainPanel.ContainsElement(prompt))
                mainPanel.AddElement(prompt);
        }


        public void Clear()
        {
            textPanel.ClearElements();
        }

        public bool CanHandleInput() => mainPanel.CanHandleInput();
        
        public bool OnInput(ConsoleKeyInfo input) => mainPanel.OnInput(input);
    }
}
