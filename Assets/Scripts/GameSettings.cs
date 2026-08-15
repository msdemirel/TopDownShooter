using UnityEngine;

// Oyun ayarları TEK yerde: PlayerPrefs'e kaydeder ve oyun açılır açılmaz uygular.
// Statik olduğu için sahneye hiçbir şey koymaya gerek YOK — MainMenu de MainGame de
// aynı değerleri okur.
//
// Ses zinciri:
//   AudioListener.volume = Master  (her şeyi birden kısar)
//   AudioManager ise Music/Sfx değerlerini kendi iç seviyeleriyle çarpar.
public static class GameSettings
{
    // PlayerPrefs anahtarları — değiştirirsen oyuncunun eski ayarları sıfırlanır.
    const string KeyMaster     = "set_master";
    const string KeyMusic      = "set_music";
    const string KeySfx        = "set_sfx";
    const string KeyFullscreen = "set_fullscreen";
    const string KeyVSync      = "set_vsync";
    const string KeyQuality    = "set_quality";
    const string KeyResWidth   = "set_res_w";
    const string KeyResHeight  = "set_res_h";

    // Bir ayar değişince haber verir (AudioManager müzik sesini anında günceller).
    public static event System.Action Changed;

    public static float MasterVolume { get; private set; } = 1f;
    public static float MusicVolume  { get; private set; } = 1f;
    public static float SfxVolume    { get; private set; } = 1f;
    public static bool  Fullscreen   { get; private set; } = true;
    public static bool  VSync        { get; private set; } = true;
    public static int   QualityLevel { get; private set; }
    public static int   ResolutionWidth  { get; private set; }
    public static int   ResolutionHeight { get; private set; }

    // Oyun açılırken, İLK SAHNE YÜKLENMEDEN önce otomatik çalışır. Böylece MainMenu'den
    // başlasan da Editor'da doğrudan MainGame'e Play'lesen de ayarlar uygulanmış olur.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Load()
    {
        MasterVolume = PlayerPrefs.GetFloat(KeyMaster, 1f);
        MusicVolume  = PlayerPrefs.GetFloat(KeyMusic,  1f);
        SfxVolume    = PlayerPrefs.GetFloat(KeySfx,    1f);

        Fullscreen   = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
        VSync        = PlayerPrefs.GetInt(KeyVSync, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
        QualityLevel = PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel());

        ResolutionWidth  = PlayerPrefs.GetInt(KeyResWidth,  Screen.currentResolution.width);
        ResolutionHeight = PlayerPrefs.GetInt(KeyResHeight, Screen.currentResolution.height);

        Apply();
    }

    // ---- Ayarlayıcılar (OptionsUI buradan çağırır) ----

    public static void SetMasterVolume(float v)
    {
        MasterVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KeyMaster, MasterVolume);
        AudioListener.volume = MasterVolume;
        Changed?.Invoke();
    }

    public static void SetMusicVolume(float v)
    {
        MusicVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KeyMusic, MusicVolume);
        Changed?.Invoke();
    }

    public static void SetSfxVolume(float v)
    {
        SfxVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KeySfx, SfxVolume);
        Changed?.Invoke();
    }

    public static void SetFullscreen(bool on)
    {
        Fullscreen = on;
        PlayerPrefs.SetInt(KeyFullscreen, on ? 1 : 0);
        ApplyScreen();
        Changed?.Invoke();
    }

    public static void SetVSync(bool on)
    {
        VSync = on;
        PlayerPrefs.SetInt(KeyVSync, on ? 1 : 0);
        QualitySettings.vSyncCount = on ? 1 : 0;
        Changed?.Invoke();
    }

    public static void SetQualityLevel(int level)
    {
        QualityLevel = Mathf.Clamp(level, 0, QualitySettings.names.Length - 1);
        PlayerPrefs.SetInt(KeyQuality, QualityLevel);
        // ikinci parametre: expensive change'leri de hemen uygula
        QualitySettings.SetQualityLevel(QualityLevel, true);
        Changed?.Invoke();
    }

    public static void SetResolution(int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        ResolutionWidth = width;
        ResolutionHeight = height;
        PlayerPrefs.SetInt(KeyResWidth, width);
        PlayerPrefs.SetInt(KeyResHeight, height);
        ApplyScreen();
        Changed?.Invoke();
    }

    // Options panelindeki "Varsayılana Dön" butonu için.
    public static void ResetToDefaults()
    {
        MasterVolume = MusicVolume = SfxVolume = 1f;
        Fullscreen = true;
        VSync = true;
        QualityLevel = QualitySettings.names.Length - 1;
        ResolutionWidth  = Screen.currentResolution.width;
        ResolutionHeight = Screen.currentResolution.height;

        PlayerPrefs.DeleteKey(KeyMaster);
        PlayerPrefs.DeleteKey(KeyMusic);
        PlayerPrefs.DeleteKey(KeySfx);
        PlayerPrefs.DeleteKey(KeyFullscreen);
        PlayerPrefs.DeleteKey(KeyVSync);
        PlayerPrefs.DeleteKey(KeyQuality);
        PlayerPrefs.DeleteKey(KeyResWidth);
        PlayerPrefs.DeleteKey(KeyResHeight);

        Apply();
    }

    // PlayerPrefs normalde oyun kapanırken diske yazar; panel kapanınca zorluyoruz ki
    // oyun çökse bile ayarlar kaybolmasın.
    public static void Save() => PlayerPrefs.Save();

    // ---- Uygulama ----

    public static void Apply()
    {
        AudioListener.volume = MasterVolume;
        QualitySettings.SetQualityLevel(Mathf.Clamp(QualityLevel, 0, QualitySettings.names.Length - 1), true);
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        ApplyScreen();
        Changed?.Invoke();
    }

    static void ApplyScreen()
    {
        // Editor'da çözünürlük/fullscreen değiştirmek Game view'ı bozar ve gerçek build'i
        // temsil etmez — bu yüzden sadece build'de uygulanır. (Ayar yine kaydedilir.)
#if !UNITY_EDITOR
        var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Screen.SetResolution(ResolutionWidth, ResolutionHeight, mode);
#endif
    }
}
