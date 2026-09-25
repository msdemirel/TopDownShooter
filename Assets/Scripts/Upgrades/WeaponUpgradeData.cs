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

    public override void Apply(PlayerContext ctx, int tier)
    {
        if (ctx.weapons != null) ctx.weapons.AddWeapon(weapon);
    }

    // Açıklamanın altına silahın değerlerini yeşil ekler (skill/stat kartlarıyla aynı düzen).
    public override string GetDescription(int tier)
    {
        if (weapon == null) return description;

        string stats = Green($"Damage: {Num(weapon.damage)}  Attacks: {Num(weapon.fireRate)}/s") + "\n" +
                       Green($"Range: {Num(weapon.range)}");
        return string.IsNullOrEmpty(description) ? stats : description + "\n" + stats;
    }

    // HUD bildirimi: yeni silah kazanıldı.
    public override string GetBonusSummary(int tier) => Green($"{title} acquired!");
}
