using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// "Nasıl Oynanır": sayfa sayfa, solda döngüde oynayan kısa oyun videosu, sağda başlık + açıklama.
//   - İlk oyunda (oyun sahnesi ilk kez açılınca) kendiliğinden açılır ve oyunu dondurur; son sayfada "BAŞLA".
//   - Ana menüdeki HOW TO PLAY butonu istediğin zaman açar (MainMenuUI).
// Klavye: ←/→ (A/D) sayfa, Enter/Space ileri, ESC atla. Gamepad: d-pad/analog, A ileri, B atla.
//
// Videolar: Resources/Tutorial/<sayfa id>.webm (VP8 WebM: Linux editörü de oynatabilir).
// Klibi olmayan sayfada sayfanın ikonu gösterilir. Klip çekmek için oyun açıkken:
//   Menü > TopDownShooter > Tutorial > Klip Kaydet > <sayfa>   (TutorialClipRecorder, 8 sn kaydeder)
// Kendini kurar: sahneye eklemek gerekmez.
public class TutorialPanel : MonoBehaviour
{
    const string KeySeen = "tutorial_intro_seen";

    // Sayfalar: id (video + ikon adı), başlık, açıklama (açılışta seçili dile çevrilir)
    struct Page { public string id, title, body; }

    static Page[] Pages => new[]
    {
        new Page { id = "move", title = Loc.T("MOVE & SHOOT"),
            body = Loc.F("Move with <b>{0}</b>. Your weapons fire <color=#73EFF7>automatically</color> at nearby enemies - focus on dodging!",
                         InputMode.Key("WASD", Loc.T("Left stick"))) },
        new Page { id = "levelup", title = Loc.T("EXP & LEVEL UP"),
            body = Loc.F("Enemies drop <color=#E54658>red orbs</color>. Collect them to level up and <b>pick one of three cards</b>. Weapon cards cost <color=#FFCD75>coins</color> - press <b>{0}</b> to reroll.",
                         InputMode.Key("R", "Y")) },
        new Page { id = "weapons", title = Loc.T("WEAPONS & MERGE"),
            body = Loc.T("You can carry up to <b>6 weapons</b>. Buy the <b>same weapon</b> again to <color=#C78BFF>MERGE</color> it into a stronger tier.") },
        new Page { id = "skills", title = Loc.T("SKILLS"),
            body = Loc.F("Skill cards give you active abilities. Use them with <b>{0}</b>. They recharge over time, and some pairs unlock powerful <color=#73EFF7>SYNERGIES</color>.",
                         InputMode.Key("Space / E / R / Q", "A / X / Y / RB")) },
        new Page { id = "waves", title = Loc.T("WAVES & BOSSES"),
            body = Loc.T("Waves never end and keep getting harder. Watch the countdown at the top of the screen. <color=#E5533C>Bosses</color> hit hard - keep moving!") },
        new Page { id = "quests", title = Loc.T("WAVE QUESTS"),
            body = Loc.T("Each wave brings a small <color=#FFCD75>QUEST</color> (top right). Finish it before the wave ends to earn coins, EXP or health.") },
        new Page { id = "grid", title = Loc.T("FLOOR GRID"),
            body = Loc.T("Kills <color=#73EFF7>charge</color> the floor tile under the enemy. Charge a <b>2x2</b> block to <color=#73EFF7>OVERLOAD</color> it: shock, slow or magnet for a few seconds.") },
        new Page { id = "reactor", title = Loc.T("REACTOR DEFENSE"),
            body = Loc.T("Enemies raid one of the four <color=#A7F070>reactors</color>. Follow the arrow and defend it to make loot burst out. If it falls, you lose <color=#FFCD75>coins</color>.") },
        new Page { id = "core", title = Loc.T("CORE & UPGRADES"),
            body = Loc.T("When a run ends you earn <color=#73EFF7>CORE</color>. Spend it in <color=#73EFF7>UPGRADES</color> on the main menu for permanent bonuses and new unlocks.") },
    };

    public static bool IsOpen { get; private set; }
    // Kapatan tuş (ESC/Enter) aynı karede pause menüsünü / ana menüyü tetiklemesin
    public static int LastClosedFrame { get; private set; } = -1;
    public static bool BlocksInput => IsOpen || LastClosedFrame == Time.frameCount;
    public static bool Seen => PlayerPrefs.GetInt(KeySeen, 0) == 1;

    static readonly Color Cyan = new Color32(0x73, 0xEF, 0xF7, 0xFF);
    static readonly Color Muted = new Color32(0x94, 0xB0, 0xC2, 0xFF);
    static readonly Color Green = new Color32(0xA7, 0xF0, 0x70, 0xFF);
    static readonly Color Dim = new Color(0.03f, 0.03f, 0.07f, 0.88f);

    Page[] pages;
    int index;
    bool firstRun;
    float prevTimeScale = 1f;
    System.Action onClose;

    TMP_FontAsset font;
    Sprite frame;
    CanvasGroup rootGroup, contentGroup;
    RectTransform window, content;
    TMP_Text counterText, pageTitle, pageBody, nextLabel;
    Image pageIcon, fallbackIcon;
    RawImage videoImage;
    VideoPlayer player;
    RenderTexture rt;
    Image[] dots;
    GameObject backButton;
    float openAnim, pageAnim;

    // ================================================================ açma
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { IsOpen = false; LastClosedFrame = -1; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded += (s, m) => TryIntro();
        TryIntro();
    }

    // İlk oyun: oyun sahnesi açılınca bir kez
    static void TryIntro()
    {
        if (Seen || IsOpen) return;
        if (FindAnyObjectByType<WaveManager>() == null) return;
        Show(true);
    }

    public static void Show(bool firstRun = false, System.Action onClose = null)
    {
        if (IsOpen) return;
        var go = new GameObject("TutorialPanel");
        var p = go.AddComponent<TutorialPanel>();
        p.firstRun = firstRun;
        p.onClose = onClose;
        p.Build();
    }

    public static void ResetSeen() { PlayerPrefs.DeleteKey(KeySeen); PlayerPrefs.Save(); }

    void Build()
    {
        IsOpen = true;
        pages = Pages;
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;   // oyun sahnesinde oyunu dondur (menüde zararsız)

        var ui = FindAnyObjectByType<TextMeshProUGUI>();
        if (ui != null) font = ui.font;
        frame = Resources.Load<Sprite>("Reactor/hud_frame");

        // Canvas (her şeyin üstünde)
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        rootGroup = canvasGo.GetComponent<CanvasGroup>();
        var root = (RectTransform)canvasGo.transform;

        // Karartma (arkadaki UI'a tıklanmasın)
        var dim = NewImage("Dim", root, Dim);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        // Pencere
        var win = NewImage("Window", root, Color.white, frame);
        window = win.rectTransform;
        window.sizeDelta = new Vector2(1560f, 860f);
        if (frame == null) win.color = new Color(0.07f, 0.08f, 0.13f, 1f);

        Text(window, Loc.T("HOW TO PLAY"), 52f, Cyan, TextAlignmentOptions.Left, new Vector2(-400f, 360f), new Vector2(700f, 70f));
        counterText = Text(window, "", 30f, Muted, TextAlignmentOptions.Right, new Vector2(390f, 360f), new Vector2(300f, 60f));
        MakeButton(window, Loc.T("SKIP"), new Vector2(630f, 360f), new Vector2(200f, 70f), Muted, Close);

        // Sayfa içeriği (sayfa değişince kayarak belirir)
        content = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        content.SetParent(window, false);
        content.sizeDelta = window.sizeDelta;
        contentGroup = content.GetComponent<CanvasGroup>();

        // Video alanı (çerçeve + video + klip yoksa ikon)
        var vFrame = NewImage("VideoFrame", content, Color.white, frame);
        vFrame.rectTransform.anchoredPosition = new Vector2(-340f, 10f);
        vFrame.rectTransform.sizeDelta = new Vector2(860f, 560f);
        if (frame == null) vFrame.color = new Color(0.12f, 0.14f, 0.2f, 1f);
        var vBg = NewImage("VideoBg", vFrame.rectTransform, new Color(0.1f, 0.11f, 0.16f, 1f));
        vBg.rectTransform.sizeDelta = new Vector2(816f, 516f);
        fallbackIcon = NewImage("Fallback", vBg.rectTransform, Color.white);
        fallbackIcon.rectTransform.sizeDelta = new Vector2(220f, 220f);
        fallbackIcon.preserveAspect = true;
        videoImage = new GameObject("Video", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        videoImage.rectTransform.SetParent(vBg.rectTransform, false);
        videoImage.rectTransform.sizeDelta = new Vector2(816f, 516f);
        videoImage.raycastTarget = false;

        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = true;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;   // oyun donukken de oynasın
        player.skipOnDrop = true;
        player.prepareCompleted += OnVideoPrepared;

        // Sağ sütun: ikon + başlık + açıklama
        pageIcon = NewImage("Icon", content, Color.white);
        pageIcon.rectTransform.anchoredPosition = new Vector2(180f, 210f);
        pageIcon.rectTransform.sizeDelta = new Vector2(96f, 96f);
        pageIcon.preserveAspect = true;
        pageTitle = Text(content, "", 46f, Cyan, TextAlignmentOptions.Left, new Vector2(430f, 210f), new Vector2(400f, 110f));
        pageTitle.textWrappingMode = TextWrappingModes.Normal;
        pageBody = Text(content, "", 32f, Color.white, TextAlignmentOptions.TopLeft, new Vector2(375f, -40f), new Vector2(560f, 380f));
        pageBody.textWrappingMode = TextWrappingModes.Normal;
        pageBody.lineSpacing = 18f;
        pageBody.fontSizeMin = 22f;

        // Alt: sayfa noktaları + GERİ / İLERİ
        dots = new Image[pages.Length];
        for (int i = 0; i < pages.Length; i++)
        {
            dots[i] = NewImage("Dot", window, Color.white);
            dots[i].rectTransform.sizeDelta = new Vector2(18f, 18f);
            dots[i].rectTransform.anchoredPosition = new Vector2((i - (pages.Length - 1) * 0.5f) * 34f, -345f);
        }
        backButton = MakeButton(window, Loc.T("BACK"), new Vector2(-560f, -345f), new Vector2(300f, 84f), Color.white, () => Go(index - 1));
        var next = MakeButton(window, "", new Vector2(560f, -345f), new Vector2(300f, 84f), Green, () => Go(index + 1));
        nextLabel = next.GetComponentInChildren<TMP_Text>();

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(next);
        AudioManager.Play(SfxId.UiClick);
        Show(0, instant: true);
    }

    // ================================================================ sayfalar
    void Go(int i)
    {
        if (i >= pages.Length) { Close(); return; }
        if (i < 0 || i == index) return;
        AudioManager.Play(SfxId.UiHover);
        Show(i);
    }

    void Show(int i, bool instant = false)
    {
        int dir = i >= index ? 1 : -1;
        index = i;
        var p = pages[i];

        counterText.text = $"{i + 1} / {pages.Length}";
        pageTitle.text = p.title;
        pageBody.text = p.body;
        var icon = LoadIcon(p.id);
        pageIcon.sprite = icon;
        pageIcon.enabled = icon != null;
        fallbackIcon.sprite = icon;
        fallbackIcon.enabled = icon != null;

        bool last = i == pages.Length - 1;
        nextLabel.text = last ? Loc.T(firstRun ? "START!" : "CLOSE") : Loc.T("NEXT");
        backButton.SetActive(i > 0);
        for (int d = 0; d < dots.Length; d++)
        {
            dots[d].color = d == i ? Cyan : new Color(1f, 1f, 1f, 0.25f);
            dots[d].rectTransform.sizeDelta = Vector2.one * (d == i ? 22f : 16f);
        }

        PlayClip(p.id);
        pageAnim = instant ? 1f : 0f;
        slideDir = dir;
    }

    int slideDir = 1;

    static Sprite LoadIcon(string id)
    {
        if (id == "reactor") return Resources.Load<Sprite>("Reactor/reactor_0");
        var all = Resources.LoadAll<Sprite>("Tutorial/Icons/" + id);
        if (all != null && all.Length > 0) return all[0];
        return id == "levelup" ? LoadIcon("exp") : null;
    }

    void PlayClip(string id)
    {
        player.Stop();
        videoImage.enabled = false;   // hazır olana kadar ikon görünsün
        var clip = Resources.Load<VideoClip>("Tutorial/" + id);
        if (clip == null) return;

        if (rt == null || rt.width != (int)clip.width || rt.height != (int)clip.height)
        {
            if (rt != null) rt.Release();
            rt = new RenderTexture((int)clip.width, (int)clip.height, 0) { filterMode = FilterMode.Point };
        }
        player.clip = clip;
        player.targetTexture = rt;
        videoImage.texture = rt;
        FitVideo(clip.width, clip.height);
        player.Prepare();
    }

    void OnVideoPrepared(VideoPlayer vp)
    {
        vp.Play();
        videoImage.enabled = true;
    }

    // Video alanına en-boy oranını bozmadan sığdır
    void FitVideo(float w, float h)
    {
        Vector2 box = new Vector2(816f, 516f);
        float k = Mathf.Min(box.x / w, box.y / h);
        videoImage.rectTransform.sizeDelta = new Vector2(w * k, h * k);
    }

    // ================================================================ her kare
    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        openAnim = Mathf.MoveTowards(openAnim, 1f, dt * 5f);
        rootGroup.alpha = openAnim;
        window.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, 1f - (1f - openAnim) * (1f - openAnim));

        pageAnim = Mathf.MoveTowards(pageAnim, 1f, dt * 5f);
        float e = 1f - (1f - pageAnim) * (1f - pageAnim);
        contentGroup.alpha = e;
        content.anchoredPosition = new Vector2((1f - e) * 40f * slideDir, 0f);

        // Klipsiz sayfada ikon hafifçe nabız atar
        if (fallbackIcon.enabled && !videoImage.enabled)
            fallbackIcon.rectTransform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(Time.unscaledTime * 3f));

        var kb = Keyboard.current;
        bool next = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame))
                    || InputMode.HorizontalStep > 0;
        bool prev = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame))
                    || InputMode.HorizontalStep < 0;
        bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                       || InputMode.ConfirmPressed;
        bool skip = (kb != null && kb.escapeKey.wasPressedThisFrame) || InputMode.BackPressed;

        // Enter/A: seçili buton varsa EventSystem zaten tıklar (çift adım olmasın)
        bool buttonSelected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null;
        if (skip) Close();
        else if (next || (confirm && !buttonSelected)) Go(index + 1);
        else if (prev) Go(index - 1);
    }

    void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        LastClosedFrame = Time.frameCount;
        PlayerPrefs.SetInt(KeySeen, 1);
        PlayerPrefs.Save();
        Time.timeScale = firstRun ? 1f : prevTimeScale;
        AudioManager.Play(firstRun ? SfxId.UiStart : SfxId.UiBack);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        onClose?.Invoke();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (IsOpen) { IsOpen = false; Time.timeScale = prevTimeScale; }   // sahne değişirse
        if (rt != null) rt.Release();
    }

    // ================================================================ UI yardımcıları
    Image NewImage(string name, RectTransform parent, Color color, Sprite sprite = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var img = go.GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.sprite = sprite != null ? sprite : null;
        if (sprite != null && sprite.border != Vector4.zero) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 0.5f; }
        if (sprite == null && name == "Dot") img.sprite = RuntimeSprite.Circle;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    TMP_Text Text(RectTransform parent, string text, float size, Color color, TextAlignmentOptions align, Vector2 pos, Vector2 box)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        var t = go.GetComponent<TextMeshProUGUI>();
        t.rectTransform.SetParent(parent, false);
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = box;
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = size * 0.6f;   // uzun çeviriler sığsın
        t.richText = true;
        t.raycastTarget = false;
        go.AddComponent<KeepFontSize>();   // TextFitter dokunmasın (boyutu biz yönetiyoruz)
        return t;
    }

    GameObject MakeButton(RectTransform parent, string label, Vector2 pos, Vector2 size, Color tint, UnityEngine.Events.UnityAction onClick)
    {
        var img = NewImage("Button", parent, tint, frame);
        if (frame == null) img.color = new Color(0.15f, 0.17f, 0.25f, 1f);
        img.raycastTarget = true;
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = size;
        var btn = img.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.selectedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);
        img.gameObject.AddComponent<UIHoverScale>();
        var t = Text(img.rectTransform, label, 32f, tint == Green ? Green : Color.white, TextAlignmentOptions.Center, Vector2.zero, size - new Vector2(30f, 16f));
        t.name = "Label";
        return img.gameObject;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
