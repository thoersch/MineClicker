using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace IdleMine
{
    [Serializable]
    public class LayerSave
    {
        public double progress;
        public int miners;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 2; // 2: Paragon progress is per run (runMoney), plus Paragon perks
        public double money;
        public double lifetimeOre;
        public double lifetimeMoney;
        public long totalTaps;
        public int paragonLevel;
        public long boostEndUtcTicks;
        public double runMoney;
        public bool deepCoreUnlocked;   // permanent; Deep Core node ids (prefix "d") share unlockedNodes
        public List<string> paragonPerks = new List<string>();
        public List<LayerSave> layers = new List<LayerSave>();
        public List<string> unlockedNodes = new List<string>();
        public long lastSaveUtcTicks;
    }

    /// <summary>
    /// Writes JSON to Application.persistentDataPath. Writes go to a temp file first and are then
    /// swapped in, so a crash mid-write can't corrupt the save.
    /// </summary>
    public static class SaveSystem
    {
        const string FileName = "idlemine_save.json";

        static string FullPath { get { return Path.Combine(Application.persistentDataPath, FileName); } }

        public static void Save(SaveData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data);
                string tmp = FullPath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(FullPath)) File.Delete(FullPath);
                File.Move(tmp, FullPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IdleMine] Save failed: " + e.Message);
            }
        }

        public static SaveData Load()
        {
            try
            {
                if (!File.Exists(FullPath)) return null;
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(FullPath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IdleMine] Load failed, starting fresh: " + e.Message);
                return null;
            }
        }

        public static void Delete()
        {
            try { if (File.Exists(FullPath)) File.Delete(FullPath); }
            catch (Exception e) { Debug.LogWarning("[IdleMine] Delete failed: " + e.Message); }
        }
    }
}
