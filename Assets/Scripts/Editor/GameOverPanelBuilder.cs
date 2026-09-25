using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Game Over panelini sahnede baştan kurar: karartma, pencere, başlık, 6 istatistik kartı
// (ikon + değer + rekor + NEW! rozeti), "NEW RECORD!" şeridi, Core (kalıcı ilerleme) şeridi +
// "NEW UNLOCK" bölümü ve Restart / Main Menu / Quit butonları. GameOverUI'daki tüm
// referansları ve butonların OnClick'lerini bağlar.
//
// Panel zaten kuruluysa ve sadece Core bölümü eksikse: Menü > TopDownShooter > UI >
// Game Over'a Core Bölümü Ekle (paneli silmez; pencereyi uzatıp butonları aşağı kaydırır).
//
// Kullanım: MainGame sahnesi açıkken Menü > TopDownShooter > UI > Game Over Panelini Kur
// Tekrar çalıştırmak güvenli: panelin ESKİ içeriği silinip yeniden kurulur. Sonrasında her
// şeyi Inspector'dan elle düzenleyebilirsin (renk, yazı, konum...). Ama elle yaptığın
// değişiklikler bu menüyü tekrar çalıştırınca kaybolur.
public static class GameOverPanelBuilder
{
    const string ScenePath = "Assets/Scenes/MainGame.unity";
    const string IconDir = "Assets/Art/Generated/Icons/GameOver/";
    const string FontPath = "Assets/Art/UI/PixCon SDF.asset";
    const string FramePanel = "Assets/Art/Generated/UI/frame_panel.png";
    const string FrameSlot = "Assets/Art/Generated/UI/frame_slot.png";
    const string CoinIcon = "Assets/Art/Generated/Pickups/pickup_coin.png";
    const string DamageIcon = "Assets/Art/Generated/Icons/Upgrades/stat_damage.png";
    const string CoreIcon = "Assets/Art/Generated_v2/HUD/icon_core.png";

    // Core bölümü: şerit (78) + boşluk (8) + NEW UNLOCK (80) ve üstündeki boşluk
    const float CoreSectionH = 170f, CoreSectionGap = 14f;

    // Renkler (Sweetie-16 paleti, ikonlarla uyumlu)
    static readonly Color Dim = new Color(0.04f, 0.04f, 0.08f, 0.82f);
    static readonly Color TitleRed = new Color32(0xE5, 0x53, 0x3C, 0xFF);
    static readonly Color Muted = new Color32(0x94, 0xB0, 0xC2, 0xFF);
    static readonly Color Gold = new Color32(0xFF, 0xCD, 0x75, 0xFF);
    static readonly Color White = new Color32(0xF4, 0xF4, 0xF4, 0xFF);
    static readonly Color PrimaryTint = new Color32(0xA7, 0xF0, 0x70, 0xFF);   // Restart: yeşilimsi
    static readonly Color CoreCyan = new Color32(0x73, 0xEF, 0xF7, 0xFF);

    static TMP_FontAsset font;

    [MenuItem("TopDownShooter/UI/Game Over Panelini Kur")]
    static void BuildMenu()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Game Over", "Önce Play Mode'dan çık.", "Tamam");
            return;
        }
        if (!EditorUtility.DisplayDialog("Game Over",
                "GameOverPanel'in içi silinip yeniden kurulacak. Devam edilsin mi?", "Kur", "Vazgeç"))
            return;

        if (Build()) Debug.Log("[GameOverPanelBuilder] Panel kuruldu. Sahneyi kaydetmeyi unutma (Ctrl+S).");
    }

    // Komut satırından (batchmode) çalıştırmak için: sahneyi açar, kurar, kaydeder.
    public static void BuildFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath);
        if (!Build()) { EditorApplication.Exit(1); return; }
        EditorSceneManager.SaveOpenScenes();
    }

    static bool Build()
    {
        var ui = Object.FindAnyObjectByType<GameOverUI>(FindObjectsInactive.Include);
        if (ui == null) { Debug.LogError("[GameOverPanelBuilder] Sahnede GameOverUI yok."); return false; }

        var so = new SerializedObject(ui);
        var panelGo = so.FindProperty("panel").objectReferenceValue as GameObject;
        if (panelGo == null) { Debug.LogError("[GameOverPanelBuilder] GameOverUI.panel atanmamış."); return false; }

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // ---- Kök: tam ekran karartma ----
        var panel = (RectTransform)panelGo.transform;
        for (int i = panel.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(panel.GetChild(i).gameObject);

        Stretch(panel);
        panel.SetAsLastSibling();   // diğer HUD'ların önünde
        var bg = GetOrAdd<Image>(panelGo);
        bg.sprite = null;
        bg.type = Image.Type.Simple;
        bg.color = Dim;
        bg.raycastTarget = true;    // arkadaki HUD'a tıklanmasın
        var group = GetOrAdd<CanvasGroup>(panelGo);

        // ---- Pencere ----
        var window = Frame("Window", panel, FramePanel, new Vector2(900f, 840f + CoreSectionH + CoreSectionGap));
        window.anchoredPosition = Vector2.zero;

        Icon("SkullIcon", window, IconDir + "ui_skull.png", new Vector2(88f, 88f), Top(0f, -36f));
        Text("Title", window, "GAME OVER", 76f, TitleRed, TextAlignmentOptions.Center,
             Top(0f, -128f), new Vector2(820f, 90f));
        var subtitle = Text("Subtitle", window, "WAVE 1  -  RUN #1", 28f, Muted, TextAlignmentOptions.Center,
                            Top(0f, -190f), new Vector2(820f, 40f));

        // "NEW RECORD!" şeridi
        var banner = new GameObject("NewRecordBanner", typeof(RectTransform));
        var brt = (RectTransform)banner.transform;
        brt.SetParent(window, false);
        Place(brt, Top(0f, -240f), new Vector2(420f, 48f));
        Icon("Trophy", brt, IconDir + "ui_trophy.png", new Vector2(44f, 44f), Left(0f, 0f));
        Text("Label", brt, "NEW RECORD!", 36f, Gold, TextAlignmentOptions.Left,
             Left(56f, 0f), new Vector2(360f, 48f));
        banner.SetActive(false);

        // ---- İstatistik kartları (2 sütun x 3 satır) ----
        const float cardW = 400f, cardH = 112f, gapX = 20f, gapY = 18f, gridTop = -290f;
        string[] labels = { "TIME SURVIVED", "WAVE REACHED", "ENEMIES KILLED", "LEVEL", "COINS COLLECTED", "DAMAGE DEALT" };
        string[] icons = { IconDir + "ui_time.png", IconDir + "ui_wave.png", IconDir + "ui_skull.png",
                           IconDir + "ui_level.png", CoinIcon, DamageIcon };
        string[] rows = { "timeRow", "waveRow", "killsRow", "levelRow", "coinsRow", "damageRow" };

        for (int i = 0; i < rows.Length; i++)
        {
            int col = i % 2, row = i / 2;
            float x = (col == 0 ? -1f : 1f) * (cardW + gapX) * 0.5f;
            float y = gridTop - cardH * 0.5f - row * (cardH + gapY);

            var card = Frame("Stat_" + rows[i].Replace("Row", ""), window, FrameSlot, new Vector2(cardW, cardH));
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
            card.anchoredPosition = new Vector2(x, y);

            Icon("Icon", card, icons[i], new Vector2(64f, 64f), Left(22f, 0f));
            Text("Label", card, labels[i], 20f, Muted, TextAlignmentOptions.Left,
                 TopLeft(104f, -16f), new Vector2(280f, 26f));
            var value = Text("Value", card, "0", 42f, White, TextAlignmentOptions.Left,
                             TopLeft(104f, -44f), new Vector2(170f, 52f));
            var best = Text("Best", card, "BEST 0", 18f, Gold, TextAlignmentOptions.Right,
                            BottomRight(-18f, 14f), new Vector2(200f, 24f));

            // NEW! rozeti (sağ üst)
            var badge = new GameObject("NewBest", typeof(RectTransform));
            var bdrt = (RectTransform)badge.transform;
            bdrt.SetParent(card, false);
            Place(bdrt, TopRight(-14f, -12f), new Vector2(110f, 32f));
            Icon("Trophy", bdrt, IconDir + "ui_trophy.png", new Vector2(30f, 30f), Left(0f, 0f));
            Text("Label", bdrt, "NEW!", 24f, Gold, TextAlignmentOptions.Left, Left(36f, 0f), new Vector2(74f, 32f));
            badge.SetActive(false);

            var rp = so.FindProperty(rows[i]);
            rp.FindPropertyRelative("value").objectReferenceValue = value;
            rp.FindPropertyRelative("best").objectReferenceValue = best;
            rp.FindPropertyRelative("newBestBadge").objectReferenceValue = badge;
        }

        // ---- Core (kalıcı ilerleme) + NEW UNLOCK ----
        float gridBottom = gridTop - 3 * cardH - 2 * gapY;
        BuildCoreSection(window, so, gridBottom - CoreSectionGap);

        // ---- Butonlar ----
        float btnY = gridBottom - CoreSectionH - CoreSectionGap - 70f;
        var restart = MakeButton("RestartButton", window, "RESTART", IconDir + "ui_restart.png",
                                 new Vector2(-285f, btnY), PrimaryTint, ui.Restart);
        MakeButton("MainMenuButton", window, "MAIN MENU", IconDir + "ui_home.png",
                   new Vector2(0f, btnY), White, ui.GoToMainMenu);
        MakeButton("QuitButton", window, "QUIT", IconDir + "ui_quit.png",
                   new Vector2(285f, btnY), White, ui.QuitGame);

        // ---- GameOverUI referansları ----
        so.FindProperty("window").objectReferenceValue = window;
        so.FindProperty("canvasGroup").objectReferenceValue = group;
        so.FindProperty("subtitleText").objectReferenceValue = subtitle;
        so.FindProperty("newRecordBanner").objectReferenceValue = banner;
        so.FindProperty("firstSelected").objectReferenceValue = restart;
        var runStats = GetOrAdd<RunStats>(ui.gameObject);
        so.FindProperty("runStats").objectReferenceValue = runStats;
        so.ApplyModifiedPropertiesWithoutUndo();

        panelGo.SetActive(false);   // sahnede kapalı başlamalı
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        return true;
    }

    // Kurulu panele sadece Core bölümünü ekler (panelin geri kalanına dokunmaz).
    [MenuItem("TopDownShooter/UI/Game Over'a Core Bölümü Ekle")]
    static void AddCoreSectionMenu()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("Game Over", "Önce Play Mode'dan çık.", "Tamam"); return; }

        var ui = Object.FindAnyObjectByType<GameOverUI>(FindObjectsInactive.Include);
        var so = ui != null ? new SerializedObject(ui) : null;
        var window = so?.FindProperty("window").objectReferenceValue as RectTransform;
        if (window == null)
        {
            EditorUtility.DisplayDialog("Game Over", "Kurulu Game Over paneli bulunamadı. Önce 'Game Over Panelini Kur'.", "Tamam");
            return;
        }
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // Varsa eskisini sil; yoksa pencereyi uzat ve butonları aşağı kaydır (bir kez)
        var old = window.Find("CoreSection");
        if (old != null)
        {
            var oldUnlock = window.Find("UnlockSection");
            if (oldUnlock != null) Undo.DestroyObjectImmediate(oldUnlock.gameObject);
            Undo.DestroyObjectImmediate(old.gameObject);
        }
        else
        {
            Undo.RecordObject(window, "Core Bölümü");
            window.sizeDelta += new Vector2(0f, CoreSectionH + CoreSectionGap);
            foreach (Transform child in window)
                if (child.name.EndsWith("Button"))
                {
                    var brt = (RectTransform)child;
                    Undo.RecordObject(brt, "Core Bölümü");
                    brt.anchoredPosition -= new Vector2(0f, CoreSectionH + CoreSectionGap);
                }
        }

        // Kartların alt kenarı: en alttaki Stat_ kartı
        float gridBottom = 0f;
        foreach (Transform child in window)
            if (child.name.StartsWith("Stat_"))
            {
                var crt = (RectTransform)child;
                gridBottom = Mathf.Min(gridBottom, crt.anchoredPosition.y - crt.sizeDelta.y * 0.5f);
            }

        BuildCoreSection(window, so, gridBottom - CoreSectionGap);
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        Debug.Log("[GameOverPanelBuilder] Core bölümü eklendi. Sahneyi kaydetmeyi unutma (Ctrl+S).");
    }

    // Core şeridi: [kristal] CORE EARNED / döküm ............ +61 / TOTAL 245
    // Altında (varsa) NEW UNLOCK: yazı + açılan içeriklerin ikonları.
    static void BuildCoreSection(RectTransform window, SerializedObject so, float top)
    {
        var strip = Frame("CoreSection", window, FrameSlot, new Vector2(840f, 78f));
        strip.anchorMin = strip.anchorMax = new Vector2(0.5f, 1f);
        strip.anchoredPosition = new Vector2(0f, top - 39f);

        Icon("CoreIcon", strip, CoreIcon, new Vector2(52f, 52f), Left(20f, 0f));
        Text("Label", strip, "CORE EARNED", 20f, Muted, TextAlignmentOptions.Left,
             TopLeft(88f, -12f), new Vector2(300f, 26f));
        var breakdown = Text("Breakdown", strip, "WAVES +0   KILLS +0", 18f, White, TextAlignmentOptions.Left,
                             TopLeft(88f, -42f), new Vector2(460f, 24f));
        var earned = Text("Earned", strip, "+0", 42f, CoreCyan, TextAlignmentOptions.Right,
                          TopRight(-24f, -6f), new Vector2(200f, 46f));
        var total = Text("Total", strip, "TOTAL 0", 18f, Gold, TextAlignmentOptions.Right,
                         BottomRight(-24f, 8f), new Vector2(240f, 24f));

        var unlock = new GameObject("UnlockSection", typeof(RectTransform));
        var urt = (RectTransform)unlock.transform;
        urt.SetParent(window, false);
        Place(urt, Top(0f, top - 86f), new Vector2(840f, 80f));
        var unlockText = Text("Label", urt, "NEW UNLOCK: -", 26f, Gold, TextAlignmentOptions.Center,
                              Top(0f, -2f), new Vector2(820f, 32f));
        var icons = new GameObject("Icons", typeof(RectTransform));
        var irt = (RectTransform)icons.transform;
        irt.SetParent(urt, false);
        Place(irt, Top(0f, -36f), new Vector2(820f, 44f));
        irt.pivot = new Vector2(0.5f, 1f);
        unlock.SetActive(false);

        so.FindProperty("coreEarnedText").objectReferenceValue = earned;
        so.FindProperty("coreBreakdownText").objectReferenceValue = breakdown;
        so.FindProperty("coreTotalText").objectReferenceValue = total;
        so.FindProperty("unlockSection").objectReferenceValue = unlock;
        so.FindProperty("unlockText").objectReferenceValue = unlockText;
        so.FindProperty("unlockIcons").objectReferenceValue = irt;
    }

    // ---- Yapı taşları ----

    // Not: '??' Unity objelerinde güvenilmez (Editor'da eksik bileşen "sahte null" döner).
    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    // Konum: (anchor, pivot, anchoredPosition)
    struct Pos { public Vector2 anchor, pivot, offset; }
    static Pos Top(float x, float y) => new Pos { anchor = new Vector2(0.5f, 1f), pivot = new Vector2(0.5f, 1f), offset = new Vector2(x, y) };
    static Pos Left(float x, float y) => new Pos { anchor = new Vector2(0f, 0.5f), pivot = new Vector2(0f, 0.5f), offset = new Vector2(x, y) };
    static Pos TopLeft(float x, float y) => new Pos { anchor = new Vector2(0f, 1f), pivot = new Vector2(0f, 1f), offset = new Vector2(x, y) };
    static Pos TopRight(float x, float y) => new Pos { anchor = new Vector2(1f, 1f), pivot = new Vector2(1f, 1f), offset = new Vector2(x, y) };
    static Pos BottomRight(float x, float y) => new Pos { anchor = new Vector2(1f, 0f), pivot = new Vector2(1f, 0f), offset = new Vector2(x, y) };
    static Pos Center => new Pos { anchor = new Vector2(0.5f, 0.5f), pivot = new Vector2(0.5f, 0.5f) };

    static void Place(RectTransform rt, Pos p, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = p.anchor;
        rt.pivot = p.pivot;
        rt.anchoredPosition = p.offset;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null)   // Multiple modda import edilmişse alt sprite'ı al
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite sp) { s = sp; break; }
        if (s == null) Debug.LogWarning($"[GameOverPanelBuilder] Sprite bulunamadı: {path}");
        return s;
    }

    // 9-slice çerçeve (pixel art kenarı gerilmeden büyür)
    static RectTransform Frame(string name, Transform parent, string spritePath, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Place(rt, Center, size);
        var img = go.GetComponent<Image>();
        img.sprite = LoadSprite(spritePath);
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        return rt;
    }

    static Image Icon(string name, Transform parent, string spritePath, Vector2 size, Pos p)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Place(rt, p, size);
        var img = go.GetComponent<Image>();
        img.sprite = LoadSprite(spritePath);
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    static TMP_Text Text(string name, Transform parent, string text, float size, Color color,
                         TextAlignmentOptions align, Pos p, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Place(rt, p, box);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static GameObject MakeButton(string name, RectTransform parent, string label, string iconPath,
                                 Vector2 pos, Color tint, UnityAction onClick)
    {
        var rt = Frame(name, parent, FrameSlot, new Vector2(260f, 84f));
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;

        var img = rt.GetComponent<Image>();
        img.raycastTarget = true;
        img.color = tint;

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.selectedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        btn.colors = colors;
        UnityEventTools.AddPersistentListener(btn.onClick, onClick);

        rt.gameObject.AddComponent<UIHoverScale>();

        Icon("Icon", rt, iconPath, new Vector2(44f, 44f), Left(24f, 0f));
        Text("Label", rt, label, 28f, White, TextAlignmentOptions.Center,
             new Pos { anchor = new Vector2(0.5f, 0.5f), pivot = new Vector2(0.5f, 0.5f), offset = new Vector2(24f, 0f) },
             new Vector2(190f, 60f));
        return rt.gameObject;
    }
}
