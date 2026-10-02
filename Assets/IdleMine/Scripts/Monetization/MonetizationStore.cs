using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace IdleMine
{
    [Serializable]
    public class MonetizationData
    {
        public int version = 1;
        /// <summary>Cached entitlement so the perk works offline and before the store answers. The store
        /// re-confirms (or restores) it on every launch.</summary>
        public bool foremanPass;
        public double playSeconds;
        public string adDay = "";
        public int adsToday;
        public long lifetimeAds;
        public List<string> cooldownKeys = new List<string>();
        public List<long> cooldownUntilTicks = new List<long>();
    }

    /// <summary>
    /// Ad caps, cooldowns and the Foreman Pass flag, kept in their own file so wiping or rewriting the
    /// game save can never take away something the player paid for.
    /// </summary>
    public static class MonetizationStore
    {
        const string FileName = "idlemine_monetization.json";

        static MonetizationData _data;

        static string FullPath { get { return Path.Combine(Application.persistentDataPath, FileName); } }

        public static MonetizationData Data
        {
            get
            {
                if (_data == null) _data = Load() ?? new MonetizationData();
                return _data;
            }
        }

        public static long CooldownUntil(string key)
        {
            int i = Data.cooldownKeys.IndexOf(key);
            return i >= 0 ? Data.cooldownUntilTicks[i] : 0;
        }

        public static void SetCooldownUntil(string key, long utcTicks)
        {
            int i = Data.cooldownKeys.IndexOf(key);
            if (i >= 0) Data.cooldownUntilTicks[i] = utcTicks;
            else { Data.cooldownKeys.Add(key); Data.cooldownUntilTicks.Add(utcTicks); }
        }

        public static void Save()
        {
            if (_data == null) return;
            try
            {
                string tmp = FullPath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(_data));
                if (File.Exists(FullPath)) File.Delete(FullPath);
                File.Move(tmp, FullPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IdleMine] Monetization save failed: " + e.Message);
            }
        }

        static MonetizationData Load()
        {
            try
            {
                if (!File.Exists(FullPath)) return null;
                return JsonUtility.FromJson<MonetizationData>(File.ReadAllText(FullPath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IdleMine] Monetization load failed, starting fresh: " + e.Message);
                return null;
            }
        }

        public static void Delete()
        {
            _data = null;
            try { if (File.Exists(FullPath)) File.Delete(FullPath); }
            catch (Exception e) { Debug.LogWarning("[IdleMine] Monetization delete failed: " + e.Message); }
        }
    }
}
