using UnityEngine;

// Bir oyunun (run) istatistiklerini tutar: süre, dalga, öldürme, level, coin, hasar.
// Game Over ekranı ölüm anında Snapshot() ile bunları okur ve BestRecords'a yollar.
// Sahnede bir tane olur; GameOverUI yoksa kendisi ekler.
public class RunStats : MonoBehaviour
{
    PlayerStats playerStats;
    Health playerHealth;
    WaveManager waves;

    int kills;
    int crits;
    int bossKills;
    int coinsCollected;
    float damageDealt;
    float deathTime = -1f;   // ölünce süre donsun (Game Over açıkken artmasın)
    int lastMoney;

    public int Kills => kills;
    public int CoinsCollected => coinsCollected;
    public float DamageDealt => damageDealt;

    // Scaled zaman: upgrade paneli / pause sırasında geçen süre SAYILMAZ.
    public float TimeSurvived => (deathTime >= 0f ? deathTime : Time.timeSinceLevelLoad) + timeOffset;
    float timeOffset;   // kayıttan devam edilince önceki oturumun süresi

    // Kayıttan devam (RunSave). Para geri yüklendikten SONRA çağrılmalı: o artış "toplanan" sayılmasın.
    public void RestoreState(RunResult r)
    {
        kills = r.kills;
        coinsCollected = r.coins;
        damageDealt = r.damage;
        crits = r.crits;
        bossKills = r.bossKills;
        timeOffset = r.timeSurvived - Time.timeSinceLevelLoad;
        if (playerStats != null) lastMoney = playerStats.Money;
    }

    public int WaveReached => waves != null ? Mathf.Max(1, waves.CurrentWave) : 1;
    public int Level => playerStats != null ? playerStats.Level : 1;

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            playerStats = p.GetComponent<PlayerStats>();
            playerHealth = p.GetComponent<Health>();
        }
        waves = FindAnyObjectByType<WaveManager>();

        if (playerStats != null)
        {
            lastMoney = playerStats.Money;
            playerStats.OnMoneyChanged += HandleMoneyChanged;
        }
        if (playerHealth != null) playerHealth.OnDeath += HandlePlayerDeath;

        EnemyBase.AnyKilled += HandleKill;
        Health.AnyDamaged += HandleDamage;
    }

    void OnDestroy()
    {
        if (playerStats != null) playerStats.OnMoneyChanged -= HandleMoneyChanged;
        if (playerHealth != null) playerHealth.OnDeath -= HandlePlayerDeath;
        EnemyBase.AnyKilled -= HandleKill;
        Health.AnyDamaged -= HandleDamage;
    }

    void HandleKill(EnemyBase e)
    {
        kills++;
        if (e != null && e.IsBoss) bossKills++;
    }

    // Sadece düşmanlara verilen hasar (oyuncunun aldığı değil)
    void HandleDamage(Health h, float amount, bool isCrit)
    {
        if (h == null || h.Team != Team.Enemy) return;
        damageDealt += amount;
        if (isCrit) crits++;
    }

    // OnMoneyChanged toplam parayı verir; sadece ARTIŞLAR toplanır (harcama düşmez).
    void HandleMoneyChanged(int money)
    {
        if (money > lastMoney) coinsCollected += money - lastMoney;
        lastMoney = money;
    }

    void HandlePlayerDeath(Health h)
    {
        if (deathTime < 0f) deathTime = Time.timeSinceLevelLoad;
    }

    public RunResult Snapshot() => new RunResult
    {
        timeSurvived = TimeSurvived,
        wave = WaveReached,
        kills = kills,
        level = Level,
        coins = coinsCollected,
        damage = damageDealt,
        crits = crits,
        bossKills = bossKills,
    };
}

// Bir oyunun sonuç özeti (Game Over ekranı + rekor karşılaştırması için).
[System.Serializable]
public struct RunResult
{
    public float timeSurvived;
    public int wave;
    public int kills;
    public int level;
    public int coins;
    public float damage;
    public int crits;       // kalıcı ilerleme (Revolver kilidi) için
    public int bossKills;   // Core ödülü + Sniper kilidi için
}
