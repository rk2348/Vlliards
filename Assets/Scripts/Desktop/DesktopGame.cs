using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vlliards.Desktop
{
    // Local game loop. No XR device, account, or network session is required.
    public sealed partial class DesktopGame : MonoBehaviour
    {
        [SerializeField] private GameObject tableModel;
        [SerializeField] private AudioClip music;
        [SerializeField] private Shader surfaceShader;
        [SerializeField] private Shader guideShader;
        [SerializeField, Min(0)] private float launchCooldown = 3f;
        private readonly List<DesktopBall> balls = new List<DesktopBall>();
        private readonly List<Vector3> pockets = new List<Vector3>();
        private DesktopBall player;
        private Camera view;
        private LineRenderer aimLine;
        private Vector3 aim = Vector3.forward;
        private int remaining, scratches, shots;
        private float elapsed, charge;
        private bool paused, finished, charging;
        private string notice = "Aim with the mouse. Hold left click, then release to launch.";
        private float noticeUntil = 7;
        private Material ballMaterial;
        private PhysicsMaterial bounce;

        private void Start()
        {
            Application.targetFrameRate = 90;
            Time.timeScale = 1;
            BuildArena();
            var arguments = Environment.GetCommandLineArgs();
            bool mechanics = Array.IndexOf(arguments, "--desktop-smoke") >= 0;
            bool worldPreview = Array.IndexOf(arguments, "--desktop-preview") >= 0 || Array.IndexOf(arguments, "--shot-motion-preview") >= 0;
            bool shellTest = Array.IndexOf(arguments, "--shell-smoke") >= 0;
            bool shellPreview = Array.IndexOf(arguments, "--shell-preview") >= 0;
            if (mechanics || worldPreview) Restart();
            else InitializeApplication(shellTest || shellPreview);
            if (mechanics) StartCoroutine(SmokeTest());
            if (shellTest) StartCoroutine(VerifyApplicationFlow());
            if (shellPreview) StartCoroutine(CaptureApplicationPreview());
            if (Array.IndexOf(arguments, "--shot-motion-preview") >= 0) StartCoroutine(CaptureShotMotionPreview());
            else if (worldPreview) StartCoroutine(CapturePreview());
        }

        private Material Material(Color color, float metallic = 0)
        {
            var result = new Material(surfaceShader != null ? surfaceShader : Shader.Find("Universal Render Pipeline/Lit"));
            result.color = color;
            result.SetFloat("_Metallic", metallic);
            result.SetFloat("_Smoothness", .55f);
            return result;
        }

        private GameObject Box(string name, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(transform);
            box.transform.position = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            box.GetComponent<Collider>().enabled = solid;
            if (solid) box.GetComponent<Collider>().sharedMaterial = bounce;
            return box;
        }

        private void BuildArena()
        {
            bounce = new PhysicsMaterial("Desktop cushion") { bounciness = .86f,
                dynamicFriction = .08f, staticFriction = .08f,
                bounceCombine = PhysicsMaterialCombine.Maximum };
            var wood = Material(new Color(.12f, .065f, .035f));
            var green = Material(new Color(.025f, .24f, .19f));
            var brass = Material(new Color(.68f, .48f, .19f), .7f);
            var dark = Material(new Color(.015f, .02f, .025f));
            ballMaterial = Material(Color.white);
            CreateTableArtwork();
            CreateLounge();
            if (loungeEnvironment == null) Box("Lounge floor", new Vector3(0, loungeFloor - .1f, 0), new Vector3(80, .2f, 80), dark);
            if (tableModel == null) Box("Table base", new Vector3(0, -.55f, 0), new Vector3(9.2f, 1, 15.2f), wood);

            var cloth = Box("Playing cloth collision", new Vector3(0, -.08f, 0), new Vector3(8, .16f, 14), green);
            cloth.GetComponent<Renderer>().enabled = !originalCloth;
            CreatePocketLayout(dark);
            CreateOpenCushions(wood);

            var cameraObject = new GameObject("Desktop Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform);
            view = cameraObject.GetComponent<Camera>();
            view.tag = "MainCamera";
            view.orthographic = false;
            view.fieldOfView = 80;
            view.nearClipPlane = .025f;
            view.farClipPlane = 180;
            view.stereoTargetEye = StereoTargetEyeMask.None;
            view.backgroundColor = new Color(.025f, .035f, .045f);
            view.clearFlags = CameraClearFlags.SolidColor;
            var light = new GameObject("Table light", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(transform);
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.transform.rotation = Quaternion.Euler(55, -25, 0);
            RenderSettings.ambientLight = new Color(.4f, .45f, .5f);

            aimLine = new GameObject("Launch guide", typeof(LineRenderer)).GetComponent<LineRenderer>();
            aimLine.transform.SetParent(transform);
            aimLine.sharedMaterial = new Material(guideShader != null ? guideShader : Shader.Find("Universal Render Pipeline/Unlit"));
            aimLine.sharedMaterial.color = new Color(.7f, 1, .85f);
            aimLine.positionCount = 2;
            aimLine.startWidth = .008f;
            aimLine.endWidth = .004f;
            aimLine.useWorldSpace = true;
            if (music != null)
            {
                var audio = gameObject.AddComponent<AudioSource>();
                musicPlayer = audio;
                audio.clip = music;
                audio.loop = true;
                audio.volume = .12f;
                audio.Play();
            }
            player = CreateBall(0, Color.white);
            foreach (var renderer in player.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            opponent = CreateBall(-1, new Color(.35f, .85f, 1));
            Color[] colors = { Color.yellow, new Color(.1f,.3f,1), Color.red, new Color(.6f,.2f,.8f),
                new Color(1,.45f,.1f), new Color(.1f,.65f,.3f), new Color(.55f,.08f,.08f), new Color(.06f,.06f,.08f), Color.yellow };
            for (int i = 1; i <= 9; i++) CreateBall(i, colors[i - 1]);
            CreateShotMotion();
        }

        private DesktopBall CreateBall(int number, Color color)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = number == 0 ? "You - cue ball" : "Target " + number;
            sphere.transform.SetParent(transform);
            sphere.transform.localScale = Vector3.one * .54f;
            var material = new Material(ballMaterial) { color = color };
            sphere.GetComponent<Renderer>().sharedMaterial = material;
            ApplyBallArtwork(sphere, number);
            sphere.GetComponent<Collider>().sharedMaterial = bounce;
            sphere.GetComponent<Collider>().contactOffset = .001f;
            sphere.AddComponent<Rigidbody>();
            var ball = sphere.AddComponent<DesktopBall>();
            ball.Initialize(number);
            ball.Contact = OnBallContact;
            balls.Add(ball);
            return ball;
        }

        public void Restart() => RestartMatch();

        private void Update()
        {
            if (testing)
            {
                if (!paused && !finished) { elapsed += Time.deltaTime; CheckPockets(); ResolveShots(); }
                return;
            }
            if (shell != null && !shell.IsPlaying) { aimLine.enabled = false; return; }
            var keyboard = Keyboard.current;
            if (shell == null && keyboard != null && keyboard.rKey.wasPressedThisFrame) Restart();
            if (shell == null && keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !finished)
            {
                paused = !paused;
                charging = false;
                Time.timeScale = paused ? 0 : 1;
                SetMouseCapture(!paused);
            }
            if (paused || finished) { aimLine.enabled = false; return; }
            elapsed += Time.deltaTime;
            UpdateCpu();
            ReadBallViewInput();
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) Launch(12);
            aimLine.enabled = !player.Frozen && (appliedPreferences == null || appliedPreferences.showGuide);
            aimLine.SetPosition(0, player.transform.position + aim * .35f - Vector3.up * .1f);
            aimLine.SetPosition(1, player.transform.position + aim * (1.4f + charge * 2.2f) - Vector3.up * .1f);
            CheckPockets();
            ResolveShots();
        }

        private void FixedUpdate()
        {
            if (testing || paused || finished || player == null || (shell != null && !shell.IsPlaying)) return;
            var keyboard = Keyboard.current;
            if (keyboard == null || player.Frozen) return;
            Vector3 input = ViewRelativeMovement(new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0)));
            // Small steering impulses keep positioning possible without replacing launch momentum.
            if (input.sqrMagnitude > 0 && player.Body.linearVelocity.sqrMagnitude < 9)
                player.Body.AddForce(input.normalized * 8, ForceMode.Acceleration);
        }

        private bool Launch(float speed)
        {
            if (!TryShoot(player, aim, speed)) return false;
            charge = 0;
            charging = false;
            return true;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && !Application.isBatchMode && !testing && !finished)
            {
                if (shell != null) shell.Pause();
                else { charging = false; charge = 0; paused = true; Time.timeScale = 0; SetMouseCapture(false); }
            }
        }

        private IEnumerator CapturePreview()
        {
            yield return new WaitForSeconds(1);
            UpdateBallView();
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            var imageTarget = new RenderTexture(1280, 800, 24);
            var previous = RenderTexture.active;
            view.targetTexture = imageTarget;
            Canvas.ForceUpdateCanvases();
            view.Render();
            yield return null;
            view.Render();
            RenderTexture.active = imageTarget;
            var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
            image.Apply();
            string previewDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Artifacts"));
            System.IO.Directory.CreateDirectory(previewDirectory);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(previewDirectory, "DesktopPreview.png"), image.EncodeToPNG());
            view.targetTexture = null;
            RenderTexture.active = previous;
            Destroy(image);
            imageTarget.Release();
            Destroy(imageTarget);
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
            else UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy() { DisposeApplication(); Time.timeScale = 1; SetMouseCapture(false); }
    }
}
