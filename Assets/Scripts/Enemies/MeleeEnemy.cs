using UnityEngine;

// Yakın dövüş düşmanı: oyuncuyu kovalar, menziline girince durup vurur.
public class MeleeEnemy : EnemyBase
{
    float nextAttackTime;

    void FixedUpdate()
    {
        if (IsDead) { rb.linearVelocity = Vector2.zero; return; }
        if (!HasTarget) { rb.linearVelocity = Vector2.zero; SetAnimSpeed(0f); return; }

        if (DistanceToTarget <= stats.attackRange)
        {
            // Menzilde: dur ve vur
            rb.linearVelocity = Vector2.zero;
            FaceTarget();
            if (Time.time >= nextAttackTime)
            {
                Attack();
                nextAttackTime = Time.time + stats.attackCooldown;
            }
        }
        else
        {
            // Oyuncuyu kovala
            rb.linearVelocity = GetMoveVector() * MoveSpeed;
        }

        SetAnimSpeed(rb.linearVelocity.magnitude);  // idle/run geçişi
    }

    void Attack()
    {
        TriggerAttack();

        if (target.TryGetComponent<Health>(out var hp))
            hp.TakeDamage(stats.attackDamage);
    }
}
