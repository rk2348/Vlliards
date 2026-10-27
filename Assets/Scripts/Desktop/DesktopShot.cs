namespace Vlliards.Desktop
{
    public sealed class DesktopShot
    {
        public DesktopBall Owner { get; }
        public DesktopShotRules Rules { get; }
        public float StartedAt { get; }
        public bool Completed { get; set; }
        public bool PenaltyApplied { get; set; }

        public DesktopShot(DesktopBall owner, int firstBall, float startedAt)
        {
            Owner = owner;
            Rules = new DesktopShotRules(firstBall);
            StartedAt = startedAt;
        }
    }
}
