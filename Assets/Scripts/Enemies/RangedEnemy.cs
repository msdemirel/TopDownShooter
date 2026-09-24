using UnityEngine;

// Uzaktan saldıran düşman: oyuncuyu kovalar, menziline girince durup ateş eder.
// Attığı mermi Team.Enemy olduğu için diğer düşmanlara zarar vermez.
public class RangedEnemy : EnemyBase
{
    [Header("Ranged References")]
    [SerializeField] GameObject projectilePrefab;
    [SerializeField] Transform firePoint;  // boşsa kendi pozisyonundan atar

    [Header("Yelpaze Atış (opsiyonel)")]
    [Tooltip("Bir atışta kaç mermi. 1 = tek mermi (klasik ranged).")]
    [SerializeField, Min(1)] int projectilesPerShot = 1;
    [Tooltip("Mermilerin yayıldığı toplam açı (derece). Sadece projectilesPerShot > 1 iken kullanılır.")]
    [SerializeField] float spreadAngle = 0f;

    float nextShotTime;

    void FixedUpdate()
    {
        if (IsDead) { rb.linearVelocity = Vector2.zero; return; }
        if (!HasTarget) { rb.linearVelocity = Vector2.zero; SetAnimSpeed(0f); return; }

        if (DistanceToTarget <= stats.attackRange)
        {
            // Menzilde: dur ve ateş et
            rb.linearVelocity = Vector2.zero;
            FaceTarget();
            if (Time.time >= nextShotTime)
            {
                Shoot();
                nextShotTime = Time.time + stats.attackCooldown;
            }
        }
        else
        {
            // Oyuncuyu kovala
            rb.linearVelocity = GetMoveVector() * MoveSpeed;
        }

        SetAnimSpeed(rb.linearVelocity.magnitude);  // idle/run geçişi
    }

    void Shoot()
    {
        TriggerAttack();

        if (projectilePrefab == null) return;

        Vector2 dir = target != null
            ? ((Vector2)(target.position - transform.position)).normalized
            : Vector2.right;
        Transform fp = firePoint != null ? firePoint : transform;
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // Tek mermide sapma yok; birden fazlaysa spreadAngle'a eşit aralıklarla yayılır
        int n = Mathf.Max(1, projectilesPerShot);
        for (int i = 0; i < n; i++)
        {
            float offset = n == 1 ? 0f : Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, i / (float)(n - 1));
            SpawnProjectile(fp.position, baseAngle + offset);
        }
    }

    void SpawnProjectile(Vector3 pos, float angle)
    {
        // Sprite gittiği yöne baksın (sağa bakan sprite varsayılır, Weapon.cs ile aynı)
        Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        GameObject go = Instantiate(projectilePrefab, pos, Quaternion.Euler(0f, 0f, angle));

        if (go.TryGetComponent<Rigidbody2D>(out var prb))
            prb.linearVelocity = dir * stats.projectileSpeed;

        if (go.TryGetComponent<Projectile>(out var proj))
        {
            proj.team = Team.Enemy;
            proj.damage = stats.attackDamage;
        }
    }
}
