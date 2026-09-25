using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static UIBuilderKit;

// Ana menüyü (MainMenu sahnesi) baştan kurar:
//   - Hareketli arka plan: kayan zemin, ışık parçacıkları, kenar karartması (MenuBackground)
//   - Başlık + parıltı, Play / Upgrades / Options / Quit butonları, "BEST RECORDS" kartı
//   - Kalıcı upgrade mağazası (MetaShopBuilder)
//   - Options paneli: Master/Music/SFX slider'ları, Fullscreen/VSync, Quality/Resolution,
//     Reset ve Back butonları (OptionsUI bu panele taşınır)
//   - Açılış/geçiş için siyah perde, sürüm yazısı, "PRESS ENTER TO PLAY"
// MainMenuUI ve OptionsUI'daki tüm referanslar ve buton OnClick'leri bağlanır.
//
// Kullanım: MainMenu sahnesi açıkken Menü > TopDownShooter > UI > Ana Menüyü Kur
// Canvas'ın İÇİ silinip yeniden kurulur. Sonra her şeyi Inspector'dan düzenleyebilirsin;
// menüyü tekrar çalıştırırsan elle yaptığın değişiklikler kaybolur.
public static class MainMenuBuilder
{
    const string Menu = MenuIcons;
    const string GameOver = GameOverIcons;
    const string FloorTile = "Assets/Art/Generated/Tiles/tile_floor_a.png";
    const string Glow = "Assets/Art/Generated/VFX/vfx_glow_soft.png";

    [MenuItem("TopDownShooter/UI/Ana Menüyü Kur")]
    static void BuildMenu()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Ana Menü", "Önce Play Mode'dan çık.", "Tamam");
            return;
        }
        if (Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include) == null)
        {
            EditorUtility.DisplayDialog("Ana Menü", "Açık sahnede MainMenuUI yok. Önce MainMenu sahnesini aç.", "Tamam");
            return;
        }
        if (!EditorUtility.DisplayDialog("Ana Menü",
                "Canvas'ın içi silinip ana menü yeniden kurulacak. Devam edilsin mi?", "Kur", "Vazgeç"))
            return;

        if (Build()) Debug.Log("[MainMenuBuilder] Ana menü kuruldu. Sahneyi kaydetmeyi unutma (Ctrl+S).");
    }

    static bool Build()
    {
        var menu = Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
        var canvas = menu.GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogError("[MainMenuBuilder] MainMenuUI bir Canvas altında değil."); return false; }
        var root = (RectTransform)canvas.transform;

        // Canvas ayarları: oyun sahnesiyle aynı referans çözünürlüğü
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Eski içerik
        for (int i = root.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.GetChild(i).gameObject);
        // OptionsUI Canvas'tan panele taşınacak (OnEnable/OnDisable panelle çalışsın)
        foreach (var old in canvas.GetComponents<OptionsUI>()) Object.DestroyImmediate(old);

        // ================= Arka plan =================
        var bgImg = Img("Background", root, null, Navy); Stretch(bgImg.rectTransform);
        var bgFx = bgImg.gameObject.AddComponent<MenuBackground>();

        var floor = Img("Floor", bgImg.transform, LoadSprite(FloorTile), new Color(0.5f, 0.56f, 0.72f, 0.55f));
        floor.type = Image.Type.Tiled;
        Stretch(floor.rectTransform, 220f);   // ekrandan taşsın: kayarken kenar görünmesin

        var particles = new GameObject("Particles", typeof(RectTransform));
        particles.transform.SetParent(bgImg.transform, false);
        Stretch((RectTransform)particles.transform);

        var vignette = Img("Vignette", bgImg.transform, LoadSprite(UIDir + "menu_vignette.png"), Color.white);
        Stretch(vignette.rectTransform);

        // ================= Ana panel =================
        var main = Panel("MainPanel", root);

        // Başlık
        var titleRt = Empty("Title", main, new Vector2(-430f, 250f), new Vector2(900f, 300f));
        var glow = Img("Glow", titleRt, LoadSprite(Glow), new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f));
        Place(glow.rectTransform, new Vector2(0f, 10f), new Vector2(1150f, 560f));
        string[] words = PlayerSettings.productName.ToUpperInvariant().Split(' ');
        string line1 = words.Length > 2 ? string.Join(" ", words, 0, words.Length - 1) : words[0];
        string line2 = words.Length > 1 ? words[words.Length - 1] : "";
        ShadowText("Line1", titleRt, line1, 118f, Cyan, new Vector2(0f, 62f));
        ShadowText("Line2", titleRt, line2, 150f, Gold, new Vector2(0f, -62f));
        var tagline = Text("Tagline", main, "SURVIVE THE ENDLESS WAVES", 28f, new Color(1f, 1f, 1f, 0.75f),
                           TextAlignmentOptions.Center, new Vector2(-430f, 60f), new Vector2(800f, 40f));
        tagline.characterSpacing = 8f;

        // Butonlar
        var play = MakeButton("PlayButton", main, "PLAY", Menu + "ui_play.png", new Vector2(-430f, -50f),
                              new Vector2(460f, 96f), 40f, PrimaryTint, menu.StartGame);
        MakeButton("OptionsButton", main, "OPTIONS", Menu + "ui_gear.png", new Vector2(-430f, -165f),
                   new Vector2(420f, 84f), 32f, White, menu.OpenOptions);
        MakeButton("QuitButton", main, "QUIT", GameOver + "ui_quit.png", new Vector2(-430f, -270f),
                   new Vector2(420f, 84f), 32f, White, menu.QuitGame);

        // Rekor kartı
        var card = Frame("RecordsCard", main, UIDir + "frame_panel.png", new Vector2(470f, -30f), new Vector2(560f, 580f));
        Icon("Trophy", card, GameOver + "ui_trophy.png", new Vector2(-150f, 232f), 56f);
        Text("Header", card, "BEST RECORDS", 40f, Gold, TextAlignmentOptions.Left,
             new Vector2(50f, 232f), new Vector2(330f, 56f));
        var divider = Img("Divider", card, null, new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f));
        Place(divider.rectTransform, new Vector2(0f, 186f), new Vector2(480f, 4f));

        var content = Empty("Content", card, Vector2.zero, new Vector2(560f, 580f));
        string[] labels = { "BEST WAVE", "BEST TIME", "MOST KILLS", "HIGHEST LEVEL", "RUNS PLAYED" };
        string[] icons = { GameOver + "ui_wave.png", GameOver + "ui_time.png", GameOver + "ui_skull.png",
                           GameOver + "ui_level.png", GameOver + "ui_restart.png" };
        string[] fields = { "bestWaveText", "bestTimeText", "bestKillsText", "bestLevelText", "totalRunsText" };
        var mso = new SerializedObject(menu);
        for (int i = 0; i < labels.Length; i++)
        {
            float y = 120f - i * 80f;
            Icon("Icon" + i, content, icons[i], new Vector2(-222f, y), 48f);
            Text("Label" + i, content, labels[i], 24f, Muted, TextAlignmentOptions.Left,
                 new Vector2(-40f, y), new Vector2(300f, 40f));
            var value = Text("Value" + i, content, "0", 36f, White, TextAlignmentOptions.Right,
                             new Vector2(160f, y), new Vector2(180f, 50f));
            mso.FindProperty(fields[i]).objectReferenceValue = value;
        }
        var noRecords = Text("NoRecords", card, "NO RUNS YET\n<size=60%><color=#94B0C2>PLAY TO SET YOUR FIRST RECORD</color></size>",
                             36f, White, TextAlignmentOptions.Center, new Vector2(0f, -20f), new Vector2(480f, 160f));

        var pressStart = Text("PressStart", main, "PRESS ENTER TO PLAY", 28f, White, TextAlignmentOptions.Center,
                              Vector2.zero, new Vector2(700f, 40f));
        Anchor(pressStart.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 70f));
        pressStart.characterSpacing = 6f;

        // ================= Options paneli =================
        var options = BuildOptionsPanel(root, menu.ShowMain, out var back);

        // ================= Alt bilgi + perde =================
        var version = Text("Version", root, "v1.0", 22f, Muted, TextAlignmentOptions.Left, Vector2.zero, new Vector2(300f, 30f));
        Anchor(version.rectTransform, new Vector2(0f, 0f), new Vector2(170f, 30f));

        var fader = Img("ScreenFader", root, null, Color.black);
        Stretch(fader.rectTransform);
        var faderGroup = fader.gameObject.AddComponent<CanvasGroup>();
        faderGroup.alpha = 0f;              // Editor'da menü görünsün; oyunda Start siyahtan açar
        faderGroup.blocksRaycasts = false;

        // ================= Referanslar =================
        var bso = new SerializedObject(bgFx);
        bso.FindProperty("scrollingFloor").objectReferenceValue = floor.rectTransform;
        bso.FindProperty("particleArea").objectReferenceValue = particles.transform;
        bso.FindProperty("particleSprite").objectReferenceValue = LoadSprite(Glow);
        bso.FindProperty("pulseGlow").objectReferenceValue = glow;
        bso.ApplyModifiedPropertiesWithoutUndo();

        mso.FindProperty("mainPanel").objectReferenceValue = main.gameObject;
        mso.FindProperty("optionsPanel").objectReferenceValue = options.gameObject;
        mso.FindProperty("firstSelected").objectReferenceValue = play;
        mso.FindProperty("optionsFirstSelected").objectReferenceValue = back;
        mso.FindProperty("noRecordsObject").objectReferenceValue = noRecords.gameObject;
        mso.FindProperty("recordsContent").objectReferenceValue = content.gameObject;
        mso.FindProperty("screenFader").objectReferenceValue = faderGroup;
        mso.FindProperty("title").objectReferenceValue = titleRt;
        mso.FindProperty("pressStartText").objectReferenceValue = pressStart;
        mso.FindProperty("versionText").objectReferenceValue = version;

        // Mağaza: UPGRADES butonu + panel (Options/Quit'i bir adım aşağı kaydırır)
        MetaShopBuilder.Build(menu, root, main, mso);
        DifficultyPanelBuilder.Build(menu, root, main, mso);   // PLAY -> zorluk seçimi
        CharacterPanelBuilder.Build(menu, root, main, mso);    // PLAY -> karakter -> zorluk
        fader.transform.SetAsLastSibling();   // perde mağazanın da önünde kalsın
        mso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        return true;
    }
}
