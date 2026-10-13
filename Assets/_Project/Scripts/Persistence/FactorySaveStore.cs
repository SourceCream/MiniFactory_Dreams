using System;
using System.IO;
using System.Text;
using MiniFactory.Economy;
using UnityEngine;

namespace MiniFactory.Persistence
{
    /// <summary>Versioned local JSON with a previous-save backup. Call TryLoad before playing.</summary>
    public sealed class FactorySaveStore
    {
        private const int CurrentVersion = 1;
        private readonly string directory;
        private readonly Action<string> reportWarning;
        private bool preserveBackup = true;
        private bool writeFailureReported;

        public string FilePath { get; }
        private string BackupPath => FilePath + ".bak";
        private string TemporaryPath => FilePath + ".tmp";

        public FactorySaveStore(string directory, Action<string> reportWarning = null)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("A save directory is required.", nameof(directory));
            this.directory = directory;
            this.reportWarning = reportWarning;
            FilePath = Path.Combine(directory, "factory-save.json");
        }

        public bool TryLoad(FactoryEconomy economy)
        {
            if (economy == null) throw new ArgumentNullException(nameof(economy));
            preserveBackup = true;
            if (TryRead(FilePath, economy))
            {
                preserveBackup = false;
                return true;
            }
            if (!TryRead(BackupPath, economy)) return false;

            reportWarning?.Invoke("Factory progress was recovered from the previous save.");
            return true;
        }

        public bool TrySave(FactoryEconomy economy)
        {
            if (economy == null) throw new ArgumentNullException(nameof(economy));
            try
            {
                var save = new SaveData { Version = CurrentVersion, Progress = economy.CaptureProgress() };
                string json = JsonUtility.ToJson(save, true);
                Directory.CreateDirectory(directory);
                using (var stream = new FileStream(TemporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                // Write completely before touching the last save. Portable same-directory renames
                // leave a readable backup even if the app stops between the two moves.
                if (File.Exists(FilePath))
                {
                    if (preserveBackup)
                        File.Delete(FilePath);
                    else
                    {
                        File.Delete(BackupPath);
                        File.Move(FilePath, BackupPath);
                        preserveBackup = true;
                    }
                }
                File.Move(TemporaryPath, FilePath);
                preserveBackup = false;
                writeFailureReported = false;
                return true;
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                if (!writeFailureReported)
                    reportWarning?.Invoke("Could not save factory progress: " + exception.Message);
                writeFailureReported = true;
                return false;
            }
        }

        private bool TryRead(string path, FactoryEconomy economy)
        {
            try
            {
                if (!File.Exists(path)) return false;
                SaveData save = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (save != null && save.Version == CurrentVersion && economy.TryRestoreProgress(save.Progress))
                    return true;
                reportWarning?.Invoke("Ignored invalid or unsupported factory save: " + path);
            }
            catch (Exception exception) when (IsFileError(exception) || exception is ArgumentException)
            {
                reportWarning?.Invoke("Could not read factory save: " + exception.Message);
            }
            return false;
        }

        private static bool IsFileError(Exception exception) =>
            exception is IOException || exception is UnauthorizedAccessException ||
            exception is System.Security.SecurityException;

        [Serializable]
        private sealed class SaveData
        {
            public int Version;
            public FactoryProgress Progress;
        }
    }
}
