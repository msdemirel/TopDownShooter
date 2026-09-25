using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Menü kurulum script'lerinin (MainMenuBuilder, PauseMenuBuilder) ortak parçaları:
// oyunun pixel stili (çerçeve, ikon, font, renk) ile buton / slider / toggle / dropdown
// ve Options paneli üretir. Böylece tüm menüler aynı görünür.
public static class UIBuilderKit
{
    public const string MenuIcons = "Assets/Art/Generated/Icons/Menu/";
    public const string GameOverIcons = "Assets/Art/Generated/Icons/GameOver/";
    public const string UIDir = "Assets/Art/Generated/UI/";
    public const string FontPath = "Assets/Art/UI/PixCon SDF.asset";

    // Sweetie-16 paleti (oyunun ikonlarıyla aynı)
    public static readonly Color Navy = new Color32(0x0B, 0x0F, 0x1E, 0xFF);
    public static readonly Color Ink = new Color32(0x1A, 0x1C, 0x2C, 0xFF);
    public static readonly Color Cyan = new Color32(0x73, 0xEF, 0xF7, 0xFF);
    public static readonly Color Gold = new Color32(0xFF, 0xCD, 0x75, 0xFF);
    public static readonly Color White = new Color32(0xF4, 0xF4, 0xF4, 0xFF);
    public static readonly Color Muted = new Color32(0x94, 0xB0, 0xC2, 0xFF);
    public static readonly Color PrimaryTint = new Color32(0xA7, 0xF0, 0x70, 0xFF);

    // Oyunun pixel fontu (ilk kullanımda yüklenir)
    static TMP_FontAsset font;
    public static TMP_FontAsset Font => font != null ? font : (font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath));

    // Options paneli (tam ekran, hafif karartmalı): Master/Music/SFX, Fullscreen/VSync,
    // Quality/Resolution, Reset ve Back. OptionsUI panele eklenir ve tüm alanları bağlanır.
    // Ana menü ve pause menüsü aynı paneli kullanır. Panel KAPALI döner.
    public static RectTransform BuildOptionsPanel(RectTransform root, UnityAction onBack, out GameObject backButton)
    {
        var options = Panel("OptionsPanel", root);
        var dim = options.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0.03f, 0.55f);   // menü arkada hafif kararsın, tıklanmasın

        var win = Frame("Window", options, UIDir + "frame_panel.png", Vector2.zero, new Vector2(1000f, 820f));
        Icon("GearIcon", win, MenuIcons + "ui_gear.png", new Vector2(-150f, 350f), 60f);
        Text("Header", win, "OPTIONS", 60f, White, TextAlignmentOptions.Left, new Vector2(80f, 350f), new Vector2(360f, 70f));

        Section(win, "AUDIO", MenuIcons + "ui_speaker.png", 250f);
        var master = SliderRow(win, "MASTER", MenuIcons + "ui_speaker.png", 180f, out var masterVal);
        var music = SliderRow(win, "MUSIC", MenuIcons + "ui_music.png", 110f, out var musicVal);
        var sfx = SliderRow(win, "SFX", MenuIcons + "ui_sfx.png", 40f, out var sfxVal);

        Section(win, "DISPLAY", MenuIcons + "ui_monitor.png", -50f);
        var fullscreen = ToggleRow(win, "FULLSCREEN", -120f, -400f);
        var vsync = ToggleRow(win, "VSYNC", -120f, 60f);
        var quality = DropdownRow(win, "QUALITY", -195f);
        var resolution = DropdownRow(win, "RESOLUTION", -270f);

        var reset = MakeButton("ResetButton", win, "RESET", GameOverIcons + "ui_restart.png", new Vector2(-210f, -350f),
                               new Vector2(340f, 80f), 30f, White, null);
        var back = MakeButton("BackButton", win, "BACK", MenuIcons + "ui_back.png", new Vector2(210f, -350f),
                              new Vector2(340f, 80f), 30f, PrimaryTint, onBack);

        var opt = options.gameObject.AddComponent<OptionsUI>();
        var oso = new SerializedObject(opt);
        oso.FindProperty("masterSlider").objectReferenceValue = master;
        oso.FindProperty("masterValueText").objectReferenceValue = masterVal;
        oso.FindProperty("musicSlider").objectReferenceValue = music;
        oso.FindProperty("musicValueText").objectReferenceValue = musicVal;
        oso.FindProperty("sfxSlider").objectReferenceValue = sfx;
        oso.FindProperty("sfxValueText").objectReferenceValue = sfxVal;
        oso.FindProperty("fullscreenToggle").objectReferenceValue = fullscreen;
        oso.FindProperty("vsyncToggle").objectReferenceValue = vsync;
        oso.FindProperty("qualityDropdown").objectReferenceValue = quality;
        oso.FindProperty("resolutionDropdown").objectReferenceValue = resolution;
        oso.FindProperty("resetButton").objectReferenceValue = reset.GetComponent<Button>();
        oso.ApplyModifiedPropertiesWithoutUndo();

        backButton = back;
        options.gameObject.SetActive(false);
        return options;
    }

    // ================= Options satırları =================

    public static void Section(RectTransform parent, string label, string icon, float y)
    {
        Icon(label + "Icon", parent, icon, new Vector2(-440f, y), 40f);
        var t = Text(label + "Header", parent, label, 30f, Gold, TextAlignmentOptions.Left,
                     new Vector2(-215f, y), new Vector2(400f, 44f));
        t.characterSpacing = 6f;
        var line = Img(label + "Line", parent, null, new Color(Cyan.r, Cyan.g, Cyan.b, 0.25f));
        Place(line.rectTransform, new Vector2(200f, y), new Vector2(460f, 3f));
    }

    public static Slider SliderRow(RectTransform parent, string label, string icon, float y, out TMP_Text valueText)
    {
        Icon(label + "Icon", parent, icon, new Vector2(-400f, y), 40f);
        Text(label + "Label", parent, label, 28f, White, TextAlignmentOptions.Left, new Vector2(-230f, y), new Vector2(260f, 44f));

        var rt = Empty(label + "Slider", parent, new Vector2(110f, y), new Vector2(460f, 30f));
        var bg = Img("Background", rt, LoadSprite(UIDir + "bar_bg.png"), Color.white);
        bg.type = Image.Type.Sliced;
        Stretch(bg.rectTransform);

        var fillArea = Empty("Fill Area", rt, Vector2.zero, Vector2.zero);
        Stretch(fillArea); fillArea.offsetMin = new Vector2(4f, 4f); fillArea.offsetMax = new Vector2(-4f, -4f);
        var fill = Img("Fill", fillArea, LoadSprite(UIDir + "bar_fill.png"), Color.white);
        fill.type = Image.Type.Sliced;
        Stretch(fill.rectTransform);

        var handleArea = Empty("Handle Slide Area", rt, Vector2.zero, Vector2.zero);
        Stretch(handleArea); handleArea.offsetMin = new Vector2(12f, 0f); handleArea.offsetMax = new Vector2(-12f, 0f);
        var handle = Img("Handle", handleArea, LoadSprite(MenuIcons + "ui_knob.png"), Color.white);
        handle.preserveAspect = true;
        handle.raycastTarget = true;
        var hrt = handle.rectTransform;
        hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(0f, 1f);
        hrt.sizeDelta = new Vector2(40f, 26f);

        bg.raycastTarget = true;   // çubuğa tıklayınca değer oraya atlasın
        var slider = rt.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = hrt;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;

        valueText = Text(label + "Value", parent, "100%", 28f, Cyan, TextAlignmentOptions.Right,
                         new Vector2(420f, y), new Vector2(110f, 44f));
        return slider;
    }

    // x: satırın sol kenarı (pencere merkezine göre)
    public static Toggle ToggleRow(RectTransform parent, string label, float y, float x)
    {
        Text(label + "Label", parent, label, 28f, White, TextAlignmentOptions.Left,
             new Vector2(x + 150f, y), new Vector2(300f, 44f));

        var box = Img(label + "Toggle", parent, LoadSprite(UIDir + "frame_slot.png"), Color.white);
        box.type = Image.Type.Sliced;
        box.raycastTarget = true;
        Place(box.rectTransform, new Vector2(x + 340f, y), new Vector2(52f, 52f));

        var check = Img("Checkmark", box.transform, LoadSprite(MenuIcons + "ui_check.png"), Color.white);
        Place(check.rectTransform, Vector2.zero, new Vector2(38f, 38f));

        var toggle = box.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic = check;
        toggle.isOn = true;
        box.gameObject.AddComponent<UIHoverScale>();
        return toggle;
    }

    public static TMP_Dropdown DropdownRow(RectTransform parent, string label, float y)
    {
        Text(label + "Label", parent, label, 28f, White, TextAlignmentOptions.Left, new Vector2(-250f, y), new Vector2(300f, 44f));

        var res = new TMP_DefaultControls.Resources
        {
            standard = LoadSprite(UIDir + "frame_slot.png"),
            background = LoadSprite(UIDir + "frame_panel.png"),
            dropdown = LoadSprite(MenuIcons + "ui_dropdown.png"),
            checkmark = LoadSprite(MenuIcons + "ui_check.png"),
            knob = LoadSprite(UIDir + "bar_fill.png"),
        };
        var go = TMP_DefaultControls.CreateDropdown(res);
        go.name = label + "Dropdown";
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Place(rt, new Vector2(170f, y), new Vector2(500f, 56f));

        var dd = go.GetComponent<TMP_Dropdown>();

        // Yazılar: oyunun fontu ve büyük boyut
        foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
        {
            if (Font != null) t.font = Font;
            t.fontSize = 26f;
            t.color = White;
        }
        dd.captionText.rectTransform.offsetMin = new Vector2(20f, 4f);

        // Ok ikonu
        var arrow = rt.Find("Arrow") as RectTransform;
        if (arrow != null) { arrow.sizeDelta = new Vector2(30f, 30f); arrow.anchoredPosition = new Vector2(-26f, 0f); }

        // Açılan liste: daha yüksek satırlar, pixel çerçeve
        var template = dd.template;
        template.sizeDelta = new Vector2(0f, 260f);
        foreach (var img in template.GetComponentsInChildren<Image>(true))
            if (img.sprite != null && (img.sprite == res.standard || img.sprite == res.background)) img.type = Image.Type.Sliced;
        var item = template.Find("Viewport/Content/Item") as RectTransform;
        if (item != null)
        {
            item.sizeDelta = new Vector2(item.sizeDelta.x, 44f);
            var content = (RectTransform)item.parent;
            content.sizeDelta = new Vector2(content.sizeDelta.x, 48f);
            var itemBg = item.Find("Item Background")?.GetComponent<Image>();
            if (itemBg != null) itemBg.color = new Color(1f, 1f, 1f, 0.08f);
            var toggle = item.GetComponent<Toggle>();
            if (toggle != null)
            {
                var c = toggle.colors;
                c.normalColor = new Color(1f, 1f, 1f, 0f);
                c.highlightedColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f);
                c.selectedColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f);
                toggle.colors = c;
            }
            var itemCheck = item.Find("Item Checkmark") as RectTransform;
            if (itemCheck != null) itemCheck.sizeDelta = new Vector2(28f, 28f);
            var itemLabel = item.Find("Item Label") as RectTransform;
            if (itemLabel != null) itemLabel.offsetMin = new Vector2(44f, itemLabel.offsetMin.y);
        }
        return dd;
    }

    // ================= Yapı taşları =================

    public static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null)   // Multiple modda import edilmişse alt sprite'ı al
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite sp) { s = sp; break; }
        if (s == null) Debug.LogWarning($"[UIBuilderKit] Sprite bulunamadı: {path}");
        return s;
    }

    public static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
    }

    public static void Stretch(RectTransform rt, float overflow = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(-overflow, -overflow);
        rt.offsetMax = new Vector2(overflow, overflow);
    }

    public static RectTransform Empty(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Place(rt, pos, size);
        return rt;
    }

    // Tam ekran, CanvasGroup'lu panel (MainMenuUI geçişte saydamlığını oynatır)
    public static RectTransform Panel(string name, Transform parent)
    {
        var rt = Empty(name, parent, Vector2.zero, Vector2.zero);
        Stretch(rt);
        rt.gameObject.AddComponent<CanvasGroup>();
        return rt;
    }

    public static Image Img(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static RectTransform Frame(string name, Transform parent, string sprite, Vector2 pos, Vector2 size)
    {
        var img = Img(name, parent, LoadSprite(sprite), Color.white);
        img.type = Image.Type.Sliced;
        Place(img.rectTransform, pos, size);
        return img.rectTransform;
    }

    public static Image Icon(string name, Transform parent, string sprite, Vector2 pos, float size)
    {
        var img = Img(name, parent, LoadSprite(sprite), Color.white);
        img.preserveAspect = true;
        Place(img.rectTransform, pos, new Vector2(size, size));
        return img;
    }

    public static TMP_Text Text(string name, Transform parent, string text, float size, Color color,
                         TextAlignmentOptions align, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Place(rt, pos, box);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (Font != null) t.font = Font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        t.richText = true;
        return t;
    }

    // Pixel "gölgeli" başlık: arkada koyu kopya, önde renkli yazı
    public static void ShadowText(string name, RectTransform parent, string text, float size, Color color, Vector2 pos)
    {
        Text(name + "Shadow", parent, text, size, Ink, TextAlignmentOptions.Center, pos + new Vector2(8f, -8f), new Vector2(1000f, size + 20f));
        Text(name, parent, text, size, color, TextAlignmentOptions.Center, pos, new Vector2(1000f, size + 20f));
    }

    public static GameObject MakeButton(string name, RectTransform parent, string label, string icon, Vector2 pos,
                                 Vector2 size, float fontSize, Color tint, UnityAction onClick)
    {
        var rt = Frame(name, parent, UIDir + "frame_slot.png", pos, size);
        var img = rt.GetComponent<Image>();
        img.raycastTarget = true;
        img.color = tint;

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var c = btn.colors;
        c.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        c.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        c.selectedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        btn.colors = c;
        if (onClick != null) UnityEventTools.AddPersistentListener(btn.onClick, onClick);
        rt.gameObject.AddComponent<UIHoverScale>();

        float iconSize = size.y * 0.52f;
        Icon("Icon", rt, icon, new Vector2(-size.x * 0.5f + 28f + iconSize * 0.5f, 0f), iconSize);
        Text("Label", rt, label, fontSize, White, TextAlignmentOptions.Center,
             new Vector2(iconSize * 0.5f, 0f), new Vector2(size.x - iconSize - 40f, size.y));
        return rt.gameObject;
    }
}
