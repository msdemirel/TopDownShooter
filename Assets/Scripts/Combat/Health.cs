using System;
using UnityEngine;

// Hem player hem enemy tarafından kullanılan can bileşeni.
public class Health : MonoBehaviour, IDamageable
{
    [Header("Team")]
    [SerializeField] Team team = Team.Enemy;

    [Header("Health")]
    [SerializeField] float maxHealth = 100f;
    [SerializeField] bool destroyOnDeath = true;  // Player için false yapılır, ölümü event ile yönetiriz

    float current;
    bool dead;

    public Team Team => team;
    public float Max => maxHealth;
    public float Current => current;
    public float Normalized => maxHealth > 0f ? current / maxHealth : 0f;
    public bool IsDead => dead;

    // Kalkan gibi etkiler için: true iken gelen hasar tamamen yok sayılır.
    // (PlayerSkills süreli olarak açıp kapatır.)
    public bool Invulnerable { get; set; }

    // Kalıcı "Second Chance" upgrade'i: ölümcül hasarda ölmek yerine canın bu oranıyla dirilir.
    public int ExtraLives { get; set; }
    public float ReviveHealthFraction { get; set; } = 0.5f;
    public event Action<Health> OnRevived;

    // UI / loot / spawner bunlara abone olur
    public event Action<Health> OnHealthChanged;
    public event Action<Health> OnDeath;

    // Invulnerable iken gelen hasar engellenince (kalkan görseli vuruşa tepki versin diye)
    public event Action<Health, float> OnDamageBlocked;

    // Sahnedeki HERHANGİ bir Health hasar alınca tetiklenir (kim, ne kadar, kritik mi).
    // Hasar yazıları / vuruş flaşı tek yerden bunu dinler — enemy prefab'larına script gerekmez.
    public static event Action<Health, float, bool> AnyDamaged;

    // Sahnedeki HERHANGİ bir Health ölünce tetiklenir (ölüm partikülleri dinler).
    public static event Action<Health> AnyDeath;

    // Sahnedeki HERHANGİ bir Health iyileşince (gerçekten eklenen miktar). İyileşme yazısı dinler.
    public static event Action<Health, float> AnyHealed;

    // Play Mode'a her girişte statik event'leri temizle.
    // (Domain Reload kapalıyken abonelikler önceki oturumdan kalır -> hayalet dinleyiciler.)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        AnyDamaged = null;
        AnyDeath = null;
        AnyHealed = null;
    }

    void Awake()
    {
        current = maxHealth;
    }

    // EnemyBase, death animasyonunu oynatmak için yok etmeyi kendisi yönetir.
    public void SetDestroyOnDeath(bool value) => destroyOnDeath = value;

    // Spawner enemy'yi seviye verisiyle başlatırken çağırır.
    public void SetMaxHealth(float max, bool fill = true)
    {
        maxHealth = max;
        current = fill ? max : Mathf.Min(current, max);
        OnHealthChanged?.Invoke(this);
    }

    public void TakeDamage(float amount) => TakeDamage(amount, false);

    // isCrit sadece bilgi amaçlı taşınır (hasar yazısının rengi/boyutu için);
    // hasar hesabı çağırandan çarpılmış olarak gelir.
    public void TakeDamage(float amount, bool isCrit)
    {
        if (dead || amount <= 0f) return;
        if (Invulnerable) { OnDamageBlocked?.Invoke(this, amount); return; }

        current = Mathf.Max(0f, current - amount);
        OnHealthChanged?.Invoke(this);
        AnyDamaged?.Invoke(this, amount, isCrit);

        if (current <= 0f)
        {
            if (ExtraLives > 0) Revive();
            else Die();
        }
    }

    void Revive()
    {
        ExtraLives--;
        current = Mathf.Max(1f, maxHealth * ReviveHealthFraction);
        OnHealthChanged?.Invoke(this);
        OnRevived?.Invoke(this);   // dokunulmazlık + efekt dinleyen tarafta (MetaApplier)
    }

    // Anında öldürür — HASAR SAYILMAZ: AnyDamaged tetiklenmez (hasar yazısı çıkmaz,
    // can barı doğmaz) ve Invulnerable'ı umursamaz. Exploder'ın kendini patlatması gibi
    // "vurulmadan ölme" durumları için.
    public void Kill()
    {
        if (dead) return;
        current = 0f;
        OnHealthChanged?.Invoke(this);
        Die();
    }

    public void Heal(float amount)
    {
        if (dead || amount <= 0f) return;

        float before = current;
        current = Mathf.Min(maxHealth, current + amount);
        OnHealthChanged?.Invoke(this);
        if (current > before) AnyHealed?.Invoke(this, current - before);
    }

    void Die()
    {
        if (dead) return;
        dead = true;

        OnDeath?.Invoke(this);  // loot drop / spawner sayacı burada çalışır
        AnyDeath?.Invoke(this); // ölüm efektleri burada çalışır

        if (destroyOnDeath)
            Destroy(gameObject);
    }
}
