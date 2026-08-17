using TerribleDialogue;

namespace TerribleDialogueConsole
{
    internal record Character
    {
        public string Name { get; }
        public DialogueEngine Engine { get; }

        public FileInfo DialogueFile { get; }

        public Character(string name, DialogueEngine engine, FileInfo dialogueFile)
        {
            Name = name;
            Engine = engine;
            DialogueFile = dialogueFile;
        }
    }
}
