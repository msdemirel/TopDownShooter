using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Ana menü: Start / Options / Exit.
//
// Kurulum:
//   1) Canvas altına iki panel koy: Main (butonlar) ve Options (ayarlar).
//   2) Bu scripti Canvas'a (ya da boş bir "MainMenu" objesine) ekle, panelleri ata.
//   3) Butonların OnClick'ine bu scriptin metotlarını bağla:
//        Start   -> StartGame
//        Options -> OpenOptions
//        Exit    -> QuitGame
//        Back    -> ShowMain   (Options panelindeki geri butonu)
//
// Options paneli sahnede açık kalmış olsa da Start()'ta kapatılır — endişelenme.
public class MainMenuUI : MonoBehaviour
{
    [Header("Paneller")]
    [Tooltip("Start / Options / Exit butonlarının olduğu panel.")]
    [SerializeField] GameObject mainPanel;

    [Tooltip("Ayarların olduğu panel (OptionsUI burada). Başlangıçta kapatılır.")]
    [SerializeField] GameObject optionsPanel;

    [Header("Sahne")]
    [Tooltip("Start'a basınca yüklenecek sahne. Build Profiles > Scene List'te EKLİ olmalı.")]
    [SerializeField] string gameSceneName = "MainGame";

    [Header("Klavye / Gamepad (opsiyonel)")]
    [Tooltip("Menü açılınca seçili gelecek buton. Boşsa sadece mouse ile gezilir.")]
    [SerializeField] GameObject firstSelected;

    [Tooltip("Options açılınca seçili gelecek eleman.")]
    [SerializeField] GameObject optionsFirstSelected;

    bool OptionsOpen => optionsPanel != null && optionsPanel.activeSelf;

    void Start()
    {
        // ÖNEMLİ: Game Over ekranı timeScale'i 0'da bırakmış olabilir ve timeScale sahne
        // değişince kendiliğinden sıfırlanmaz. Menüye dönünce 1'e çekmezsek oyun donuk başlar.
        Time.timeScale = 1f;

        ShowMain();
    }

    void Update()
    {
        // ESC ile Options'tan geri dön. (Proje yeni Input System kullanıyor —
        // eski Input.GetKeyDown burada exception atar.)
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && OptionsOpen)
            ShowMain();
    }

    // ---- Buton metotları (Inspector'dan OnClick'e bağlanır) ----

    public void StartGame()
    {
        Time.timeScale = 1f;

        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError($"[MainMenuUI] '{gameSceneName}' sahnesi Build Profiles listesinde yok — " +
                            "eklemeden yüklenemez.", this);
            return;
        }

        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenOptions() => SwitchPanels(showMain: false);

    public void ShowMain() => SwitchPanels(showMain: true);

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

    void SwitchPanels(bool showMain)
    {
        if (mainPanel != null) mainPanel.SetActive(showMain);
        if (optionsPanel != null) optionsPanel.SetActive(!showMain);

        Select(showMain ? firstSelected : optionsFirstSelected);
    }

    void Select(GameObject target)
    {
        if (target == null || EventSystem.current == null) return;

        // Önce boşalt: aynı obje zaten seçiliyken tekrar set edilirse EventSystem yok sayar.
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target);
    }
}
