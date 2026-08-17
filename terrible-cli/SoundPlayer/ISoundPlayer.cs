namespace TerribleDialogueConsole.SoundPlayer
{
    internal interface ISoundPlayer
    {
        public float Volume { get; set; }

        public void Play(string path);
        public void PlayLooping(string path);
        public void Stop();
    }
}