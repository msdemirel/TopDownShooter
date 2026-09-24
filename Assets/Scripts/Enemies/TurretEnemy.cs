using UnityEngine;

// Sabit taret: doğduğu yerden kıpırdamaz. Oyuncu menzile girince uyanır (Active animasyonu),
// kısa bir hazırlık süresinden sonra oyuncuya doğru seri (burst) ateş eder.
// Oyuncu menzilden çıkınca tekrar uykuya (Idle) döner.
//
// Hareket etmediği için GetMoveVector/separation kullanmaz. Prefab'taki Rigidbody2D Kinematic:
// oyuncu ve diğer düşmanlar onu itemez, ama mermiler (trigger) yine isabet eder.
// Attığı mermi Team.Enemy olduğu için diğer düşmanlara zarar vermez (RangedEnemy ile aynı).
public class TurretEnemy : EnemyBase
{
    [Header("Turret References")]
    [SerializeField] GameObject projectilePrefab;
    [Tooltip("Namlunun taret merkezine göre konumu (SAĞA bakarken). Sola dönünce X otomatik aynalanır.")]
    [SerializeField] Vector2 muzzleOffset = new Vector2(0.3f, 0.1f);

    [Header("Seri Atış")]
    [Tooltip("Bir seride kaç mermi atılır. Seriler arası süre = stats.attackCooldown.")]
    [SerializeField, Min(1)] int shotsPerBurst = 3;
    [SerializeField] float timeBetweenShots = 0.15f;
    [Tooltip("Her mermiye eklenen rastgele sapma (derece). 0 = tam isabet.")]
    [SerializeField] float spreadAngle = 6f;

    [Header("Uyanma")]
    [Tooltip("Oyuncu menzile girdikten sonra ilk seriye kadar beklenen süre — oyuncuya tepki fırsatı.")]
    [SerializeField] float wakeUpDelay = 0.6f;
    [SerializeField] string activeParam = "Active";   // bool: Idle <-> Active

    bool active;
    int shotsLeft;        // devam eden serideki kalan mermi
    float nextBurstTime;
    float nextShotTime;

    void FixedUpdate()
    {
        rb.linearVelocity = Vector2.zero;
        if (IsDead) return;

        bool inRange = HasTarget && DistanceToTarget <= stats.attackRange;
        SetActive(inRange);

        if (!inRange)
        {
            shotsLeft = 0;   // menzilden çıkınca yarım kalan seri iptal
            return;
        }

        FaceTarget();

        // Yeni seri başlat
        if (shotsLeft == 0 && Time.time >= nextBurstTime)
        {
            shotsLeft = shotsPerBurst;
            nextShotTime = Time.time;
            TriggerAttack();
        }

        // Serideki sıradaki mermi
        if (shotsLeft > 0 && Time.time >= nextShotTime)
        {
            Fire();
            shotsLeft--;
            nextShotTime = Time.time + timeBetweenShots;

            if (shotsLeft == 0)
                nextBurstTime = Time.time + stats.attackCooldown;
        }
    }

    void SetActive(bool value)
    {
        if (active == value) return;
        active = value;

        // Uyanınca hemen ateş etmesin: kısa hazırlık süresi
        if (value) nextBurstTime = Mathf.Max(nextBurstTime, Time.time + wakeUpDelay);

        if (animator != null && !string.IsNullOrEmpty(activeParam))
            animator.SetBool(activeParam, value);
    }

    // Sprite sola bakıyorsa namlu da sola geçer
    Vector3 MuzzlePosition
    {
        get
        {
            Vector2 offset = muzzleOffset;
            if (sr != null && sr.flipX) offset.x = -offset.x;
            return transform.position + (Vector3)offset;
        }
    }

    void Fire()
    {
        if (projectilePrefab == null || target == null) return;

        Vector3 from = MuzzlePosition;
        Vector2 aim = (Vector2)(target.position - from);
        if (aim == Vector2.zero) aim = Vector2.right;

        // Sağa bakan mermi sprite'ı varsayılır (Weapon.cs / RangedEnemy ile aynı)
        float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg
                      + Random.Range(-spreadAngle, spreadAngle);
        Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

        GameObject go = Instantiate(projectilePrefab, from, Quaternion.Euler(0f, 0f, angle));

        if (go.TryGetComponent<Rigidbody2D>(out var prb))
            prb.linearVelocity = dir * stats.projectileSpeed;

        if (go.TryGetComponent<Projectile>(out var proj))
        {
            proj.team = Team.Enemy;
            proj.damage = stats.attackDamage;
        }
    }

    // Scene'de: kırmızı = ateş menzili, sarı = namlu
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + (Vector3)muzzleOffset, 0.05f);

        if (stats == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stats.attackRange);
    }
}
