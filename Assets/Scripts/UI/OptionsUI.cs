using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Options paneli: temel ses ve görüntü ayarları. Değerleri GameSettings'e yazar,
// GameSettings de PlayerPrefs'e kaydedip anında uygular.
//
// Kurulum: Bu scripti Options paneline ekle ve aşağıdaki alanlardan İSTEDİKLERİNİ ata —
// atanmayanlar sessizce atlanır, hepsi opsiyonel.
//
// NOT: Listener'lar koddan bağlanır. Inspector'dan ayrıca OnValueChanged bağlarsan
// ayar iki kez işlenir; bağlama.
//
// NOT: Fullscreen ve çözünürlük Editor'da bilerek uygulanmaz (Game view'ı bozar) —
// build'de çalışır. Ayar yine de kaydedilir.
public class OptionsUI : MonoBehaviour
{
    [Header("Ses (0-1 arası slider'lar)")]
    [SerializeField] Slider masterSlider;
    [Tooltip("Slider'ın yanındaki yüzde yazısı (opsiyonel).")]
    [SerializeField] TMP_Text masterValueText;

    [SerializeField] Slider musicSlider;
    [SerializeField] TMP_Text musicValueText;

    [SerializeField] Slider sfxSlider;
    [SerializeField] TMP_Text sfxValueText;

    [Header("Görüntü")]
    [SerializeField] Toggle fullscreenToggle;
    [SerializeField] Toggle vsyncToggle;

    [Tooltip("Kalite seçenekleri Project Settings > Quality'den otomatik doldurulur.")]
    [SerializeField] TMP_Dropdown qualityDropdown;

    [Tooltip("Ekranın desteklediği çözünürlükler otomatik doldurulur.")]
    [SerializeField] TMP_Dropdown resolutionDropdown;

    [Header("Biçim")]
    [Tooltip("Slider yazısı. {0} = 0-100 arası değer.")]
    [SerializeField] string volumeFormat = "{0}%";

    // Dropdown sırası ile gerçek çözünürlükler burada eşleşir.
    readonly List<Vector2Int> resolutions = new List<Vector2Int>();

    void Awake()
    {
        BuildQualityOptions();
        BuildResolutionOptions();

        if (masterSlider       != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (musicSlider        != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (sfxSlider          != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        if (fullscreenToggle   != null) fullscreenToggle.onValueChanged.AddListener(GameSettings.SetFullscreen);
        if (vsyncToggle        != null) vsyncToggle.onValueChanged.AddListener(GameSettings.SetVSync);
        if (qualityDropdown    != null) qualityDropdown.onValueChanged.AddListener(GameSettings.SetQualityLevel);
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    // Panel her açıldığında kayıtlı değerleri UI'a bas.
    void OnEnable() => Refresh();

    // Panel kapanınca ayarları diske yaz.
    void OnDisable() => GameSettings.Save();

    // ---- Değişiklik dinleyicileri ----

    void OnMasterChanged(float v)
    {
        GameSettings.SetMasterVolume(v);
        SetVolumeLabel(masterValueText, v);
    }

    void OnMusicChanged(float v)
    {
        GameSettings.SetMusicVolume(v);
        SetVolumeLabel(musicValueText, v);
    }

    void OnSfxChanged(float v)
    {
        GameSettings.SetSfxVolume(v);
        SetVolumeLabel(sfxValueText, v);
    }

    void OnResolutionChanged(int index)
    {
        if (index < 0 || index >= resolutions.Count) return;
        GameSettings.SetResolution(resolutions[index].x, resolutions[index].y);
    }

    // "Varsayılana Dön" butonuna bağlanabilir (opsiyonel).
    public void ResetToDefaults()
    {
        GameSettings.ResetToDefaults();
        Refresh();
    }

    // ---- UI doldurma ----

    void Refresh()
    {
        SetupSlider(masterSlider, GameSettings.MasterVolume, masterValueText);
        SetupSlider(musicSlider,  GameSettings.MusicVolume,  musicValueText);
        SetupSlider(sfxSlider,    GameSettings.SfxVolume,    sfxValueText);

        // ...WithoutNotify: değeri koddan yazarken onValueChanged tetiklenmesin,
        // yoksa Refresh ayarları kendi üstüne tekrar yazar.
        if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
        if (vsyncToggle != null) vsyncToggle.SetIsOnWithoutNotify(GameSettings.VSync);

        if (qualityDropdown != null && qualityDropdown.options.Count > 0)
            qualityDropdown.SetValueWithoutNotify(
                Mathf.Clamp(GameSettings.QualityLevel, 0, qualityDropdown.options.Count - 1));

        if (resolutionDropdown != null)
        {
            int index = resolutions.IndexOf(new Vector2Int(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight));
            if (index >= 0) resolutionDropdown.SetValueWithoutNotify(index);
        }
    }

    // Slider'ın aralığını da burada zorluyoruz — Inspector'da yanlış ayarlanmış olsa bile
    // 0-1 arası çalışsın.
    void SetupSlider(Slider slider, float value, TMP_Text label)
    {
        if (slider == null) return;

        slider.wholeNumbers = false;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(value);

        SetVolumeLabel(label, value);
    }

    void SetVolumeLabel(TMP_Text label, float value)
    {
        if (label != null) label.text = string.Format(volumeFormat, Mathf.RoundToInt(value * 100f));
    }

    void BuildQualityOptions()
    {
        if (qualityDropdown == null) return;

        qualityDropdown.ClearOptions();
        qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
    }

    void BuildResolutionOptions()
    {
        if (resolutionDropdown == null) return;

        resolutions.Clear();
        var labels = new List<string>();

        // Screen.resolutions aynı boyutu farklı tazeleme hızlarıyla defalarca verir —
        // listeyi boyuta göre tekilleştiriyoruz.
        foreach (var r in Screen.resolutions)
        {
            var size = new Vector2Int(r.width, r.height);
            if (resolutions.Contains(size)) continue;

            resolutions.Add(size);
            labels.Add($"{size.x} x {size.y}");
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(labels);
    }
}
