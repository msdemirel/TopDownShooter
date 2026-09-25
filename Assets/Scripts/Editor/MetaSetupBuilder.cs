using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Kalıcı ilerleme (meta) sistemini kurar / günceller:
//   - Mağaza upgrade'leri (Assets/Prefabs/Data/Meta/*.asset)
//   - Katalog (Assets/Resources/MetaCatalog.asset): upgrade listesi + kilitli içerikler
//   - Silah/skill kartlarına kalıcı kilit koşulları
//   - Zorluk seviyeleri (Normal / Hard / Nightmare)
//   - Başlangıç karakterleri (Vanguard / Scout / Gunslinger / Juggernaut)
//   - BigBoss prefab'ına "Is Boss", Player prefab'ına başlangıç silahı (Bat)
//
// Kullanım: Menü > TopDownShooter > Meta > Kurulumu Yap - Güncelle
// Tekrar çalıştırmak güvenli: fiyat/etki değerleri ve kilit koşulları SADECE ilk kurulumda
// yazılır; sonra Inspector'da yaptığın değişiklikler korunur. Katalog her seferinde yeniden taranır.
public static class MetaSetupBuilder
{
    const string MetaFolder = "Assets/Prefabs/Data/Meta";
    const string CatalogPath = "Assets/Resources/MetaCatalog.asset";
    const string Data = "Assets/Prefabs/Data/";

    struct Up
    {
        public string id, title, desc, icon;
        public MetaEffect effect;
        public float value;
        public int[] costs;
    }

    static readonly Up[] Upgrades =
    {
        new Up { id = "vitality", icon = "stat_max_health", title = "Vitality", desc = "More max health every run.", effect = MetaEffect.MaxHealth, value = 10, costs = new[] { 20, 40, 70, 110, 160 } },
        new Up { id = "power", icon = "stat_damage", title = "Power", desc = "All weapons deal more damage.", effect = MetaEffect.Damage, value = 0.05f, costs = new[] { 30, 55, 90, 140, 200 } },
        new Up { id = "agility", icon = "stat_move_speed", title = "Agility", desc = "Move faster.", effect = MetaEffect.MoveSpeed, value = 0.04f, costs = new[] { 25, 50, 90 } },
        new Up { id = "wisdom", icon = "meta_wisdom", title = "Wisdom", desc = "Gain more experience.", effect = MetaEffect.ExpGain, value = 0.1f, costs = new[] { 25, 45, 75, 115, 165 } },
        new Up { id = "funds", icon = "meta_funds", title = "Starting Funds", desc = "Start each run with extra coins.", effect = MetaEffect.StartMoney, value = 10, costs = new[] { 20, 40, 70, 110 } },
        new Up { id = "dice", icon = "meta_dice", title = "Lucky Dice", desc = "Free card rerolls every run.", effect = MetaEffect.FreeRerolls, value = 1, costs = new[] { 40, 90, 160 } },
        new Up { id = "magnet", icon = "stat_magnet_range", title = "Magnet", desc = "Pick up loot from farther away.", effect = MetaEffect.MagnetRange, value = 0.15f, costs = new[] { 15, 35, 60 } },
        new Up { id = "second_chance", icon = "meta_second_chance", title = "Second Chance", desc = "Revive once with half health.", effect = MetaEffect.ExtraLife, value = 1, costs = new[] { 250 } },
    };

    struct Diff
    {
        public string id, title, desc;
        public Color color;
        public float hp, dmg, speed, core;
        public int requiresIndex, wave;   // requiresIndex -1 = baştan açık
    }

    static readonly Diff[] Difficulties =
    {
        new Diff { id = "normal", title = "NORMAL", desc = "The standard challenge.", color = new Color32(0x73, 0xEF, 0xF7, 0xFF),
                   hp = 1f, dmg = 1f, speed = 1f, core = 1f, requiresIndex = -1 },
        new Diff { id = "hard", title = "HARD", desc = "Tougher, faster enemies. Better rewards.", color = new Color32(0xFF, 0xCD, 0x75, 0xFF),
                   hp = 1.6f, dmg = 1.4f, speed = 1.1f, core = 1.5f, requiresIndex = 0, wave = 10 },
        new Diff { id = "nightmare", title = "NIGHTMARE", desc = "Only for the bravest. Huge rewards.", color = new Color32(0xE5, 0x53, 0x3C, 0xFF),
                   hp = 2.4f, dmg = 1.9f, speed = 1.2f, core = 2.25f, requiresIndex = 1, wave = 10 },
    };

    const string PlayerSheet = "Assets/Art/Tech Dungeon Roguelite - Asset Pack (v7)/Players/players red x1.png";
    const string SkinDir = "Assets/Art/Generated_v2/Characters/";
    // Seçim ekranı portresi: askerin yürüme kareleri (Walk.anim ile aynı)
    static readonly string[] WalkFrames = { "players red x1_16", "players red x1_17", "players red x1_18", "players red x1_19" };

    struct Char
    {
        public string id, title, desc, weapon, sheet;   // sheet: null = orijinal sheet
        public Color color;
        public Vector2Int offset;
        public float hp, dmg, rate, speed, crit, magnet;
        public UnlockCondition cond;
        public float threshold;
        public CharacterData.CellRemap[] remap;   // görünüm: kare eşleme (kaymadan önce)
    }

    static CharacterData.CellRemap R(int fx, int fy, int tx, int ty)
        => new CharacterData.CellRemap { from = new Vector2Int(fx, fy), to = new Vector2Int(tx, ty) };

    static readonly Char[] Characters =
    {
        new Char { id = "vanguard", title = "VANGUARD", desc = "A steady all-rounder.", weapon = "Bat",
                   color = new Color32(0xE5, 0x53, 0x3C, 0xFF) },
        new Char { id = "scout", title = "SCOUT", desc = "Fast and nimble, but fragile.", weapon = "Sword",
                   sheet = SkinDir + "players_green.png", color = new Color32(0x5E, 0xDC, 0x5E, 0xFF),
                   speed = 0.15f, magnet = 0.25f, hp = -10f, cond = UnlockCondition.RunsPlayed, threshold = 5 },
        new Char { id = "gunslinger", title = "GUNSLINGER", desc = "Lives for the perfect shot.", weapon = "Revolver",
                   sheet = SkinDir + "players_blue.png", color = new Color32(0x41, 0xA6, 0xF6, 0xFF),
                   crit = 0.1f, rate = 0.15f, cond = UnlockCondition.TotalCrits, threshold = 250 },
        new Char { id = "juggernaut", title = "JUGGERNAUT", desc = "Heavy armor, heavy hits, slow feet.", weapon = "Greatsword",
                   offset = new Vector2Int(0, -224), color = new Color32(0xFF, 0xCD, 0x75, 0xFF),
                   hp = 50f, dmg = 0.15f, speed = -0.12f, cond = UnlockCondition.ReachWave, threshold = 10,
                   // Robotun "no gun" kareleri (silahlar karakterin etrafında ayrı dönüyor):
                   // idle 0. sütun -> 2. sütun, koşu 0-3 -> 5-8, ateş (no gun'ı yok) -> no gun idle
                   remap = new[]
                   {
                       R(0, 384, 64, 384),
                       R(0, 288, 160, 288), R(32, 288, 192, 288), R(64, 288, 224, 288), R(96, 288, 256, 288),
                       R(0, 256, 64, 384), R(32, 256, 64, 384), R(64, 256, 64, 384), R(96, 256, 64, 384),
                   } },
    };

    // Kart asset'i -> kalıcı kilit koşulu. Listede olmayanlar baştan açık.
    static readonly (string asset, UnlockCondition cond, float threshold)[] Locks =
    {
        ("[W_UP] Katana.asset", UnlockCondition.ReachWave, 5),
        ("[W_UP] Revolver.asset", UnlockCondition.TotalCrits, 100),
        ("[SKL]FrostNova.asset", UnlockCondition.SurviveSeconds, 480),
        ("[SKL]ChainLightning.asset", UnlockCondition.RunKills, 300),
        ("[W_UP] Assault Rifle.asset", UnlockCondition.RunsPlayed, 10),
        ("[W_UP] Laser Sword.asset", UnlockCondition.ReachWave, 8),
        ("[W_UP] Shotgun.asset", UnlockCondition.TotalKills, 1000),
        ("[SKL] PulseWave.asset", UnlockCondition.ReachLevel, 15),
        ("[W_UP] Sniper Rifle.asset", UnlockCondition.BossKills, 1),
        ("[SKL]Overdrive.asset", UnlockCondition.CoreEarned, 500),
        ("[W_UP] Minigun.asset", UnlockCondition.ReachWave, 15),
    };

    [MenuItem("TopDownShooter/Meta/Kurulumu Yap - Güncelle")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Meta Kurulum", "Önce Play Mode'dan çık.", "Tamam");
            return;
        }

        Directory.CreateDirectory(MetaFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));

        // ---- Katalog ----
        var catalog = AssetDatabase.LoadAssetAtPath<MetaCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<MetaCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        // ---- Mağaza upgrade'leri ----
        catalog.upgrades.Clear();
        foreach (var d in Upgrades)
        {
            string path = $"{MetaFolder}/{d.title}.asset";
            var u = AssetDatabase.LoadAssetAtPath<MetaUpgradeData>(path);
            if (u == null)
            {
                u = ScriptableObject.CreateInstance<MetaUpgradeData>();
                u.id = d.id;
                u.effect = d.effect;
                u.valuePerLevel = d.value;
                u.costs = d.costs;
                AssetDatabase.CreateAsset(u, path);
            }
            u.title = d.title;
            u.description = d.desc;
            if (u.icon == null)
                u.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Generated_v2/Icons/{d.icon}.png");
            EditorUtility.SetDirty(u);
            catalog.upgrades.Add(u);
        }

        // ---- Zorluklar (değerler sadece ilk oluşturmada) ----
        catalog.difficulties.Clear();
        foreach (var d in Difficulties)
        {
            string path = $"{MetaFolder}/Difficulty_{d.title}.asset";
            var a = AssetDatabase.LoadAssetAtPath<DifficultyData>(path);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<DifficultyData>();
                a.id = d.id;
                a.title = d.title;
                a.description = d.desc;
                a.color = d.color;
                a.enemyHealth = d.hp;
                a.enemyDamage = d.dmg;
                a.enemySpeed = d.speed;
                a.coreMultiplier = d.core;
                a.requiredWave = d.wave;
                if (d.requiresIndex >= 0) a.requires = catalog.difficulties[d.requiresIndex];
                AssetDatabase.CreateAsset(a, path);
            }
            catalog.difficulties.Add(a);
        }

        // ---- Karakterler (denge değerleri sadece ilk oluşturmada; görünüm her seferinde) ----
        catalog.characters.Clear();
        var original = AssetDatabase.LoadAllAssetsAtPath(PlayerSheet).OfType<Sprite>().ToArray();
        var originalByName = original.ToDictionary(s => s.name);
        foreach (var c in Characters)
        {
            string path = $"{MetaFolder}/Character_{c.title}.asset";
            var a = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<CharacterData>();
                a.id = c.id;
                a.title = c.title;
                a.description = c.desc;
                a.color = c.color;
                a.startingWeapon = AssetDatabase.LoadAssetAtPath<WeaponData>($"Assets/Prefabs/Data/Guns/{c.weapon}.asset");
                a.maxHealth = c.hp; a.damage = c.dmg; a.fireRate = c.rate;
                a.moveSpeed = c.speed; a.critChance = c.crit; a.magnetRange = c.magnet;
                a.unlockCondition = c.cond;
                a.unlockThreshold = c.threshold;
                AssetDatabase.CreateAsset(a, path);
            }

            // Görünüm: varyant sheet (ya da aynı sheet + kayma). Vanguard orijinal: skin yok.
            Sprite[] set = c.sheet != null
                ? AssetDatabase.LoadAllAssetsAtPath(c.sheet).OfType<Sprite>().ToArray()
                : original;
            bool needsSkin = c.sheet != null || c.offset != Vector2Int.zero || c.remap != null;
            a.skinSprites = needsSkin ? set : new Sprite[0];
            a.cellOffset = c.offset;
            a.cellRemap = c.remap ?? new CharacterData.CellRemap[0];

            var byCell = set.ToDictionary(s => new Vector2Int((int)s.rect.x, (int)s.rect.y));
            a.previewFrames = WalkFrames
                .Where(originalByName.ContainsKey)
                .Select(n => originalByName[n].rect)
                .Select(r => byCell.TryGetValue(a.MapCell(new Vector2Int((int)r.x, (int)r.y)), out var s) ? s : null)
                .Where(s => s != null).ToArray();
            if (needsSkin && set.Length == 0)
                Debug.LogWarning($"[MetaSetup] {c.title}: {c.sheet} bulunamadı (Tools/SpriteGen/gen_hud_v2.py ile üretilir).");

            EditorUtility.SetDirty(a);
            catalog.characters.Add(a);
        }

        // ---- Kilit koşulları (sadece henüz koşulu olmayan kartlara) ----
        int locked = 0;
        foreach (var (asset, cond, threshold) in Locks)
        {
            var u = AssetDatabase.LoadAssetAtPath<UpgradeData>(Data + asset);
            if (u == null) { Debug.LogWarning($"[MetaSetup] {Data + asset} bulunamadı, kilit atlandı."); continue; }
            if (u.unlockCondition != UnlockCondition.None || u.unlockThreshold != 0f) continue;
            u.unlockCondition = cond;
            u.unlockThreshold = threshold;
            EditorUtility.SetDirty(u);
            locked++;
        }

        // ---- Kilitli içerik listesi: projedeki koşullu tüm kartlar ----
        catalog.lockables.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:UpgradeData"))
        {
            var u = AssetDatabase.LoadAssetAtPath<UpgradeData>(AssetDatabase.GUIDToAssetPath(guid));
            if (u != null && u.unlockCondition != UnlockCondition.None) catalog.lockables.Add(u);
        }
        EditorUtility.SetDirty(catalog);

        // ---- Prefab ayarları ----
        SetPrefabField<EnemyBase>("Assets/Prefabs/BigBoss.prefab", "isBoss", p => p.boolValue = true);
        var bat = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Prefabs/Data/Guns/Bat.asset");
        if (bat != null)
            SetPrefabField<PlayerWeapons>("Assets/Prefabs/Player.prefab", "startingWeapon", p => p.objectReferenceValue = bat);
        else
            Debug.LogWarning("[MetaSetup] Bat.asset yok: önce TopDownShooter > Silahlar > Yeni Silahları Ekle.");

        AssetDatabase.SaveAssets();
        Debug.Log($"[MetaSetup] Hazır: {catalog.upgrades.Count} mağaza upgrade'i, {catalog.difficulties.Count} zorluk, " +
                  $"{catalog.characters.Count} karakter, " +
                  $"{catalog.lockables.Count} kilitli içerik " +
                  $"({locked} yeni kilit atandı). Başlangıç silahı: Bat.");
    }

    static void SetPrefabField<T>(string prefabPath, string field, System.Action<SerializedProperty> set) where T : Component
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var comp = prefab != null ? prefab.GetComponentInChildren<T>(true) : null;
        if (comp == null) { Debug.LogWarning($"[MetaSetup] {prefabPath} içinde {typeof(T).Name} yok."); return; }

        var so = new SerializedObject(comp);
        set(so.FindProperty(field));
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(prefab);
    }

    // ---- Test araçları ----
    [MenuItem("TopDownShooter/Meta/Test - 1000 Core Ekle")]
    static void AddCore()
    {
        MetaProgress.AddCoreForTesting(1000);
        Debug.Log($"[Meta] Core: {MetaProgress.Core}");
    }

    [MenuItem("TopDownShooter/Meta/Test - İlerlemeyi Sıfırla")]
    static void ResetMeta()
    {
        if (!EditorUtility.DisplayDialog("Meta Sıfırla",
                "Core, mağaza seviyeleri ve ömür boyu sayaçlar silinecek (rekorlar kalır). Emin misin?", "Sıfırla", "Vazgeç"))
            return;
        MetaProgress.ResetAll();
        Debug.Log("[Meta] Kalıcı ilerleme sıfırlandı.");
    }

    [MenuItem("TopDownShooter/Meta/Durumu Yazdır")]
    static void PrintStatus()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Meta] Core: {MetaProgress.Core}  (toplam kazanılan {MetaProgress.CoreEarned})");
        sb.AppendLine($"Toplam öldürme {MetaProgress.TotalKills}, kritik {MetaProgress.TotalCrits}, boss {MetaProgress.BossKills}, oyun {BestRecords.TotalRuns}");
        var catalog = MetaProgress.Catalog;
        if (catalog != null)
        {
            foreach (var u in catalog.upgrades)
                if (u != null) sb.AppendLine($"  {u.title}: {u.Level}/{u.MaxLevel}  ({u.FormatValue(u.TotalValue)})");
            foreach (var u in catalog.lockables)
                if (u != null)
                    sb.AppendLine($"  {(u.IsMetaUnlocked ? "AÇIK " : "KİLİT")} {u.title}: " +
                                  $"{MetaProgress.Describe(u.unlockCondition, u.unlockThreshold)} " +
                                  $"({MetaProgress.ProgressText(u.unlockCondition, u.unlockThreshold)})");
        }
        Debug.Log(sb.ToString());
    }
}
