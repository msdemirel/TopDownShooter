using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Oyunun TÜM denge değerlerini tek yerden asset'lere yazar:
// düşman seviyeleri, drop tabloları, oyuncu canı/exp eğrisi, silahlar, fiyatlar,
// dalga kilitleri, stat/skill kademeleri, dalgalar ve sonsuz mod.
//
// Kullanım: Menü > TopDownShooter > Denge > Tüm Değerleri Uygula
// Değer değiştirmek istersen BURAYI düzenle ve menüden tekrar uygula (tekrar çalıştırmak güvenli).
// Yeni düşman/silah eklerken ilgili bölüme bir satır eklemen yeterli.
//
// Tasarım notları:
// - Tüm düşmanlar kamikaze (ExploderEnemy): hasar = TEMAS BAŞINA tek seferlik patlama.
//   Patlayan düşman drop vermez; drop sadece oyuncu öldürünce düşer.
// - Ölçek: Fly Lv1 = 15 can, başlangıç kılıcı 10 hasar (2 vuruş). Oyuncu 50 can.
// - Exp eğrisi: dalga başına ~1 level. Coin: Sword ~2., Sword II ~5., Pistol ~7. dalgada alınır.
public static class BalanceSetup
{
    const string Data = "Assets/Prefabs/Data/";
    const string Prefabs = "Assets/Prefabs/";
    const string ScenePath = "Assets/Scenes/MainGame.unity";

    [MenuItem("TopDownShooter/Denge/Tüm Değerleri Uygula")]
    static void ApplyAll()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Denge", "Önce Play Mode'dan çık.", "Tamam");
            return;
        }
        if (!EditorUtility.DisplayDialog("Denge",
                "Düşman, drop, silah, upgrade, skill ve dalga değerleri üzerine yazılacak.\n" +
                "(Geri almak için git kullanabilirsin.) Devam edilsin mi?", "Uygula", "Vazgeç"))
            return;

        // ---- 1) Düşmanlar ----
        var fly = Load<EnemyTypeData>(Data + "Enemy/FlyEnemy.asset");
        var exploder = Load<EnemyTypeData>(Data + "Enemy/Exploder.asset");
        var boss = Load<EnemyTypeData>(Data + "Enemy/BIgBoss.asset");

        //                     can   hız  hasar  yarıçap  lootRolls
        SetEnemy(fly, "Fly", new[] {
            E(15, 1.5f,  4, 1.0f, 1),
            E(25, 1.7f,  5, 1.0f, 1),
            E(40, 1.9f,  6, 1.1f, 2),
            E(60, 2.0f,  8, 1.1f, 2),
            E(90, 2.1f, 10, 1.2f, 3),
            E(130,2.2f, 12, 1.2f, 3),
        });
        // Hızlı ve kırılgan; Lv3'ten sonra oyuncudan (3) hızlı -> hız upgrade'i anlam kazanır
        SetEnemy(exploder, "Exploder", new[] {
            E( 8, 2.4f,  6, 1.2f, 1),
            E(12, 2.7f,  8, 1.3f, 1),
            E(20, 3.0f, 10, 1.4f, 2),
            E(30, 3.2f, 13, 1.5f, 2),
            E(45, 3.4f, 16, 1.6f, 3),
        });
        // Yavaş tank: ulaşırsa canın ~yarısını alır
        SetEnemy(boss, "BigBoss", new[] {
            E( 300, 1.2f, 20, 1.8f, 2),
            E( 600, 1.3f, 28, 2.0f, 2),
            E(1000, 1.4f, 36, 2.2f, 3),
        });

        // ---- 2) Pickup miktarları ----
        var expPickup = LoadPrefab("Exp");
        var moneyPickup = LoadPrefab("Money");
        var healthPickup = LoadPrefab("Health");
        EditPrefab("Exp", root => SetField<Pickup>(root, "amount", 1f));
        EditPrefab("Money", root => SetField<Pickup>(root, "amount", 1f));
        EditPrefab("Health", root => SetField<Pickup>(root, "amount", 5f));   // oyuncu canının %10'u

        // ---- 3) Drop tabloları (tek zar: yüzdeler + nothing = 100) ----
        EditPrefab("FlyEnemy", root => SetLoot(root, 22f,
            D(expPickup, 55, 1, 1), D(moneyPickup, 20, 1, 1), D(healthPickup, 3, 1, 1)));
        EditPrefab("Exploder", root => SetLoot(root, 22f,
            D(expPickup, 50, 1, 1), D(moneyPickup, 25, 1, 1), D(healthPickup, 3, 1, 1)));
        EditPrefab("BigBoss", root => SetLoot(root, 0f,
            D(expPickup, 50, 8, 12), D(moneyPickup, 40, 6, 10), D(healthPickup, 10, 3, 4)));

        // ---- 4) Oyuncu ----
        EditPrefab("Player", root =>
        {
            SetField<Health>(root, "maxHealth", 50f);
            SetField<PlayerStats>(root, "baseExpToLevel", 4);
            SetField<PlayerStats>(root, "expIncreasePerLevel", 3);
        });

        // ---- 5) Silahlar ----
        var sword = Load<WeaponData>(Data + "Guns/Sword.asset");
        var pistol = Load<WeaponData>(Data + "Guns/Pistol.asset");
        var sword2 = LoadOrCopy<WeaponData>(Data + "Guns/Sword II.asset", Data + "Guns/Sword.asset");

        SetWeapon(sword, "Sword", damage: 10, fireRate: 1.5f, range: 2f);
        SetWeapon(sword2, "Sword II", damage: 18, fireRate: 1.6f, range: 2.2f);
        SetWeapon(pistol, "Pistol", damage: 12, fireRate: 2f, range: 5f);

        // ---- 6) Silah satın alma kartları: fiyat + dalga kilidi ----
        var upSword = Load<WeaponUpgradeData>(Data + "[W_UP] Sword.asset");
        var upPistol = Load<WeaponUpgradeData>(Data + "[W_UP]Pistol.asset");
        var upSword2 = LoadOrCopy<WeaponUpgradeData>(Data + "[W_UP] Sword II.asset", Data + "[W_UP] Sword.asset");

        SetWeaponUp(upSword, "Buy +1 Sword", "Adds 1 more sword to an empty slot", sword, cost: 5, unlock: 1, last: 8);
        SetWeaponUp(upSword2, "Buy +1 Sword II", "A heavier blade for tougher waves", sword2, cost: 12, unlock: 4, last: 0);
        SetWeaponUp(upPistol, "Buy +1 Pistol", "Shoots enemies from a distance", pistol, cost: 20, unlock: 6, last: 0);

        // ---- 7) Stat upgrade'leri (kademe miktarları) ----
        var upHealth = Load<StatUpgradeData>(Data + "[UP]Health.asset");
        var upSpeed = Load<StatUpgradeData>(Data + "[UP]Speed.asset");
        var upCrit = Load<StatUpgradeData>(Data + "[UP]CritChance.asset");
        var upMagnet = Load<StatUpgradeData>(Data + "[UP]MagnetRange.asset");
        var upDamage = LoadOrCopy<StatUpgradeData>(Data + "[UP]Damage.asset", Data + "[UP]Speed.asset");
        var upFireRate = LoadOrCopy<StatUpgradeData>(Data + "[UP]FireRate.asset", Data + "[UP]Speed.asset");

        SetStat(upHealth, StatType.MaxHealth, "Health", "Increases max health by {0}", 1,
            10, 10, 15, 15, 20, 20, 25);
        SetStat(upSpeed, StatType.MoveSpeed, "Movement Speed", "Increase Movement Speed by {0}", 1,
            0.3f, 0.3f, 0.4f, 0.4f, 0.5f);
        SetStat(upDamage, StatType.Damage, "Damage", "All weapons hit harder.", 1,
            0.10f, 0.10f, 0.15f, 0.15f, 0.20f, 0.20f, 0.25f);
        SetStat(upFireRate, StatType.FireRate, "Attack Speed", "All weapons attack faster.", 2,
            0.08f, 0.08f, 0.10f, 0.10f, 0.12f, 0.15f);
        SetStat(upCrit, StatType.CritChance, "Crit Chance", "More critical hits.", 3,
            0.03f, 0.04f, 0.05f, 0.05f, 0.06f);
        SetStat(upMagnet, StatType.MagnetRange, "Magnet Range", "Pulls loot from farther away.", 1,
            0.5f, 0.5f, 1f, 1f, 1.5f);

        // ---- 8) Skill'ler (5 seviye, dalga kilidiyle sırayla açılır) ----
        var dash = Load<SkillUpgradeData>(Data + "[SKL]Dash.asset");
        var blast = Load<SkillUpgradeData>(Data + "[SKL]AreaBlast.asset");
        var shield = Load<SkillUpgradeData>(Data + "[SKL]Shield.asset");
        var pulse = Load<SkillUpgradeData>(Data + "[SKL] PulseWave.asset");
        var burst = Load<SkillUpgradeData>(Data + "[SKL] FireBurst.asset");

        SetSkill(dash, unlock: 1, lv => {
            lv.cooldown = Step(5f, 4.5f, 4f, 3.5f, 3f);
            lv.dashSpeed = Step(18f, 19f, 20f, 21f, 22f);
            lv.dashDuration = Step(0.15f, 0.16f, 0.17f, 0.18f, 0.2f);
        });
        SetSkill(blast, unlock: 3, lv => {
            lv.cooldown = Step(8f, 7.5f, 7f, 6.5f, 6f);
            lv.blastDamage = Step(20f, 28f, 38f, 50f, 65f);
            lv.blastRadius = Step(2f, 2.2f, 2.4f, 2.6f, 3f);
        });
        SetSkill(shield, unlock: 5, lv => {
            lv.cooldown = Step(12f, 11f, 10f, 9f, 8f);
            lv.shieldDuration = Step(2f, 2.3f, 2.6f, 3f, 3.5f);
        });
        // Halka aralığı / büyüme süresi (görsel his) asset'teki mevcut değerlerden korunur
        float pulseInterval = pulse.GetLevel(0).waveInterval;
        float pulseExpand = pulse.GetLevel(0).waveExpandTime;
        SetSkill(pulse, unlock: 7, lv => {
            lv.cooldown = Step(9f, 8.5f, 8f, 7.5f, 7f);
            lv.waveCount = (int)Step(3, 3, 4, 4, 5);
            lv.waveDamage = Step(15f, 20f, 26f, 33f, 42f);
            lv.waveRadius = Step(4f, 4.3f, 4.6f, 5f, 5.5f);
            lv.waveInterval = pulseInterval;
            lv.waveExpandTime = pulseExpand;
        });
        SetSkill(burst, unlock: 9, lv => {
            lv.cooldown = Step(6f, 5.5f, 5f, 4.5f, 4f);
            lv.burstCount = (int)Step(6, 8, 8, 10, 12);
            lv.burstDamage = Step(12f, 15f, 19f, 24f, 30f);
            lv.burstSpeed = Step(8f, 8.5f, 9f, 9.5f, 10f);
        });

        // ---- 9) Dalgalar (1-10 elle yazılmış, kolaydan zora) ----
        //   W(aralık sn, giriş...)  G(tür, level, adet)
        var waves = new[]
        {
            W(1.5f,  G(fly, 1, 12)),
            W(1.2f,  G(fly, 1, 16), G(exploder, 1, 4)),
            W(0.9f,  G(fly, 1, 18), G(fly, 2, 6), G(exploder, 1, 5)),
            W(0.9f,  G(fly, 2, 20), G(exploder, 1, 8)),
            W(1.0f,  G(fly, 2, 15), G(exploder, 2, 8), G(boss, 1, 1)),       // ilk boss
            W(0.8f,  G(fly, 2, 25), G(exploder, 2, 10)),                    // Pistol açılır
            W(0.7f,  G(fly, 3, 20), G(fly, 2, 10), G(exploder, 2, 10)),
            W(0.65f, G(fly, 3, 30), G(exploder, 3, 12)),
            W(0.6f,  G(fly, 3, 25), G(fly, 4, 10), G(exploder, 3, 12)),
            W(0.6f,  G(fly, 4, 30), G(exploder, 3, 15), G(boss, 2, 1)),      // ikinci boss
        };
        var waveAssets = new WaveData[waves.Length];
        for (int i = 0; i < waves.Length; i++)
            waveAssets[i] = SetWave(i + 1, waves[i]);

        // ---- 10) Sahne: WaveManager (sonsuz mod) + UpgradeManager havuzu ----
        var pool = new List<UpgradeData> {
            upSword, upSword2, upPistol,
            upHealth, upSpeed, upDamage, upFireRate, upCrit, upMagnet,
            dash, blast, shield, pulse, burst,
        };
        bool sceneOk = ApplyScene(waveAssets, fly, exploder, boss, pool);

        AssetDatabase.SaveAssets();
        Debug.Log("[BalanceSetup] Denge değerleri uygulandı." +
                  (sceneOk ? " Sahneyi kaydetmeyi unutma (Ctrl+S)." : " Sahne ayarları UYGULANAMADI (uyarıya bak)."));
    }

    // =====================================================================
    // Yardımcılar
    // =====================================================================

    static EnemyLevelStats E(float hp, float speed, float damage, float radius, int rolls)
        => new EnemyLevelStats
        {
            maxHealth = hp, moveSpeed = speed, attackDamage = damage,
            explosionRadius = radius, lootRolls = rolls,
            attackRange = 1.5f, attackCooldown = 1f, projectileSpeed = 8f,
        };

    static void SetEnemy(EnemyTypeData t, string name, EnemyLevelStats[] levels)
    {
        if (t == null) return;
        t.typeName = name;
        t.levels = levels;
        EditorUtility.SetDirty(t);
    }

    static void SetWeapon(WeaponData w, string name, float damage, float fireRate, float range)
    {
        if (w == null) return;
        w.weaponName = name;
        w.damage = damage;
        w.fireRate = fireRate;
        w.range = range;
        w.critChance = 0.05f;
        w.critMultiplier = 2f;
        EditorUtility.SetDirty(w);
    }

    static void SetWeaponUp(WeaponUpgradeData u, string title, string desc, WeaponData weapon,
                            int cost, int unlock, int last)
    {
        if (u == null) return;
        u.title = title;
        u.description = desc;
        u.weapon = weapon;
        u.cost = cost;
        u.unlockWave = unlock;
        u.lastWave = last;
        EditorUtility.SetDirty(u);
    }

    static void SetStat(StatUpgradeData u, StatType stat, string title, string desc, int unlock,
                        params float[] tiers)
    {
        if (u == null) return;
        u.stat = stat;
        u.title = title;
        u.description = desc;
        u.unlockWave = unlock;
        u.lastWave = 0;
        u.tierAmounts = tiers;
        EditorUtility.SetDirty(u);
    }

    // SetSkill içindeki Step(...) çağrıları o anki seviyenin değerini seçer.
    static int stepIndex;
    static float Step(params float[] perLevel) => perLevel[Mathf.Min(stepIndex, perLevel.Length - 1)];

    const int SkillLevels = 5;

    static void SetSkill(SkillUpgradeData s, int unlock, System.Action<SkillLevelStats> fill)
    {
        if (s == null) return;
        s.unlockWave = unlock;
        s.lastWave = 0;
        s.levels = new SkillLevelStats[SkillLevels];
        for (stepIndex = 0; stepIndex < SkillLevels; stepIndex++)
        {
            var lv = new SkillLevelStats();
            fill(lv);
            s.levels[stepIndex] = lv;
        }
        EditorUtility.SetDirty(s);
    }

    static WaveData.SpawnEntry G(EnemyTypeData type, int level, int count)
        => new WaveData.SpawnEntry { type = type, level = level, count = count };

    static WaveData.SpawnEntry[] W(float interval, params WaveData.SpawnEntry[] entries)
    {
        foreach (var e in entries) { e.spawnInterval = interval; e.startDelay = 0f; }
        return entries;
    }

    static WaveData SetWave(int number, WaveData.SpawnEntry[] entries)
    {
        string path = $"{Data}Waves/Wave{number}.asset";
        var w = AssetDatabase.LoadAssetAtPath<WaveData>(path);
        if (w == null)
        {
            w = ScriptableObject.CreateInstance<WaveData>();
            AssetDatabase.CreateAsset(w, path);
        }
        w.randomOrder = true;
        w.entries = entries;
        EditorUtility.SetDirty(w);
        return w;
    }

    static LootDropper.Drop D(GameObject prefab, float chance, int min, int max)
        => new LootDropper.Drop { pickupPrefab = prefab, dropChance = chance, minCount = min, maxCount = max };

    static void SetLoot(GameObject root, float nothing, params LootDropper.Drop[] drops)
    {
        var ld = root.GetComponent<LootDropper>();
        if (ld == null) { Debug.LogWarning($"[BalanceSetup] {root.name}: LootDropper yok."); return; }

        var so = new SerializedObject(ld);
        var arr = so.FindProperty("drops");
        arr.arraySize = drops.Length;
        for (int i = 0; i < drops.Length; i++)
        {
            var el = arr.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("pickupPrefab").objectReferenceValue = drops[i].pickupPrefab;
            el.FindPropertyRelative("dropChance").floatValue = drops[i].dropChance;
            el.FindPropertyRelative("minCount").intValue = drops[i].minCount;
            el.FindPropertyRelative("maxCount").intValue = drops[i].maxCount;
        }
        so.FindProperty("nothingChance").floatValue = nothing;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Private [SerializeField] alanlara yazmak için.
    static void SetField<T>(GameObject root, string field, float value) where T : Component
    {
        var p = FindProp<T>(root, field, out var so);
        if (p == null) return;
        if (p.propertyType == SerializedPropertyType.Integer) p.intValue = Mathf.RoundToInt(value);
        else p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static SerializedProperty FindProp<T>(GameObject root, string field, out SerializedObject so) where T : Component
    {
        so = null;
        var c = root.GetComponentInChildren<T>(true);
        if (c == null) { Debug.LogWarning($"[BalanceSetup] {root.name}: {typeof(T).Name} yok."); return null; }
        so = new SerializedObject(c);
        var p = so.FindProperty(field);
        if (p == null) Debug.LogWarning($"[BalanceSetup] {typeof(T).Name}.{field} bulunamadı.");
        return p;
    }

    static GameObject LoadPrefab(string name) => Load<GameObject>($"{Prefabs}{name}.prefab");

    // Prefab'ı izole açıp düzenler ve kaydeder (Unity'nin önerdiği güvenli yol).
    static void EditPrefab(string name, System.Action<GameObject> edit)
    {
        string path = $"{Prefabs}{name}.prefab";
        if (!File.Exists(path)) { Debug.LogWarning($"[BalanceSetup] Prefab yok: {path}"); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static T Load<T>(string path) where T : Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a == null) Debug.LogWarning($"[BalanceSetup] Asset bulunamadı: {path}");
        return a;
    }

    // Yoksa şablondan kopyalar (ikon/sprite gibi görsel referanslar şablondan gelir).
    static T LoadOrCopy<T>(string path, string templatePath) where T : Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a != null) return a;
        if (!AssetDatabase.CopyAsset(templatePath, path))
        {
            Debug.LogWarning($"[BalanceSetup] Kopyalanamadı: {templatePath} -> {path}");
            return null;
        }
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    static bool ApplyScene(WaveData[] waves, EnemyTypeData fly, EnemyTypeData exploder, EnemyTypeData boss,
                           List<UpgradeData> pool)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            scene = EditorSceneManager.OpenScene(ScenePath);
        }

        var wm = Object.FindFirstObjectByType<WaveManager>();
        var um = Object.FindFirstObjectByType<UpgradeManager>();
        if (wm == null || um == null)
        {
            Debug.LogWarning("[BalanceSetup] Sahnede WaveManager veya UpgradeManager bulunamadı.");
            return false;
        }

        // WaveManager
        var so = new SerializedObject(wm);
        var wavesProp = so.FindProperty("waves");
        wavesProp.arraySize = waves.Length;
        for (int i = 0; i < waves.Length; i++)
            wavesProp.GetArrayElementAtIndex(i).objectReferenceValue = waves[i];

        // Sonsuz mod: 11. dalgadan itibaren. Level 4'ten başlar (W10 ile aynı), 3 dalgada bir +1.
        // Maliyet x level: Lv4 Fly = 4 -> 170 bütçe ~ 42 düşman (W10 kalabalığı). Boss en fazla 1.
        var endless = so.FindProperty("endlessEnemies");
        endless.arraySize = 3;
        SetEndless(endless.GetArrayElementAtIndex(0), fly, 1, 1f);
        SetEndless(endless.GetArrayElementAtIndex(1), exploder, 1, 1f);
        SetEndless(endless.GetArrayElementAtIndex(2), boss, 11, 40f);
        so.FindProperty("baseBudget").floatValue = 170f;
        so.FindProperty("budgetPerWave").floatValue = 15f;
        so.FindProperty("wavesPerEnemyLevel").intValue = 3;
        so.FindProperty("endlessStartLevel").intValue = 4;
        so.FindProperty("targetWaveDuration").floatValue = 30f;
        so.FindProperty("maxEnemiesPerWave").intValue = 60;
        so.ApplyModifiedProperties();

        // UpgradeManager havuzu: eksikleri ekle (sahnede elle eklenmiş başka upgrade'ler korunur)
        var uso = new SerializedObject(um);
        var poolProp = uso.FindProperty("pool");
        var existing = new HashSet<Object>();
        for (int i = 0; i < poolProp.arraySize; i++)
            existing.Add(poolProp.GetArrayElementAtIndex(i).objectReferenceValue);
        foreach (var u in pool)
        {
            if (u == null || existing.Contains(u)) continue;
            poolProp.arraySize++;
            poolProp.GetArrayElementAtIndex(poolProp.arraySize - 1).objectReferenceValue = u;
        }
        uso.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    static void SetEndless(SerializedProperty el, EnemyTypeData type, int minWave, float cost)
    {
        el.FindPropertyRelative("type").objectReferenceValue = type;
        el.FindPropertyRelative("minWave").intValue = minWave;
        el.FindPropertyRelative("cost").floatValue = cost;
    }
}
