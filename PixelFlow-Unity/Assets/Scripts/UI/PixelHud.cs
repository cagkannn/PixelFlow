using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelFlow.UI
{
    /// <summary>Portrait HUD. Queue UFOs and their pads remain real objects in the scene.</summary>
    public sealed class PixelHud : MonoBehaviour
    {
        public const float PortraitAspect = 390f / 844f;
        public Action OnPause;
        public Action OnRestart;
        public Action OnPrevLevel;
        public Action OnNextLevel;
        public Action OnPower;

        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private RectTransform _design;
        private Font _font;
        private Sprite _rounded;
        private Sprite _circle;
        private Text _levelLabel;
        private Text _speedLabel;
        private Text _powerCount;
        private Text _levelPosition;
        private Text _toastLabel;
        private Image _powerBackground;
        private HudGlyph _pauseGlyph;
        private Button _powerButton;
        private Button _previousButton;
        private Button _nextButton;
        private GameObject _levelPanel;
        private GameObject _toast;
        private GameObject _overlay;
        private Text _overlayTitle;
        private Text _overlayBody;
        private Text _primaryLabel;
        private Text _secondaryLabel;
        private GameObject _secondaryObject;
        private Action _primaryAction;
        private Action _secondaryAction;
        private bool _paused;
        private bool _resumeAfterHelp;
        private float _toastUntil;
        private int _screenWidth;
        private int _screenHeight;

        private static readonly Color Blue = Hex(0x235de4);
        private static readonly Color Cream = Hex(0xfff9ed);
        private static readonly Color Muted = Hex(0xe1d2cb);
        private static readonly Color Cyan = Hex(0x38cef4);

        public static PixelHud Create(Transform parent)
        {
            GameObject root = new GameObject("PixelFlow HUD", typeof(RectTransform));
            if (parent != null) root.transform.SetParent(parent, false);
            return root.AddComponent<PixelHud>();
        }

        private void Awake()
        {
            _font = LoadFont();
            _rounded = MakeRoundedSprite(64, 14);
            _circle = MakeRoundedSprite(64, 32);
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            _design = MakeRect("Portrait interface", transform, new Vector2(390, 844));
            _design.anchorMin = _design.anchorMax = new Vector2(.5f, .5f);
            _design.pivot = new Vector2(.5f, .5f);
            BuildHeader();
            BuildFooter();
            BuildLevelPanel();
            BuildToast();
            BuildOverlay();
            Canvas.ForceUpdateCanvases();
            FitPortrait();
            SetState(1, 6, false, 1, false, false, true);
        }

        private void BuildHeader()
        {
            Button badge = MakeButton("Bölüm seçimi", _design, new Vector2(100, 34), new Vector2(14, -16), new Vector2(0, 1), Blue);
            _levelLabel = MakeLabel("Bölüm numarası", badge.transform, "Bölüm 1", 14, Cream, TextAnchor.MiddleCenter);
            Stretch(_levelLabel.rectTransform, 6, 0, 18, 0);
            HudGlyph down = MakeGlyph("Bölüm menüsü", badge.transform, HudGlyph.Kind.ChevronDown, Muted);
            Anchor(down.rectTransform, new Vector2(1, .5f), new Vector2(-13, 0), new Vector2(12, 12));
            badge.onClick.AddListener(delegate { _levelPanel.SetActive(!_levelPanel.activeSelf); });

            Button pause = IconButton("Duraklat veya devam et", new Vector2(-108, -16), HudGlyph.Kind.Pause, true);
            _pauseGlyph = pause.GetComponentInChildren<HudGlyph>();
            pause.onClick.AddListener(delegate { if (OnPause != null) OnPause(); });
            Button restart = IconButton("Yeniden başlat", new Vector2(-71, -16), HudGlyph.Kind.Restart, false);
            restart.onClick.AddListener(delegate { _levelPanel.SetActive(false); if (OnRestart != null) OnRestart(); });
            Button help = IconButton("Nasıl oynanır", new Vector2(-34, -16), HudGlyph.Kind.Help, false);
            help.onClick.AddListener(OpenHelp);
            _speedLabel = MakeLabel("Oyun hızı", _design, "1×", 11, Muted, TextAnchor.MiddleCenter);
            Anchor(_speedLabel.rectTransform, new Vector2(1, 1), new Vector2(-142, -33), new Vector2(27, 24));
        }

        private void BuildFooter()
        {
            Image footer = MakeImage("Mavi güç şeridi", _design, Hex(0x1539b7), false);
            footer.rectTransform.anchorMin = new Vector2(0, 0); footer.rectTransform.anchorMax = new Vector2(1, 0);
            footer.rectTransform.pivot = new Vector2(.5f, 0); footer.rectTransform.sizeDelta = new Vector2(0, 58);
            footer.rectTransform.anchoredPosition = Vector2.zero;
            Image topLine = MakeImage("Şerit aydınlığı", footer.transform, Hex(0x4a7cef), false);
            topLine.rectTransform.anchorMin = new Vector2(0, 1); topLine.rectTransform.anchorMax = new Vector2(1, 1);
            topLine.rectTransform.pivot = new Vector2(.5f, 1); topLine.rectTransform.sizeDelta = new Vector2(0, 2);
            topLine.rectTransform.anchoredPosition = Vector2.zero;
            _powerButton = MakeButton("Havadan atış", _design, new Vector2(58, 58), new Vector2(0, 57), new Vector2(.5f, 0), Cyan);
            _powerBackground = _powerButton.GetComponent<Image>();
            _powerButton.onClick.AddListener(delegate { if (OnPower != null) OnPower(); });

            RectTransform propeller = MakeRect("Pervane", _powerButton.transform, new Vector2(62, 62));
            propeller.anchoredPosition = new Vector2(0, 10);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f + 18f;
                float radians = angle * Mathf.Deg2Rad;
                Image bladeShadow = MakeImage("Kanat gölgesi " + i, propeller, new Color(.15f, .34f, .50f, .25f), false);
                bladeShadow.sprite = _circle; bladeShadow.type = Image.Type.Sliced;
                bladeShadow.rectTransform.sizeDelta = new Vector2(31, 8);
                bladeShadow.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(radians) * 10, Mathf.Sin(radians) * 10 - 1.8f);
                bladeShadow.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                Image blade = MakeImage("Pervane kanadı " + i, propeller, Hex(0xf5fdff), false);
                blade.sprite = _circle; blade.type = Image.Type.Sliced;
                blade.rectTransform.sizeDelta = new Vector2(31, 7);
                blade.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(radians) * 10, Mathf.Sin(radians) * 10);
                blade.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            }
            Image hub = MakeImage("Pervane göbeği", propeller, Hex(0xe8f8ff), false);
            hub.sprite = _circle; hub.rectTransform.sizeDelta = new Vector2(11, 11);
            Image hubCenter = MakeImage("Göbek merkezi", hub.transform, Hex(0x85a6ba), false);
            hubCenter.sprite = _circle; hubCenter.rectTransform.sizeDelta = new Vector2(7, 7);
            Image count = MakeImage("Kalan hak", _powerButton.transform, Hex(0xf36560), false);
            count.sprite = _rounded; count.type = Image.Type.Sliced;
            Anchor(count.rectTransform, new Vector2(1, 1), new Vector2(0, 0), new Vector2(20, 20));
            _powerCount = MakeLabel("Hak sayısı", count.transform, "1", 12, Cream, TextAnchor.MiddleCenter);
            Stretch(_powerCount.rectTransform);
        }

        private void BuildLevelPanel()
        {
            Image panel = MakeImage("Bölüm seçme paneli", _design, Hex(0xeaded7), true);
            panel.sprite = _rounded; panel.type = Image.Type.Sliced;
            Anchor(panel.rectTransform, new Vector2(0, 1), new Vector2(14, -60), new Vector2(166, 62));
            panel.rectTransform.pivot = new Vector2(0, 1);
            _levelPanel = panel.gameObject;
            _previousButton = MakeButton("Önceki bölüm", panel.transform, new Vector2(38, 38), new Vector2(7, -12), new Vector2(0, 1), Blue);
            MakeGlyph("Önceki", _previousButton.transform, HudGlyph.Kind.Previous, Cream);
            _previousButton.onClick.AddListener(delegate { if (OnPrevLevel != null) OnPrevLevel(); });
            _nextButton = MakeButton("Sonraki bölüm", panel.transform, new Vector2(38, 38), new Vector2(-26, -12), new Vector2(1, 1), Blue);
            MakeGlyph("Sonraki", _nextButton.transform, HudGlyph.Kind.Next, Cream);
            _nextButton.onClick.AddListener(delegate { if (OnNextLevel != null) OnNextLevel(); });
            _levelPosition = MakeLabel("Bölüm sırası", panel.transform, "1 / 6", 14, Hex(0x51424b), TextAnchor.MiddleCenter);
            Anchor(_levelPosition.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(65, 32));
            _levelPanel.SetActive(false);
        }

        private void BuildToast()
        {
            Image toast = MakeImage("Kısa bildirim", _design, new Color(.18f, .13f, .17f, .92f), false);
            toast.sprite = _rounded; toast.type = Image.Type.Sliced;
            Anchor(toast.rectTransform, new Vector2(.5f, 0), new Vector2(0, 143), new Vector2(294, 48));
            _toast = toast.gameObject;
            _toastLabel = MakeLabel("Bildirim metni", toast.transform, "", 13, Cream, TextAnchor.MiddleCenter);
            Stretch(_toastLabel.rectTransform, 12, 7, 12, 7);
            _toast.SetActive(false);
        }

        private void BuildOverlay()
        {
            Image shade = MakeImage("Sonuç ve yardım", _design, new Color(.10f, .07f, .11f, .48f), true);
            Stretch(shade.rectTransform); _overlay = shade.gameObject;
            Image panel = MakeImage("İleti kutusu", shade.transform, Hex(0xf3e8df), true);
            panel.sprite = _rounded; panel.type = Image.Type.Sliced;
            panel.rectTransform.sizeDelta = new Vector2(300, 258);
            _overlayTitle = MakeLabel("İleti başlığı", panel.transform, "Bölüm tamamlandı", 22, Hex(0x3c2d3a), TextAnchor.MiddleCenter);
            Anchor(_overlayTitle.rectTransform, new Vector2(.5f, 1), new Vector2(0, -33), new Vector2(274, 52));
            _overlayBody = MakeLabel("İleti açıklaması", panel.transform, "", 14, Hex(0x66525d), TextAnchor.MiddleCenter);
            Anchor(_overlayBody.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 20), new Vector2(262, 81));
            _overlayBody.fontStyle = FontStyle.Normal;
            _overlayBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _overlayBody.verticalOverflow = VerticalWrapMode.Truncate;
            Button primary = MakeButton("Devam", panel.transform, new Vector2(236, 43), new Vector2(0, 77), new Vector2(.5f, 0), Blue);
            _primaryLabel = MakeLabel("Devam metni", primary.transform, "Sıradaki bölüm", 15, Cream, TextAnchor.MiddleCenter);
            Stretch(_primaryLabel.rectTransform);
            primary.onClick.AddListener(delegate { Action callback = _primaryAction; HideOverlay(); if (callback != null) callback(); });
            Button secondary = MakeButton("Tekrar", panel.transform, new Vector2(236, 38), new Vector2(0, 29), new Vector2(.5f, 0), Hex(0xd8c8bd));
            _secondaryObject = secondary.gameObject;
            _secondaryLabel = MakeLabel("Tekrar metni", secondary.transform, "Tekrar oyna", 14, Hex(0x554150), TextAnchor.MiddleCenter);
            Stretch(_secondaryLabel.rectTransform);
            secondary.onClick.AddListener(delegate { Action callback = _secondaryAction; HideOverlay(); if (callback != null) callback(); });
            _overlay.SetActive(false);
        }

        public void SetState(int levelNumber, int levelCount, bool paused, int powerLeft, bool powerArmed,
            bool canPrev, bool canNext, int speed = 1)
        {
            _paused = paused;
            _levelLabel.text = "Bölüm " + levelNumber;
            _levelPosition.text = levelNumber + " / " + Mathf.Max(1, levelCount);
            _speedLabel.text = speed > 1 ? "2×" : "1×";
            _speedLabel.color = speed > 1 ? Hex(0xffdc86) : Muted;
            _pauseGlyph.Symbol = paused ? HudGlyph.Kind.Play : HudGlyph.Kind.Pause;
            _pauseGlyph.SetVerticesDirty();
            _powerCount.text = Mathf.Max(0, powerLeft).ToString();
            _powerButton.interactable = powerLeft > 0;
            _powerBackground.color = powerArmed ? Hex(0x94f4ff) : Cyan;
            _previousButton.interactable = canPrev;
            _nextButton.interactable = canNext;
        }

        public void ShowWin(Action next, Action replay)
        {
            ShowOverlay("Bölüm tamamlandı", "", "Sıradaki bölüm", next, "Tekrar oyna", replay);
        }

        public void ShowLoss(string reason, Action retry)
        {
            ShowOverlay("Yeniden deneyelim", reason ?? "", "Tekrar oyna", retry, null, null);
        }

        public void HideOverlay()
        {
            if (_overlay != null) _overlay.SetActive(false);
            _primaryAction = null; _secondaryAction = null;
        }

        public void SetEnabled(bool value)
        {
            _canvasGroup.alpha = value ? 1f : 0f;
            _canvasGroup.interactable = value;
            _canvasGroup.blocksRaycasts = value;
        }

        public void ShowMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _toastLabel.text = message; _toastUntil = Time.unscaledTime + 1.6f;
            _toast.SetActive(true); _toast.transform.SetAsLastSibling();
        }

        private void OpenHelp()
        {
            _levelPanel.SetActive(false);
            _resumeAfterHelp = !_paused;
            if (_resumeAfterHelp && OnPause != null) OnPause();
            ShowOverlay("Nasıl oynanır?", "Bir UFO seç. Kendi rengine ateş eder. 0 olunca kaybolur.\n\nPervaneye, sonra UFO’ya dokun: bir kez havadan ateş et.",
                "Devam et", delegate { if (_resumeAfterHelp && _paused && OnPause != null) OnPause(); _resumeAfterHelp = false; }, null, null);
        }

        private void ShowOverlay(string title, string body, string primary, Action primaryAction,
            string secondary, Action secondaryAction)
        {
            _levelPanel.SetActive(false); _toast.SetActive(false);
            _overlayTitle.text = title; _overlayBody.text = body;
            _overlayBody.gameObject.SetActive(!string.IsNullOrEmpty(body));
            _primaryLabel.text = primary; _primaryAction = primaryAction;
            _secondaryObject.SetActive(!string.IsNullOrEmpty(secondary));
            _secondaryLabel.text = secondary ?? ""; _secondaryAction = secondaryAction;
            _overlay.SetActive(true); _overlay.transform.SetAsLastSibling();
        }

        private void LateUpdate()
        {
            if (_screenWidth != Screen.width || _screenHeight != Screen.height) FitPortrait();
            if (_toast != null && _toast.activeSelf && Time.unscaledTime >= _toastUntil) _toast.SetActive(false);
        }

        private void FitPortrait()
        {
            if (_design == null) return;
            _screenWidth = Screen.width; _screenHeight = Screen.height;
            RectTransform root = (RectTransform)transform;
            float width = root.rect.width;
            float height = root.rect.height;
            if (width <= 0 || height <= 0) return;
            _design.localScale = Vector3.one * Mathf.Min(width / 390f, height / 844f);
            _design.anchoredPosition = Vector2.zero;
        }

        private Button IconButton(string name, Vector2 offset, HudGlyph.Kind glyph, bool blue)
        {
            Button button = MakeButton(name, _design, new Vector2(30, 32), offset, new Vector2(1, 1), blue ? Blue : new Color(1, 1, 1, 0));
            MakeGlyph(name + " çizgisi", button.transform, glyph, blue ? Cream : Muted);
            return button;
        }

        private Button MakeButton(string name, Transform parent, Vector2 size, Vector2 offset, Vector2 anchor, Color paint)
        {
            Image image = MakeImage(name, parent, paint, true);
            image.sprite = _rounded; image.type = Image.Type.Sliced;
            Anchor(image.rectTransform, anchor, offset, size);
            if (anchor.y >= .99f) image.rectTransform.pivot = new Vector2(anchor.x == 0 ? 0 : .5f, 1);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1, 1, 1, 1);
            colors.pressedColor = new Color(.82f, .88f, .95f, 1);
            colors.disabledColor = new Color(.58f, .63f, .70f, .55f);
            colors.fadeDuration = .08f;
            button.colors = colors;
            Navigation navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            return button;
        }

        private HudGlyph MakeGlyph(string name, Transform parent, HudGlyph.Kind kind, Color paint)
        {
            RectTransform rect = MakeRect(name, parent, new Vector2(22, 22));
            HudGlyph graphic = rect.gameObject.AddComponent<HudGlyph>();
            graphic.Symbol = kind; graphic.color = paint; graphic.raycastTarget = false;
            return graphic;
        }

        private Image MakeImage(string name, Transform parent, Color paint, bool raycast)
        {
            RectTransform rect = MakeRect(name, parent, Vector2.zero);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = paint; image.raycastTarget = raycast;
            return image;
        }

        private Text MakeLabel(string name, Transform parent, string content, int size, Color paint, TextAnchor alignment)
        {
            RectTransform rect = MakeRect(name, parent, Vector2.zero);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = _font; label.fontSize = size; label.fontStyle = FontStyle.Bold;
            label.text = content; label.color = paint; label.alignment = alignment; label.raycastTarget = false;
            label.supportRichText = false;
            return label;
        }

        private static RectTransform MakeRect(string name, Transform parent, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = size;
            return rect;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
        }

        private static Color Hex(uint rgb)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1);
        }

        private static Font LoadFont()
        {
            try
            {
                Font legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (legacy != null) return legacy;
            }
            catch (ArgumentException) { }
            return Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Liberation Sans", "DejaVu Sans" }, 16);
        }

        private static Sprite MakeRoundedSprite(int pixels, int radius)
        {
            Texture2D texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
            texture.name = "PixelFlow rounded interface shape";
            texture.wrapMode = TextureWrapMode.Clamp; texture.filterMode = FilterMode.Bilinear;
            Color32[] data = new Color32[pixels * pixels];
            for (int y = 0; y < pixels; y++)
                for (int x = 0; x < pixels; x++)
                {
                    float nearestX = Mathf.Clamp(x + .5f, radius, pixels - radius);
                    float nearestY = Mathf.Clamp(y + .5f, radius, pixels - radius);
                    float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(nearestX, nearestY));
                    byte alpha = (byte)(Mathf.Clamp01(radius + .5f - distance) * 255f);
                    data[y * pixels + x] = new Color32(255, 255, 255, alpha);
                }
            texture.SetPixels32(data); texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, pixels, pixels), new Vector2(.5f, .5f), 100,
                0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
