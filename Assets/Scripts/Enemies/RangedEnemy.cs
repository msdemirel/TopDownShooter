using UnityEngine;

// Uzaktan saldıran düşman: oyuncuyu kovalar, menziline girince durup ateş eder.
// Attığı mermi Team.Enemy olduğu için diğer düşmanlara zarar vermez.
public class RangedEnemy : EnemyBase
{
    [Header("Ranged References")]
    [SerializeField] GameObject projectilePrefab;
    [SerializeField] Transform firePoint;  // boşsa kendi pozisyonundan atar

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
            rb.linearVelocity = GetMoveVector() * stats.moveSpeed;
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

        GameObject go = Instantiate(projectilePrefab, fp.position, Quaternion.identity);

        if (go.TryGetComponent<Rigidbody2D>(out var prb))
            prb.linearVelocity = dir * stats.projectileSpeed;

        if (go.TryGetComponent<Projectile>(out var proj))
        {
            proj.team = Team.Enemy;
            proj.damage = stats.attackDamage;
        }
    }
}
