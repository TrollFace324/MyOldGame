using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;

namespace MyGame.Services
{
    public sealed class GameAudioService : IDisposable
    {
        private readonly List<MediaPlayer> activeEffects = [];
        private readonly MediaPlayer? backgroundMusicPlayer;
        private bool disposed;

        public GameAudioService()
        {
            var backgroundMusicPath = FindSoundPath("background-music");

            if (backgroundMusicPath == null)
                return;

            backgroundMusicPlayer = new MediaPlayer
            {
                Volume = 0.35
            };
            backgroundMusicPlayer.Open(new Uri(backgroundMusicPath));
            backgroundMusicPlayer.MediaEnded += OnBackgroundMusicEnded;
        }

        public void PlayBackgroundMusic()
        {
            if (disposed || backgroundMusicPlayer == null)
                return;

            backgroundMusicPlayer.Position = TimeSpan.Zero;
            backgroundMusicPlayer.Play();
        }

        public void PlayShooting()
        {
            PlayEffect("shooting");
        }

        public void PlayTankHit()
        {
            PlayEffect("shooting-metal", "shooting-metall");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            backgroundMusicPlayer?.Close();

            foreach (var effectPlayer in activeEffects.ToArray())
                effectPlayer.Close();

            activeEffects.Clear();
        }

        private void OnBackgroundMusicEnded(object? sender, EventArgs e)
        {
            if (disposed || backgroundMusicPlayer == null)
                return;

            backgroundMusicPlayer.Position = TimeSpan.Zero;
            backgroundMusicPlayer.Play();
        }

        private void PlayEffect(params string[] soundNames)
        {
            if (disposed)
                return;

            var soundPath = FindFirstSoundPath(soundNames);

            if (soundPath == null)
                return;

            var effectPlayer = new MediaPlayer
            {
                Volume = 0.8
            };
            EventHandler endedHandler = null!;
            EventHandler<ExceptionEventArgs> failedHandler = null!;
            endedHandler = (_, _) => DisposeEffectPlayer(effectPlayer, endedHandler, failedHandler);
            failedHandler = (_, _) => DisposeEffectPlayer(effectPlayer, endedHandler, failedHandler);

            effectPlayer.MediaEnded += endedHandler;
            effectPlayer.MediaFailed += failedHandler;
            activeEffects.Add(effectPlayer);
            effectPlayer.Open(new Uri(soundPath));
            effectPlayer.Play();
        }

        private void DisposeEffectPlayer(
            MediaPlayer effectPlayer,
            EventHandler endedHandler,
            EventHandler<ExceptionEventArgs> failedHandler
        )
        {
            effectPlayer.MediaEnded -= endedHandler;
            effectPlayer.MediaFailed -= failedHandler;
            activeEffects.Remove(effectPlayer);
            effectPlayer.Close();
        }

        private static string? FindFirstSoundPath(IEnumerable<string> soundNames)
        {
            foreach (var soundName in soundNames)
            {
                var soundPath = FindSoundPath(soundName);

                if (soundPath != null)
                    return soundPath;
            }

            return null;
        }

        private static string? FindSoundPath(string baseName)
        {
            var soundsDirectory = FindSoundsDirectory();

            if (soundsDirectory == null)
                return null;

            var exactPath = Path.Combine(soundsDirectory, baseName + ".mp3");

            if (File.Exists(exactPath))
                return exactPath;
            

            var candidates = Directory.GetFiles(soundsDirectory, baseName + "*" + ".mp3");
            Array.Sort(candidates, StringComparer.OrdinalIgnoreCase);

            if (candidates.Length > 0)
                return candidates[0];

            return null;
        }

        private static string? FindSoundsDirectory()
        {
            var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

            while (currentDirectory != null)
            {
                var candidate = Path.Combine(currentDirectory.FullName, "sounds");

                if (Directory.Exists(candidate))
                    return candidate;

                currentDirectory = currentDirectory.Parent;
            }

            var workingDirectoryCandidate = Path.Combine(Environment.CurrentDirectory, "sounds");
            return Directory.Exists(workingDirectoryCandidate) ? workingDirectoryCandidate : null;
        }
    }
}
