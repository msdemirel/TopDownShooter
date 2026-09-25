using System.IO;
using UnityEditor;
using UnityEngine;

// Kalıcı ilerleme (meta) sistemini kurar / günceller:
//   - Mağaza upgrade'leri (Assets/Prefabs/Data/Meta/*.asset)
//   - Katalog (Assets/Resources/MetaCatalog.asset): upgrade listesi + kilitli içerikler
//   - Silah/skill kartlarına kalıcı kilit koşulları
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
        Debug.Log($"[MetaSetup] Hazır: {catalog.upgrades.Count} mağaza upgrade'i, {catalog.lockables.Count} kilitli içerik " +
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
