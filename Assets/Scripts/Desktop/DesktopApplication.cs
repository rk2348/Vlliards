using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vlliards.App;

namespace Vlliards.Desktop
{
    public sealed partial class DesktopGame : IGameSession
    {
        [SerializeField] private TMP_FontAsset uiFont;
        private GameShell shell;
        private GamePreferences appliedPreferences;
        private bool titleCamera, practiceMode;
        private CpuDifficulty activeDifficulty = CpuDifficulty.Normal;
        private int playerFouls, playerPocketed;
        private float baseCpuWindup, previousListenerVolume;
        private AudioSource musicPlayer;
        private RenderPipelineAsset previousPipeline;
        private UniversalRenderPipelineAsset runtimePipeline;
        private RadarBlip[] radarBlips;

        private void InitializeApplication(bool transient)
        {
            baseCpuWindup = cpuWindupSeconds;
            previousListenerVolume = AudioListener.volume;
            previousPipeline = QualitySettings.renderPipeline;
            shell = gameObject.AddComponent<GameShell>();
            shell.Initialize(this, uiFont, view, transient ? new MemoryProfileStorage() : null);
        }

        public void BeginMatch(bool practice, CpuDifficulty difficulty)
        {
            practiceMode = practice;
            activeDifficulty = difficulty;
            titleCamera = false;
            cpuWindupSeconds = Mathf.Max(.35f, baseCpuWindup * (difficulty == CpuDifficulty.Easy ? 1.45f : difficulty == CpuDifficulty.Hard ? .84f : 1));
            view.fieldOfView = appliedPreferences?.fieldOfView ?? 80;
            foreach (var renderer in player.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            RestartMatch();
        }

        public void SetPaused(bool value)
        {
            paused = value;
            charging = false; charge = 0;
            Time.timeScale = value ? 0 : 1;
            SetMouseCapture(!value && !finished);
            if (value) aimLine.enabled = false;
        }

        public void ReturnToTitle()
        {
            practiceMode = false;
            activeDifficulty = CpuDifficulty.Normal;
            cpuWindupSeconds = baseCpuWindup > 0 ? baseCpuWindup : .9f;
            RestartMatch();
            titleCamera = true;
            foreach (var renderer in player.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.On;
            SetPaused(true);
        }

        public void ApplyPreferences(GamePreferences preferences)
        {
            appliedPreferences = preferences.Clone(); appliedPreferences.Validate();
            mouseSensitivity = appliedPreferences.sensitivity;
            if (view != null) view.fieldOfView = titleCamera ? 54 : appliedPreferences.fieldOfView;
            AudioListener.volume = appliedPreferences.masterVolume;
            if (musicPlayer != null) musicPlayer.volume = appliedPreferences.musicVolume * .3f;
            Application.targetFrameRate = appliedPreferences.frameRate;
            QualitySettings.vSyncCount = 0;
            if (runtimePipeline == null)
            {
                var current = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
                if (current is UniversalRenderPipelineAsset source)
                {
                    runtimePipeline = Instantiate(source);
                    runtimePipeline.name = "Vlliards runtime quality";
                }
            }
            if (runtimePipeline != null)
            {
                int quality = appliedPreferences.graphicsQuality;
                runtimePipeline.renderScale = quality == 0 ? .7f : quality == 1 ? .85f : 1;
                runtimePipeline.msaaSampleCount = quality == 0 ? 1 : quality == 1 ? 2 : 4;
                runtimePipeline.shadowDistance = quality == 0 ? 0 : quality == 1 ? 20 : 40;
                QualitySettings.renderPipeline = runtimePipeline;
            }
        }

        public GameSnapshot CaptureSnapshot()
        {
            if (radarBlips == null || radarBlips.Length != balls.Count) radarBlips = new RadarBlip[balls.Count];
            for (int i = 0; i < balls.Count; i++)
                radarBlips[i] = new RadarBlip { number = balls[i].Number, position = balls[i].transform.position, visible = !balls[i].Potted && balls[i].gameObject.activeInHierarchy };
            return new GameSnapshot
            {
                finished = finished, playerWon = winner == player, practice = practiceMode, elapsed = elapsed,
                playerCooldown = player.CooldownRemaining, playerFreeze = player.FreezeRemaining,
                cpuCooldown = opponent.CooldownRemaining, cpuFreeze = opponent.FreezeRemaining,
                cpuAiming = cpuWindingUp, cpuPreparation = cpuWindingUp ? Mathf.Clamp01((Time.time - cpuWindupAt) / cpuWindupSeconds) : 0,
                charge = charge, charging = charging, nextTarget = MinimumTarget(), shots = shots, fouls = playerFouls, pocketed = playerPocketed,
                notice = Time.unscaledTime < noticeUntil ? notice : "", balls = radarBlips
            };
        }

        private void RenderTitleCamera()
        {
            float drift = appliedPreferences != null && appliedPreferences.reduceMotion ? 0 : Mathf.Sin(Time.unscaledTime * .08f) * .55f;
            view.transform.position = new Vector3(-5.6f + drift, 3.5f, -9.8f);
            view.transform.LookAt(new Vector3(.2f, .3f, .1f));
            view.fieldOfView = 54;
        }

        private void DisposeApplication()
        {
            if (shell == null) return;
            AudioListener.volume = previousListenerVolume;
            QualitySettings.renderPipeline = previousPipeline;
            if (runtimePipeline != null) Destroy(runtimePipeline);
        }

        private float CpuAimError => activeDifficulty == CpuDifficulty.Easy ? 5.5f : activeDifficulty == CpuDifficulty.Hard ? .45f : 1.5f;
        private float CpuRecoveryDelay => activeDifficulty == CpuDifficulty.Easy ? 1.3f : activeDifficulty == CpuDifficulty.Hard ? .25f : .6f;
    }
}
