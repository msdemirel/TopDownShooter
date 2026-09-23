using UnityEngine;

// Kamikaze düşman: oyuncunun üstüne koşup temas edince patlar.
// Patlama = ölüm; death (patlama) animasyonu oynar.
// Patlama yalnızca Team.Player birimlere hasar verir (diğer düşmanlara değil).
public class ExploderEnemy : EnemyBase
{
    [Header("Exploder")]
    [SerializeField] GameObject explosionVfx;  // opsiyonel efekt prefab'ı

    [Header("Kamera Sarsıntısı")]
    [Tooltip("Patlama anındaki sarsıntı gücü (0 = kapalı).")]
    [SerializeField] float shakeStrength = 0.25f;
    [SerializeField] float shakeDuration = 0.3f;

    [Tooltip("0 = sadece TEMAS edince patlar. 0'dan büyükse, oyuncu bu mesafeye girince (temastan önce) patlar.")]
    [SerializeField] float triggerDistance = 0f;   // erken patlama fitili (temas zaten patlatır)

    bool exploded;

    // Kendini patlatarak öldüyse ödül yok — loot sadece oyuncu vurup öldürünce düşer.
    // (exploded, Explode()'da ölümden ÖNCE true yapılır; HandleDeath -> ShouldDropLoot bunu okur.)
    protected override bool ShouldDropLoot => !exploded;

    void FixedUpdate()
    {
        if (IsDead) { rb.linearVelocity = Vector2.zero; return; }
        if (!HasTarget || exploded) { rb.linearVelocity = Vector2.zero; SetAnimSpeed(0f); return; }

        // Oyuncuyu kovala
        rb.linearVelocity = GetMoveVector() * MoveSpeed;
        SetAnimSpeed(rb.linearVelocity.magnitude);  // idle/run geçişi

        // Erken fitil: triggerDistance > 0 ise oyuncu o mesafeye girince patla.
        // Asıl "temasta patlama" aşağıdaki çarpışma olaylarında. Hasar alanı ayrı: stats.explosionRadius
        if (triggerDistance > 0f && DistanceToTarget <= triggerDistance)
            Explode();
    }

    // Oyuncuya fiziksel olarak DEĞİNCE patla (collider'lar merkezlerin yaklaşmasını
    // engellediği için mesafeye güvenmiyoruz). Katı çarpışma ve trigger'ın ikisini de yakala.
    void OnCollisionEnter2D(Collision2D col) => ContactExplode(col.collider);
    void OnCollisionStay2D(Collision2D col) => ContactExplode(col.collider);
    void OnTriggerEnter2D(Collider2D other) => ContactExplode(other);
    void OnTriggerStay2D(Collider2D other) => ContactExplode(other);

    void ContactExplode(Collider2D other)
    {
        if (exploded || IsDead) return;

        // Sadece Player'a değince patla (başka düşmana değil).
        var hp = other.GetComponentInParent<Health>();
        if (hp != null && hp.Team == Team.Player)
            Explode();
    }

    void Explode()
    {
        if (exploded) return;
        exploded = true;
        rb.linearVelocity = Vector2.zero;

        // Yarıçaptaki tüm Player birimlerine hasar
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, stats.explosionRadius);
        foreach (var c in hits)
        {
            if (c.TryGetComponent<Health>(out var hp) && hp.Team == Team.Player)
                hp.TakeDamage(stats.attackDamage);
        }

        if (explosionVfx != null)
            Instantiate(explosionVfx, transform.position, Quaternion.identity);

        if (shakeStrength > 0f)
            CameraShake.Shake(shakeStrength, shakeDuration);

        // Kendini öldür -> Health.OnDeath -> HandleDeath: death (patlama) animasyonu + loot.
        // Kill: hasar SAYILMAZ — patlayanın üstünde hasar yazısı/can barı çıkmasın.
        health.Kill();
    }

    // Scene'de: sarı = patlama tetiği (ne zaman patlar), kırmızı = hasar yarıçapı
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerDistance);

        if (stats == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stats.explosionRadius);
    }
}
