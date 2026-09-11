using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Oyun içi duraklatma menüsü. ESC (ya da gamepad'de Start) ile açılır, oyunu dondurur.
//
// Kurulum:
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
        if (!PausePressed()) return;

        // ESC sırası: Options -> Pause paneli -> oyun
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
        Select(firstSelected);
    }

    public void Resume()
    {
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
        // timeScale'i 1'e çekmeden sahne yüklersek yeni oyun donuk başlar.
        paused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        paused = false;
        Time.timeScale = 1f;

        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError($"[PauseMenuUI] '{mainMenuSceneName}' sahnesi Build Profiles listesinde yok — " +
                            "eklemeden yüklenemez.", this);
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
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

    // ---- Yardımcılar ----

    void Select(GameObject target)
    {
        if (EventSystem.current == null) return;

        // Önce boşalt: aynı obje zaten seçiliyken tekrar set edilirse EventSystem yok sayar.
        EventSystem.current.SetSelectedGameObject(null);
        if (target != null) EventSystem.current.SetSelectedGameObject(target);
    }
}
