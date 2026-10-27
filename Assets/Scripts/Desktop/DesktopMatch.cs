using System.Collections.Generic;
using UnityEngine;

namespace Vlliards.Desktop
{
    public sealed partial class DesktopGame
    {
        [SerializeField, Min(0)] private float foulFreezeSeconds = 5;
        private DesktopBall opponent, winner;
        private readonly List<DesktopShot> activeShots = new List<DesktopShot>();
        private readonly HashSet<long> contactsThisStep = new HashSet<long>();
        private float contactStep = -1, cpuThinkAt;
        private bool testing;

        private void RestartMatch()
        {
            matchRevision++;
            Time.timeScale = 1;
            paused = finished = charging = false;
            winner = null;
            activeShots.Clear();
            contactsThisStep.Clear();
            remaining = 9;
            shots = scratches = 0;
            elapsed = charge = 0;
            foreach (var ball in balls) ball.ResetForMatch();
            player.ResetAt(new Vector3(-1.2f, .3f, -4));
            opponent.ResetAt(new Vector3(1.2f, .3f, -4));
            int index = 2;
            for (int row = 0; row < 5; row++)
            {
                int count = row < 3 ? row + 1 : 5 - row;
                for (int column = 0; column < count; column++)
                    balls[index++].ResetAt(new Vector3((column - (count - 1) * .5f) * .57f, .3f, 1.7f + row * .51f));
            }
            cpuThinkAt = Time.time + 1.5f;
            lookYaw = 0; lookPitch = 6; aim = Vector3.forward;
            SetMouseCapture(true);
            UpdateBallView();
            notice = "Hit the lowest ball first. Legal nine wins. Fouls freeze you for 5 seconds.";
            noticeUntil = Time.unscaledTime + 7;
        }

        private int MinimumTarget()
        {
            int minimum = 10;
            foreach (var ball in balls)
                if (!ball.IsCue && !ball.Potted) minimum = Mathf.Min(minimum, ball.Number);
            return minimum == 10 ? 0 : minimum;
        }

        private bool TryShoot(DesktopBall cue, Vector3 direction, float speed)
        {
            if (paused || finished || !cue.TryLaunch(direction, speed, launchCooldown)) return false;
            BeginShot(cue);
            if (cue == player) shots++;
            return true;
        }

        private DesktopShot BeginShot(DesktopBall cue)
        {
            var shot = new DesktopShot(cue, MinimumTarget(), Time.time);
            cue.Shot = shot;
            activeShots.Add(shot);
            return shot;
        }

        private void OnBallContact(DesktopBall ball, Collision contact)
        {
            if (paused || finished) return;
            var other = contact.collider.GetComponent<DesktopBall>();
            if (other == null)
            {
                // The horizontal surface is not a cushion.
                if (ball.Shot != null && !ball.Shot.Completed && contact.contactCount > 0 && Mathf.Abs(contact.GetContact(0).normal.y) < .5f)
                    ball.Shot.Rules.RecordRailContact();
                return;
            }
            if (contactStep != Time.fixedTime) { contactStep = Time.fixedTime; contactsThisStep.Clear(); }
            int a = ball.GetInstanceID(), b = other.GetInstanceID();
            long pair = ((long)(uint)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
            if (!contactsThisStep.Add(pair)) return;
            RecordCueContact(ball, other);
            RecordCueContact(other, ball);
            if (ball.IsCue && !other.IsCue) TransferShot(ball, other);
            else if (other.IsCue && !ball.IsCue) TransferShot(other, ball);
            else if (!ball.IsCue && !other.IsCue)
            {
                // Most recent incoming momentum determines credit through combinations.
                if (ball.IncomingVelocity.sqrMagnitude > other.IncomingVelocity.sqrMagnitude) TransferShot(ball, other);
                else TransferShot(other, ball);
            }
        }

        private void RecordCueContact(DesktopBall cue, DesktopBall other)
        {
            if (!cue.IsCue || cue.Frozen) return;
            var shot = cue.Shot;
            if (shot == null || shot.Completed)
            {
                // Steering into a ball counts as a shot too; it cannot bypass cooldowns.
                if (!cue.CanLaunch || cue.IncomingVelocity.sqrMagnitude < .01f) return;
                cue.ConsumeCooldown(launchCooldown);
                shot = BeginShot(cue);
            }
            shot.Rules.RecordFirstContact(other.Number);
            if (shot.Rules.FirstBallHit != shot.Rules.RequiredFirstBall) ApplyFoul(shot, ShotFoul.WrongFirstBall);
        }

        private static void TransferShot(DesktopBall source, DesktopBall target)
        {
            if (source.Shot != null && !source.Shot.Completed) target.Shot = source.Shot;
        }

        private void ApplyFoul(DesktopShot shot, ShotFoul reason)
        {
            if (shot.PenaltyApplied) return;
            shot.PenaltyApplied = true;
            shot.Owner.Freeze(foulFreezeSeconds);
            if (shot.Owner == player) { charging = false; charge = 0; }
            notice = (shot.Owner == player ? "YOU" : "CPU") + " FOUL: " + reason + " / STOP 5s";
            noticeUntil = Time.unscaledTime + 4;
        }

        private void CheckPockets()
        {
            foreach (var ball in balls)
            {
                if (ball.Potted) continue;
                Vector3 position = ball.transform.position;
                bool outside = Mathf.Abs(position.x) > 4.8f || Mathf.Abs(position.z) > 7.8f;
                bool inPocket = IsInPocket(position, out var pocketCentre);
                if (!outside && !inPocket) continue;
                if (ball.IsCue)
                {
                    var shot = ball.Shot;
                    if (shot == null || shot.Completed) shot = BeginShot(ball);
                    shot.Rules.RecordScratch();
                    Vector3 preferred = ball == player ? new Vector3(-1.2f, .3f, -4) : new Vector3(1.2f, .3f, -4);
                    if (inPocket)
                    {
                        ball.Pot(pocketCentre);
                        StartCoroutine(ReturnScratchedCue(ball, preferred, matchRevision));
                    }
                    else Respawn(ball, preferred);
                    ApplyFoul(shot, ShotFoul.Scratch);
                    if (ball == player) scratches++;
                }
                else if (outside)
                {
                    if (ball.Shot != null && !ball.Shot.Completed)
                    {
                        ball.Shot.Rules.RecordScratch();
                        ApplyFoul(ball.Shot, ShotFoul.Scratch);
                    }
                    Respawn(ball, new Vector3(0, .3f, 2));
                }
                else
                {
                    ball.Pot(pocketCentre);
                    remaining--;
                    if (ball.Shot != null && !ball.Shot.Completed) ball.Shot.Rules.RecordObjectBallPocketed(ball.Number);
                    else if (ball.Number == 9) { Respawn(ball, new Vector3(0, .3f, 2)); remaining++; }
                }
            }
        }

        private void Respawn(DesktopBall ball, Vector3 preferred)
        {
            for (int attempt = 0; attempt < 78; attempt++)
            {
                Vector3 candidate = attempt == 0 ? preferred : new Vector3(-3 + (attempt - 1) % 7, .3f, -5 + (attempt - 1) / 7);
                bool free = true;
                foreach (var other in balls)
                    if (other != ball && !other.Potted && (other.transform.position - candidate).sqrMagnitude < .65f * .65f) { free = false; break; }
                if (!free) continue;
                ball.ResetAt(candidate);
                return;
            }
        }

        private void ResolveShots()
        {
            for (int i = activeShots.Count - 1; i >= 0; i--)
            {
                var shot = activeShots[i];
                if (Time.time - shot.StartedAt < .35f) continue;
                bool moving = false;
                foreach (var ball in balls)
                    if (ball.Shot == shot && !ball.Potted && ball.Body.linearVelocity.sqrMagnitude > .025f) { moving = true; break; }
                if (moving) continue;
                FinishShot(shot);
                activeShots.RemoveAt(i);
                if (finished) break;
            }
        }

        private void FinishShot(DesktopShot shot)
        {
            if (shot.Completed) return;
            shot.Completed = true;
            ShotFoul foul = shot.Rules.EvaluateFoul();
            if (foul != ShotFoul.None) ApplyFoul(shot, foul);
            if (!shot.Rules.NinePocketed) return;
            if (shot.Rules.IsLegalNine)
            {
                winner = shot.Owner;
                finished = true;
                charging = false;
                SetMouseCapture(false);
                foreach (var ball in balls)
                    if (!ball.Potted) ball.Freeze(999999);
            }
            else
            {
                var nine = balls.Find(ball => ball.Number == 9);
                if (nine.Potted) { Respawn(nine, new Vector3(0, .3f, 2)); nine.Shot = null; remaining++; }
            }
        }

        private void UpdateCpu()
        {
            if (testing || !opponent.CanLaunch || Time.time < cpuThinkAt) return;
            var target = balls.Find(ball => !ball.IsCue && !ball.Potted && ball.Number == MinimumTarget());
            if (target == null) return;
            Vector3 targetPosition = target.transform.position;
            Vector3 destination = targetPosition;
            float bestAlignment = -2;
            foreach (var pocket in pockets)
            {
                Vector3 towardsPocket = pocket - targetPosition;
                towardsPocket.y = 0;
                Vector3 ghost = targetPosition - towardsPocket.normalized * .55f;
                Vector3 approach = ghost - opponent.transform.position;
                approach.y = 0;
                float alignment = Vector3.Dot(approach.normalized, towardsPocket.normalized);
                if (Mathf.Abs(ghost.x) < 3.5f && Mathf.Abs(ghost.z) < 6.5f && alignment > bestAlignment)
                { bestAlignment = alignment; destination = ghost; }
            }
            Vector3 direction = destination - opponent.transform.position;
            direction.y = 0;
            // Small deterministic error keeps the CPU fallible, with exactly the same cooldown and fouls.
            direction = Quaternion.Euler(0, Mathf.Sin(Time.time * 1.7f) * 1.5f, 0) * direction;
            if (TryShoot(opponent, direction, Mathf.Clamp(direction.magnitude * 1.6f, 5, 14)))
                cpuThinkAt = Time.time + launchCooldown + .6f;
        }

        private static string BallStatus(DesktopBall cue)
        {
            if (cue.Frozen) return "FOUL STOP  " + cue.FreezeRemaining.ToString("0.0") + "s";
            if (!cue.CanLaunch) return "COOLDOWN  " + cue.CooldownRemaining.ToString("0.0") + "s";
            return "LAUNCH READY";
        }
    }
}
