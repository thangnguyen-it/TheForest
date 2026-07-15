using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace TheForest.Persistence
{
    /// <summary>Versioned JSON slot storage with a temporary file and one-generation backup.</summary>
    public sealed class SaveFileStore
    {
        private const string SaveFolderName = "Saves";
        private readonly string _rootPath;

        public SaveFileStore(string rootPath = null)
        {
            _rootPath = string.IsNullOrWhiteSpace(rootPath)
                ? Path.Combine(Application.persistentDataPath, SaveFolderName)
                : rootPath;
        }

        public string RootPath => _rootPath;

        public bool Exists(string slot) => File.Exists(GetPath(slot));

        public void Write(string slot, SaveGameData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            Directory.CreateDirectory(_rootPath);
            string path = GetPath(slot);
            string temporaryPath = path + ".tmp";
            string backupPath = path + ".bak";
            string json = JsonUtility.ToJson(data, true);

            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));

            if (!File.Exists(path))
            {
                File.Move(temporaryPath, path);
                return;
            }

            try
            {
                File.Replace(temporaryPath, path, backupPath);
            }
            catch (PlatformNotSupportedException)
            {
                ReplacePortable(path, temporaryPath, backupPath);
            }
            catch (IOException)
            {
                ReplacePortable(path, temporaryPath, backupPath);
            }
        }

        public SaveGameData Read(string slot)
        {
            string path = GetPath(slot);
            try
            {
                return Parse(path);
            }
            catch (Exception primaryError) when (primaryError is IOException ||
                                                  primaryError is UnauthorizedAccessException ||
                                                  primaryError is InvalidDataException ||
                                                  primaryError is ArgumentException)
            {
                string backupPath = path + ".bak";
                if (!File.Exists(backupPath)) throw;

                Debug.LogWarning($"[SaveFileStore] Primary save is invalid; loading backup. {primaryError.Message}");
                return Parse(backupPath);
            }
        }

        public string GetPath(string slot)
        {
            return Path.Combine(_rootPath, SanitizeSlot(slot) + ".json");
        }

        private static SaveGameData Parse(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Save slot does not exist.", path);

            string json = File.ReadAllText(path, Encoding.UTF8);
            SaveGameData data = JsonUtility.FromJson<SaveGameData>(json);
            if (data == null) throw new InvalidDataException("Save file contains no data.");
            if (data.schemaVersion <= 0 || data.schemaVersion > SaveGameData.CurrentSchemaVersion)
                throw new InvalidDataException($"Unsupported save schema {data.schemaVersion}.");
            UpgradeToCurrentSchema(data);
            return data;
        }

        private static void UpgradeToCurrentSchema(SaveGameData data)
        {
            if (data.schemaVersion >= SaveGameData.CurrentSchemaVersion) return;

            if (data.schemaVersion == 1)
            {
                if (data.players != null)
                {
                    foreach (PlayerSaveData player in data.players)
                    {
                        if (player != null && string.IsNullOrEmpty(player.playerId)) player.playerId = "local";
                    }
                }
                data.schemaVersion = 2;
            }
        }

        private static void ReplacePortable(string path, string temporaryPath, string backupPath)
        {
            File.Copy(path, backupPath, true);
            File.Delete(path);
            File.Move(temporaryPath, path);
        }

        private static string SanitizeSlot(string slot)
        {
            string value = string.IsNullOrWhiteSpace(slot) ? "autosave" : slot.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value;
        }
    }
}
