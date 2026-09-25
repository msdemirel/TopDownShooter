using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Yarım kalan oyunu kaydeder ve kaldığı yerden devam ettirir (Save & Quit / Continue).
//
// Ne zaman kaydedilir: pause menüsünden Main Menu / Quit, ya da pencere kapanırken.
// Ne kaydedilir: oyuncunun tüm durumu (level, exp, para, can, silahlar + kademe, skill'ler +
// seviye, alınan upgrade'ler, çarpanlar, reroll/dirilme hakları), istatistikler, karakter,
// zorluk ve dalga. Sahadaki düşmanlar KAYDEDİLMEZ: devamda o dalga BAŞINDAN yeniden başlar.
// Ölünce kayıt silinir. Kayıt varken yeni oyuna başlanırsa eski oyun "bitmiş" sayılır:
// Core'u ve rekorları yine verilir (Abandon) — hiçbir ilerleme kaybolmaz.
//
// Dosya: Application.persistentDataPath/run_save.json
public static class RunSave
{
    static string FilePath => Path.Combine(Application.persistentDataPath, "run_save.json");

    [System.Serializable]
    public class Data
    {
        public int version = 1;
        public string character, difficulty;
        public int wave;
        // oyuncu
        public int level, exp, totalExp, money;
        public float maxHealth, health;
        public int extraLives, freeRerolls, pendingLevelUps;
        public float expMultiplier, moveSpeed, magnetRange;
        public float damageMult, fireRateMult, critBonus;
        public List<WeaponEntry> weapons = new List<WeaponEntry>();
        public List<SkillEntry> skills = new List<SkillEntry>();
        public List<TakenEntry> taken = new List<TakenEntry>();
        public RunResult run;
    }

    [System.Serializable] public class WeaponEntry { public int slot; public string weapon; public int tier; }
    [System.Serializable] public class SkillEntry { public string skill; public int level; }
    [System.Serializable] public class TakenEntry { public string upgrade; public int times; }

    // Oyuncu bilerek KAYDETMEDEN çıkıyorsa (Quit / Main Menu): pencere kapanırken otomatik kayıt yapılmaz
    public static bool SuppressAutoSave;

    // Kaydetmeden çıkış: bu oyun bitmiş sayılır (Core + rekorlar verilir), devam kaydı kalmaz
    public static void EndCurrentWithoutSave()
    {
        SuppressAutoSave = true;
        if (SaveCurrent()) AbandonIfExists();
        else Delete();
    }

    // Ana menüde "Continue" seçilince: bir sonraki oyun sahnesi bu kayıtla açılır
    public static Data Pending { get; private set; }

    // ---- Dosya ----
    public static bool Exists => File.Exists(FilePath);

    public static Data Load()
    {
        try { return Exists ? JsonUtility.FromJson<Data>(File.ReadAllText(FilePath)) : null; }
        catch (System.Exception e) { Debug.LogWarning($"[RunSave] Kayıt okunamadı: {e.Message}"); return null; }
    }

    public static void Delete()
    {
        if (Exists) File.Delete(FilePath);
    }

    // ---- Kaydet (oyun sahnesinde) ----
    // Oyuncu yaşıyorsa ve oyun sürüyorsa kaydeder. true = kaydedildi.
    public static bool SaveCurrent()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return false;
        var ctx = new PlayerContext(p);
        if (ctx.health == null || ctx.health.IsDead || ctx.stats == null) return false;

        var um = Object.FindAnyObjectByType<UpgradeManager>();
        var runStats = Object.FindAnyObjectByType<RunStats>();
        var d = new Data
        {
            character = CharacterSelection.Current != null ? CharacterSelection.Current.id : "",
            difficulty = Difficulty.Current != null ? Difficulty.Current.id : "",
            wave = ctx.waves != null ? Mathf.Max(1, ctx.waves.CurrentWave) : 1,
            level = ctx.stats.Level, exp = ctx.stats.Exp, totalExp = ctx.stats.TotalExp, money = ctx.stats.Money,
            expMultiplier = ctx.stats.ExpMultiplier,
            maxHealth = ctx.health.Max, health = ctx.health.Current, extraLives = ctx.health.ExtraLives,
            moveSpeed = ctx.movement != null ? ctx.movement.MoveSpeed : 0f,
            magnetRange = ctx.collector != null ? ctx.collector.MagnetRange : 0f,
            freeRerolls = um != null ? um.FreeRerolls : 0,
            pendingLevelUps = um != null ? um.PendingLevelUps : 0,
            run = runStats != null ? runStats.Snapshot() : default,
        };

        if (ctx.weapons != null)
        {
            // Süren Overdrive'ın geçici bonusu kalıcı olmasın
            float odDmg = ctx.skills != null ? ctx.skills.ActiveOverdriveDamage : 0f;
            float odRate = ctx.skills != null ? ctx.skills.ActiveOverdriveFireRate : 0f;
            d.damageMult = ctx.weapons.DamageMultiplier - odDmg;
            d.fireRateMult = ctx.weapons.FireRateMultiplier - odRate;
            d.critBonus = ctx.weapons.CritChanceBonus;
            for (int i = 0; i < ctx.weapons.SlotCount; i++)
            {
                var w = ctx.weapons.GetSlotData(i);
                if (w != null) d.weapons.Add(new WeaponEntry { slot = i, weapon = w.name, tier = ctx.weapons.GetSlotTier(i) });
            }
        }
        if (ctx.skills != null)
            for (int i = 0; i < ctx.skills.SkillCount; i++)
                d.skills.Add(new SkillEntry { skill = ctx.skills.GetSkill(i).name, level = ctx.skills.GetSkillLevel(i) });
        if (um != null)
            foreach (var (u, t) in um.GetTimesTaken())
                d.taken.Add(new TakenEntry { upgrade = u.name, times = t });

        File.WriteAllText(FilePath, JsonUtility.ToJson(d));
        return true;
    }

    // ---- Ana menü ----
    // "Continue": karakter/zorluk seçimini kayıttakine çevirir, oyun sahnesi açılınca geri yüklenir.
    public static bool PrepareContinue()
    {
        var d = Load();
        if (d == null) return false;
        var cat = MetaProgress.Catalog;
        if (cat != null)
        {
            foreach (var c in cat.characters) if (c != null && c.id == d.character) CharacterSelection.Select(c);
            foreach (var x in cat.difficulties) if (x != null && x.id == d.difficulty) Difficulty.Select(x);
        }
        WaveManager.PendingStartWave = d.wave;
        Pending = d;
        return true;
    }

    // Yeni oyuna başlanırken eski kayıt varsa: o oyun bitmiş sayılır (Core + rekorlar), kayıt silinir
    public static void AbandonIfExists()
    {
        Pending = null;
        var d = Load();
        Delete();
        if (d == null) return;

        // Zorluk çarpanı kayıttaki zorluğa göre hesaplansın (Core ödülü)
        var prevDifficulty = Difficulty.Current;
        var cat = MetaProgress.Catalog;
        if (cat != null)
            foreach (var x in cat.difficulties) if (x != null && x.id == d.difficulty) Difficulty.Select(x);

        var locked = MetaProgress.CurrentlyLocked();
        var chars = MetaProgress.CurrentlyLockedCharacters();
        BestRecords.Submit(d.run, out _);
        MetaProgress.EndRun(d.run, locked, chars);

        if (prevDifficulty != null) Difficulty.Select(prevDifficulty);
    }

    // Menüde gösterilecek özet: "WAVE 7  ·  SCOUT  ·  HARD  ·  LV 12"
    public static string Describe(Data d)
    {
        if (d == null) return "";
        string ch = d.character, df = d.difficulty;
        var cat = MetaProgress.Catalog;
        if (cat != null)
        {
            foreach (var c in cat.characters) if (c != null && c.id == d.character) ch = $"<color=#{ColorUtility.ToHtmlStringRGB(c.color)}>{c.title}</color>";
            foreach (var x in cat.difficulties) if (x != null && x.id == d.difficulty) df = $"<color=#{ColorUtility.ToHtmlStringRGB(x.color)}>{Loc.T(x.title)}</color>";
        }
        int s = Mathf.FloorToInt(d.run.timeSurvived);
        return $"{Loc.F("WAVE {0}", d.wave)}  -  {ch}  -  {df}  -  {Loc.F("LV {0}", d.level)}  -  {s / 60:00}:{s % 60:00}";
    }

    // ---- Oyun sahnesinde geri yükleme ----
    // MetaApplier (mağaza + karakter bonusları) bir kare bekleyip uygular; biz ondan SONRA mutlak
    // değerleri yazarız: bonuslar iki kez eklenmez (kayıttaki değerler zaten onları içeriyor).
    internal static IEnumerator RestoreRoutine()
    {
        var d = Pending;
        Pending = null;
        if (d == null) yield break;
        yield return null;
        yield return null;

        var p = GameObject.FindGameObjectWithTag("Player");
        var um = Object.FindAnyObjectByType<UpgradeManager>();
        if (p == null || um == null) yield break;
        var ctx = new PlayerContext(p);

        // Asset'leri adlarından bul: sahnedeki kart havuzu tüm silah/skill/upgrade'leri içerir
        var upgrades = new Dictionary<string, UpgradeData>();
        var weaponsByName = new Dictionary<string, WeaponData>();
        foreach (var u in um.Pool)
        {
            if (u == null) continue;
            upgrades[u.name] = u;
            if (u is WeaponUpgradeData w && w.weapon != null) weaponsByName[w.weapon.name] = w.weapon;
        }
        var start = CharacterSelection.Current != null ? CharacterSelection.Current.startingWeapon : null;
        if (start != null) weaponsByName[start.name] = start;

        ctx.stats.RestoreState(d.level, d.exp, d.totalExp, d.money);
        ctx.stats.ExpMultiplier = d.expMultiplier;
        ctx.health.RestoreState(d.maxHealth, d.health, d.extraLives);
        if (ctx.movement != null && d.moveSpeed > 0f) ctx.movement.MoveSpeed = d.moveSpeed;
        if (ctx.collector != null && d.magnetRange > 0f) ctx.collector.MagnetRange = d.magnetRange;

        if (ctx.weapons != null)
        {
            ctx.weapons.RestoreMultipliers(d.damageMult, d.fireRateMult, d.critBonus);
            var list = new List<(int, WeaponData, int)>();
            foreach (var w in d.weapons)
                if (weaponsByName.TryGetValue(w.weapon, out var data)) list.Add((w.slot, data, w.tier));
                else Debug.LogWarning($"[RunSave] Silah bulunamadı: {w.weapon}");
            ctx.weapons.RestoreWeapons(list);
        }
        if (ctx.skills != null)
        {
            var list = new List<(SkillUpgradeData, int)>();
            foreach (var s in d.skills)
                if (upgrades.TryGetValue(s.skill, out var u) && u is SkillUpgradeData sk) list.Add((sk, s.level));
            ctx.skills.RestoreSkills(list);
        }

        var taken = new List<(UpgradeData, int)>();
        foreach (var t in d.taken)
            if (upgrades.TryGetValue(t.upgrade, out var u)) taken.Add((u, t.times));

        var runStats = Object.FindAnyObjectByType<RunStats>();
        if (runStats != null) runStats.RestoreState(d.run);   // paradan sonra: geri yükleme "toplanan" sayılmasın

        um.RestoreState(taken, d.freeRerolls, d.pendingLevelUps);   // bekleyen seçim varsa panel açılır

        var toast = Object.FindAnyObjectByType<UpgradeToastUI>();
        if (toast != null) toast.Show($"<color=#73EFF7>{Loc.F("RUN RESUMED - WAVE {0}", d.wave)}</color>");
    }
}
