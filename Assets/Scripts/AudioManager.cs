using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tüm ses ve müzik. Resources/AudioLibrary varsa KENDİNİ KURAR ve sahneler arası yaşar
// (ana menüde de ses olur); sahnedeki eski AudioManager kopya sayılıp kaldırılır.
// Kütüphane yoksa eski davranış: sahnedeki AudioManager aşağıdaki "Eski" kliplerle çalışır.
//
// Kendiliğinden çalan sesler (global event'ler): isabet/kritik, hasar alma, ölüm, level, toplama,
// kalkan engeli, boss uyarısı, UI hover/tık. Diğerleri: AudioManager.Play(SfxId.X).
// Müzik: menü / oyun / boss (boss sahnedeyken) arasında yumuşak geçiş; pause'da kısılır ve boğuklaşır.
//
// Kurulum: Menü > TopDownShooter > Ses > Ses Kütüphanesini Kur
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Eski (sadece Resources/AudioLibrary yoksa kullanılır)")]
    [SerializeField] AudioClip shoot;
    [SerializeField] AudioClip hit;
    [SerializeField] AudioClip enemyDeath;
    [SerializeField] AudioClip pickupCoin;
    [SerializeField] AudioClip pickupExp;
    [SerializeField] AudioClip pickupHealth;
    [SerializeField] AudioClip levelUp;
    [SerializeField] AudioClip hurt;
    [SerializeField] AudioClip skill;
    [SerializeField] AudioClip wave;
    [SerializeField] AudioClip music;
    [Range(0f, 1f)] [SerializeField] float musicVolume = 0.4f;
    [Range(0f, 1f)] [SerializeField] float sfxVolume = 0.8f;
    [SerializeField] float minSameClipInterval = 0.05f;
    [SerializeField] float minShootInterval = 0.06f;

    const int VoiceCount = 16;
    const float PausedMusicVolume = 0.45f;   // pause / panel açıkken müzik bu orana iner
    const float PausedLowpass = 900f;        // ... ve boğuklaşır (Hz)

    AudioLibrary lib;
    AudioSource[] voices;
    int nextVoice;
    AudioSource musicA, musicB;              // çapraz geçiş için iki kaynak
    AudioLowPassFilter musicFilter;
    MusicId currentMusic;
    float fade = 1f;                         // 0 -> 1: B'den A'ya geçiş ilerlemesi
    float duck = 1f;

    readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();

    PlayerStats stats;
    Health playerHealth;
    bool inGameScene;
    bool bossAlive;
    float bossCheckTimer;
    readonly HashSet<EnemyBase> warnedBosses = new HashSet<EnemyBase>();

    int expCombo;                            // arka arkaya toplanan exp: perde yükselir
    float expComboUntil;

    // UI sesleri
    readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    Selectable lastHover;
    GameObject lastSelected;

    // ================================================================ kurulum
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        var library = Resources.Load<AudioLibrary>("AudioLibrary");
        if (library == null || Instance != null) return;   // kütüphane yoksa eski sahne davranışı

        var go = new GameObject("AudioManager");
        DontDestroyOnLoad(go);
        go.AddComponent<AudioSource>();
        go.AddComponent<AudioManager>().lib = library;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (lib == null) lib = Resources.Load<AudioLibrary>("AudioLibrary");

        var main = GetComponent<AudioSource>();
        main.playOnAwake = false;
        voices = new AudioSource[VoiceCount];
        voices[0] = main;
        for (int i = 1; i < VoiceCount; i++)
        {
            voices[i] = gameObject.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
        }

        // Müzik ayrı objede: alçak geçiren filtre sadece müziği etkilesin
        var m = new GameObject("Music");
        m.transform.SetParent(transform, false);
        musicA = m.AddComponent<AudioSource>();
        musicB = m.AddComponent<AudioSource>();
        foreach (var s in new[] { musicA, musicB }) { s.loop = true; s.playOnAwake = false; }
        musicFilter = m.AddComponent<AudioLowPassFilter>();
        musicFilter.cutoffFrequency = 22000f;

        GameSettings.Changed += ApplyVolumes;
        Health.AnyDamaged += OnDamaged;
        Health.AnyDeath += OnDeath;
        PlayerCollector.AnyCollected += OnCollected;
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (lib == null && music != null) { musicA.clip = music; musicA.Play(); }   // eski davranış
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        GameSettings.Changed -= ApplyVolumes;
        Health.AnyDamaged -= OnDamaged;
        Health.AnyDeath -= OnDeath;
        PlayerCollector.AnyCollected -= OnCollected;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unhook();
        Instance = null;
    }

    // Sahne değişince: oyuncuya yeniden abone ol, doğru müziği seç
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Unhook();
        warnedBosses.Clear();
        bossAlive = false;

        var p = GameObject.FindGameObjectWithTag("Player");
        inGameScene = p != null;
        if (p != null)
        {
            stats = p.GetComponent<PlayerStats>();
            playerHealth = p.GetComponent<Health>();
            if (stats != null) stats.OnLevelUp += OnLevelUp;
            if (playerHealth != null) playerHealth.OnDamageBlocked += OnBlocked;
        }
        PlayMusic(inGameScene ? MusicId.Game : MusicId.Menu);
    }

    void Unhook()
    {
        if (stats != null) stats.OnLevelUp -= OnLevelUp;
        if (playerHealth != null) playerHealth.OnDamageBlocked -= OnBlocked;
        stats = null;
        playerHealth = null;
    }

    // ================================================================ dışarıdan
    public static void Play(SfxId id, float volumeScale = 1f, float pitch = 1f)
    {
        if (Instance != null) Instance.PlaySfx(id, volumeScale, pitch);
    }

    // Eski çağrılar (Weapon / WaveManager / PlayerSkills) — yeni sisteme yönlenir
    public static void PlayShoot() => Play(SfxId.Shoot);
    public static void PlaySkill() => Play(SfxId.Blast);
    public static void PlayWave() => Play(SfxId.WaveStart);

    void PlaySfx(SfxId id, float volumeScale, float pitch)
    {
        if (lib == null) { PlayLegacy(id); return; }

        var e = lib.Get(id);
        if (e == null || e.clip == null) return;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(e.clip, out float t) && now - t < e.minInterval) return;
        lastPlayed[e.clip] = now;

        var v = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        v.pitch = pitch * (1f + Random.Range(-e.pitchVariance, e.pitchVariance));
        v.PlayOneShot(e.clip, e.volume * volumeScale * GameSettings.SfxVolume);
    }

    void PlayLegacy(SfxId id)
    {
        AudioClip c = id switch
        {
            SfxId.Shoot or SfxId.MeleeSwing => shoot,
            SfxId.Hit or SfxId.Crit => hit,
            SfxId.EnemyDeath => enemyDeath,
            SfxId.PickupCoin => pickupCoin,
            SfxId.PickupExp => pickupExp,
            SfxId.PickupHealth => pickupHealth,
            SfxId.LevelUp => levelUp,
            SfxId.Hurt => hurt,
            SfxId.WaveStart => wave,
            SfxId.Dash or SfxId.Blast or SfxId.Shield or SfxId.Pulse or SfxId.Burst or SfxId.Heal
                or SfxId.Frost or SfxId.Overdrive or SfxId.Lightning => skill,
            _ => null,
        };
        if (c == null) return;
        float now = Time.unscaledTime;
        float min = id == SfxId.Shoot ? minShootInterval : minSameClipInterval;
        if (lastPlayed.TryGetValue(c, out float t) && now - t < min) return;
        lastPlayed[c] = now;
        voices[0].PlayOneShot(c, sfxVolume * GameSettings.SfxVolume);
    }

    // ================================================================ müzik
    public static void SetMusic(MusicId id) { if (Instance != null) Instance.PlayMusic(id); }

    void PlayMusic(MusicId id)
    {
        if (lib == null || id == currentMusic) return;
        currentMusic = id;
        var clip = lib.GetMusic(id);

        // Eski parça B'ye geçer ve söner, yeni parça A'da yükselir
        (musicA, musicB) = (musicB, musicA);
        musicA.clip = clip;
        musicA.volume = 0f;
        if (clip != null) musicA.Play(); else musicA.Stop();
        fade = 0f;
    }

    void ApplyVolumes()
    {
        float target = (lib != null ? lib.musicVolume : musicVolume) * GameSettings.MusicVolume * duck;
        musicA.volume = target * fade;
        musicB.volume = target * (1f - fade);
        if (fade >= 1f && musicB.isPlaying) musicB.Stop();
    }

    // ================================================================ her kare
    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Pause / upgrade paneli / game over: müzik kısılır ve boğuklaşır
        bool paused = inGameScene && Time.timeScale == 0f;
        duck = Mathf.MoveTowards(duck, paused ? PausedMusicVolume : 1f, dt * 2f);
        float cutoff = paused ? PausedLowpass : 22000f;
        musicFilter.cutoffFrequency = Mathf.Lerp(musicFilter.cutoffFrequency, cutoff, 1f - Mathf.Exp(-dt * 6f));

        if (lib != null) fade = Mathf.MoveTowards(fade, 1f, dt / Mathf.Max(0.05f, lib.crossfade));
        ApplyVolumes();

        if (inGameScene) UpdateBoss(dt);
        UpdateUiSounds();
    }

    // Boss sahneye girince: uyarı + sarsıntı + boss müziği; hepsi ölünce oyun müziğine dön
    void UpdateBoss(float dt)
    {
        bossCheckTimer -= dt;
        if (bossCheckTimer > 0f) return;
        bossCheckTimer = 0.25f;

        bool any = false;
        var alive = EnemyRegistry.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            var e = alive[i];
            if (e == null || e.IsDead || !e.IsBoss) continue;
            any = true;
            if (warnedBosses.Add(e))
            {
                PlaySfx(SfxId.BossWarning, 1f, 1f);
                CameraShake.Shake(0.25f, 0.6f);
                var toast = FindAnyObjectByType<UpgradeToastUI>();
                if (toast != null) toast.Show($"<color=#E5533C>{Loc.T("BOSS INCOMING!")}</color>");
            }
        }

        if (any != bossAlive && (playerHealth == null || !playerHealth.IsDead))
        {
            bossAlive = any;
            PlayMusic(any ? MusicId.Boss : MusicId.Game);
        }
    }

    // UI: fare üstüne gelince hover, tıklayınca click (pasifse hata sesi), klavyeyle gezinince hover.
    // Tüm Button/Toggle/Slider'lara (kodla kurulanlar dahil) kendiliğinden gelir.
    void UpdateUiSounds()
    {
        var es = EventSystem.current;
        if (es == null) return;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            var data = new PointerEventData(es) { position = mouse.position.ReadValue() };
            uiHits.Clear();
            es.RaycastAll(data, uiHits);
            Selectable over = null;
            foreach (var h in uiHits)
            {
                over = h.gameObject.GetComponentInParent<Selectable>();
                if (over != null) break;
            }

            if (over != lastHover)
            {
                lastHover = over;
                if (over != null && over.IsInteractable()) PlaySfx(SfxId.UiHover, 1f, 1f);
            }
            if (over != null && mouse.leftButton.wasPressedThisFrame)
                PlaySfx(!over.IsInteractable() ? SfxId.UiError : IsBack(over) ? SfxId.UiBack : SfxId.UiClick, 1f, 1f);
        }

        // Klavye/gamepad ile seçim değişince hover, Enter ile tık
        var sel = es.currentSelectedGameObject;
        if (sel != lastSelected)
        {
            if (lastSelected != null && sel != null && (mouse == null || !mouse.leftButton.isPressed))
                PlaySfx(SfxId.UiHover, 1f, 1f);
            lastSelected = sel;
        }
        var kb = Keyboard.current;
        if (kb != null && sel != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            && sel.TryGetComponent<Selectable>(out var s) && s.IsInteractable())
            PlaySfx(IsBack(s) ? SfxId.UiBack : SfxId.UiClick, 1f, 1f);
    }

    static bool IsBack(Selectable s)
    {
        string n = s.gameObject.name;
        return n.Contains("Back") || n.Contains("Cancel") || n.Contains("Resume");
    }

    // ================================================================ event'ler
    void OnDamaged(Health t, float amount, bool isCrit)
    {
        if (t.Team == Team.Enemy) PlaySfx(isCrit ? SfxId.Crit : SfxId.Hit, isCrit ? 1f : 0.8f, 1f);
        else
        {
            PlaySfx(SfxId.Hurt, 1f, 1f);
            GameFeel.HitStop(0.05f);
            GameFeel.Rumble(0.35f, 0.5f, 0.12f);
        }
    }

    void OnDeath(Health t)
    {
        if (t.Team == Team.Enemy)
        {
            PlaySfx(SfxId.EnemyDeath, 1f, 1f);
            if (t.TryGetComponent<EnemyBase>(out var e) && e.IsBoss)
            {
                PlaySfx(SfxId.Explosion, 1f, 0.8f);
                CameraShake.Shake(0.4f, 0.5f);
                GameFeel.HitStop(0.22f);
                GameFeel.Rumble(0.8f, 0.6f, 0.4f);
            }
        }
        else if (t == playerHealth)
        {
            PlaySfx(SfxId.PlayerDeath, 1f, 1f);
            PlayMusic(MusicId.None);   // müzik söner; Game Over ekranı kendi sesini çalar
        }
    }

    void OnBlocked(Health h, float amount) => PlaySfx(SfxId.ShieldBlock, 1f, 1f);

    void OnLevelUp(int level) => PlaySfx(SfxId.LevelUp, 1f, 1f);

    void OnCollected(PickupType type)
    {
        switch (type)
        {
            case PickupType.Money: PlaySfx(SfxId.PickupCoin, 1f, 1f); break;
            case PickupType.Health: PlaySfx(SfxId.PickupHealth, 1f, 1f); break;
            case PickupType.Exp:
                // Art arda toplanan exp'ler tatmin edici bir "tırmanış" yapsın
                float now = Time.unscaledTime;
                expCombo = now < expComboUntil ? Mathf.Min(expCombo + 1, 12) : 0;
                expComboUntil = now + 0.35f;
                PlaySfx(SfxId.PickupExp, 1f, Mathf.Pow(1.0595f, expCombo));   // her toplamada yarım ton
                break;
        }
    }
}
