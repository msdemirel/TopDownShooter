using System.Collections.Generic;
using UnityEngine;

// Tüm ses efektlerini tek yerden çalar. Sahneye BİR tane koy (bir AudioSource ile).
// Çoğu ses global event'lere abone olarak KENDİLİĞİNDEN çalar (hit/hurt/ölüm/level/pickup);
// shoot/skill/wave için ilgili yerlerden statik metotlar çağrılır.
//
// Not: PlayOneShot timeScale'den etkilenmez — oyun donsa (upgrade paneli) bile ses çalar.
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Sesler")]
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

    [Header("Müzik")]
    [SerializeField] AudioClip music;
    [Range(0f, 1f)] [SerializeField] float musicVolume = 0.4f;

    [Header("Ayarlar")]
    [Range(0f, 1f)] [SerializeField] float sfxVolume = 0.8f;
    [Tooltip("AYNI ses bu süre içinde tekrar çalmaz — üst üste yığılmayı engeller (saniye).")]
    [SerializeField] float minSameClipInterval = 0.05f;
    [Tooltip("Ateş sesi bu aralıktan sık çalınmaz (çok silahta gürültü olmasın).")]
    [SerializeField] float minShootInterval = 0.06f;

    AudioSource src;
    AudioSource musicSrc;
    PlayerStats stats;

    // Aynı klibin en son ne zaman çalındığı — üst üste binmeyi engellemek için
    readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        src = GetComponent<AudioSource>();
        src.playOnAwake = false;

        // Müzik için ayrı, döngüsel bir AudioSource
        musicSrc = gameObject.AddComponent<AudioSource>();
        musicSrc.clip = music;
        musicSrc.loop = true;
        musicSrc.playOnAwake = false;
        if (music != null) musicSrc.Play();

        // Options'taki müzik/efekt seviyeleri (GameSettings) buradaki seviyelerle ÇARPILIR.
        // Ayar değişince müzik sesi anında güncellensin diye event'e abone oluyoruz.
        ApplyUserVolumes();
        GameSettings.Changed += ApplyUserVolumes;

        // Global event'ler (Health statik) — sınıflarına dokunmadan abone oluyoruz
        Health.AnyDamaged += OnDamaged;
        Health.AnyDeath += OnDeath;
        PlayerCollector.AnyCollected += OnCollected;

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            stats = p.GetComponent<PlayerStats>();
            if (stats != null) stats.OnLevelUp += OnLevelUp;
        }
    }

    void OnDestroy()
    {
        GameSettings.Changed -= ApplyUserVolumes;
        Health.AnyDamaged -= OnDamaged;
        Health.AnyDeath -= OnDeath;
        PlayerCollector.AnyCollected -= OnCollected;
        if (stats != null) stats.OnLevelUp -= OnLevelUp;
        if (Instance == this) Instance = null;
    }

    // Options'tan gelen seviyeleri uygular (Master zaten AudioListener.volume'de).
    void ApplyUserVolumes()
    {
        if (musicSrc != null) musicSrc.volume = musicVolume * GameSettings.MusicVolume;
    }

    // ---- Event dinleyicileri ----
    void OnDamaged(Health t, float amount, bool isCrit)
    {
        if (t.Team == Team.Enemy) PlayClip(hit);
        else PlayClip(hurt);
    }

    void OnDeath(Health t)
    {
        if (t.Team == Team.Enemy) PlayClip(enemyDeath);
    }

    void OnLevelUp(int level) => PlayClip(levelUp);

    void OnCollected(PickupType type)
    {
        switch (type)
        {
            case PickupType.Money:  PlayClip(pickupCoin);   break;
            case PickupType.Exp:    PlayClip(pickupExp);    break;
            case PickupType.Health: PlayClip(pickupHealth); break;
        }
    }

    // ---- Dışarıdan çağrılan statikler ----
    public static void PlayShoot() => Instance?.PlayClip(Instance.shoot, Instance.minShootInterval);
    public static void PlaySkill() => Instance?.PlayClip(Instance.skill);
    public static void PlayWave()  => Instance?.PlayClip(Instance.wave);

    void PlayClip(AudioClip c) => PlayClip(c, minSameClipInterval);

    // Aynı klip 'minInterval' içinde tekrar istenirse ATLA — üst üste yığılma olmaz.
    void PlayClip(AudioClip c, float minInterval)
    {
        if (c == null) return;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(c, out float t) && now - t < minInterval) return;

        lastPlayed[c] = now;
        src.PlayOneShot(c, sfxVolume * GameSettings.SfxVolume);
    }
}
