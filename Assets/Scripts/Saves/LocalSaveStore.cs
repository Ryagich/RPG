using System;
using System.IO;
using UnityEngine;

namespace Saves
{
    /// <summary>Owns the on-disk representation of the project's save snapshot.</summary>
    public sealed class LocalSaveStore
    {
        private const string FileName = "savegame.json";
        private bool isLoaded;

        public GameSaveData Data { get; private set; } = new();

        public void Load()
        {
            if (isLoaded)
            {
                return;
            }

            isLoaded = true;
            string path = GetPath();
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                Data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path)) ?? new GameSaveData();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read the local save file: {exception.Message}");
                Data = new GameSaveData();
            }
        }

        public void Replace(GameSaveData data)
        {
            Data = data ?? new GameSaveData();
        }

        public void Save()
        {
            Load();
            string path = GetPath();
            string temporaryPath = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(Data));
                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not write the local save file: {exception.Message}");
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static string GetPath() => Path.Combine(Application.persistentDataPath, FileName);
    }
}
