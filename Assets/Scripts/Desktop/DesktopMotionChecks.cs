using System.Collections;
using UnityEngine;

namespace Vlliards.Desktop
{
    public sealed partial class DesktopGame
    {
        private bool shotMotionPassed, motionCpuShoots;

        private IEnumerator VerifyShotMotion()
        {
            Restart();
            testing = true;
            playerCue.Render(player, aim, true, .1f, Time.time, view);
            Vector3 readyTip = playerCue.TipPosition;
            playerCue.Render(player, aim, true, .9f, Time.time, view);
            bool drawBack = playerCue.Visible && playerCue.Preparing && Vector3.Dot(readyTip - playerCue.TipPosition, aim) > .15f;
            Vector3 drawnTip = playerCue.TipPosition;
            TryShoot(player, aim, 8);
            playerCue.Render(player, aim, false, 0, Time.time + .075f, view);
            bool thrust = playerCue.Visible && !playerCue.Preparing && Vector3.Dot(playerCue.TipPosition - drawnTip, aim) > .25f;
            bool originalCue = playerCue.UsesOriginalCue && cpuCue.UsesOriginalCue;
            bool nonPhysical = true;
            foreach (string name in new[] { "Player shot cue and hands", "CPU shot cue and hands" })
                foreach (var collider in transform.Find(name).GetComponentsInChildren<Collider>(true))
                    nonPhysical &= !collider.enabled;

            testing = false;
            cpuThinkAt = Time.time;
            UpdateCpu();
            UpdateShotMotion();
            bool warning = cpuWindingUp && opponent.Shot == null && opponent.CooldownRemaining == 0 && cpuCue.Visible && cpuCue.Preparing;
            testing = true;
            Time.timeScale = 0;
            Vector3 pausedTip = cpuCue.TipPosition;
            yield return new WaitForSecondsRealtime(.12f);
            UpdateShotMotion();
            bool pauseSafe = (cpuCue.TipPosition - pausedTip).sqrMagnitude < .000001f;
            Time.timeScale = 1;
            yield return new WaitForSeconds(cpuWindupSeconds + .02f);
            testing = false;
            UpdateCpu();
            UpdateShotMotion();
            motionCpuShoots = !cpuWindingUp && opponent.Shot != null && opponent.CooldownRemaining > 0 && player.CooldownRemaining > 0 && cpuCue.Visible && !cpuCue.Preparing;

            Restart();
            cpuThinkAt = Time.time;
            UpdateCpu();
            opponent.Freeze(5);
            UpdateCpu();
            UpdateShotMotion();
            bool cancel = !cpuWindingUp && !cpuCue.Visible && opponent.Shot == null;
            Restart();
            bool reset = !cpuWindingUp && !cpuCue.Visible && !playerCue.Visible;
            testing = true;
            shotMotionPassed = drawBack && thrust && originalCue && nonPhysical && warning && pauseSafe && motionCpuShoots && cancel && reset;
            Debug.Log($"DESKTOP_MOTION {(shotMotionPassed ? "PASS" : "FAIL")}: pullBack={drawBack}, thrust={thrust}, originalCue={originalCue}, noCollisions={nonPhysical}, cpuWarning={warning}, pause={pauseSafe}, delayedCpuShot={motionCpuShoots}, foulCancel={cancel}, restart={reset}");
        }

        private IEnumerator CaptureShotMotionPreview()
        {
            testing = true;
            Restart();
            player.ResetAt(new Vector3(0, .3f, -3));
            opponent.ResetAt(new Vector3(.75f, .3f, 0));
            charging = true;
            charge = .85f;
            cpuWindingUp = true;
            cpuWindupAt = Time.time - .65f;
            cpuAim = Vector3.left;
            UpdateBallView();
            UpdateShotMotion();
            yield return CapturePreview();
        }
    }
}
