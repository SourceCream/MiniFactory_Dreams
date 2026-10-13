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
        private const int CurrentVersion = 2;
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

        public bool TryLoad(FactoryEconomy economy) => TryLoad(economy, out _);

        public bool TryLoad(FactoryEconomy economy, out long savedAtUtcTicks)
        {
            if (economy == null) throw new ArgumentNullException(nameof(economy));
            preserveBackup = true;
            if (TryRead(FilePath, economy, out savedAtUtcTicks))
            {
                preserveBackup = false;
                return true;
            }
            if (!TryRead(BackupPath, economy, out savedAtUtcTicks)) return false;

            reportWarning?.Invoke("Factory progress was recovered from the previous save.");
            return true;
        }

        public bool TrySave(FactoryEconomy economy, long savedAtUtcTicks = 0)
        {
            if (economy == null) throw new ArgumentNullException(nameof(economy));
            if (savedAtUtcTicks < 0 || savedAtUtcTicks > DateTime.MaxValue.Ticks)
                throw new ArgumentOutOfRangeException(nameof(savedAtUtcTicks));
            try
            {
                var save = new SaveData
                {
                    Version = CurrentVersion,
                    Progress = economy.CaptureProgress(),
                    SavedAtUtcTicks = savedAtUtcTicks
                };
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

        private bool TryRead(string path, FactoryEconomy economy, out long savedAtUtcTicks)
        {
            savedAtUtcTicks = 0;
            try
            {
                if (!File.Exists(path)) return false;
                SaveData save = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (save != null && (save.Version == 1 || save.Version == CurrentVersion) &&
                    save.SavedAtUtcTicks >= 0 && save.SavedAtUtcTicks <= DateTime.MaxValue.Ticks &&
                    economy.TryRestoreProgress(save.Progress))
                {
                    // Old saves keep player progress but have no reliable offline time to award.
                    savedAtUtcTicks = save.Version == 1 ? 0 : save.SavedAtUtcTicks;
                    return true;
                }
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
            public long SavedAtUtcTicks;
        }
    }
}
