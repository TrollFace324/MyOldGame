using System;
using System.Drawing;
using System.IO;

namespace MyGame.Services
{
    public static class ImageAssetService
    {
        public static Image? LoadImage(params string[] relativePathParts)
        {
            var filePath = FindFile(Path.Combine(relativePathParts));

            if (filePath == null)
                return null;

            using var loadedImage = Image.FromFile(filePath);
            return new Bitmap(loadedImage);
        }

        private static string? FindFile(string relativePath)
        {
            var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

            while (currentDirectory != null)
            {
                var candidate = Path.Combine(currentDirectory.FullName, relativePath);

                if (File.Exists(candidate))
                    return candidate;

                currentDirectory = currentDirectory.Parent;
            }

            var workingDirectoryCandidate = Path.Combine(Environment.CurrentDirectory, relativePath);
            return File.Exists(workingDirectoryCandidate) ? workingDirectoryCandidate : null;
        }
    }
}
