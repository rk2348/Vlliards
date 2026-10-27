using UnityEngine;

namespace Vlliards.Desktop
{
    public sealed partial class DesktopGame
    {
        [SerializeField, Min(.35f)] private float cpuWindupSeconds = .9f;
        private MeshFilter cueArtwork;
        private CueStrokeVisual playerCue, cpuCue;
        private bool cpuWindingUp;
        private float cpuWindupAt;
        private Vector3 cpuAim = Vector3.forward;
        private float cpuPower;

        private void CreateShotMotion()
        {
            var wood = Material(new Color(.35f, .18f, .055f));
            var glove = Material(new Color(.85f, .88f, .92f));
            var sleeve = Material(new Color(.04f, .065f, .09f));
            var signal = new Material(guideShader != null ? guideShader : Shader.Find("Universal Render Pipeline/Unlit"));
            signal.color = new Color(1, .58f, .12f);
            playerCue = new CueStrokeVisual(transform, true, cueArtwork, wood, glove, sleeve, signal);
            cpuCue = new CueStrokeVisual(transform, false, cueArtwork, wood, glove, sleeve, signal);
        }

        private void ResetShotMotion()
        {
            cpuWindingUp = false;
            playerCue?.Reset();
            cpuCue?.Reset();
        }

        private void UpdateShotMotion()
        {
            playerCue?.Render(player, aim, charging && player.CanLaunch, charge, Time.time, view);
            float preparation = cpuWindingUp ? Mathf.Clamp01((Time.time - cpuWindupAt) / cpuWindupSeconds) : 0;
            cpuCue?.Render(opponent, cpuAim, cpuWindingUp && opponent.CanLaunch, preparation, Time.time, view);
        }

        private void ShowStrike(DesktopBall cue, Vector3 direction)
        {
            (cue == player ? playerCue : cpuCue)?.Strike(cue.transform.position, direction, Time.time);
        }
    }
}
