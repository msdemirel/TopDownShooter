using UnityEngine;

// Seçili zorluk (oyunlar arası hatırlanır) + zorluk bazlı en iyi dalga + düşman ölçekleme.
// Ana menüdeki zorluk ekranı Select çağırır; MainGame'de EnemyBase.Init Scale ile değerleri
// çarpar, MetaProgress Core ödülünü CoreMultiplier ile çarpar.
public static class Difficulty
{
    const string KeySelected = "difficulty_selected";
    const string KeyBestWavePrefix = "difficulty_best_wave_";

    static DifficultyData current;

    // Seçili zorluk. Katalog yoksa (kurulum yapılmadı) null: her şey Normal gibi davranır.
    public static DifficultyData Current
    {
        get
        {
            if (current != null) return current;
            var list = MetaProgress.Catalog != null ? MetaProgress.Catalog.difficulties : null;
            if (list == null || list.Count == 0) return null;

            string id = PlayerPrefs.GetString(KeySelected, "");
            foreach (var d in list)
                if (d != null && d.id == id && d.IsUnlocked) return current = d;
            return current = list[0];   // seçilmemiş ya da artık kilitli: ilk (Normal)
        }
    }

    public static float CoreMultiplier => Current != null ? Current.coreMultiplier : 1f;

    public static void Select(DifficultyData d)
    {
        if (d == null || !d.IsUnlocked) return;
        current = d;
        PlayerPrefs.SetString(KeySelected, d.id);
        PlayerPrefs.Save();
    }

    // Bu zorlukta ulaşılan en yüksek dalga. orHarder: daha zor seviyelerdeki rekorlar da sayılır
    // (Nightmare'de 10'a gelen, Hard'ın 10 şartını da sağlamış olur).
    public static int BestWave(DifficultyData d, bool orHarder = false)
    {
        if (d == null) return 0;
        int best = PlayerPrefs.GetInt(KeyBestWavePrefix + d.id, 0);
        if (!orHarder || MetaProgress.Catalog == null) return best;

        var list = MetaProgress.Catalog.difficulties;
        int idx = list.IndexOf(d);
        for (int i = idx + 1; idx >= 0 && i < list.Count; i++)
            if (list[i] != null) best = Mathf.Max(best, PlayerPrefs.GetInt(KeyBestWavePrefix + list[i].id, 0));
        return best;
    }

    // Oyun sonunda çağrılır (MetaProgress.EndRun).
    public static void RecordWave(int wave)
    {
        var d = Current;
        if (d == null) return;
        string key = KeyBestWavePrefix + d.id;
        if (wave > PlayerPrefs.GetInt(key, 0)) PlayerPrefs.SetInt(key, wave);
    }

    // Düşman değerlerinin zorlukla çarpılmış KOPYASI. Asset'teki değerler değişmez
    // (EnemyLevelStats bir asset'in parçası — üstüne yazsaydık Play bitince de kalırdı).
    public static EnemyLevelStats Scale(EnemyLevelStats s)
    {
        var d = Current;
        if (s == null || d == null) return s;
        if (Mathf.Approximately(d.enemyHealth, 1f) && Mathf.Approximately(d.enemyDamage, 1f)
            && Mathf.Approximately(d.enemySpeed, 1f)) return s;

        return new EnemyLevelStats
        {
            maxHealth = s.maxHealth * d.enemyHealth,
            moveSpeed = s.moveSpeed * d.enemySpeed,
            attackDamage = s.attackDamage * d.enemyDamage,
            attackRange = s.attackRange,
            attackCooldown = s.attackCooldown,
            projectileSpeed = s.projectileSpeed,
            explosionRadius = s.explosionRadius,
            lootRolls = s.lootRolls,
        };
    }

    public static void ResetForTesting()
    {
        PlayerPrefs.DeleteKey(KeySelected);
        if (MetaProgress.Catalog != null)
            foreach (var d in MetaProgress.Catalog.difficulties)
                if (d != null) PlayerPrefs.DeleteKey(KeyBestWavePrefix + d.id);
        current = null;
    }
}
