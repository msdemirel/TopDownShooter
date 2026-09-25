using UnityEngine;

public enum StatType
{
    MaxHealth,    // maks. canı artırır (eklenen kadarını da doldurur)
    MoveSpeed,    // hareket hızını artırır
    Damage,       // TÜM silahların hasarını oransal artırır
    FireRate,     // TÜM silahların atış hızını oransal artırır
    Heal,         // anında can doldurur
    MagnetRange,  // pickup çekim menzilini artırır (PickupMagnet)
    CritChance,   // TÜM silahlara kritik şansı ekler (0.05 = +%5)
}

// Oyuncunun bir istatistiğini KADEMELİ artıran upgrade.
// Örn. tierAmounts = [2, 5, 10] -> ilk seçilişte +2 can, ikincide +5, üçüncüde +10.
// Kademeler sırayla gelir; hepsi alınınca bu upgrade artık teklif edilmez.
[CreateAssetMenu(menuName = "TopDownShooter/Upgrades/Stat", fileName = "NewStatUpgrade")]
public class StatUpgradeData : UpgradeData
{
    public StatType stat = StatType.MaxHealth;

    [Tooltip("Kademe miktarları: 1. seçilişte element 0, 2. seçilişte element 1... " +
             "MaxHealth / MoveSpeed / Heal: doğrudan miktar (ör. 20 can). " +
             "Damage / FireRate: oran (0.1 = %10 artış). " +
             "Tek elemanlı bırakırsan tek seferlik upgrade olur.")]
    public float[] tierAmounts = { 10f };

    [Tooltip("Editor'de Kademe Sayısı artırılınca yeni kademeler bir öncekinden bu oranda BÜYÜR: " +
             "0.5 = %50 artış (ör. 2 -> 3 -> 4.5). Üretilen değerleri elle düzeltebilirsin.")]
    [Range(0f, 2f)] public float autoImprovePerTier = 0.5f;

    public override int TierCount => tierAmounts != null ? tierAmounts.Length : 0;

    // Açıklamadaki {0}, kademenin miktarıyla değiştirilir.
    // Altına kazanım satırı otomatik eklenir (yeşil): "+20 Maks Can" gibi.
    public override string GetDescription(int tier)
    {
        string text = Loc.F(description, GetAmount(tier));
        return text + "\n" + Green(BonusLabel(tier));
    }

    // HUD bildirimi: kazanım satırının aynısı ("+1 Hız").
    public override string GetBonusSummary(int tier) => Green(BonusLabel(tier));

    // Bu kademenin kazandırdığını kısa metin olarak yazar (oyun içi metinler İngilizce).
    // Damage/FireRate/CritChance oran tutar (0.1 = %10), o yüzden yüzdeye çevirip gösteririz.
    string BonusLabel(int tier)
    {
        float a = GetAmount(tier);
        switch (stat)
        {
            case StatType.MaxHealth: return Loc.F("+{0} Max Health", Num(a));
            case StatType.MoveSpeed: return Loc.F("+{0} Speed", Num(a));
            case StatType.Damage:    return Loc.F("+{0}% Damage", Num(a * 100f));
            case StatType.FireRate:  return Loc.F("+{0}% Fire Rate", Num(a * 100f));
            case StatType.Heal:      return Loc.F("+{0} Health", Num(a));
            case StatType.MagnetRange: return Loc.F("+{0} Pickup Range", Num(a));
            case StatType.CritChance: return Loc.F("+{0}% Crit Chance", Num(a * 100f));
            default: return "";
        }
    }

    float GetAmount(int tier)
    {
        if (tierAmounts == null || tierAmounts.Length == 0) return 0f;
        return tierAmounts[Mathf.Clamp(tier, 0, tierAmounts.Length - 1)];
    }

    public override void Apply(PlayerContext ctx, int tier)
    {
        float amount = GetAmount(tier);

        switch (stat)
        {
            case StatType.MaxHealth:
                if (ctx.health != null)
                {
                    // fill:false -> mevcut canı olduğu gibi bırak, sonra eklenen kadarını doldur
                    ctx.health.SetMaxHealth(ctx.health.Max + amount, false);
                    ctx.health.Heal(amount);
                }
                break;

            case StatType.MoveSpeed:
                if (ctx.movement != null) ctx.movement.MoveSpeed += amount;
                break;

            case StatType.Damage:
                if (ctx.weapons != null) ctx.weapons.AddDamageMultiplier(amount);
                break;

            case StatType.FireRate:
                if (ctx.weapons != null) ctx.weapons.AddFireRateMultiplier(amount);
                break;

            case StatType.Heal:
                if (ctx.health != null) ctx.health.Heal(amount);
                break;

            case StatType.MagnetRange:
                if (ctx.collector != null) ctx.collector.MagnetRange += amount;
                break;

            case StatType.CritChance:
                if (ctx.weapons != null) ctx.weapons.AddCritChance(amount);
                break;
        }
    }
}
