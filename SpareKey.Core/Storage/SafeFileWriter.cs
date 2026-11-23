using System.Globalization;

namespace SpareKey.Core.Storage
{
    /// <summary>
    /// Saves a file so that a crash or power cut can never leave you with
    /// a half-written vault
    ///
    ///   1. write everything to vault.sparekey.tmp and force it to disk
    ///   2. copy the current vault.sparekey to a backup
    ///   3. rename .tmp over vault.sparekey
    ///   4. delete the oldest backups
    ///
    /// If it crashes during 1, the real file is untouched.
    /// If it crashes after 2, there's a backup anyway.
    /// </summary>
    internal static class SafeFileWriter
    {
        // UTC so backups sort correctly by name, even across daylight saving
        // changes. Example: vault.2026-11-23_20-15-42Z.sparekey.bak
        private const string TimestampFormat = "yyyy-MM-dd_HH-mm-ss'Z'";

        public static void Write(string filePath, byte[] fileBytes, int backupsToKeep)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path can't be empty.", nameof(filePath));
            }
            if (fileBytes == null)
            {
                throw new ArgumentNullException(nameof(fileBytes));
            }

            string fullPath = Path.GetFullPath(filePath);
            string tempPath = fullPath + ".tmp";

            // Step 1: write the temp file
            using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(fileBytes, 0, fileBytes.Length);

                // Flush(true) pushes the bytes all the way to the disk, not just
                // into the operating system's memory cache. Without it, a power
                // cut right after the rename could leave an empty file.
                stream.Flush(true);
            }

            // Step 2: back up the current file 
            if (backupsToKeep > 0 && File.Exists(fullPath))
            {
                File.Copy(fullPath, MakeBackupPath(fullPath), true);
            }

            // Step 3: replace the real file with the temp file.
            File.Move(tempPath, fullPath, true);

            // Step 4: keep only the newest backups
            DeleteOldBackups(fullPath, backupsToKeep);
        }

        // vault.sparekey -> vault.2026-11-23_20-15-42Z.sparekey.bak
        // Ends in ".sparekey.bak" on purpose, so .gitignore catches it.
        private static string MakeBackupPath(string fullPath)
        {
            string folder = Path.GetDirectoryName(fullPath);
            if (folder == null)
                throw new ArgumentException("File path has no folder: " + fullPath);
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
            string extension = Path.GetExtension(fullPath);
            string timestamp = DateTime.UtcNow.ToString(TimestampFormat, CultureInfo.InvariantCulture);

            return Path.Combine(folder, nameWithoutExtension + "." + timestamp + extension + ".bak");
        }

        private static void DeleteOldBackups(string fullPath, int backupsToKeep)
        {
            string folder = Path.GetDirectoryName(fullPath);
            if (folder == null)
                throw new ArgumentException("File path has no folder: " + fullPath);
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
            string extension = Path.GetExtension(fullPath);

            string start = nameWithoutExtension + ".";
            string end = extension + ".bak";
            int expectedLength = start.Length + TimestampFormat.Replace("'", "").Length + end.Length;

            // Find our backups.
            List<string> backups = new List<string>();
            foreach (string path in Directory.GetFiles(folder, start + "*" + end))
            {
                string name = Path.GetFileName(path);
                if (name.Length == expectedLength)
                {
                    backups.Add(path);
                }
            }

            // Names contain the time, so sorting by name sorts oldest first
            backups.Sort(StringComparer.Ordinal);

            int numberToDelete = backups.Count - backupsToKeep;
            for (int i = 0; i < numberToDelete; i++)
            {
                File.Delete(backups[i]);
            }
        }
    }
}