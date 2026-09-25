using System.Collections.Generic;
using UnityEngine;

// Kalıcı (oyunlar arası) ilerleme: Core bakiyesi, mağaza upgrade seviyeleri, ömür boyu
// istatistikler ve bunlardan hesaplanan içerik kilitleri. PlayerPrefs'te saklanır
// (BestRecords kalıbı). "Tek oyunda" koşulları BestRecords'un rekorlarından okunur.
//
// Oyun sonu sırası (GameOverUI):
//   var locked = MetaProgress.CurrentlyLocked();   // rekorlar güncellenmeden ÖNCE
//   BestRecords.Submit(run, ...);
//   MetaProgress.EndRun(run, locked);              // Core + toplamlar + yeni açılanlar
public static class MetaProgress
{
    // Anahtarlar sabit kalmalı: değiştirilirse kayıtlar kaybolur.
    const string KeyCore = "meta_core";
    const string KeyCoreEarned = "meta_core_earned";
    const string KeyTotalKills = "meta_total_kills";
    const string KeyTotalCrits = "meta_total_crits";
    const string KeyBossKills = "meta_boss_kills";
    const string KeyLevelPrefix = "meta_lvl_";

    // ---- Core formülü: 5 x geçilen dalga + öldürme / 8 + boss başına 15 ----
    public const int CorePerWave = 5;
    public const int KillsPerCore = 8;
    public const int CorePerBoss = 15;

    public static int Core => PlayerPrefs.GetInt(KeyCore, 0);
    public static int CoreEarned => PlayerPrefs.GetInt(KeyCoreEarned, 0);
    public static int TotalKills => PlayerPrefs.GetInt(KeyTotalKills, 0);
    public static int TotalCrits => PlayerPrefs.GetInt(KeyTotalCrits, 0);
    public static int BossKills => PlayerPrefs.GetInt(KeyBossKills, 0);

    // Son oyunun raporu (Game Over ekranı gösterir). Oyun bitmeden null.
    public static RunReport LastReport { get; private set; }

    static MetaCatalog catalog;
    // Resources/MetaCatalog: mağaza upgrade'leri + kilitli içerik listesi (sahne bağlantısı gerekmez)
    public static MetaCatalog Catalog => catalog != null ? catalog : catalog = Resources.Load<MetaCatalog>("MetaCatalog");

    public class RunReport
    {
        public int coreFromWaves, coreFromKills, coreFromBosses;
        public int coreFromDifficulty;          // zorluk çarpanının getirdiği ek (Hard x1.5 -> +%50)
        public DifficultyData difficulty;
        public int CoreBase => coreFromWaves + coreFromKills + coreFromBosses;
        public int CoreTotal => CoreBase + coreFromDifficulty;
        public readonly List<UpgradeData> newlyUnlocked = new List<UpgradeData>();
        public readonly List<CharacterData> newCharacters = new List<CharacterData>();
    }

    public static RunReport CalculateReward(RunResult run)
    {
        var r = new RunReport
        {
            coreFromWaves = CorePerWave * Mathf.Max(0, run.wave - 1),
            coreFromKills = run.kills / KillsPerCore,
            coreFromBosses = CorePerBoss * run.bossKills,
            difficulty = Difficulty.Current,
        };
        r.coreFromDifficulty = Mathf.RoundToInt(r.CoreBase * (Difficulty.CoreMultiplier - 1f));
        return r;
    }

    // ---- Oyun sonu ----
    public static List<UpgradeData> CurrentlyLocked()
    {
        var list = new List<UpgradeData>();
        if (Catalog == null) return list;
        foreach (var u in Catalog.lockables)
            if (u != null && !IsMet(u.unlockCondition, u.unlockThreshold)) list.Add(u);
        return list;
    }

    public static List<CharacterData> CurrentlyLockedCharacters()
    {
        var list = new List<CharacterData>();
        if (Catalog == null) return list;
        foreach (var c in Catalog.characters)
            if (c != null && !c.IsUnlocked) list.Add(c);
        return list;
    }

    // Core'u ve ömür boyu toplamları kaydeder, bu oyunla açılan içerikleri bulur.
    // BestRecords.Submit'ten SONRA çağrılmalı (rekor tabanlı koşullar güncel olsun).
    public static RunReport EndRun(RunResult run, List<UpgradeData> lockedBefore,
                                   List<CharacterData> charactersLockedBefore = null)
    {
        RunReport report = CalculateReward(run);

        AddInt(KeyTotalKills, run.kills);
        AddInt(KeyTotalCrits, run.crits);
        AddInt(KeyBossKills, run.bossKills);
        AddInt(KeyCore, report.CoreTotal);
        AddInt(KeyCoreEarned, report.CoreTotal);
        Difficulty.RecordWave(run.wave);

        if (lockedBefore != null)
            foreach (var u in lockedBefore)
                if (u != null && IsMet(u.unlockCondition, u.unlockThreshold)) report.newlyUnlocked.Add(u);
        if (charactersLockedBefore != null)
            foreach (var c in charactersLockedBefore)
                if (c != null && c.IsUnlocked) report.newCharacters.Add(c);

        PlayerPrefs.Save();
        LastReport = report;
        return report;
    }

    // ---- Mağaza ----
    public static int GetLevel(string id) => string.IsNullOrEmpty(id) ? 0 : PlayerPrefs.GetInt(KeyLevelPrefix + id, 0);

    // Parası yetiyorsa bir seviye alır.
    public static bool TryBuy(MetaUpgradeData u)
    {
        if (u == null) return false;
        int cost = u.NextCost;
        if (cost < 0 || Core < cost) return false;

        PlayerPrefs.SetInt(KeyCore, Core - cost);
        PlayerPrefs.SetInt(KeyLevelPrefix + u.id, u.Level + 1);
        PlayerPrefs.Save();
        return true;
    }

    // ---- Kilit koşulları ----
    public static float GetProgress(UnlockCondition c)
    {
        RunResult best = BestRecords.Load();
        switch (c)
        {
            case UnlockCondition.ReachWave: return best.wave;
            case UnlockCondition.TotalKills: return TotalKills;
            case UnlockCondition.TotalCrits: return TotalCrits;
            case UnlockCondition.RunsPlayed: return BestRecords.TotalRuns;
            case UnlockCondition.BossKills: return BossKills;
            case UnlockCondition.SurviveSeconds: return best.timeSurvived;
            case UnlockCondition.RunKills: return best.kills;
            case UnlockCondition.ReachLevel: return best.level;
            case UnlockCondition.CoreEarned: return CoreEarned;
            default: return 0f;
        }
    }

    public static bool IsMet(UnlockCondition c, float threshold)
        => c == UnlockCondition.None || GetProgress(c) >= threshold;

    // Kilit ekranında görünen metin (oyun içi metinler İngilizce).
    public static string Describe(UnlockCondition c, float t)
    {
        int n = Mathf.RoundToInt(t);
        switch (c)
        {
            case UnlockCondition.ReachWave: return $"Reach wave {n}";
            case UnlockCondition.TotalKills: return $"Kill {n:N0} enemies in total";
            case UnlockCondition.TotalCrits: return $"Land {n:N0} critical hits";
            case UnlockCondition.RunsPlayed: return $"Play {n} runs";
            case UnlockCondition.BossKills: return n <= 1 ? "Defeat the Big Boss" : $"Defeat {n} bosses";
            case UnlockCondition.SurviveSeconds: return $"Survive {Mathf.RoundToInt(t / 60f)} minutes in one run";
            case UnlockCondition.RunKills: return $"Kill {n} enemies in one run";
            case UnlockCondition.ReachLevel: return $"Reach level {n} in one run";
            case UnlockCondition.CoreEarned: return $"Earn {n:N0} Core in total";
            default: return "";
        }
    }

    // "412/1000" gibi ilerleme metni (süre koşulunda dakika:saniye).
    public static string ProgressText(UnlockCondition c, float t)
    {
        float cur = Mathf.Min(GetProgress(c), t);
        if (c == UnlockCondition.SurviveSeconds)
            return $"{(int)cur / 60}:{(int)cur % 60:00} / {(int)t / 60}:{(int)t % 60:00}";
        return $"{Mathf.FloorToInt(cur):N0}/{Mathf.RoundToInt(t):N0}";
    }

    // ---- Test ----
    public static void AddCoreForTesting(int amount) { AddInt(KeyCore, amount); PlayerPrefs.Save(); }

    // Tüm meta ilerlemeyi siler (rekorlara dokunmaz).
    public static void ResetAll()
    {
        foreach (var k in new[] { KeyCore, KeyCoreEarned, KeyTotalKills, KeyTotalCrits, KeyBossKills })
            PlayerPrefs.DeleteKey(k);
        if (Catalog != null)
            foreach (var u in Catalog.upgrades)
                if (u != null) PlayerPrefs.DeleteKey(KeyLevelPrefix + u.id);
        Difficulty.ResetForTesting();
        CharacterSelection.ResetForTesting();
        PlayerPrefs.Save();
        LastReport = null;
    }

    static void AddInt(string key, int amount)
    {
        if (amount != 0) PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + amount);
    }
}

// Bir içeriğin kalıcı kilidini açan koşul. Yeniler SONA eklenir (asset'ler sayı olarak saklar).
public enum UnlockCondition
{
    None,           // baştan açık
    ReachWave,      // en yüksek dalga (rekor)
    TotalKills,     // tüm oyunlarda toplam öldürme
    TotalCrits,     // tüm oyunlarda toplam kritik vuruş
    RunsPlayed,     // oynanan oyun sayısı
    BossKills,      // toplam öldürülen boss
    SurviveSeconds, // tek oyunda hayatta kalma süresi (saniye)
    RunKills,       // tek oyunda öldürme
    ReachLevel,     // tek oyunda level
    CoreEarned,     // toplam kazanılan Core (harcanan dahil)
}
