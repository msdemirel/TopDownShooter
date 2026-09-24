using System.Collections.Generic;
using UnityEngine;

// Çağırıcı: oyuncuyla arasında mesafe tutar (attackRange = tercih ettiği mesafe),
// her attackCooldown saniyede bir etrafına minion doğurur.
// Minion'lar doğum anı Attack animasyonundaki "OnSummon" event'i ile ayarlanır;
// Animator yoksa saldırı başlar başlamaz doğarlar.
public class SummonerEnemy : EnemyBase
{
    [Header("Çağırma")]
    [SerializeField] EnemyTypeData minionType;
    [SerializeField, Min(1)] int minionLevel = 1;
    [SerializeField, Min(1)] int minionsPerSummon = 3;
    [Tooltip("Bu çağırıcıya ait aynı anda yaşayan en fazla minion sayısı.")]
    [SerializeField, Min(1)] int maxAliveMinions = 6;
    [SerializeField] float spawnRadius = 0.8f;
    [Tooltip("Sahneye girdikten sonra ilk çağırmaya kadar beklenen süre.")]
    [SerializeField] float firstSummonDelay = 2f;

    [Header("Mesafe")]
    [Tooltip("Oyuncu attackRange * bu orandan yakınsa geri çekilir.")]
    [SerializeField, Range(0.2f, 0.95f)] float retreatRatio = 0.7f;

    readonly List<EnemyBase> minions = new List<EnemyBase>();
    float nextSummonTime;
    bool summonPending;

    protected override void Start()
    {
        base.Start();
        nextSummonTime = Time.time + firstSummonDelay;
    }

    void FixedUpdate()
    {
        if (IsDead) { rb.linearVelocity = Vector2.zero; return; }
        if (!HasTarget) { rb.linearVelocity = Vector2.zero; SetAnimSpeed(0f); return; }

        float d = DistanceToTarget;
        if (d > stats.attackRange)
        {
            rb.linearVelocity = GetMoveVector() * MoveSpeed;           // yaklaş
        }
        else if (d < stats.attackRange * retreatRatio)
        {
            // Geri çekil: kovalama yönünün tersi (separation dahil)
            rb.linearVelocity = -GetMoveVector() * MoveSpeed;
            FaceTarget();
        }
        else
        {
            rb.linearVelocity = Vector2.zero;                            // ideal mesafe
            FaceTarget();
        }

        SetAnimSpeed(rb.linearVelocity.magnitude);

        if (!summonPending && Time.time >= nextSummonTime && CountAliveMinions() < maxAliveMinions)
        {
            nextSummonTime = Time.time + stats.attackCooldown;
            TriggerAttack();
            if (animator != null) summonPending = true;   // doğum OnSummon event'inde
            else SpawnMinions();
        }
    }

    // Attack animasyonundaki Animation Event çağırır.
    public void OnSummon()
    {
        if (!summonPending || IsDead) return;
        summonPending = false;
        SpawnMinions();
    }

    int CountAliveMinions()
    {
        minions.RemoveAll(m => m == null || m.IsDead);
        return minions.Count;
    }

    void SpawnMinions()
    {
        if (minionType == null || minionType.prefab == null) return;

        int room = maxAliveMinions - CountAliveMinions();
        int count = Mathf.Min(minionsPerSummon, room);
        EnemyLevelStats minionStats = minionType.GetLevel(minionLevel);

        for (int i = 0; i < count; i++)
        {
            // Etrafına eşit aralıklı halka
            float ang = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            Vector3 pos = transform.position + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * spawnRadius;

            GameObject go = Instantiate(minionType.prefab, pos, Quaternion.identity);
            if (go.TryGetComponent<EnemyBase>(out var e))
            {
                e.Init(minionStats);
                minions.Add(e);
            }
        }
    }

    protected override void HandleDeath(Health h)
    {
        summonPending = false;
        base.HandleDeath(h);
    }
}
