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
        if (weapon == null) return description;

        string stats = Green($"Damage: {Num(weapon.damage)}  Attacks: {Num(weapon.fireRate)}/s") + "\n" +
                       Green($"Range: {Num(weapon.range)}");
        string text = string.IsNullOrEmpty(description) ? stats : description + "\n" + stats;

        // Sahip olunan bir kopyayla birleşecekse: hangi kademeye çıkacağını yaz
        var owner = PlayerWeapons.Current;
        if (owner != null && owner.CanMerge(weapon))
        {
            int t = owner.MergeResultTier(weapon);
            text += $"\n<color={WeaponTiers.Hex(t)}>MERGE: {weapon.weaponName} -> Tier {WeaponTiers.RomanNumeral(t)}" +
                    $" (x{Num(WeaponTiers.DamageMultiplier(t))} damage)</color>";
        }
        return text;
    }

    // HUD bildirimi: yeni silah kazanıldı.
    // Bayrak okunur okunmaz sıfırlanır: swap yolunda (Apply çağrılmaz) eski değer kalmasın
    public override string GetBonusSummary(int tier)
    {
        bool merged = lastApplyMerged;
        lastApplyMerged = false;
        return merged ? "" : Green($"{title} acquired!");
    }
}
