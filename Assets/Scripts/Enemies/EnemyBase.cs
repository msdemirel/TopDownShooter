using UnityEngine;

// Tüm enemy türlerinin ortak temeli: oyuncuyu bulma, kovalama, birbirinden ayrılma,
// can, animasyon, ölünce loot. Saldırı davranışı türev sınıflarda (Melee/Ranged/Exploder).
//
// Tasarım notu: Haritada duvar YOK. Bu yüzden görüş kontrolü (line of sight),
// pathfinding ve rastgele dolaşma kaldırıldı — düşman nerede doğarsa doğsun
// her zaman doğrudan oyuncuya gider.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("References")]
    [SerializeField] protected LootDropper loot;    // boşsa aynı objede aranır
    [SerializeField] protected Animator animator;   // boşsa child'larda aranır

    [Header("Ayrılma (birbirine yapışmasın)")]
    [Tooltip("Yakındaki diğer düşmanlardan itilerek üst üste yığılmayı önler.")]
    [SerializeField] bool useSeparation = true;
    [Tooltip("Bu mesafedeki düşmanlardan uzaklaşır. Genelde düşmanın çapı kadar.")]
    [SerializeField] float separationRadius = 0.9f;
    [Tooltip("İtme gücü. Büyütürsen daha çok dağılırlar ama oyuncuya gelişleri bozulur.")]
    [SerializeField] float separationWeight = 1.2f;
    [Tooltip("Opsiyonel: sadece bu layer(lar) taranır. Boşsa tüm layer'lar taranıp EnemyBase'i olanlar seçilir.")]
    [SerializeField] LayerMask enemyMask;

    [Header("Animator Parametreleri")]
    [SerializeField] string speedParam = "Speed";       // float: idle/run geçişi
    [SerializeField] string attackTrigger = "Attack";   // trigger
    [SerializeField] string deathTrigger = "Death";     // trigger

    [Header("Boss")]
    [Tooltip("Boss öldürmeleri kalıcı ilerlemede (Core, kilitler) ayrıca sayılır.")]
    [SerializeField] bool isBoss;
    public bool IsBoss => isBoss;

    [Header("Death")]
    [Tooltip("Death animasyonu oynasın diye yok etmeden önce beklenen süre (sn).")]
    [SerializeField] float destroyDelay = 1f;

    protected EnemyLevelStats stats;
    protected Transform target;
    protected Rigidbody2D rb;
    protected Health health;
    protected SpriteRenderer sr;

    public bool IsDead { get; private set; }

    // Oyuncu bir düşmanı öldürünce tetiklenir (kendini patlatan exploder SAYILMAZ).
    // RunStats öldürme sayısını buradan tutar.
    public static event System.Action<EnemyBase> AnyKilled;

    // Domain Reload kapalıyken önceki Play oturumunun aboneleri kalmasın (Health kalıbı).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => AnyKilled = null;

    // Yavaşlatma (Frost Nova): süre bitene kadar hız bu çarpanla çarpılır.
    float slowMultiplier = 1f;
    float slowUntil;
    public bool IsSlowed => Time.time < slowUntil;   // skill sinerjileri okur (Conductive, Thermal Shock)

    // Separation için paylaşılan tampon: her karede yeni dizi ayırmamak için.
    // 16'dan fazla komşu varsa ilk 16'sı dikkate alınır (ayrılma için fazlasıyla yeterli).
    static readonly Collider2D[] sepBuffer = new Collider2D[16];
    ContactFilter2D sepFilter;

    // ---- Kurulum ----
    // Bileşenler Awake'te alınır: Init hiç çağrılmasa bile (sahneye elle konan düşman)
    // null referans hatası olmaz, düşman sadece hareketsiz kalır.
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;  // prefab'ta Freeze Rotation Z unutulsa bile çarpışmalar sprite'ı döndürmesin
        health = GetComponent<Health>();
        sr = GetComponentInChildren<SpriteRenderer>();
        if (loot == null) loot = GetComponent<LootDropper>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        health.SetDestroyOnDeath(false);  // yok etmeyi biz yönetiriz (death animasyonu için)
        health.OnDeath += HandleDeath;

        sepFilter = new ContactFilter2D();
        sepFilter.NoFilter();                                        // tüm layer'lar + trigger'lar
        if (enemyMask.value != 0) sepFilter.SetLayerMask(enemyMask); // maske verildiyse daralt
    }

    void OnEnable() => EnemyRegistry.Register(this);
    void OnDisable() => EnemyRegistry.Unregister(this);

    void OnDestroy()
    {
        if (health != null) health.OnDeath -= HandleDeath;
    }

    // Spawner Instantiate'ten hemen sonra çağırır ve seviyeyi uygular.
    public virtual void Init(EnemyLevelStats levelStats)
    {
        stats = Difficulty.Scale(levelStats);   // seçili zorluğun çarpanları (kopya üzerinde)
        health.SetMaxHealth(stats.maxHealth);
    }

    protected virtual void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) target = p.transform;
    }

    // Ölünce loot düşsün mü? Türevler ezebilir.
    // Örn. exploder kendini patlatarak öldüyse ödül vermez (bkz. ExploderEnemy).
    protected virtual bool ShouldDropLoot => true;

    protected virtual void HandleDeath(Health h)
    {
        if (IsDead) return;
        IsDead = true;

        EnemyRegistry.Unregister(this);  // silahlar cesedi hedeflemesin

        // Hareketi ve çarpışmayı durdur (death animasyonu boyunca)
        if (rb != null) rb.linearVelocity = Vector2.zero;
        foreach (var col in GetComponents<Collider2D>())
            col.enabled = false;

        SetAnimSpeed(0f);
        TriggerDeath();

        // Loot hakkı = oyuncu öldürdü (exploder kendini patlattıysa ikisi de yok)
        if (ShouldDropLoot) AnyKilled?.Invoke(this);

        if (loot != null && ShouldDropLoot)
        {
            // Eski asset'lerde alan yoksa 0 gelir; en az bir kez zar atılsın
            int rolls = stats != null ? Mathf.Max(1, stats.lootRolls) : 1;
            for (int i = 0; i < rolls; i++) loot.DropLoot();
        }

        Destroy(gameObject, destroyDelay);
    }

    // ---- Yavaşlatma ----
    // percent: 0.5 = %50 yavaş. Üst üste binerse GÜÇLÜ olan kazanır, süre tazelenir.
    public void ApplySlow(float percent, float duration)
    {
        if (IsDead || duration <= 0f) return;

        float mult = 1f - Mathf.Clamp(percent, 0f, 0.9f);
        slowMultiplier = IsSlowed ? Mathf.Min(slowMultiplier, mult) : mult;
        slowUntil = Mathf.Max(slowUntil, Time.time + duration);
        SlowVisual.Refresh(this, duration);   // ayak altı buz + kar (renk tint'i yok, bkz. SlowVisual)
    }

    // Türevler stats.moveSpeed yerine bunu kullanır (yavaşlatma dahil).
    protected float MoveSpeed => stats.moveSpeed * (IsSlowed ? slowMultiplier : 1f);

    // Yavaşlayan düşmanın animasyonu da yavaşlasın: görsel ipucu.
    // (Renk değiştirmiyoruz; HitFlash sprite rengini kendi yönetiyor.)
    void Update()
    {
        if (animator != null)
            animator.speed = !IsDead && IsSlowed ? slowMultiplier : 1f;
    }

    // ---- Hareket ----
    // Oyuncuya doğru yön + "diğer düşmanlardan uzaklaş" kuvveti.
    // Dönen vektörü türev sınıf stats.moveSpeed ile çarpar.
    protected Vector2 GetMoveVector()
    {
        Vector2 toTarget = target != null
            ? ((Vector2)(target.position - transform.position)).normalized
            : Vector2.zero;

        if (toTarget != Vector2.zero) FaceDir(toTarget);

        Vector2 sep = Separation() * separationWeight;
        if (sep == Vector2.zero) return toTarget;

        // Toplam yönü birim uzunlukta tut (hız stats.moveSpeed ile veriliyor).
        return Vector2.ClampMagnitude(toTarget + sep, 1f);
    }

    // Yakındaki her düşman için ondan uzağa doğru bir itme üretir (yakınsa daha güçlü).
    Vector2 Separation()
    {
        if (!useSeparation || separationRadius <= 0f) return Vector2.zero;

        int count = Physics2D.OverlapCircle(transform.position, separationRadius, sepFilter, sepBuffer);

        Vector2 push = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            // Sadece diğer düşmanlar (duvar/oyuncu değil), kendini atla
            var other = sepBuffer[i].GetComponentInParent<EnemyBase>();
            if (other == null || other == this) continue;

            Vector2 away = (Vector2)transform.position - (Vector2)other.transform.position;
            float d = away.magnitude;

            // Tam üst üsteyse rastgele bir yöne kaç (yoksa yön hesaplanamaz)
            if (d < 0.001f) { push += RandomDirection(); continue; }

            push += away.normalized * (1f - Mathf.Clamp01(d / separationRadius));
        }
        return push;
    }

    static Vector2 RandomDirection()
    {
        Vector2 d = Random.insideUnitCircle.normalized;
        return d == Vector2.zero ? Vector2.up : d;
    }

    // ---- Animasyon yardımcıları ----
    protected void SetAnimSpeed(float speed)
    {
        if (animator != null && !string.IsNullOrEmpty(speedParam))
            animator.SetFloat(speedParam, speed);
    }

    protected void TriggerAttack()
    {
        if (animator != null && !string.IsNullOrEmpty(attackTrigger))
            animator.SetTrigger(attackTrigger);
    }

    void TriggerDeath()
    {
        if (animator != null && !string.IsNullOrEmpty(deathTrigger))
            animator.SetTrigger(deathTrigger);
    }

    // ---- Türev yardımcıları ----
    protected bool HasTarget => target != null && stats != null;

    protected float DistanceToTarget =>
        target != null ? Vector2.Distance(transform.position, target.position) : Mathf.Infinity;

    // Oyuncuya bakacak şekilde sprite'ı çevirir (saldırı sırasında kullanılır).
    protected void FaceTarget()
    {
        if (sr == null || target == null) return;
        if (Mathf.Abs(target.position.x - transform.position.x) > 0.01f)
            sr.flipX = target.position.x < transform.position.x;
    }

    // Verilen hareket yönüne göre sprite'ı çevirir.
    protected void FaceDir(Vector2 dir)
    {
        if (sr == null) return;
        if (Mathf.Abs(dir.x) > 0.01f)
            sr.flipX = dir.x < 0f;
    }
}
