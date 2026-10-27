using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Vlliards.App
{
    public sealed partial class GameShellUI : MonoBehaviour
    {
        private GameShell shell;
        private TMP_FontAsset font;
        private Camera cameraView;
        private RectTransform safe, menu, hud, radar;
        private Image shade, energy, energyBackground;
        private TMP_Text target, clock, cpu, status, controls, toast, countText, goText;
        private readonly Image[] radarDots = new Image[11];
        private readonly TMP_Text[] worldLabels = new TMP_Text[11];
        private float goUntil;
        private Button firstButton;
        private Rect lastSafe;
        private int lastWidth, lastHeight;
        private static readonly Color Ink = new Color(.025f, .045f, .06f, .96f);
        private static readonly Color Panel = new Color(.045f, .075f, .095f, .94f);
        private static readonly Color Muted = new Color(.60f, .71f, .75f);
        private static readonly Color Gold = new Color(.88f, .71f, .43f);
        private static readonly Color Mint = new Color(.35f, .85f, .72f);
        private static readonly Color Red = new Color(1, .40f, .38f);
        private static readonly string[] Numbers = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        public void Initialize(GameShell owner, TMP_FontAsset uiFont, Camera view)
        {
            shell = owner; font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset; cameraView = view;
            if (EventSystem.current == null)
            {
                var events = new GameObject("Game UI input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var canvasObject = new GameObject("Game interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.layer = 5;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = view;
            canvas.planeDistance = .4f;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            safe = Node(canvasObject.transform, "Safe area"); Stretch(safe);
            hud = Node(safe, "Match HUD"); Stretch(hud);
            BuildHud();
            menu = Node(safe, "Menus"); Stretch(menu);
            shade = menu.gameObject.AddComponent<Image>(); shade.color = new Color(.01f, .025f, .04f, .66f);
            UpdateSafeArea();
            Canvas.ForceUpdateCanvases();
        }

        private static RectTransform Node(Transform parent, string name)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            node.gameObject.layer = 5;
            node.SetParent(parent, false);
            return node;
        }
        private static void Stretch(RectTransform rect, float inset = 0)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset;
        }
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        private static Image Image(Transform parent, string name, Color color)
        {
            var rect = Node(parent, name);
            var image = rect.gameObject.AddComponent<Image>(); image.color = color;
            return image;
        }
        private TMP_Text Text(Transform parent, string value, float size = 24, Color? color = null, float height = 40)
        {
            var rect = Node(parent, value.Length > 20 ? value.Substring(0, 20) : value);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSize = size; text.color = color ?? Color.white; text.text = value;
            text.raycastTarget = false; text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            var layout = rect.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = height; layout.flexibleWidth = 1;
            return text;
        }
        private static void Height(Transform node, float height)
        {
            var layout = node.GetComponent<LayoutElement>() ?? node.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
        }
        private static RectTransform Stack(Transform parent, string name, int padding = 0, float gap = 12)
        {
            var rect = Node(parent, name);
            var group = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(padding, padding, padding, padding); group.spacing = gap;
            group.childForceExpandHeight = false; group.childControlHeight = true; group.childForceExpandWidth = true;
            return rect;
        }
        private static RectTransform Row(Transform parent, float height = 52)
        {
            var rect = Node(parent, "Row"); Height(rect, height);
            var group = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 12; group.childForceExpandWidth = true; group.childControlWidth = true; group.childControlHeight = true;
            return rect;
        }
        private Button Button(Transform parent, string label, UnityEngine.Events.UnityAction action, bool primary = false, float height = 54)
        {
            var background = Image(parent, label, primary ? Gold : new Color(.10f, .17f, .21f)); Height(background.transform, height);
            var button = background.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1.16f, 1.16f, 1.16f); colors.pressedColor = new Color(.8f, .8f, .8f);
            button.colors = colors;
            button.onClick.AddListener(() => { shell.Audio.Click(); action(); });
            var text = Text(background.transform, label, primary ? 25 : 23, primary ? Ink : Color.white);
            Stretch(text.rectTransform, 8); text.alignment = TextAlignmentOptions.Center;
            if (firstButton == null) firstButton = button;
            var layout = background.gameObject.GetComponent<LayoutElement>(); layout.flexibleWidth = 1;
            return button;
        }
        private RectTransform Card(string heading, string subheading, float width = 860, float height = 690)
        {
            var background = Image(menu, heading, Panel);
            Place(background.rectTransform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero,
                new Vector2(Mathf.Min(width, safe.rect.width - 48), Mathf.Min(height, safe.rect.height - 48)));
            var content = Stack(background.transform, "Content", 32, 14); Stretch(content);
            var title = Text(content, heading, 38, Color.white, 58); title.fontStyle = FontStyles.Bold;
            if (!string.IsNullOrEmpty(subheading)) Text(content, subheading, 20, Muted, 52);
            return content;
        }

        public void Show(GameScreen screen)
        {
            firstButton = null;
            for (int i = menu.childCount - 1; i >= 0; i--) { var child = menu.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
            countText = null;
            menu.gameObject.SetActive(screen != GameScreen.Playing);
            hud.gameObject.SetActive(screen == GameScreen.Playing || screen == GameScreen.Countdown || screen == GameScreen.Paused);
            shade.color = new Color(.01f, .025f, .04f, screen == GameScreen.Title ? .50f : screen == GameScreen.Countdown ? .18f : .76f);
            switch (screen)
            {
                case GameScreen.Title: BuildTitle(); break;
                case GameScreen.MatchSetup: BuildSetup(); break;
                case GameScreen.Countdown: BuildCountdown(); break;
                case GameScreen.Paused: BuildPause(); break;
                case GameScreen.Results: BuildResults(); break;
                case GameScreen.Options: BuildOptions(); break;
                case GameScreen.Help: BuildHelp(); break;
                case GameScreen.Records: BuildRecords(); break;
                case GameScreen.Credits: BuildCredits(); break;
                case GameScreen.Confirm: BuildConfirm(); break;
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(screen == GameScreen.Playing ? null : firstButton != null ? firstButton.gameObject : null);
        }

        private void BuildHud()
        {
            var left = Image(hud, "Target panel", Panel);
            Place(left.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -26), new Vector2(260, 126));
            var stack = Stack(left.transform, "Target", 18, 2); Stretch(stack);
            Text(stack, "NEXT TARGET", 14, Muted, 20).characterSpacing = 3;
            target = Text(stack, "1", 42, Gold, 54);
            clock = Text(stack, "00:00", 18, Muted, 26);
            var right = Image(hud, "CPU state", Panel);
            Place(right.rectTransform, Vector2.one, Vector2.one, new Vector2(-26, -26), new Vector2(280, 104));
            stack = Stack(right.transform, "CPU", 18, 8); Stretch(stack);
            Text(stack, "OPPONENT", 14, Muted, 20).characterSpacing = 3;
            cpu = Text(stack, "READY", 23, Mint, 38);
            var bottom = Image(hud, "Player state", Panel);
            Place(bottom.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 26), new Vector2(420, 94));
            status = Text(bottom.transform, "READY", 23, Mint);
            Place(status.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -12), new Vector2(384, 34)); status.alignment = TextAlignmentOptions.Center;
            energyBackground = Image(bottom.transform, "Charge track", new Color(.12f, .22f, .26f));
            Place(energyBackground.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 32), new Vector2(372, 5));
            energy = Image(energyBackground.transform, "Charge", Mint); Stretch(energy.rectTransform);
            controls = Text(bottom.transform, "長押し → 離して発射", 15, Muted);
            Place(controls.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 5), new Vector2(390, 24)); controls.alignment = TextAlignmentOptions.Center;
            var reticle = Text(hud, "+", 26, Color.white); Place(reticle.rectTransform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(30, 36)); reticle.alignment = TextAlignmentOptions.Center;
            reticle.name = "Crosshair";
            var toastBackground = Image(hud, "Notice", Panel);
            Place(toastBackground.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -26), new Vector2(620, 65));
            toast = Text(toastBackground.transform, "", 20, Gold); Stretch(toast.rectTransform, 10); toast.alignment = TextAlignmentOptions.Center;
            var pause = Button(hud, "Ⅱ", shell.Pause, false, 44);
            Place(pause.GetComponent<RectTransform>(), new Vector2(0, 0), Vector2.zero, new Vector2(26, 26), new Vector2(56, 48));
            var hint = Text(hud, "WASD 移動   マウス 視点\nSpace 発射   Esc 一時停止", 15, Muted, 54);
            Place(hint.rectTransform, Vector2.zero, Vector2.zero, new Vector2(96, 25), new Vector2(290, 48));
            var radarImage = Image(hud, "Table radar", Panel); radar = radarImage.rectTransform;
            Place(radar, Vector2.one, Vector2.one, new Vector2(-26, -146), new Vector2(140, 222));
            var cloth = Image(radar, "Radar cloth", new Color(.04f, .18f, .15f)); Stretch(cloth.rectTransform, 10);
            for (int i = 0; i < 11; i++)
            {
                radarDots[i] = Image(radar, "Ball marker", Color.white);
                Place(radarDots[i].rectTransform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(7, 7));
                worldLabels[i] = Text(hud, "", 20, Color.white, 40);
                Place(worldLabels[i].rectTransform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(130, 38));
                worldLabels[i].alignment = TextAlignmentOptions.Center;
                worldLabels[i].outlineWidth = .14f;
            }
            goText = Text(hud, "GO", 80, Gold, 120);
            Place(goText.rectTransform, new Vector2(.5f, .65f), Vector2.one * .5f, Vector2.zero, new Vector2(320, 120)); goText.alignment = TextAlignmentOptions.Center;
        }

        public void SetCountdown(string text) { if (countText != null) countText.text = text; }
        public void FlashGo() => goUntil = Time.unscaledTime + .7f;
        public void Tick(GameSnapshot state, GamePreferences settings)
        {
            UpdateSafeArea();
            if (!hud.gameObject.activeSelf) return;
            target.SetText("{0}", state.nextTarget);
            clock.text = FormatTime(state.elapsed) + "   入球 " + state.pocketed;
            cpu.text = state.practice ? "PRACTICE" : state.cpuFreeze > 0 ? "停止中 " + state.cpuFreeze.ToString("0.0") + "s" : state.cpuAiming ? "構え中 " + Mathf.RoundToInt(state.cpuPreparation * 100) + "%" : state.cpuCooldown > 0 ? "待機 " + state.cpuCooldown.ToString("0.0") + "s" : "READY";
            cpu.color = state.cpuFreeze > 0 ? Red : state.cpuAiming ? Gold : Mint;
            float fill = state.charging ? state.charge : state.playerFreeze > 0 ? 1 - state.playerFreeze / 5 : state.playerCooldown > 0 ? 1 - state.playerCooldown / 3 : 1;
            status.text = state.playerFreeze > 0 ? "ファウル停止 " + state.playerFreeze.ToString("0.0") + "s" : state.charging ? "CHARGE " + Mathf.RoundToInt(state.charge * 100) + "%" : state.playerCooldown > 0 ? "COOLDOWN " + state.playerCooldown.ToString("0.0") + "s" : "READY / 発射できます";
            status.color = energy.color = state.playerFreeze > 0 ? Red : state.charging ? Gold : Mint;
            energy.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fill), 1);
            toast.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(state.notice)); toast.text = state.notice;
            radar.gameObject.SetActive(settings.showRadar);
            hud.Find("Crosshair").gameObject.SetActive(settings.showGuide);
            goText.gameObject.SetActive(shell.IsPlaying && Time.unscaledTime < goUntil);
            if (state.balls == null) return;
            for (int i = 0; i < radarDots.Length; i++)
            {
                bool visible = i < state.balls.Length && state.balls[i].visible;
                radarDots[i].gameObject.SetActive(visible);
                worldLabels[i].gameObject.SetActive(false);
                if (!visible) continue;
                var ball = state.balls[i];
                radarDots[i].rectTransform.anchoredPosition = new Vector2(ball.position.x / 8.6f * 120, (ball.position.z - .12f) / 14.8f * 202);
                radarDots[i].color = ball.number == 0 ? Color.white : ball.number < 0 ? state.cpuAiming ? Gold : Color.cyan : ball.number == state.nextTarget ? Gold : Muted;
                if (ball.number == 0) continue;
                Vector3 point = cameraView.WorldToViewportPoint(ball.position + Vector3.up * .38f);
                if (point.z <= cameraView.nearClipPlane || point.x < .03f || point.x > .97f || point.y < .05f || point.y > .95f) continue;
                var label = worldLabels[i]; label.gameObject.SetActive(true);
                label.rectTransform.anchoredPosition = new Vector2((point.x - .5f) * hud.rect.width, (point.y - .5f) * hud.rect.height);
                label.text = ball.number < 0 ? state.cpuAiming ? "CPU 構え中" : "CPU" : Numbers[Mathf.Clamp(ball.number, 0, 9)];
                label.color = ball.number < 0 && state.cpuAiming ? Gold : Color.white;
            }
        }

        private void UpdateSafeArea()
        {
            if (safe == null || Screen.width == 0 || Screen.height == 0) return;
            Rect area = Screen.safeArea;
            if (area == lastSafe && lastWidth == Screen.width && lastHeight == Screen.height) return;
            lastSafe = area; lastWidth = Screen.width; lastHeight = Screen.height;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }
        public static string FormatTime(float seconds) => Mathf.FloorToInt(Mathf.Max(0, seconds) / 60).ToString("00") + ":" + (Mathf.FloorToInt(Mathf.Max(0, seconds)) % 60).ToString("00");
    }
}
