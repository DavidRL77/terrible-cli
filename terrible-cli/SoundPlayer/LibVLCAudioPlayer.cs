using LibVLCSharp.Shared;
using System.Runtime.InteropServices;

namespace TerribleDialogueConsole.SoundPlayer
{
    internal class LibVLCAudioPlayer : ISoundPlayer, IDisposable
    {
        public float Volume { get => player.Volume; set => player.Volume = (int)value; }

        private readonly LibVLC libVLC;
        private readonly MediaPlayer player;

        public LibVLCAudioPlayer()
        {
        	string[] args;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
            && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) //LINUX
            {
                args = ["--aout=alsa"];
            } 
            else
            {
                args = [];
            }
            libVLC = new LibVLC(args);
            player = new MediaPlayer(libVLC);

            //libVLC.Log += (obj, e) => { }; // Disable stderr output
        }

        public void Play(string path)
        {
            PlayInternal(new Media(libVLC, new Uri(path)));
        }

        public void PlayLooping(string path)
        {
            PlayInternal(new Media(libVLC, new Uri(path), ":input-repeat=9999"));
        }

        private void PlayInternal(Media media)
        {
            Stop();
            player.Media?.Dispose();

            player.Media = media;
            player.Play();
        }

        public void Stop()
        {
            player.Stop();
        }

        public void Dispose()
        {
            Stop();

            player.Media?.Dispose();
            player.Dispose();
            libVLC.Dispose();
        }
    }
}
