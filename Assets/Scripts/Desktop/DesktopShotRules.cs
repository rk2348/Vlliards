namespace Vlliards.Desktop
{
    public enum ShotFoul
    {
        None,
        Scratch,
        NoObjectBallContact,
        WrongFirstBall,
        NoRailOrPocket
    }

    // One instance per shot, allowing both players' shots to be judged independently.
    // The caller supplies contacts and pockets attributed to this shot by the simulation.
    public sealed class DesktopShotRules
    {
        public int RequiredFirstBall { get; }
        public int? FirstBallHit { get; private set; }
        public bool Scratched { get; private set; }
        public bool RailAfterContact { get; private set; }
        public bool ObjectBallPocketed { get; private set; }
        public bool NinePocketed { get; private set; }

        public DesktopShotRules(int requiredFirstBall)
        {
            RequiredFirstBall = requiredFirstBall;
        }

        public void RecordFirstContact(int ballNumber)
        {
            if (!FirstBallHit.HasValue) FirstBallHit = ballNumber;
        }

        public void RecordRailContact()
        {
            if (FirstBallHit.HasValue) RailAfterContact = true;
        }

        public void RecordObjectBallPocketed(int ballNumber)
        {
            if (ballNumber <= 0) return;
            ObjectBallPocketed = true;
            if (ballNumber == 9) NinePocketed = true;
        }

        public void RecordScratch() => Scratched = true;

        public ShotFoul EvaluateFoul()
        {
            if (Scratched) return ShotFoul.Scratch;
            if (!FirstBallHit.HasValue) return ShotFoul.NoObjectBallContact;
            if (RequiredFirstBall <= 0 || FirstBallHit.Value != RequiredFirstBall) return ShotFoul.WrongFirstBall;
            if (!RailAfterContact && !ObjectBallPocketed) return ShotFoul.NoRailOrPocket;
            return ShotFoul.None;
        }

        public bool IsLegalNine => NinePocketed && EvaluateFoul() == ShotFoul.None;
    }
}
