using UnityEngine;

// Oyuncuya yeni bir silah verir. Kademeli değildir ama TEKRARLANABİLİR:
// aynı silah tekrar teklif edilebilir (ikinci kopya yeni slota gider).
// Slotlar doluysa da teklif edilir: seçilirse oyuncu bir silahı değiştirir (swap, bkz. UpgradeManager).
[CreateAssetMenu(menuName = "TopDownShooter/Upgrades/Weapon", fileName = "NewWeaponUpgrade")]
public class WeaponUpgradeData : UpgradeData
{
    [Tooltip("Verilecek silah.")]
    public WeaponData weapon;

    [Tooltip("Satın alma bedeli (para).")]
    public int cost = 100;

    public override int GetCost(int tier) => cost;

    // Kademe sınırı yok: sadece dalga kilidine bakar. Slotlar doluysa swap ile alınır.
    public override bool CanOffer(PlayerContext ctx, int tier)
        => IsUnlocked(ctx) && weapon != null && ctx.weapons != null;

    // Başlık numaralanmasın ("Shotgun 3" olmasın) — her seferinde aynı silah.
    public override string GetTitle(int tier) => title;

    // Son Apply bir birleşme miydi? (birleşmede bildirimi PlayerWeapons'ın merge olayı verir)
    [System.NonSerialized] bool lastApplyMerged;

    public override void Apply(PlayerContext ctx, int tier)
    {
        if (ctx.weapons == null) return;
        lastApplyMerged = ctx.weapons.CanMerge(weapon);
        ctx.weapons.AddWeapon(weapon);
    }

    // Açıklamanın altına silahın değerlerini yeşil ekler (skill/stat kartlarıyla aynı düzen).
    public override string GetDescription(int tier)
    {
        if (weapon == null) return Loc.T(description);

        string stats = Green(Loc.F("Damage: {0}  Attacks: {1}/s", Num(weapon.damage), Num(weapon.fireRate))) + "\n" +
                       Green(Loc.F("Range: {0}", Num(weapon.range)));
        string text = string.IsNullOrEmpty(description) ? stats : Loc.T(description) + "\n" + stats;

        // Sahip olunan bir kopyayla birleşecekse: hangi kademeye çıkacağını yaz
        var owner = PlayerWeapons.Current;
        if (owner != null && owner.CanMerge(weapon))
        {
            int t = owner.MergeResultTier(weapon);
            text += $"\n<color={WeaponTiers.Hex(t)}>" +
                    Loc.F("MERGE: {0} -> Tier {1} (x{2} damage)", weapon.weaponName, WeaponTiers.RomanNumeral(t),
                          Num(WeaponTiers.DamageMultiplier(t))) + "</color>";
        }
        return text;
    }

    // HUD bildirimi: yeni silah kazanıldı.
    // Bayrak okunur okunmaz sıfırlanır: swap yolunda (Apply çağrılmaz) eski değer kalmasın
    public override string GetBonusSummary(int tier)
    {
        bool merged = lastApplyMerged;
        lastApplyMerged = false;
        return merged ? "" : Green(Loc.F("{0} acquired!", title));
    }
}
