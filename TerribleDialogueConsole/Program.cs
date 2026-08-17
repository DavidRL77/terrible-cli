using System.CommandLine;
using TerribleDialogue;
using TerribleDialogue.Parser;
using TerribleDialogueConsole.SoundPlayer;

namespace TerribleDialogueConsole
{
    internal class Program
    {
        private static readonly Random random = new Random();
        private static Character activeCharacter;

        static int Main(string[] args)
        {
            Argument<FileInfo[]> fileArgument = new Argument<FileInfo[]>("files")
            {
                Description = "The terrible dialogue files to run.",
                CustomParser = result =>
                {
                    List<FileInfo> resultFiles = new List<FileInfo>();
                    foreach(var token in result.Tokens)
                    {
                        FileInfo file = new FileInfo(token.Value);

                        if(file.Name == "*") // Brute force wildcard expansion bc windows cmd is dumb
                        {
                            resultFiles.AddRange(file.Directory.GetFiles());
                        }
                        else
                        {
                            resultFiles.Add(file);
                        }
                    }

                    return resultFiles.ToArray();
                },
                Arity = ArgumentArity.OneOrMore
            };
            fileArgument.Validators.Add(result =>
            {
                FileInfo[] files = result.GetValueOrDefault<FileInfo[]>();
                foreach(FileInfo fileInfo in files)
                {
                    if(!fileInfo.Exists)
                    {
                        result.AddError($"File {fileInfo.FullName} not found.");
                    }
                }
            });

            RootCommand cmd = new RootCommand("Console application to run terrible dialogue files.")
            {
                fileArgument
            };

            cmd.SetAction(parseResult =>
            {
                FileInfo[] files = parseResult.GetValue(fileArgument);
                Run(files);
            });

            return cmd.Parse(args).Invoke();
        }

        private static void Run(FileInfo[] files)
        {
            // Loading libvlc takes a while
            Console.WriteLine("Loading LibVLC...");
            using(var musicPlayer = new LibVLCAudioPlayer())
            using(var sfxPlayer = new LibVLCAudioPlayer())
            {
                App app = new App(musicPlayer, sfxPlayer);
                List<Character> characters = new List<Character>();
                foreach(FileInfo file in files)
                {
                    DialogueEngine engine = new DialogueEngine(
                        TerribleDialogueParser.Parse(File.ReadAllText(file.FullName)),
                        random.Next
                    );

                    Character character = new Character(Path.GetFileNameWithoutExtension(file.Name), engine, file);
                    characters.Add(character);
                }

                while(true)
                {
                    Console.Clear();
                    Console.WriteLine("Type name to run.");
                    foreach(Character character in characters)
                    {
                        Console.WriteLine(character.Name);
                    }
                    Console.Write("> ");
                    string name = Console.ReadLine();

                    activeCharacter = characters.FirstOrDefault(c => c.Name == name);
                    if(activeCharacter != null)
                        app.Run(activeCharacter.Engine, activeCharacter.DialogueFile.DirectoryName);

                    activeCharacter = null;
                }
            }
        }
    }
}
