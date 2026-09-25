using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Paketteki silah sprite'larından (Assets/Art/1.png) yeni silahlar üretir:
//   - WeaponData asset'i (Guns/<Ad>.asset): ateşliler Pistol'den, yakın dövüşler Greatsword'den kopyalanır
//   - Satın alma kartı ([W_UP] <Ad>.asset): fiyat, dalga kilidi, açıklama, ikon
//   - Kartlar sahnedeki UpgradeManager havuzuna eklenir
//
// Kullanım: MainGame sahnesi açıkken Menü > TopDownShooter > Silahlar > Yeni Silahları Ekle
// Tekrar çalıştırmak güvenli: varolan asset'ler kopyalanmaz. Denge değerleri (hasar, hız, fiyat,
// dalga kilidi...) SADECE ilk oluşturmada yazılır; sonra Inspector'da yaptığın ayarlar korunur.
// Tekrar çalıştırınca yalnızca isim/sprite/ikon/açıklama güncellenir ve eksik kartlar havuza eklenir.
public static class WeaponContentBuilder
{
    const string Guns = "Assets/Prefabs/Data/Guns/";
    const string Data = "Assets/Prefabs/Data/";
    const string Sheet = "Assets/Art/1.png";
    const string Icons = "Assets/Art/Generated_v2/Icons/";

    class Def
    {
        public string name, slice, icon, desc;
        public bool melee;
        public float damage, fireRate, range;
        // ateşli
        public float projSpeed = 12f, spread, inaccuracy = 5f;
        public int count = 1;
        public bool pierce;
        // yakın dövüş
        public float arc = 120f, lunge = 1.5f, swing = 0.3f;
        // ortak
        public float crit = 0.05f, critMult = 2f;
        public int cost, unlock;
    }

    // Fiyat/dalga değerleri denge turundaki tabloyla aynı (Tools/Balance/apply_balance.py).
    static readonly Def[] Weapons =
    {
        new Def { name = "SMG", slice = "1_60", icon = "weapon_smg", desc = "Sprays a fast stream of light bullets.",
                  damage = 5, fireRate = 7, range = 4.5f, projSpeed = 14, inaccuracy = 10, crit = 0.03f, cost = 25, unlock = 3 },
        new Def { name = "Shotgun", slice = "1_57", icon = "weapon_shotgun", desc = "Blasts a wide cone of pellets up close.",
                  damage = 7, fireRate = 0.9f, range = 3.5f, projSpeed = 11, count = 5, spread = 36, inaccuracy = 4, cost = 42, unlock = 5 },
        new Def { name = "Revolver", slice = "1_68", icon = "weapon_revolver", desc = "Slow, heavy shots with a high crit chance.",
                  damage = 26, fireRate = 1.1f, range = 6, projSpeed = 16, inaccuracy = 2, crit = 0.2f, critMult = 2.5f, cost = 36, unlock = 4 },
        new Def { name = "Assault Rifle", slice = "1_55", icon = "weapon_rifle", desc = "Steady, accurate automatic fire.",
                  damage = 9, fireRate = 4.5f, range = 6, projSpeed = 15, inaccuracy = 5, cost = 58, unlock = 7 },
        new Def { name = "Sniper Rifle", slice = "1_49", icon = "weapon_sniper", desc = "Long-range shots that pierce through enemies.",
                  damage = 55, fireRate = 0.55f, range = 10, projSpeed = 26, inaccuracy = 0, pierce = true, crit = 0.2f, critMult = 3f, cost = 68, unlock = 8 },
        new Def { name = "Minigun", slice = "1_52", icon = "weapon_minigun", desc = "Shreds everything in front of it.",
                  damage = 4, fireRate = 12, range = 5, projSpeed = 14, inaccuracy = 14, crit = 0.03f, cost = 90, unlock = 10 },
        new Def { name = "Katana", slice = "1_26", icon = "weapon_katana", desc = "Lightning-fast cuts in a narrow arc.", melee = true,
                  damage = 14, fireRate = 3, range = 2.4f, arc = 80, lunge = 2.2f, swing = 0.2f, crit = 0.1f, cost = 30, unlock = 3 },
        new Def { name = "Laser Sword", slice = "1_58", icon = "weapon_laser_sword", desc = "An energy blade that cleaves wide.", melee = true,
                  damage = 30, fireRate = 1.8f, range = 2.4f, arc = 160, lunge = 1.8f, swing = 0.3f, crit = 0.08f, cost = 52, unlock = 6 },
        new Def { name = "Bat", slice = "1_41", icon = "weapon_bat", desc = "Wide, heavy swings that hit everything nearby.", melee = true,
                  damage = 20, fireRate = 1.1f, range = 1.9f, arc = 200, lunge = 1f, swing = 0.35f, cost = 12, unlock = 1 },
    };

    // Silahın denge değerleri (sadece ilk oluşturmada yazılır)
    static void ApplyStats(SerializedObject w, Def d)
    {
        w.FindProperty("damage").floatValue = d.damage;
        w.FindProperty("fireRate").floatValue = d.fireRate;
        w.FindProperty("range").floatValue = d.range;
        w.FindProperty("critChance").floatValue = d.crit;
        w.FindProperty("critMultiplier").floatValue = d.critMult;
        if (d.melee)
        {
            w.FindProperty("meleeArc").floatValue = d.arc;
            w.FindProperty("lungeDistance").floatValue = d.lunge;
            w.FindProperty("swingTime").floatValue = d.swing;
        }
        else
        {
            w.FindProperty("projectileSpeed").floatValue = d.projSpeed;
            w.FindProperty("projectileCount").intValue = d.count;
            w.FindProperty("spreadAngle").floatValue = d.spread;
            w.FindProperty("inaccuracyAngle").floatValue = d.inaccuracy;
            w.FindProperty("pierce").boolValue = d.pierce;
        }
    }

    [MenuItem("TopDownShooter/Silahlar/Yeni Silahları Ekle")]
    static void Build()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Yeni Silahlar", "Önce Play Mode'dan çık.", "Tamam");
            return;
        }
        var manager = Object.FindAnyObjectByType<UpgradeManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            EditorUtility.DisplayDialog("Yeni Silahlar", "Sahnede UpgradeManager yok. Önce MainGame sahnesini aç.", "Tamam");
            return;
        }

        var sprites = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().ToDictionary(s => s.name);
        var pool = new SerializedObject(manager);
        var poolProp = pool.FindProperty("pool");
        int created = 0, added = 0;

        foreach (var d in Weapons)
        {
            if (!sprites.TryGetValue(d.slice, out var sprite))
            {
                Debug.LogWarning($"[WeaponContentBuilder] {Sheet} içinde '{d.slice}' yok, {d.name} atlandı.");
                continue;
            }

            // ---- WeaponData ----
            string wPath = $"{Guns}{d.name}.asset";
            bool newWeapon = AssetDatabase.LoadAssetAtPath<WeaponData>(wPath) == null;
            if (newWeapon)
            {
                AssetDatabase.CopyAsset(d.melee ? Guns + "Greatsword.asset" : Guns + "Pistol.asset", wPath);
                created++;
            }
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(wPath);
            var w = new SerializedObject(weapon);
            w.FindProperty("weaponName").stringValue = d.name;
            w.FindProperty("sprite").objectReferenceValue = sprite;
            w.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{Icons}{d.icon}.png");
            w.FindProperty("weaponType").enumValueIndex = (int)(d.melee ? WeaponType.Melee : WeaponType.Ranged);
            if (newWeapon) ApplyStats(w, d);
            w.ApplyModifiedPropertiesWithoutUndo();

            // ---- Satın alma kartı ----
            string uPath = $"{Data}[W_UP] {d.name}.asset";
            bool newCard = AssetDatabase.LoadAssetAtPath<WeaponUpgradeData>(uPath) == null;
            if (newCard)
            {
                AssetDatabase.CopyAsset(Data + "[W_UP]Pistol.asset", uPath);
                created++;
            }
            var up = AssetDatabase.LoadAssetAtPath<WeaponUpgradeData>(uPath);
            var u = new SerializedObject(up);
            u.FindProperty("title").stringValue = $"Buy +1 {d.name}";
            u.FindProperty("description").stringValue = d.desc;
            u.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{Icons}{d.icon}.png");
            u.FindProperty("weapon").objectReferenceValue = weapon;
            if (newCard)
            {
                u.FindProperty("unlockWave").intValue = d.unlock;
                u.FindProperty("lastWave").intValue = 0;
                u.FindProperty("cost").intValue = d.cost;
            }
            u.ApplyModifiedPropertiesWithoutUndo();

            // ---- Havuza ekle ----
            bool inPool = false;
            for (int i = 0; i < poolProp.arraySize; i++)
                if (poolProp.GetArrayElementAtIndex(i).objectReferenceValue == up) { inPool = true; break; }
            if (!inPool)
            {
                poolProp.arraySize++;
                poolProp.GetArrayElementAtIndex(poolProp.arraySize - 1).objectReferenceValue = up;
                added++;
            }
        }

        pool.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Debug.Log($"[WeaponContentBuilder] {Weapons.Length} silah hazır ({created} yeni asset, havuza {added} kart eklendi). " +
                  "Sahneyi kaydetmeyi unutma (Ctrl+S).");
    }
}
