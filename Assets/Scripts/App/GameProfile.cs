using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vlliards.App
{
    public enum CpuDifficulty { Easy, Normal, Hard }
    public enum GameScreen { Title, MatchSetup, Countdown, Playing, Paused, Results, Options, Help, Records, Credits, Confirm }

    [Serializable]
    public sealed class GamePreferences
    {
        public int version = 1;
        public float masterVolume = .8f, musicVolume = .3f, effectsVolume = .7f;
        public float sensitivity = .12f, fieldOfView = 80;
        public bool invertY, showGuide = true, showRadar = true, reduceMotion;
        public int graphicsQuality = 1, frameRate = 60;
        public CpuDifficulty difficulty = CpuDifficulty.Normal;
        public bool hasReadHelp;

        public GamePreferences Clone() => JsonUtility.FromJson<GamePreferences>(JsonUtility.ToJson(this));
        public void Validate()
        {
            masterVolume = Safe(masterVolume, 0, 1, .8f);
            musicVolume = Safe(musicVolume, 0, 1, .3f);
            effectsVolume = Safe(effectsVolume, 0, 1, .7f);
            sensitivity = Safe(sensitivity, .03f, .3f, .12f);
            fieldOfView = Safe(fieldOfView, 65, 95, 80);
            graphicsQuality = Mathf.Clamp(graphicsQuality, 0, 2);
            if (frameRate != 30 && frameRate != 60 && frameRate != 90 && frameRate != 120) frameRate = 60;
            difficulty = (CpuDifficulty)Mathf.Clamp((int)difficulty, 0, 2);
            version = 1;
        }
        private static float Safe(float value, float min, float max, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
    }

    [Serializable]
    public sealed class MatchResult
    {
        public string id, playedAtUtc;
        public bool won, practice;
        public CpuDifficulty difficulty;
        public float seconds;
        public int shots, fouls, pockets;
    }

    [Serializable]
    public sealed class GameRecords
    {
        public int matches, wins, practiceClears, totalShots, totalFouls;
        public float bestWinSeconds;
        public List<MatchResult> recent = new List<MatchResult>();
    }

    public interface IProfileStorage
    {
        string Read(string key);
        void Write(string key, string value);
    }

    public sealed class PlayerProfileStorage : IProfileStorage
    {
        public string Read(string key) => PlayerPrefs.GetString(key, "");
        public void Write(string key, string value) { PlayerPrefs.SetString(key, value); PlayerPrefs.Save(); }
    }

    public sealed class MemoryProfileStorage : IProfileStorage
    {
        private readonly Dictionary<string, string> data = new Dictionary<string, string>();
        public string Read(string key) => data.TryGetValue(key, out var value) ? value : "";
        public void Write(string key, string value) => data[key] = value;
    }

    public sealed class GameProfile
    {
        public const string PreferencesKey = "Vlliards.Preferences.v1";
        public const string RecordsKey = "Vlliards.Records.v1";
        private readonly IProfileStorage storage;
        public GamePreferences Preferences { get; private set; }
        public GameRecords Records { get; private set; }

        public GameProfile(IProfileStorage storage)
        {
            this.storage = storage;
            Preferences = new GamePreferences();
            try { if (!string.IsNullOrEmpty(storage.Read(PreferencesKey))) JsonUtility.FromJsonOverwrite(storage.Read(PreferencesKey), Preferences); }
            catch (ArgumentException) { Preferences = new GamePreferences(); }
            Preferences.Validate();
            Records = new GameRecords();
            try { if (!string.IsNullOrEmpty(storage.Read(RecordsKey))) JsonUtility.FromJsonOverwrite(storage.Read(RecordsKey), Records); }
            catch (ArgumentException) { Records = new GameRecords(); }
            if (Records.recent == null) Records.recent = new List<MatchResult>();
            Records.matches = Mathf.Max(0, Records.matches);
            Records.wins = Mathf.Clamp(Records.wins, 0, Records.matches);
        }

        public void SavePreferences(GamePreferences preferences)
        {
            Preferences = preferences.Clone();
            Preferences.Validate();
            storage.Write(PreferencesKey, JsonUtility.ToJson(Preferences));
        }

        public bool CommitResult(MatchResult result)
        {
            if (result == null || string.IsNullOrEmpty(result.id) || Records.recent.Exists(item => item.id == result.id)) return false;
            result.playedAtUtc = DateTime.UtcNow.ToString("O");
            Records.recent.Insert(0, result);
            if (Records.recent.Count > 20) Records.recent.RemoveAt(20);
            if (result.practice) { if (result.won) Records.practiceClears++; }
            else
            {
                Records.matches++;
                if (result.won)
                {
                    Records.wins++;
                    if (Records.bestWinSeconds <= 0 || result.seconds < Records.bestWinSeconds) Records.bestWinSeconds = result.seconds;
                }
            }
            Records.totalShots += result.shots;
            Records.totalFouls += result.fouls;
            storage.Write(RecordsKey, JsonUtility.ToJson(Records));
            return true;
        }

        public void ResetRecords() { Records = new GameRecords(); storage.Write(RecordsKey, JsonUtility.ToJson(Records)); }
    }
}
