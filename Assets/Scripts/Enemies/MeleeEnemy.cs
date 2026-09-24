using UnityEngine;

// Yakın dövüş düşmanı: oyuncuyu kovalar, menziline girince durup vurur.
//
// Hasar zamanlaması: Attack animasyonundaki "OnAttackHit" event'i vuruş karesinde hasarı verir.
// Böylece hasar, kıskaç/yumruk görsel olarak değdiği anda düşer; oyuncu o ana kadar
// menzilden çıkarsa (kaçarsa) hasar almaz. Animator yoksa veya event kullanılmıyorsa
// hasar saldırı başlar başlamaz verilir (eski davranış).
public class MeleeEnemy : EnemyBase
{
    [Header("Melee Vuruş")]
    [Tooltip("Açıksa hasar Attack animasyonundaki OnAttackHit event'inde verilir. " +
             "Kapalıysa (veya Animator yoksa) saldırı başladığı anda verilir.")]
    [SerializeField] bool damageOnAnimationEvent = true;
    [Tooltip("Vuruş anında oyuncu attackRange + bu pay içindeyse hasar alır. " +
             "Saldırı başladıktan sonra biraz uzaklaşan oyuncuya yine de isabet etsin diye.")]
    [SerializeField] float hitRangeBonus = 0.3f;

    float nextAttackTime;

    bool UsesEvent => damageOnAnimationEvent && animator != null;

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
        if (!UsesEvent) DealHit();
    }

    // Attack animasyonunun vuruş karesindeki Animation Event çağırır.
    // Animator ile aynı GameObject'te olmalı (prefab'ta kök obje).
    public void OnAttackHit()
    {
        if (!UsesEvent || IsDead || !HasTarget) return;
        if (DistanceToTarget > stats.attackRange + hitRangeBonus) return;  // oyuncu kaçtı
        DealHit();
    }

    void DealHit()
    {
        if (target.TryGetComponent<Health>(out var hp))
            hp.TakeDamage(stats.attackDamage);
    }
}
