using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static UIBuilderKit;

// Oyun içi pause menüsünü (MainGame sahnesi) ana menüyle AYNI stilde kurar:
//   - Hafif karartma + pixel çerçeveli pencere, "PAUSED" başlığı
//   - Bu oyunun özet istatistikleri (süre, dalga, öldürme, level)
//   - Resume / Options / Restart / Main Menu / Quit butonları, "ESC TO RESUME" ipucu
//   - Ana menüdekiyle aynı Options paneli (Back -> CloseOptions)
// PauseMenuUI yoksa HUD Canvas'ına "PauseMenu" objesiyle eklenir; tüm referanslar bağlanır.
//
// Kullanım: MainGame sahnesi açıkken Menü > TopDownShooter > UI > Pause Menüsünü Kur
// Tekrar çalıştırmak güvenli: eski pause/options panelleri silinip yeniden kurulur.
public static class PauseMenuBuilder
{
    [MenuItem("TopDownShooter/UI/Pause Menüsünü Kur")]
    static void BuildMenu()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Pause Menü", "Önce Play Mode'dan çık.", "Tamam");
            return;
        }
        if (FindHudCanvas() == null)
        {
            EditorUtility.DisplayDialog("Pause Menü",
                "HUD Canvas'ı bulunamadı (GameOverUI paneli veya SkillHUD'un Canvas'ı). Önce MainGame sahnesini aç.", "Tamam");
            return;
        }
        if (!EditorUtility.DisplayDialog("Pause Menü",
                "Pause menüsü (ve Options paneli) kurulacak; varsa eskileri silinecek. Devam edilsin mi?", "Kur", "Vazgeç"))
            return;

        if (Build()) Debug.Log("[PauseMenuBuilder] Pause menüsü kuruldu. Sahneyi kaydetmeyi unutma (Ctrl+S).");
    }

    // Oyunun HUD Canvas'ı: Game Over panelinin (yoksa SkillHUD'un) kök Canvas'ı.
    static Canvas FindHudCanvas()
    {
        var go = Object.FindAnyObjectByType<GameOverUI>(FindObjectsInactive.Include);
        if (go != null)
        {
            var panel = new SerializedObject(go).FindProperty("panel").objectReferenceValue as GameObject;
            var c = panel != null ? panel.GetComponentInParent<Canvas>(true) : null;
            if (c != null) return c.rootCanvas;
        }
        var hud = Object.FindAnyObjectByType<SkillHUD>(FindObjectsInactive.Include);
        return hud != null ? hud.GetComponentInParent<Canvas>(true)?.rootCanvas : null;
    }

    static bool Build()
    {
        var canvas = FindHudCanvas();
        var root = (RectTransform)canvas.transform;

        // PauseMenuUI: varsa onu kullan, yoksa Canvas altında boş bir objeye ekle
        var ui = Object.FindAnyObjectByType<PauseMenuUI>(FindObjectsInactive.Include);
        if (ui == null)
        {
            var host = new GameObject("PauseMenu", typeof(RectTransform));
            host.transform.SetParent(root, false);
            ui = host.AddComponent<PauseMenuUI>();
        }
        var so = new SerializedObject(ui);

        // Eski panelleri sil (tekrar çalıştırılabilsin)
        foreach (var field in new[] { "pausePanel", "optionsPanel" })
            if (so.FindProperty(field).objectReferenceValue is GameObject old)
                Object.DestroyImmediate(old);

        // Panelleri PauseMenuUI objesinin altına koy (Canvas'ın altındaysa tam ekran olur)
        var parent = ui.transform as RectTransform;
        if (parent == null || parent.GetComponentInParent<Canvas>(true) == null) parent = root;
        if (parent != root) Stretch(parent);

        // ================= Pause paneli =================
        var pause = Panel("PausePanel", parent);
        var dim = pause.gameObject.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.03f, 0.07f, 0.72f);   // oyun arkada görünsün ama tıklanmasın

        var win = Frame("Window", pause, UIDir + "frame_panel.png", Vector2.zero, new Vector2(640f, 880f));
        Icon("PauseIcon", win, MenuIcons + "ui_pause.png", new Vector2(-120f, 330f), 60f);
        ShadowText("Title", win, "PAUSED", 72f, Cyan, new Vector2(60f, 330f));
        // ShadowText 1000 genişlik kullanır; başlığı pencereye sığdır
        foreach (var t in new[] { "TitleShadow", "Title" })
            ((RectTransform)win.Find(t)).sizeDelta = new Vector2(340f, 92f);

        // Bu oyunun özeti: 4 küçük hücre
        var divider = Img("Divider", win, null, new Color(Cyan.r, Cyan.g, Cyan.b, 0.3f));
        Place(divider.rectTransform, new Vector2(0f, 270f), new Vector2(560f, 3f));

        string[] icons = { GameOverIcons + "ui_time.png", GameOverIcons + "ui_wave.png",
                           GameOverIcons + "ui_skull.png", GameOverIcons + "ui_level.png" };
        string[] fields = { "timeText", "waveText", "killsText", "levelText" };
        for (int i = 0; i < icons.Length; i++)
        {
            float x = -210f + i * 140f;
            Icon("StatIcon" + i, win, icons[i], new Vector2(x, 222f), 40f);
            var v = Text("StatValue" + i, win, i == 0 ? "00:00" : "0", 28f, White, TextAlignmentOptions.Center,
                         new Vector2(x, 180f), new Vector2(136f, 36f));
            so.FindProperty(fields[i]).objectReferenceValue = v;
        }
        var divider2 = Img("Divider2", win, null, new Color(Cyan.r, Cyan.g, Cyan.b, 0.3f));
        Place(divider2.rectTransform, new Vector2(0f, 142f), new Vector2(560f, 3f));

        // Butonlar
        // SAVE & QUIT: kaydet + oyundan çık. MAIN MENU / QUIT: kaydetmeden (onay penceresiyle).
        var size = new Vector2(440f, 72f);
        var resume = MakeButton("ResumeButton", win, "RESUME", MenuIcons + "ui_play.png", new Vector2(0f, 84f),
                                new Vector2(460f, 84f), 36f, PrimaryTint, ui.Resume);
        var options = MakeButton("OptionsButton", win, "OPTIONS", MenuIcons + "ui_gear.png", new Vector2(0f, -4f),
                                 size, 30f, White, ui.OpenOptions);
        MakeButton("RestartButton", win, "RESTART", GameOverIcons + "ui_restart.png", new Vector2(0f, -86f),
                   size, 30f, White, ui.Restart);
        MakeButton("SaveQuitButton", win, "SAVE & QUIT", MenuIcons + "ui_check.png", new Vector2(0f, -168f),
                   size, 30f, new Color32(0x73, 0xEF, 0xF7, 0xFF), ui.SaveAndQuit);
        MakeButton("MainMenuButton", win, "MAIN MENU", GameOverIcons + "ui_home.png", new Vector2(0f, -250f),
                   size, 30f, White, ui.GoToMainMenu);
        MakeButton("QuitButton", win, "QUIT", GameOverIcons + "ui_quit.png", new Vector2(0f, -332f),
                   size, 30f, White, ui.QuitGame);

        var hint = Text("Hint", win, "ESC TO RESUME", 22f, Muted, TextAlignmentOptions.Center,
                        new Vector2(0f, -400f), new Vector2(500f, 30f));
        hint.characterSpacing = 6f;

        // ================= Options paneli (ana menüyle aynı) =================
        var optionsPanel = BuildOptionsPanel(parent, ui.CloseOptions, out var back);

        // Pause/Options diğer HUD'ların önünde, Game Over panelinin arkasında dursun
        parent.SetAsLastSibling();
        var gameOver = Object.FindAnyObjectByType<GameOverUI>(FindObjectsInactive.Include);
        if (gameOver != null && new SerializedObject(gameOver).FindProperty("panel").objectReferenceValue is GameObject goPanel
            && goPanel.transform.parent == parent.parent)
            goPanel.transform.SetAsLastSibling();

        // ================= Referanslar =================
        so.FindProperty("pausePanel").objectReferenceValue = pause.gameObject;
        so.FindProperty("optionsPanel").objectReferenceValue = optionsPanel.gameObject;
        so.FindProperty("optionsButton").objectReferenceValue = options;
        so.FindProperty("firstSelected").objectReferenceValue = resume;
        so.FindProperty("optionsFirstSelected").objectReferenceValue = back;
        so.FindProperty("mainMenuSceneName").stringValue = "MainMenu";
        var runStats = Object.FindAnyObjectByType<RunStats>(FindObjectsInactive.Include);
        if (runStats != null) so.FindProperty("runStats").objectReferenceValue = runStats;
        so.ApplyModifiedPropertiesWithoutUndo();

        pause.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        return true;
    }
}
