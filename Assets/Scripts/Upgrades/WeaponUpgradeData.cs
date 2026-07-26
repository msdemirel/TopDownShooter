using UnityEngine;

// Oyuncuya yeni bir silah verir. Kademeli değildir ama TEKRARLANABİLİR:
// boş slot olduğu sürece aynı silah tekrar teklif edilebilir (ikinci kopya yeni slota gider).
// Boş slot kalmadıysa panelde çıkmaz.
[CreateAssetMenu(menuName = "TopDownShooter/Upgrades/Weapon", fileName = "NewWeaponUpgrade")]
public class WeaponUpgradeData : UpgradeData
{
    [Tooltip("Verilecek silah.")]
    public WeaponData weapon;

    [Tooltip("Satın alma bedeli (para).")]
    public int cost = 100;

    public override int GetCost(int tier) => cost;

    // Kademe sınırı yok: kaç kez alındığına değil, boş slot olup olmadığına bakar.
    public override bool CanOffer(PlayerContext ctx, int tier)
        => weapon != null && ctx.weapons != null && ctx.weapons.HasFreeSlot;

    // Başlık numaralanmasın ("Shotgun 3" olmasın) — her seferinde aynı silah.
    public override string GetTitle(int tier) => title;

    public override void Apply(PlayerContext ctx, int tier)
    {
        if (ctx.weapons != null) ctx.weapons.AddWeapon(weapon);
    }

    // HUD bildirimi: yeni silah kazanıldı.
    public override string GetBonusSummary(int tier) => Green($"{title} acquired!");
}
