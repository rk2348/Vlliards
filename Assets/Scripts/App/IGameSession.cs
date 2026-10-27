using UnityEngine;

namespace Vlliards.App
{
    public struct RadarBlip
    {
        public Vector3 position;
        public int number;
        public bool visible;
    }

    public struct GameSnapshot
    {
        public bool finished, playerWon, practice;
        public float elapsed, playerCooldown, playerFreeze, cpuCooldown, cpuFreeze, cpuPreparation, charge;
        public bool charging, cpuAiming;
        public int nextTarget, shots, fouls, pocketed;
        public string notice;
        public RadarBlip[] balls;
    }

    public interface IGameSession
    {
        void BeginMatch(bool practice, CpuDifficulty difficulty);
        void SetPaused(bool paused);
        void ReturnToTitle();
        void ApplyPreferences(GamePreferences preferences);
        GameSnapshot CaptureSnapshot();
    }
}
