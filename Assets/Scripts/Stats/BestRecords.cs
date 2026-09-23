using UnityEngine;

// Oyuncunun en iyi skorları (rekorlar). PlayerPrefs'te saklanır: oyun kapansa da kalır.
// Her istatistik AYRI rekordur (en uzun süre, en yüksek dalga, en çok öldürme...).
// Submit, rekorları günceller ve hangilerinin yeni rekor olduğunu döndürür.
public static class BestRecords
{
    // Anahtarlar sabit kalmalı: değiştirilirse eski rekorlar kaybolur.
    const string KeyTime = "best_time";
    const string KeyWave = "best_wave";
    const string KeyKills = "best_kills";
    const string KeyLevel = "best_level";
    const string KeyCoins = "best_coins";
    const string KeyDamage = "best_damage";
    const string KeyRuns = "total_runs";

    // Hangi istatistik yeni rekor oldu?
    public struct NewBestFlags
    {
        public bool time, wave, kills, level, coins, damage;
        public bool Any => time || wave || kills || level || coins || damage;
    }

    // Mevcut rekorlar (hiç oynanmadıysa hepsi 0)
    public static RunResult Load() => new RunResult
    {
        timeSurvived = PlayerPrefs.GetFloat(KeyTime, 0f),
        wave = PlayerPrefs.GetInt(KeyWave, 0),
        kills = PlayerPrefs.GetInt(KeyKills, 0),
        level = PlayerPrefs.GetInt(KeyLevel, 0),
        coins = PlayerPrefs.GetInt(KeyCoins, 0),
        damage = PlayerPrefs.GetFloat(KeyDamage, 0f),
    };

    public static int TotalRuns => PlayerPrefs.GetInt(KeyRuns, 0);

    // Oyun sonucunu kaydeder. 'previous' = bu oyundan ÖNCEKİ rekorlar (ekranda "Best" olarak
    // gösterilir, böylece yeni rekor kırıldıysa eski değer de görülebilir).
    public static NewBestFlags Submit(RunResult run, out RunResult previous)
    {
        previous = Load();
        var flags = new NewBestFlags
        {
            // İlk oyun (rekor 0) sıfırdan büyük her değerde rekor sayılır
            time = run.timeSurvived > previous.timeSurvived,
            wave = run.wave > previous.wave,
            kills = run.kills > previous.kills,
            level = run.level > previous.level,
            coins = run.coins > previous.coins,
            damage = run.damage > previous.damage,
        };

        if (flags.time) PlayerPrefs.SetFloat(KeyTime, run.timeSurvived);
        if (flags.wave) PlayerPrefs.SetInt(KeyWave, run.wave);
        if (flags.kills) PlayerPrefs.SetInt(KeyKills, run.kills);
        if (flags.level) PlayerPrefs.SetInt(KeyLevel, run.level);
        if (flags.coins) PlayerPrefs.SetInt(KeyCoins, run.coins);
        if (flags.damage) PlayerPrefs.SetFloat(KeyDamage, run.damage);
        PlayerPrefs.SetInt(KeyRuns, TotalRuns + 1);
        PlayerPrefs.Save();   // çökme/kapanmada kaybolmasın

        return flags;
    }

#if UNITY_EDITOR
    // Test için: Menü > TopDownShooter > Rekorları Sıfırla
    [UnityEditor.MenuItem("TopDownShooter/Rekorları Sıfırla")]
    static void ResetAll()
    {
        foreach (var k in new[] { KeyTime, KeyWave, KeyKills, KeyLevel, KeyCoins, KeyDamage, KeyRuns })
            PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
        Debug.Log("[BestRecords] Rekorlar sıfırlandı.");
    }
#endif
}
