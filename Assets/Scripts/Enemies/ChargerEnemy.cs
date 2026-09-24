using UnityEngine;

// Hücumcu: oyuncuyu kovalar, attackRange'e girince durur, titreyerek hazırlanır (telegraph),
// sonra kilitlediği yöne düz bir çizgide hızla atılır. Atılış sırasında oyuncuya değerse
// bir kez hasar verir. Atılıştan sonra kısa bir süre sersem kalır — oyuncunun karşı saldırı fırsatı.
//
// Hazırlık + atılış görseli tek Attack animasyonunda: ilk yarısı titreme, ikinci yarısı atılış.
// windupTime + dashDuration ≈ Attack klibinin süresi olmalı.
public class ChargerEnemy : EnemyBase
{
    enum State { Chase, Windup, Dash, Recover }

    [Header("Hücum")]
    [Tooltip("Hazırlık süresi: oyuncunun kaçması için uyarı.")]
    [SerializeField] float windupTime = 0.5f;
    [SerializeField] float dashSpeed = 10f;
    [SerializeField] float dashDuration = 0.45f;
    [Tooltip("Atılıştan sonra hareketsiz kaldığı süre.")]
    [SerializeField] float recoverTime = 0.6f;
    [Tooltip("Atılış sırasında bu yarıçaptaki oyuncuya hasar verir.")]
    [SerializeField] float hitRadius = 0.45f;

    [Header("Kamera Sarsıntısı")]
    [SerializeField] float hitShakeStrength = 0.15f;
    [SerializeField] float hitShakeDuration = 0.15f;

    State state = State.Chase;
    float stateEndTime;
    float nextChargeTime;
    Vector2 dashDir;
    bool hitThisDash;

    void FixedUpdate()
    {
        if (IsDead) { rb.linearVelocity = Vector2.zero; return; }
        if (!HasTarget) { rb.linearVelocity = Vector2.zero; SetAnimSpeed(0f); return; }

        switch (state)
        {
            case State.Chase:
                if (DistanceToTarget <= stats.attackRange && Time.time >= nextChargeTime)
                    BeginWindup();
                else
                    rb.linearVelocity = GetMoveVector() * MoveSpeed;
                break;

            case State.Windup:
                rb.linearVelocity = Vector2.zero;
                FaceTarget();
                if (Time.time >= stateEndTime) BeginDash();
                break;

            case State.Dash:
                rb.linearVelocity = dashDir * dashSpeed;
                TryHitPlayer();
                if (Time.time >= stateEndTime) Enter(State.Recover, recoverTime);
                break;

            case State.Recover:
                rb.linearVelocity = Vector2.zero;
                if (Time.time >= stateEndTime)
                {
                    nextChargeTime = Time.time + stats.attackCooldown;
                    state = State.Chase;
                }
                break;
        }

        // Atılış hızı Run animasyonunu tetiklemesin: Attack klibi o sırada oynuyor
        SetAnimSpeed(state == State.Chase ? rb.linearVelocity.magnitude : 0f);
    }

    void Enter(State s, float duration)
    {
        state = s;
        stateEndTime = Time.time + duration;
    }

    void BeginWindup()
    {
        Enter(State.Windup, windupTime);
        TriggerAttack();
    }

    void BeginDash()
    {
        // Yön hazırlık bitince kilitlenir: oyuncu son anda yana kaçarsa ıskalar
        dashDir = ((Vector2)(target.position - transform.position)).normalized;
        if (dashDir == Vector2.zero) dashDir = Vector2.right;
        FaceDir(dashDir);
        hitThisDash = false;
        Enter(State.Dash, dashDuration);
    }

    void TryHitPlayer()
    {
        if (hitThisDash) return;

        // Temas: oyuncu merkezine olan mesafe + yarıçap (collider'lar zaten itişir)
        if (DistanceToTarget > hitRadius + 0.35f) return;

        hitThisDash = true;
        if (target.TryGetComponent<Health>(out var hp))
            hp.TakeDamage(stats.attackDamage);

        if (hitShakeStrength > 0f)
            CameraShake.Shake(hitShakeStrength, hitShakeDuration);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hitRadius);
        if (stats == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stats.attackRange);
    }
}
