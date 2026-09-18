using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BigWalkVRInstaller.Services
{
    public static class InstallerFileSystem
    {
        public static string ResolveInside(string rootPath, string relativePath)
        {
            var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(Path.Combine(root, Normalize(relativePath)));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"package tried to write outside the game folder: {relativePath}");
            return full;
        }

        public static string Normalize(string path) =>
            path.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);

        public static void DeleteRelative(string rootPath, string relativePath)
        {
            var path = ResolveInside(rootPath, relativePath);
            if (File.Exists(path)) File.Delete(path);
            PruneEmptyDirectories(rootPath, Path.GetDirectoryName(path));
        }

        public static void RemoveStaleFiles(string rootPath, IEnumerable<string> previousFiles, IEnumerable<string> currentFiles)
        {
            var current = new HashSet<string>(currentFiles.Select(Normalize), StringComparer.OrdinalIgnoreCase);
            foreach (var stale in previousFiles.Select(Normalize).Where(path => !current.Contains(path)))
                DeleteRelative(rootPath, stale);
        }

        static void PruneEmptyDirectories(string rootPath, string directory)
        {
            var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar);
            while (!string.IsNullOrEmpty(directory) && !directory.TrimEnd(Path.DirectorySeparatorChar).Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(directory) || Directory.EnumerateFileSystemEntries(directory).Any()) return;
                Directory.Delete(directory);
                directory = Path.GetDirectoryName(directory);
            }
        }
    }
}
