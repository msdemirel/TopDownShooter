using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Oyun içi duraklatma menüsü. ESC (ya da gamepad'de Start) ile açılır, oyunu dondurur.
//
// Kurulum: MainGame sahnesi açıkken Menü > TopDownShooter > UI > Pause Menüsünü Kur
// (ana menüyle aynı stilde paneli + Options'ı kurar ve her şeyi bağlar). Elle kurulum:
//   1) Canvas altına KAPALI (inactive) bir "PausePanel" objesi koy:
//      Resume / Options / Restart / Main Menu / Quit butonları.
//   2) Options için: MainMenu sahnesindeki "OptionsPanel" objesini kopyalayıp bu sahnenin
//      Canvas'ına yapıştır (üstündeki OptionsUI scripti kendi kendine çalışır, ayar istemez).
//      Kapalı bırak. Options panelini atamazsan Options butonu otomatik gizlenir.
//   3) Bu scripti Canvas'a (ya da boş bir "PauseMenu" objesine) ekle, panelleri ata.
//   4) Butonların OnClick'ine bu scriptin metotlarını bağla:
//        Resume    -> Resume
//        Options   -> OpenOptions
//        Back      -> CloseOptions    (Options panelindeki geri butonu)
//        Restart   -> Restart
//        Main Menu -> GoToMainMenu
//        Quit      -> QuitGame
//   5) Main Menu butonu çalışsın diye "MainMenu" sahnesi Build Profiles > Scene List'te EKLİ olmalı.
//
// ESC davranışı: Options açıksa önce ona geri döner, sonraki ESC oyunu devam ettirir.
//
// ÖNEMLİ: Upgrade paneli ya da Game Over ekranı açıkken oyun ZATEN donuktur; o sırada
// ESC yok sayılır. Yoksa Resume, onların panelini açık bırakıp oyunu çalıştırırdı.
public class PauseMenuUI : MonoBehaviour
{
    [Header("Paneller")]
    [Tooltip("Duraklatma menüsü paneli. Sahnede başta KAPALI olmalı (Start'ta yine de kapatılır).")]
    [SerializeField] GameObject pausePanel;

    [Tooltip("Ayarların olduğu panel (üstünde OptionsUI olan). Opsiyonel — atanmazsa " +
             "aşağıdaki Options butonu gizlenir.")]
    [SerializeField] GameObject optionsPanel;

    [Tooltip("Options paneli atanmamışsa gizlenecek buton. Opsiyonel.")]
    [SerializeField] GameObject optionsButton;

    [Header("Sahne")]
    [Tooltip("Main Menu butonunun yükleyeceği sahne. Build Profiles > Scene List'te EKLİ olmalı.")]
    [SerializeField] string mainMenuSceneName = "MainMenu";

    [Header("Tuş")]
    [Tooltip("Duraklatma tuşu. BOŞSA klavyede ESC ve gamepad'de Start kullanılır — " +
             "çoğu durumda boş bırakabilirsin.")]
    [SerializeField] InputActionReference pauseAction;

    [Header("Klavye / Gamepad (opsiyonel)")]
    [Tooltip("Menü açılınca seçili gelecek buton. Boşsa sadece mouse ile gezilir.")]
    [SerializeField] GameObject firstSelected;

    [Tooltip("Options açılınca seçili gelecek eleman.")]
    [SerializeField] GameObject optionsFirstSelected;

    [Header("Bu oyunun istatistikleri (opsiyonel)")]
    [Tooltip("Menü açılınca RunStats'tan doldurulur. Boşsa sahnede aranır.")]
    [SerializeField] RunStats runStats;
    [SerializeField] TMP_Text timeText;
    [SerializeField] TMP_Text waveText;
    [SerializeField] TMP_Text killsText;
    [SerializeField] TMP_Text levelText;

    bool paused;

    // Başka sistemler (ör. skill girdisi) merak ederse
    public bool IsPaused => paused;

    bool OptionsOpen => optionsPanel != null && optionsPanel.activeSelf;

    // Oyun BAŞKA bir sistem tarafından mı donduruldu (upgrade paneli / game over)?
    // Öyleyse duraklatmaya izin verme — bkz. sınıf başındaki not.
    bool CanPause => Time.timeScale > 0f;

    void Start()
    {
        // Sahnede açık unutulmuş olabilir; temiz başla.
        if (pausePanel != null) pausePanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);

        // Options paneli yoksa butonu gösterme (basınca hiçbir şey olmasın istemiyoruz)
        if (optionsButton != null) optionsButton.SetActive(optionsPanel != null);

        if (pausePanel == null)
            Debug.LogWarning("[PauseMenuUI] Pause Panel atanmamış.", this);

        if (runStats == null) runStats = FindAnyObjectByType<RunStats>();
    }

    void OnEnable()
    {
        if (pauseAction != null) pauseAction.action.Enable();
    }

    void OnDisable()
    {
        if (pauseAction != null) pauseAction.action.Disable();
    }

    void OnDestroy()
    {
        // Emniyet: duraklatılmışken sahne değişir/obje silinirse timeScale 0'da kalmasın.
        // (timeScale global bir değerdir, sahne yüklenince kendiliğinden sıfırlanmaz.)
        if (paused) Time.timeScale = 1f;
    }

    void Update()
    {
        // Gamepad B: açık bir şey varsa ESC gibi geri gider (oyun akarken bir şey yapmaz)
        bool back = InputMode.BackPressed && (paused || OptionsOpen || ConfirmOpen);
        if (!PausePressed() && !back) return;

        // ESC sırası: Onay penceresi -> Options -> Pause paneli -> oyun
        if (ConfirmOpen) { CloseConfirm(); return; }
        if (OptionsOpen) { CloseOptions(); return; }
        if (paused) { Resume(); return; }
        if (CanPause) Pause();
    }

    // Tuş atanmışsa onu, atanmamışsa ESC / gamepad Start'ı dinler.
    bool PausePressed()
    {
        if (pauseAction != null && pauseAction.action != null)
            return pauseAction.action.WasPressedThisFrame();

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;

        var gp = Gamepad.current;
        return gp != null && gp.startButton.wasPressedThisFrame;
    }

    // ---- Buton metotları (Inspector'dan OnClick'e bağlanır) ----

    public void Pause()
    {
        if (paused) return;

        paused = true;
        Time.timeScale = 0f;   // UI zamandan etkilenmez, butonlar çalışmaya devam eder

        if (pausePanel != null) pausePanel.SetActive(true);
        var hint = pausePanel != null ? pausePanel.transform.Find("Window/Hint")?.GetComponent<TMP_Text>() : null;
        if (hint != null) hint.text = Loc.T(InputMode.Key("ESC TO RESUME", "B TO RESUME"));   // cihaza göre
        RefreshStats();
        Select(firstSelected);
    }

    // Oyun donuk olduğu için değerler menü açıkken değişmez; açılışta bir kez yazmak yeter.
    void RefreshStats()
    {
        if (runStats == null) return;

        if (timeText != null)
        {
            int s = Mathf.FloorToInt(runStats.TimeSurvived);
            timeText.text = $"{s / 60:00}:{s % 60:00}";
        }
        if (waveText != null) waveText.text = runStats.WaveReached.ToString();
        if (killsText != null) killsText.text = runStats.Kills.ToString();
        if (levelText != null) levelText.text = runStats.Level.ToString();
    }

    public void Resume()
    {
        if (PlayerSkills.Current != null) PlayerSkills.Current.IgnoreInputThisFrame();   // A ile devam: skill tetiklenmesin
        if (!paused) return;

        paused = false;

        // Options açık kaldıysa o da kapansın (OptionsUI kapanırken ayarları diske yazar)
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        Time.timeScale = 1f;
        Select(null);   // seçim menüde asılı kalmasın
    }

    public void OpenOptions()
    {
        if (optionsPanel == null) return;

        if (pausePanel != null) pausePanel.SetActive(false);
        optionsPanel.SetActive(true);
        Select(optionsFirstSelected);
    }

    public void CloseOptions()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);
        Select(firstSelected);
    }

    public void Restart()
    {
        // Yarım oyun kaybolmasın: bitmiş sayılır (Core + rekorlar), sonra yeni oyun
        if (RunSave.SaveCurrent()) RunSave.AbandonIfExists();

        // timeScale'i 1'e çekmeden sahne yüklersek yeni oyun donuk başlar.
        paused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // SAVE & QUIT: oyunu kaydedip oyundan çıkar. Ana menüde "Continue" ile devam edilir.
    public void SaveAndQuit()
    {
        RunSave.SaveCurrent();
        ExitApplication();
    }

    // MAIN MENU: KAYDETMEDEN ana menüye döner (önce onay). Oyun bitmiş sayılır: Core + rekorlar verilir.
    public void GoToMainMenu()
    {
        ShowConfirm("RETURN TO MENU?", "MAIN MENU", () =>
        {
            RunSave.EndCurrentWithoutSave();
            paused = false;
            Time.timeScale = 1f;

            if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                Debug.LogError($"[PauseMenuUI] '{mainMenuSceneName}' sahnesi Build Profiles listesinde yok — " +
                                "eklemeden yüklenemez.", this);
                return;
            }
            SceneManager.LoadScene(mainMenuSceneName);
        });
    }

    // QUIT: KAYDETMEDEN oyundan çıkar (önce onay). Oyun bitmiş sayılır: Core + rekorlar verilir.
    public void QuitGame()
    {
        ShowConfirm("QUIT WITHOUT SAVING?", "QUIT", () =>
        {
            RunSave.EndCurrentWithoutSave();
            ExitApplication();
        });
    }

    void ExitApplication()
    {
        GameSettings.Save();   // ayarlar diske yazılmadan kapanmasın

#if UNITY_EDITOR
        // Application.Quit() Editor'da HİÇBİR ŞEY yapmaz; Play modunu burada durduruyoruz.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---- Onay penceresi (kaydetmeden çıkış) ----
    // Pause penceresinin kendi butonları kopyalanarak kodla kurulur: stil aynı, sahneye eklemek gerekmez.
    GameObject confirmRoot;
    TMP_Text confirmTitle;
    TMP_Text confirmOkLabel;
    Button confirmOk;
    GameObject confirmCancel;
    System.Action confirmAction;
    bool ConfirmOpen => confirmRoot != null && confirmRoot.activeSelf;

    void ShowConfirm(string title, string okLabel, System.Action onConfirm)
    {
        if (confirmRoot == null) BuildConfirm();
        if (confirmRoot == null) { onConfirm(); return; }   // pencere kurulamadı: eski davranış

        confirmAction = onConfirm;
        confirmTitle.text = Loc.T(title);
        if (confirmOkLabel != null) confirmOkLabel.text = Loc.T(okLabel);
        confirmRoot.SetActive(true);
        confirmRoot.transform.SetAsLastSibling();
        Select(confirmCancel);   // güvenli seçenek seçili gelsin: ENTER yanlışlıkla çıkmasın
    }

    void CloseConfirm()
    {
        if (confirmRoot != null) confirmRoot.SetActive(false);
        confirmAction = null;
        Select(firstSelected);
    }

    void BuildConfirm()
    {
        var win = pausePanel != null ? pausePanel.transform.Find("Window") as RectTransform : null;
        var template = win != null ? win.Find("QuitButton") : null;
        if (win == null || template == null) return;

        confirmRoot = new GameObject("ConfirmDialog", typeof(RectTransform), typeof(Image));
        var root = (RectTransform)confirmRoot.transform;
        root.SetParent(pausePanel.transform, false);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        confirmRoot.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.07f, 0.75f);   // arkası tıklanmasın

        var box = new GameObject("Window", typeof(RectTransform), typeof(Image));
        var brt = (RectTransform)box.transform;
        brt.SetParent(root, false);
        brt.sizeDelta = new Vector2(820f, 420f);
        var bimg = box.GetComponent<Image>();
        if (win.TryGetComponent<Image>(out var wimg)) { bimg.sprite = wimg.sprite; bimg.type = wimg.type; }

        var font = template.GetComponentInChildren<TMP_Text>()?.font;
        // Başlık tek satır (sığmazsa küçülür), açıklama iki satır: birbirine binmesinler
        confirmTitle = ConfirmText(brt, "Title", 44f, new Color32(0xE5, 0x53, 0x3C, 0xFF), font, new Vector2(0f, 140f));
        confirmTitle.rectTransform.sizeDelta = new Vector2(740f, 60f);
        confirmTitle.textWrappingMode = TextWrappingModes.NoWrap;
        confirmTitle.enableAutoSizing = true;
        confirmTitle.fontSizeMax = 44f;
        confirmTitle.fontSizeMin = 26f;
        var info = ConfirmText(brt, "Info", 22f, new Color32(0x94, 0xB0, 0xC2, 0xFF), font, new Vector2(0f, 45f));
        Loc.Bind(info, "This run will NOT be saved and can't be continued.\n<color=#FFCD75>Earned Core is still awarded.</color>");
        info.rectTransform.sizeDelta = new Vector2(740f, 90f);
        info.enableAutoSizing = true;
        info.fontSizeMax = 22f;
        info.fontSizeMin = 16f;

        confirmOk = ConfirmButton(template.gameObject, brt, new Vector2(-180f, -120f), () => confirmAction?.Invoke()).GetComponent<Button>();
        confirmOk.name = "ConfirmButton";
        confirmOkLabel = confirmOk.GetComponentInChildren<TMP_Text>();
        confirmCancel = ConfirmButton(template.gameObject, brt, new Vector2(180f, -120f), CloseConfirm);
        confirmCancel.name = "CancelButton";   // ses sistemi "Cancel" adında geri sesi çalar
        var cl = confirmCancel.GetComponentInChildren<TMP_Text>();
        if (cl != null) Loc.Bind(cl, "CANCEL");
        var icon = confirmCancel.transform.Find("Icon");   // çıkış ikonu İptal'de yanıltıcı
        if (icon != null) icon.gameObject.SetActive(false);
        if (confirmCancel.transform.Find("Label") is RectTransform cancelLabel)   // ikon yok: yazı ortada
        {
            cancelLabel.anchoredPosition = Vector2.zero;
            cancelLabel.sizeDelta = new Vector2(((RectTransform)confirmCancel.transform).sizeDelta.x - 30f, cancelLabel.sizeDelta.y);
        }
        confirmRoot.SetActive(false);
    }

    static TMP_Text ConfirmText(RectTransform parent, string name, float size, Color color, TMP_FontAsset font, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(700f, size + 16f);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size; t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    // Pause penceresindeki bir butonun kopyası: aynı çerçeve/ikon/hover; tıklama değişir
    static GameObject ConfirmButton(GameObject template, RectTransform parent, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = Instantiate(template, parent);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        var size = new Vector2(320f, 76f);
        rt.sizeDelta = size;

        // İkon ve yazı orijinal (geniş) butona göre konumluydu: yeni genişliğe göre yeniden yerleştir
        float iconW = 0f;
        if (go.transform.Find("Icon") is RectTransform icon)
        {
            iconW = icon.sizeDelta.x;
            icon.anchoredPosition = new Vector2(-size.x * 0.5f + 24f + iconW * 0.5f, 0f);
        }
        if (go.transform.Find("Label") is RectTransform label)
        {
            label.anchoredPosition = new Vector2(iconW * 0.5f, 0f);
            label.sizeDelta = new Vector2(size.x - iconW - 40f, size.y);
        }
        var btn = go.GetComponent<Button>();
        btn.onClick = new Button.ButtonClickedEvent();   // kopyalanan kalıcı (Quit) bağlantısını sil
        btn.onClick.AddListener(onClick);
        return go;
    }

    // ---- Yardımcılar ----

    void Select(GameObject target)
    {
        if (EventSystem.current == null) return;

        // Önce boşalt: aynı obje zaten seçiliyken tekrar set edilirse EventSystem yok sayar.
        EventSystem.current.SetSelectedGameObject(null);
        if (target != null) EventSystem.current.SetSelectedGameObject(target);
    }
}
