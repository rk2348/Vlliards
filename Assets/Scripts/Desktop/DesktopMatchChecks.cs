using System.Collections;
using UnityEngine;

namespace Vlliards.Desktop
{
    public sealed partial class DesktopGame
    {
        private IEnumerator SmokeTest()
        {
            testing = true;
            StartCoroutine(SmokeDeadline());
            yield return VerifyPocketShots();
            Restart();
            var first = balls.Find(ball => ball.Number == 1);
            var nine = balls.Find(ball => ball.Number == 9);
            first.ResetAt(new Vector3(0, .3f, -2));
            player.ResetAt(new Vector3(0, .3f, -4));
            opponent.ResetAt(new Vector3(3, .3f, -4));
            bool launched = TryShoot(player, Vector3.forward, 10);
            bool cooldown = !TryShoot(player, Vector3.right, 10) && shots == 1;
            bool independent = TryShoot(opponent, Vector3.forward, 5);
            yield return new WaitForSeconds(.3f);
            bool collision = first.Body.linearVelocity.magnitude > 1 && player.Shot.Rules.FirstBallHit == 1 && first.Shot == player.Shot;
            UpdateBallView();
            bool ballView = !view.orthographic && (view.transform.position - player.transform.position - Vector3.up * .12f).sqrMagnitude < .0001f;
            lookYaw = 90;
            bool relativeMovement = Vector3.Dot(ViewRelativeMovement(Vector2.up), Vector3.right) > .99f;
            player.transform.rotation = Quaternion.Euler(43, 73, 21);
            UpdateBallView();
            bool noRoll = Quaternion.Angle(view.transform.rotation, Quaternion.Euler(lookPitch, lookYaw, 0)) < .01f;
            lookYaw = 0;
            bool artwork = originalCloth && loungeEnvironment != null && artworkBalls.Count >= 9 && transform.Find("Original bar lounge - architecture and furniture") != null;

            // A pocket followed by a scratch must not become a victory.
            player.Shot.Rules.RecordObjectBallPocketed(9);
            nine.Pot(); remaining--;
            player.ResetAt(pockets[0] + Vector3.up * .28f);
            CheckPockets();
            var scratchedShot = player.Shot;
            bool freeze = player.Frozen && !TryShoot(player, Vector3.forward, 12) && scratches == 1;
            yield return new WaitForSeconds(.36f);
            freeze &= !player.Potted && player.Frozen;
            Vector3 frozenPosition = player.Body.position;
            yield return new WaitForSeconds(.2f);
            bool immobilized = (player.Body.position - frozenPosition).sqrMagnitude < .0001f;
            FinishShot(scratchedShot);
            bool illegalNine = !finished && !nine.Potted && scratchedShot.Rules.EvaluateFoul() == ShotFoul.Scratch;
            float beforePause = player.FreezeRemaining;
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.15f);
            bool pauseSafe = Mathf.Abs(player.FreezeRemaining - beforePause) < .02f;
            Time.timeScale = 1;
            // Keep the test deterministic while the penalty clock expires.
            opponent.Freeze(10);
            yield return new WaitForSeconds(foulFreezeSeconds + .1f);
            bool recovered = !player.Frozen && player.CanLaunch;

            Restart();
            var wrong = BeginShot(opponent);
            wrong.Rules.RecordFirstContact(2);
            ApplyFoul(wrong, ShotFoul.WrongFirstBall);
            bool cpuPenalty = opponent.Frozen && !TryShoot(opponent, Vector3.forward, 10) && !player.Frozen;

            Restart();
            var legal = BeginShot(player);
            legal.Rules.RecordFirstContact(1);
            nine.Shot = legal;
            nine.ResetAt(pockets[0] + Vector3.up * .28f);
            CheckPockets();
            FinishShot(legal);
            bool win = finished && winner == player;
            Restart();
            var cpuWinShot = BeginShot(opponent);
            cpuWinShot.Rules.RecordFirstContact(1);
            nine.Shot = cpuWinShot;
            nine.ResetAt(pockets[0] + Vector3.up * .28f);
            CheckPockets();
            FinishShot(cpuWinShot);
            bool cpuWin = finished && winner == opponent;
            Restart();
            bool restart = !finished && remaining == 9 && player.CanLaunch && opponent.CanLaunch && activeShots.Count == 0;

            yield return VerifyShotMotion();
            bool cpuShoots = motionCpuShoots;
            bool success = shotMotionPassed && pocketShotsPassed == 6 && launched && cooldown && independent && collision && freeze && immobilized && illegalNine && pauseSafe && recovered && cpuPenalty && win && cpuWin && restart && cpuShoots && ballView && relativeMovement && noRoll && artwork;
            string report = $"DESKTOP_SMOKE {(success ? "PASS" : "FAIL")}: shotMotion={shotMotionPassed}, naturalPocketShots={pocketShotsPassed}/6, collision={collision}, cooldown={cooldown}, independent={independent}, freeze={freeze}, immobile={immobilized}, illegalNine={illegalNine}, pause={pauseSafe}, recovery={recovered}, cpuFoul={cpuPenalty}, win={win}, cpuWin={cpuWin}, restart={restart}, cpuShoots={cpuShoots}, ballView={ballView}, relativeMovement={relativeMovement}, noRoll={noRoll}, originalArtwork={artwork}";
            Debug.Log(report);
#if UNITY_EDITOR
            System.IO.File.WriteAllText("Temp/DesktopPlayModeResult.txt", report);
            // Batch tests exit directly, avoiding an idle editor holding the project lock.
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(success ? 0 : 1);
            else UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(success ? 0 : 1);
#endif
        }

        private int pocketShotsPassed;

        private IEnumerator VerifyPocketShots()
        {
            pocketShotsPassed = 0;
            var target = balls.Find(ball => ball.Number == 1);
            for (int i = 0; i < pockets.Count; i++)
            {
                Restart();
                foreach (var ball in balls) if (ball != player && ball != target) ball.gameObject.SetActive(false);
                Vector3 direction = i % 3 == 1 ? Vector3.right * Mathf.Sign(pockets[i].x) : new Vector3(pockets[i].x, 0, pockets[i].z - .119f).normalized;
                target.ResetAt(pockets[i] - direction * 1.5f + Vector3.up * .3f);
                player.ResetAt(pockets[i] - direction * 3 + Vector3.up * .3f);
                Physics.SyncTransforms();
                TryShoot(player, direction, 10);
                var shot = player.Shot;
                float deadline = Time.time + 3;
                while (!target.Potted && Time.time < deadline) yield return new WaitForFixedUpdate();
                bool legal = target.Potted && target.Shot == shot && shot.Rules.FirstBallHit == 1 && shot.Rules.ObjectBallPocketed;
                yield return new WaitForSeconds(.34f);
                if (legal && !target.gameObject.activeSelf) pocketShotsPassed++;
                else Debug.LogError("Natural cue-to-object pocket shot failed at pocket " + (i + 1));
            }
        }

        private IEnumerator SmokeDeadline()
        {
            yield return new WaitForSecondsRealtime(30);
            const string report = "DESKTOP_SMOKE FAIL: timed out.";
            Debug.LogError(report);
#if UNITY_EDITOR
            System.IO.File.WriteAllText("Temp/DesktopPlayModeResult.txt", report);
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(1);
            else UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(1);
#endif
        }
    }
}
