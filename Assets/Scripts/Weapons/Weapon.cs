using System.Collections;
using UnityEngine;

// Tek bir silah. Bir slota takılır, menzilindeki düşmanlardan RASTGELE birine
// kilitlenir, ona döner ve otomatik saldırır. Hedef ölene ya da menzilden
// çıkana kadar bırakılmaz — böylece silahlar hep birlikte aynı düşmana dönmez,
// her biri kendi hedefiyle uğraşır (Brotato hissi). Mermi sonsuzdur (ammo yok).
// İki tip: Ranged (mermi fırlatır) ve Melee (yakın menzilde yay çizerek biçer).
// Özellikleri WeaponData'dan gelir; aynı prefab tüm silahlar için kullanılır.
public class Weapon : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Merminin çıkacağı nokta (namlu ucu). Boşsa silahın kendi pozisyonu kullanılır.")]
    [SerializeField] Transform firePoint;
    [Tooltip("Silahın görseli. Boşsa child'larda aranır.")]
    [SerializeField] SpriteRenderer sr;

    [Tooltip("Oyun başında elle atanabilir. PlayerWeapons zaten SetData ile dolduruyor.")]
    [SerializeField] WeaponData data;

    [Tooltip("Ateş edince namlu ucunda beliren efekt (opsiyonel). Tek seferlik animasyon prefab'ı.")]
    [SerializeField] GameObject muzzleEffect;

    float nextFireTime;
    PlayerWeapons owner;   // global hasar/atış hızı çarpanları buradan gelir
    EnemyBase target;      // kilitlenilen hedef (ölünce/menzilden çıkınca yenisi seçilir)
    bool swinging;         // kılıç atılma animasyonu sürüyor mu (o an AimAt devre dışı)
    Vector3 restLocalPos;  // kılıcın dinlenme (slot) konumu; dürtmeden geri döneceği yer
    bool restCaptured;

    // WeaponData bir asset olduğu için üstünde oynamıyoruz; çarpanları burada uyguluyoruz.
    float CurrentDamage => data.damage * WeaponTiers.DamageMultiplier(Tier)
                           * (owner != null ? owner.DamageMultiplier : 1f);
    float CurrentCooldown => data.FireCooldown / WeaponTiers.FireRateMultiplier(Tier)
                             / (owner != null ? owner.FireRateMultiplier : 1f);

    // Birleştirme kademesi (1-4). PlayerWeapons aynı silahtan iki tane olunca yükseltir.
    public int Tier { get; private set; } = 1;
    SpriteRenderer tierGlow;
    float CurrentCritChance => data.critChance + (owner != null ? owner.CritChanceBonus : 0f);

    void Awake()
    {
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        owner = GetComponentInParent<PlayerWeapons>();
    }

    void Start()
    {
        ApplyVisual();   // elle atanmış data varsa görselini uygula
    }

    public WeaponData Data => data;

    // Slota takılırken PlayerWeapons çağırır.
    public void SetData(WeaponData newData, int tier = 1)
    {
        data = newData;
        nextFireTime = 0f;
        ApplyVisual();
        SetTier(tier);
    }

    // Birleşince kademe yükselir: silahın arkasında kademe renginde bir parıltı belirir.
    public void SetTier(int tier)
    {
        Tier = Mathf.Clamp(tier, 1, WeaponTiers.MaxTier);

        if (Tier <= 1)
        {
            if (tierGlow != null) tierGlow.enabled = false;
            return;
        }
        if (sr == null) return;

        if (tierGlow == null)
        {
            var go = new GameObject("TierGlow");
            go.transform.SetParent(sr.transform, false);
            tierGlow = go.AddComponent<SpriteRenderer>();
            tierGlow.sprite = RuntimeSprite.Glow;
            tierGlow.material = VfxSprite.VfxMaterial;   // ışıktan etkilenmesin
            tierGlow.sortingLayerID = sr.sortingLayerID;
            tierGlow.sortingOrder = sr.sortingOrder - 1;
        }
        tierGlow.enabled = true;
        Color c = WeaponTiers.Color(Tier);
        c.a = 0.35f + 0.1f * Tier;
        tierGlow.color = c;
        // Parıltı silah sprite'ından biraz büyük (Glow sprite'ı 1x1 dünya birimi)
        Vector3 size = sr.sprite != null ? sr.sprite.bounds.size : Vector3.one * 0.5f;
        float d = Mathf.Max(size.x, size.y) * (1.3f + 0.15f * Tier);
        tierGlow.transform.localScale = new Vector3(d, d, 1f);
        tierGlow.transform.localPosition = sr.sprite != null ? sr.sprite.bounds.center : Vector3.zero;
    }

    void ApplyVisual()
    {
        if (sr != null && data != null && data.sprite != null)
            sr.sprite = data.sprite;
    }

    void Update()
    {
        if (data == null) return;

        // Mevcut hedef geçersizleştiyse (öldü / yok oldu / menzilden çıktı) yenisini seç
        if (!IsTargetValid())
            target = EnemyRegistry.FindRandomInRange(transform.position, data.range);

        if (target == null) return;   // menzilde düşman yok: son yönünde bekle

        // Salınım sırasında dönüşü coroutine yönetir; AimAt karışmasın
        if (!swinging)
            AimAt(target.transform.position);

        // Kılıç atılırken yeni saldırı başlatma (dürtme bitsin); iki hareket çakışmasın
        if (Time.time >= nextFireTime && !swinging)
        {
            Attack(target.transform.position);
            nextFireTime = Time.time + CurrentCooldown;
        }
    }

    // Silah tipine göre saldırıyı yönlendirir.
    void Attack(Vector2 targetPos)
    {
        if (data.weaponType == WeaponType.Melee)
            MeleeAttack(targetPos);
        else
            Fire(targetPos);
    }

    // Kilitli hedef hâlâ vurulabilir durumda mı?
    bool IsTargetValid()
    {
        if (target == null || target.IsDead) return false;

        float sqr = ((Vector2)target.transform.position - (Vector2)transform.position).sqrMagnitude;
        return sqr <= data.range * data.range;
    }

    void AimAt(Vector2 targetPos)
    {
        Vector2 dir = targetPos - (Vector2)transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // Sola bakarken silah ters durmasın
        if (sr != null) sr.flipY = Mathf.Abs(angle) > 90f;
    }

    void Fire(Vector2 targetPos)
    {
        if (data.projectilePrefab == null) return;

        Transform fp = firePoint != null ? firePoint : transform;
        Vector2 baseDir = targetPos - (Vector2)fp.position;
        if (baseDir.sqrMagnitude < 0.0001f) return;
        baseDir.Normalize();

        AudioManager.PlayShoot();

        // Namlu efekti (varsa): atış yönüne dönük, kendini yok eden bir animasyon
        if (muzzleEffect != null)
        {
            float ma = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
            Instantiate(muzzleEffect, fp.position, Quaternion.Euler(0f, 0f, ma));
        }

        int count = Mathf.Max(1, data.projectileCount);

        // Mermileri yay şeklinde dağıt: tek mermide açı 0, çoklukta -yarı..+yarı
        float step = count > 1 ? data.spreadAngle / (count - 1) : 0f;
        float startAngle = count > 1 ? -data.spreadAngle * 0.5f : 0f;

        for (int i = 0; i < count; i++)
        {
            // Sabit yelpaze açısı + o mermiye özel rastgele sapma
            Vector2 dir = Rotate(baseDir, startAngle + step * i + RandomError());

            // Kritik zarı her mermi için ayrı atılır (shotgun'da bazıları kritik çıkabilir)
            bool isCrit = Random.value < CurrentCritChance;
            SpawnProjectile(fp.position, dir, isCrit);
        }
    }

    // ---- Melee (kılıç) ----
    // Hedefe doğru bir KONİ içindeki tüm düşmanları biçer (menzil + meleeArc).
    // Hedef sadece yön verir; koniye giren herkes hasar alır (cleave).
    void MeleeAttack(Vector2 targetPos)
    {
        Vector2 aim = targetPos - (Vector2)transform.position;
        if (aim.sqrMagnitude < 0.0001f) return;
        AudioManager.Play(SfxId.MeleeSwing, 0.8f);

        float aimAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        StartCoroutine(ThrustRoutine(aim.normalized, aimAngle));   // hedefe doğru dürtme

        float halfArc = data.meleeArc * 0.5f;
        var alive = EnemyRegistry.Alive;

        // Sondan başa: biçilen düşman ölünce kendini EnemyRegistry'den siler
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            EnemyBase e = alive[i];
            if (e == null || e.IsDead) continue;

            Vector2 to = (Vector2)e.transform.position - (Vector2)transform.position;
            if (to.magnitude > data.range) continue;          // menzil dışı
            if (Vector2.Angle(aim, to) > halfArc) continue;   // koni dışı

            bool isCrit = Random.value < CurrentCritChance;
            if (e.TryGetComponent<Health>(out var h))
                h.TakeDamage(CurrentDamage * (isCrit ? data.critMultiplier : 1f), isCrit);
        }
    }

    // Kılıcı hedefe doğru hızlıca ileri atıp geri çeker (Brotato dürtme hissi).
    // Görsel: kılıç hedefe döner, sin eğrisiyle ileri fırlar (tepe = en uzak) ve döner.
    IEnumerator ThrustRoutine(Vector2 aimDir, float aimAngle)
    {
        swinging = true;

        // Dinlenme konumunu bir kez yakala (ilk dürtmede kılıç slotta duruyor)
        if (!restCaptured) { restLocalPos = transform.localPosition; restCaptured = true; }

        // Kılıcı hedefe döndür (dürtme boyunca sabit yönde)
        transform.rotation = Quaternion.Euler(0f, 0f, aimAngle);
        if (sr != null) sr.flipY = Mathf.Abs(aimAngle) > 90f;

        // İleri yönü slotun (parent) yerel eksenine çevir: oyuncu dönse de doğru olsun
        Vector3 localDir = transform.parent != null
            ? transform.parent.InverseTransformDirection(aimDir)
            : (Vector3)aimDir;

        float dur = Mathf.Max(0.02f, data.swingTime);
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            // sin(0..π): 0 -> 1 -> 0, yani ileri fırla sonra geri dön
            float f = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
            transform.localPosition = restLocalPos + localDir * (data.lungeDistance * f);
            yield return null;
        }

        transform.localPosition = restLocalPos;   // tam dinlenme konumuna otur
        swinging = false;
    }

    // Merminin hedeften rastgele sapması (derece).
    // İki rastgele sayının toplamı üçgen dağılım verir: sapmalar çoğunlukla 0'a yakın çıkar,
    // büyük kaçırmalar nadirdir. Tek Random.Range'e göre çok daha doğal durur.
    float RandomError()
    {
        if (data.inaccuracyAngle <= 0f) return 0f;

        float t = Random.value + Random.value - 1f;   // -1..1 arası, 0 civarında yoğun
        return t * data.inaccuracyAngle;
    }

    void SpawnProjectile(Vector3 pos, Vector2 dir, bool isCrit)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        GameObject go = Instantiate(data.projectilePrefab, pos, Quaternion.Euler(0f, 0f, angle));

        if (go.TryGetComponent<Rigidbody2D>(out var prb))
            prb.linearVelocity = dir * data.projectileSpeed;

        // Mermiyi player takımına ata: düşmanlara hasar verir, oyuncuya değil
        if (go.TryGetComponent<Projectile>(out var proj))
        {
            proj.team = Team.Player;
            proj.damage = CurrentDamage * (isCrit ? data.critMultiplier : 1f);
            proj.isCrit = isCrit;
            if (data.pierce) proj.EnablePierce();
        }
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    // Menzili Scene view'da göster
    void OnDrawGizmosSelected()
    {
        if (data == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, data.range);
    }
}
