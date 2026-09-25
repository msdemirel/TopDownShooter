using System.Collections.Generic;

// İki skill aynı anda slottayken açılan bonuslar. Etkileri PlayerSkills uygular
// (bkz. PlayerSkills.HasSynergy çağrıları); burada sadece tanımlar ve metinler var.
// Oyun içi metinler İngilizce.
public enum SynergyId
{
    Conductive,    // Frost Nova + Chain Lightning
    ThermalShock,  // Frost Nova + Area Blast
    ColdFront,     // Pulse Wave + Frost Nova
    BlazingDash,   // Dash + Fire Burst
    Afterburner,   // Overdrive + Dash
    Supercharge,   // Overdrive + Chain Lightning
    Repulsor,      // Shield + Pulse Wave
    Guardian,      // Second Wind + Shield
}

public static class SkillSynergies
{
    // ---- Etki değerleri (denge için tek yer) ----
    public const float ConductiveMultiplier = 2f;      // yavaşlamış düşmana şimşek hasarı
    public const float ThermalShockMultiplier = 1.75f; // yavaşlamış düşmana patlama hasarı
    public const float ColdFrontSlowFactor = 0.6f;     // Frost Nova'nın yavaşlatma oranının bu kadarı
    public const float ColdFrontSlowDuration = 1.5f;
    public const float BlazingDashDamageFactor = 0.6f; // Fire Burst mermi hasarının bu kadarı
    public const float AfterburnerCooldownFactor = 0.5f;
    public const int SuperchargeExtraChains = 2;
    public const float GuardianShieldDuration = 1.5f;

    public class Def
    {
        public SynergyId id;
        public string name;
        public SkillType a, b;
        public string description;

        public bool Involves(SkillType t) => a == t || b == t;
        public SkillType Partner(SkillType t) => a == t ? b : a;
    }

    public static readonly Def[] All =
    {
        new Def { id = SynergyId.Conductive, name = "Conductive", a = SkillType.FrostNova, b = SkillType.ChainLightning,
                  description = "Chain Lightning deals double damage to slowed enemies." },
        new Def { id = SynergyId.ThermalShock, name = "Thermal Shock", a = SkillType.FrostNova, b = SkillType.AreaBlast,
                  description = "Area Blast deals +75% damage to slowed enemies." },
        new Def { id = SynergyId.ColdFront, name = "Cold Front", a = SkillType.PulseWave, b = SkillType.FrostNova,
                  description = "Pulse Wave rings slow the enemies they hit." },
        new Def { id = SynergyId.BlazingDash, name = "Blazing Dash", a = SkillType.Dash, b = SkillType.Burst,
                  description = "Dashing releases a burst of fireballs where you land." },
        new Def { id = SynergyId.Afterburner, name = "Afterburner", a = SkillType.Overdrive, b = SkillType.Dash,
                  description = "Dash cooldown is halved during Overdrive." },
        new Def { id = SynergyId.Supercharge, name = "Supercharge", a = SkillType.Overdrive, b = SkillType.ChainLightning,
                  description = "During Overdrive, Chain Lightning jumps to 2 more enemies." },
        new Def { id = SynergyId.Repulsor, name = "Repulsor", a = SkillType.Shield, b = SkillType.PulseWave,
                  description = "When Shield ends, it releases a pulse wave." },
        new Def { id = SynergyId.Guardian, name = "Guardian", a = SkillType.Heal, b = SkillType.Shield,
                  description = "Second Wind also shields you for 1.5s." },
    };

    public static Def Get(SynergyId id)
    {
        foreach (var d in All) if (d.id == id) return d;
        return null;
    }

    public static IEnumerable<Def> For(SkillType t)
    {
        foreach (var d in All) if (d.Involves(t)) yield return d;
    }

    // Kart/HUD metinlerinde skill türünün görünen adı (asset başlığıyla aynı)
    public static string SkillName(SkillType t)
    {
        switch (t)
        {
            case SkillType.AreaBlast: return "Area Blast";
            case SkillType.PulseWave: return "Pulse Wave";
            case SkillType.Burst: return "Fire Burst";
            case SkillType.Heal: return "Second Wind";
            case SkillType.FrostNova: return "Frost Nova";
            case SkillType.ChainLightning: return "Chain Lightning";
            default: return t.ToString();
        }
    }
}
