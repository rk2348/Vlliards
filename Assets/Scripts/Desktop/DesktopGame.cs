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
        private GUIStyle titleStyle, bodyStyle, smallStyle;

        private void Start()
        {
            Application.targetFrameRate = 90;
            Time.timeScale = 1;
            BuildArena();
            Restart();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--desktop-smoke") >= 0)
                StartCoroutine(SmokeTest());
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--desktop-preview") >= 0)
                StartCoroutine(CapturePreview());
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
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Restart();
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !finished)
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
            aimLine.enabled = !player.Frozen;
            aimLine.SetPosition(0, player.transform.position + aim * .35f - Vector3.up * .1f);
            aimLine.SetPosition(1, player.transform.position + aim * (1.4f + charge * 2.2f) - Vector3.up * .1f);
            CheckPockets();
            ResolveShots();
        }

        private void FixedUpdate()
        {
            if (testing || paused || finished || player == null) return;
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
                charging = false; charge = 0; paused = true; Time.timeScale = 0; SetMouseCapture(false);
            }
        }

        private void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
                bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            }
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 800f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            foreach (var ball in balls)
            {
                if (ball.Potted || ball == player) continue;
                Vector3 screen = view.WorldToScreenPoint(ball.transform.position + Vector3.up * .38f);
                if (screen.z <= view.nearClipPlane || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) continue;
                GUI.Label(new Rect(screen.x / scale - 12, (Screen.height - screen.y) / scale - 10, 50, 24), ball.IsCue ? (ball == player ? "YOU" : "CPU") : ball.Number.ToString(), smallStyle);
            }
            if (!paused && !finished) GUI.Label(new Rect(width / 2 - 6, height / 2 - 12, 24, 24), "+", bodyStyle);
            DrawRadar(width);
            GUI.Box(new Rect(24, 20, 300, 184), "");
            GUI.Label(new Rect(42, 30, 270, 44), "VLLIARDS / VS CPU", titleStyle);
            GUI.Label(new Rect(42, 80, 270, 32), "NEXT TARGET  " + MinimumTarget(), bodyStyle);
            GUI.Label(new Rect(42, 114, 270, 28), "TIME  " + TimeSpan.FromSeconds(elapsed).ToString(@"mm\:ss"), bodyStyle);
            GUI.Label(new Rect(42, 148, 270, 36), "LAUNCHES " + shots + "    SCRATCHES " + scratches, smallStyle);
            GUI.Box(new Rect(24, 214, 300, 45), BallStatus(player));
            GUI.Box(new Rect(width - 290, 20, 266, 80), "CPU");
            GUI.Label(new Rect(width - 275, 54, 240, 35), BallStatus(opponent), bodyStyle);
            GUI.Box(new Rect(24, height - 114, 350, 90), "");
            GUI.Label(new Rect(40, height - 104, 320, 76), "WASD  Move relative to your view\nMOUSE  Look / Hold LMB + release to launch\nSPACE  Quick launch    R  Restart    ESC  Pause", smallStyle);
            if (charging)
            {
                GUI.Box(new Rect(width / 2 - 140, height - 65, 280, 35), "POWER " + Mathf.RoundToInt(charge * 100) + "%");
            }
            if (!finished && Time.unscaledTime < noticeUntil)
                GUI.Label(new Rect(width / 2 - 240, 28, 480, 70), notice, bodyStyle);
            if (paused || finished)
            {
                Rect panel = new Rect(width / 2 - 220, height / 2 - 125, 440, 250);
                GUI.Box(panel, "");
                GUI.Label(new Rect(panel.x + 30, panel.y + 22, 390, 48), finished ? (winner == player ? "YOU WIN" : "CPU WINS") : "PAUSED", titleStyle);
                GUI.Label(new Rect(panel.x + 30, panel.y + 78, 380, 50), finished ? "Legal nine-ball pocketed. " + shots + " launches." : "Take your time. The table is waiting.", bodyStyle);
                if (GUI.Button(new Rect(panel.x + 30, panel.y + 145, 180, 45), finished ? "PLAY AGAIN" : "RESUME"))
                {
                    if (finished) Restart();
                    else { paused = false; Time.timeScale = 1; SetMouseCapture(true); }
                }
                if (GUI.Button(new Rect(panel.x + 230, panel.y + 145, 180, 45), "QUIT")) Application.Quit();
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

        private void OnDestroy() { Time.timeScale = 1; SetMouseCapture(false); }
    }
}
