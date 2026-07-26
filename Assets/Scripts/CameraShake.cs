using UnityEngine;

// Kameraya kısa sarsıntı verir. Main Camera'ya eklenir; sahnede bir tane olmalı.
//
// Çalışma şekli: sarsıntı LateUpdate'te pozisyona OFFSET olarak eklenir ve bir sonraki
// karede önce geri alınır. Böylece kamera ister sabit dursun ister başka bir script
// oyuncuyu takip etsin, sarsıntı onların üstüne binip temiz şekilde söner.
//
// Kullanım: herhangi bir yerden CameraShake.Shake(güç, süre).
// Oyuncu hasar alınca kendiliğinden sarsar (Health.AnyDamaged'i dinler).
//
// CINEMACHINE UYUMU: CinemachineBrain kamerayı her kare LateUpdate'te yeniden
// konumlandırır. Bu yüzden (1) bu script Brain'den SONRA çalışmalı — aşağıdaki
// DefaultExecutionOrder bunu sağlar — ve (2) offset'i geri almaya gerek yoktur,
// Brain zaten her kare üstüne yazar (aynı objede Brain görürsek otomatik anlarız).
[DefaultExecutionOrder(2000)]
public class CameraShake : MonoBehaviour
{
    [Header("Oyuncu hasar alınca")]
    [SerializeField] bool shakeOnPlayerDamage = true;
    [Tooltip("Sarsıntının en fazla kaç birim kaydıracağı.")]
    [SerializeField] float playerDamageStrength = 0.15f;
    [SerializeField] float playerDamageDuration = 0.25f;

    [Header("His")]
    [Tooltip("Titreme hızı. Düşük = ağır sallantı, yüksek = ince titreme. 15-25 doğal durur.")]
    [SerializeField] float frequency = 18f;

    static CameraShake instance;

    float strength;
    float duration;
    float timeLeft;
    Vector3 lastOffset;
    bool externallyDriven;   // Cinemachine gibi bir sistem kamerayı her kare yeniden mi yazıyor?

    // Play Mode'a her girişte statik referansı temizle (Domain Reload kapalıyken kalır).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    void OnEnable()
    {
        instance = this;
        Health.AnyDamaged += HandleDamaged;

        // İsimle arıyoruz: Cinemachine paketi projede olmasa da bu script derlensin
        externallyDriven = GetComponent("CinemachineBrain") != null;
    }

    void OnDisable()
    {
        Health.AnyDamaged -= HandleDamaged;

        // Kapanırken kalan offset'i geri al, kamera kaymış kalmasın
        // (Cinemachine varsa gerek yok — bir sonraki kare zaten üstüne yazar)
        if (!externallyDriven) transform.localPosition -= lastOffset;
        lastOffset = Vector3.zero;

        if (instance == this) instance = null;
    }

    void HandleDamaged(Health target, float amount, bool isCrit)
    {
        if (shakeOnPlayerDamage && target.Team == Team.Player)
            StartShake(playerDamageStrength, playerDamageDuration);
    }

    // Her yerden çağrılabilir (ör. ExploderEnemy patlaması).
    public static void Shake(float strength, float duration)
    {
        if (instance != null) instance.StartShake(strength, duration);
    }

    // Test: Play Mode'da component'in sağ üstündeki ⋮ menüsünden "Test Shake" seç.
    // Böylece tetikleyicilerden bağımsız, sarsıntı mekanizmasının kendisini denersin.
    [ContextMenu("Test Shake")]
    void TestShake() => StartShake(0.5f, 0.6f);

    void StartShake(float newStrength, float newDuration)
    {
        // Güçlü sarsıntı zayıfı ezer; süren zayıf bir istek güçlüyü kesmesin
        if (timeLeft > 0f && newStrength < strength) return;

        strength = newStrength;
        duration = Mathf.Max(0.01f, newDuration);
        timeLeft = duration;
    }

    void LateUpdate()
    {
        // Geçen karenin offset'ini geri al — ama kamerayı Cinemachine sürüyorsa alma:
        // Brain pozisyonu bu kare zaten yeniden yazdı, çıkarırsak kamerayı kaydırırız.
        if (!externallyDriven) transform.localPosition -= lastOffset;
        lastOffset = Vector3.zero;

        if (timeLeft <= 0f) return;

        // unscaled: oyun donsa da (game over / upgrade paneli) süre akar ve sarsıntı BİTER.
        // (deltaTime kullansaydık timeScale 0'da süre hiç azalmaz, sonsuza dek sarsılırdı.)
        timeLeft -= Time.unscaledDeltaTime;

        // Donuk ekranda görsel sarsıntı uygulama — durmuş oyun titremesin
        if (Time.timeScale == 0f) return;

        // Perlin gürültüsü: kareler arası süreklilik var, ham Random'dan çok daha yumuşak.
        // Sona doğru fade ile söner.
        float fade = Mathf.Clamp01(timeLeft / duration);
        float t = Time.unscaledTime * frequency;
        Vector2 noise = new Vector2(Mathf.PerlinNoise(t, 0.5f) - 0.5f,
                                    Mathf.PerlinNoise(0.5f, t) - 0.5f) * 2f;

        lastOffset = (Vector3)(noise * (strength * fade));
        transform.localPosition += lastOffset;
    }
}
