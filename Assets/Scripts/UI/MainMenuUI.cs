using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Ana menü: Play / Upgrades (mağaza) / Options / Quit + en iyi rekorlar kartı + Core bakiyesi.
//
// Kurulum: MainMenu sahnesi açıkken Menü > TopDownShooter > UI > Ana Menüyü Kur
// (tüm paneli kurar, referansları ve butonları bağlar). Elle kurmak istersen:
//   Butonların OnClick'i:  Play -> StartGame,  Options -> OpenOptions,
//                          Quit -> QuitGame,   Back (Options) -> ShowMain
//                          Upgrades -> OpenShop, Back (mağaza) -> ShowMain
//   Zorluk seçimi kuruluysa Play -> Play (önce zorluk ekranı açılır; kart seçimi oyunu başlatır)
// Mağaza paneli: Menü > TopDownShooter > UI > Mağazayı Kur
//
// Klavye: ENTER = oyna (ana ekrandayken), ESC = Options'tan / mağazadan geri dön.
// Options paneli sahnede açık kalmış olsa da Start()'ta kapatılır — endişelenme.
public class MainMenuUI : MonoBehaviour
{
    [Header("Paneller")]
    [Tooltip("Başlık, butonlar ve rekorların olduğu panel.")]
    [SerializeField] GameObject mainPanel;

    [Tooltip("Ayarların olduğu panel (OptionsUI burada). Başlangıçta kapatılır.")]
    [SerializeField] GameObject optionsPanel;

    [Tooltip("Kalıcı upgrade mağazası (MetaShopUI). Opsiyonel. Başlangıçta kapatılır.")]
    [SerializeField] GameObject shopPanel;
    [Tooltip("PLAY'e basınca açılan zorluk seçimi (DifficultySelectUI). Boşsa PLAY oyunu direkt başlatır.")]
    [SerializeField] GameObject difficultyPanel;
    [Tooltip("PLAY'e basınca İLK açılan karakter seçimi (CharacterSelectUI). Seçimden sonra zorluk ekranı gelir.")]
    [SerializeField] GameObject characterPanel;
    [Tooltip("Ana ekrandaki Core bakiyesi yazısı. Opsiyonel.")]
    [SerializeField] TMP_Text coreBalanceText;

    [Header("Sahne")]
    [Tooltip("Play'e basınca yüklenecek sahne. Build Profiles > Scene List'te EKLİ olmalı.")]
    [SerializeField] string gameSceneName = "MainGame";

    [Header("Klavye / Gamepad (opsiyonel)")]
    [Tooltip("Menü açılınca seçili gelecek buton. Boşsa sadece mouse ile gezilir.")]
    [SerializeField] GameObject firstSelected;

    [Tooltip("Options açılınca seçili gelecek eleman.")]
    [SerializeField] GameObject optionsFirstSelected;

    [Tooltip("Mağaza açılınca seçili gelecek eleman.")]
    [SerializeField] GameObject shopFirstSelected;

    [Header("Rekorlar (opsiyonel)")]
    [SerializeField] TMP_Text bestWaveText;
    [SerializeField] TMP_Text bestTimeText;
    [SerializeField] TMP_Text bestKillsText;
    [SerializeField] TMP_Text bestLevelText;
    [SerializeField] TMP_Text totalRunsText;
    [Tooltip("Hiç oynanmadıysa rekor satırları yerine gösterilecek obje (\"NO RUNS YET\").")]
    [SerializeField] GameObject noRecordsObject;
    [Tooltip("Rekor satırlarının kökü (hiç oynanmadıysa gizlenir).")]
    [SerializeField] GameObject recordsContent;

    [Header("Görünüm (opsiyonel)")]
    [Tooltip("Tam ekran siyah perde: açılışta açılır, Play'e basınca kararır.")]
    [SerializeField] CanvasGroup screenFader;
    [SerializeField] float fadeDuration = 0.45f;
    [Tooltip("Hafifçe aşağı yukarı süzülen başlık.")]
    [SerializeField] RectTransform title;
    [SerializeField] float titleBobAmount = 8f;
    [SerializeField] float titleBobSpeed = 1.6f;
    [Tooltip("Yanıp sönen \"PRESS ENTER TO PLAY\" yazısı.")]
    [SerializeField] TMP_Text pressStartText;
    [Tooltip("Sürüm yazısı (\"v1.0\").")]
    [SerializeField] TMP_Text versionText;
    [Tooltip("Paneller değişirken yumuşak geçiş süresi.")]
    [SerializeField] float panelFadeDuration = 0.18f;

    bool starting;                 // Play'e basıldı, sahne yükleniyor (çift tıklama korunması)
    Vector2 titleBasePos;
    Coroutine panelFade;

    bool OptionsOpen => optionsPanel != null && optionsPanel.activeSelf;
    bool ShopOpen => shopPanel != null && shopPanel.activeSelf;
    bool DifficultyOpen => difficultyPanel != null && difficultyPanel.activeSelf;
    bool CharacterOpen => characterPanel != null && characterPanel.activeSelf;
    bool SubPanelOpen => OptionsOpen || ShopOpen || DifficultyOpen || CharacterOpen;

    void Start()
    {
        // ÖNEMLİ: Game Over ekranı timeScale'i 0'da bırakmış olabilir ve timeScale sahne
        // değişince kendiliğinden sıfırlanmaz. Menüye dönünce 1'e çekmezsek oyun donuk başlar.
        Time.timeScale = 1f;

        if (title != null) titleBasePos = title.anchoredPosition;
        if (versionText != null) versionText.text = "v" + Application.version;

        RefreshRecords();
        SwitchPanels(showMain: true, instant: true);

        // Açılış: siyahtan aç
        if (screenFader != null) StartCoroutine(Fade(screenFader, 1f, 0f, fadeDuration));
    }

    void Update()
    {
        // (Proje yeni Input System kullanıyor — eski Input.GetKeyDown burada exception atar.)
        var kb = Keyboard.current;
        if (kb != null && !starting)
        {
            // Zorluk ekranından ESC karakter seçimine döner (ikisi bir akışın adımları)
            if (kb.escapeKey.wasPressedThisFrame && DifficultyOpen && characterPanel != null) SwitchTo(characterPanel, null);
            else if (kb.escapeKey.wasPressedThisFrame && SubPanelOpen) ShowMain();
            else if (!SubPanelOpen && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                Play();
        }

        float t = Time.unscaledTime;
        if (title != null)
            title.anchoredPosition = titleBasePos + Vector2.up * Mathf.Sin(t * titleBobSpeed) * titleBobAmount;

        if (pressStartText != null)
        {
            Color c = pressStartText.color;
            c.a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * 3f));
            pressStartText.color = c;
        }
    }

    // ---- Buton metotları (Inspector'dan OnClick'e bağlanır) ----

    public void StartGame()
    {
        if (starting) return;

        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError($"[MainMenuUI] '{gameSceneName}' sahnesi Build Profiles listesinde yok — " +
                            "eklemeden yüklenemez.", this);
            return;
        }

        starting = true;
        Time.timeScale = 1f;
        StartCoroutine(StartGameRoutine());
    }

    IEnumerator StartGameRoutine()
    {
        if (screenFader != null) yield return Fade(screenFader, screenFader.alpha, 1f, fadeDuration);
        SceneManager.LoadScene(gameSceneName);
    }

    // PLAY butonu: karakter seçimi -> zorluk seçimi -> oyun. Kurulu olmayan adım atlanır.
    public void Play()
    {
        if (characterPanel != null) SwitchTo(characterPanel, null);
        else OpenDifficulty();
    }

    // Karakter seçilince (CharacterSelectUI) çağrılır.
    public void OpenDifficulty()
    {
        if (difficultyPanel != null) SwitchTo(difficultyPanel, null);
        else StartGame();
    }

    public void OpenOptions() => SwitchTo(optionsPanel, optionsFirstSelected);

    public void OpenShop() => SwitchTo(shopPanel, shopFirstSelected);

    public void ShowMain()
    {
        RefreshRecords();   // mağazadan dönünce Core bakiyesi güncel olsun
        SwitchTo(mainPanel, firstSelected);
    }

    public void QuitGame()
    {
        GameSettings.Save();   // ayarlar diske yazılmadan kapanmasın

#if UNITY_EDITOR
        // Application.Quit() Editor'da HİÇBİR ŞEY yapmaz; Play modunu burada durduruyoruz.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---- Rekorlar ----

    void RefreshRecords()
    {
        RunResult best = BestRecords.Load();
        bool any = BestRecords.TotalRuns > 0;

        if (recordsContent != null) recordsContent.SetActive(any);
        if (noRecordsObject != null) noRecordsObject.SetActive(!any);

        SetText(bestWaveText, best.wave.ToString());
        SetText(bestTimeText, FormatTime(best.timeSurvived));
        SetText(bestKillsText, best.kills.ToString());
        SetText(bestLevelText, best.level.ToString());
        SetText(totalRunsText, BestRecords.TotalRuns.ToString());
        SetText(coreBalanceText, MetaProgress.Core.ToString("N0"));
    }

    static void SetText(TMP_Text t, string s) { if (t != null) t.text = s; }

    static string FormatTime(float seconds)
    {
        int s = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60:00}:{s % 60:00}";
    }

    // ---- Yardımcılar ----

    void SwitchPanels(bool showMain, bool instant = false)
        => SwitchTo(showMain ? mainPanel : optionsPanel, showMain ? firstSelected : optionsFirstSelected, instant);

    // Verilen paneli açar, diğerlerini kapatır.
    void SwitchTo(GameObject show, GameObject select, bool instant = false)
    {
        foreach (var p in new[] { mainPanel, optionsPanel, shopPanel, difficultyPanel, characterPanel })
            if (p != null && p != show) p.SetActive(false);

        if (show != null)
        {
            show.SetActive(true);

            // Açılan panel hafifçe belirsin (CanvasGroup varsa)
            var group = show.GetComponent<CanvasGroup>();
            if (group != null)
            {
                if (panelFade != null) StopCoroutine(panelFade);
                if (instant) group.alpha = 1f;
                else panelFade = StartCoroutine(Fade(group, 0f, 1f, panelFadeDuration));
            }
        }

        Select(select);
    }

    static IEnumerator Fade(CanvasGroup g, float from, float to, float duration)
    {
        // Perde açıkken arkadaki butonlara tıklanmasın
        g.blocksRaycasts = to > 0.5f || from > 0.5f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            g.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        g.alpha = to;
        g.blocksRaycasts = to > 0.5f;
    }

    void Select(GameObject target)
    {
        if (target == null || EventSystem.current == null) return;

        // Önce boşalt: aynı obje zaten seçiliyken tekrar set edilirse EventSystem yok sayar.
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target);
    }
}
