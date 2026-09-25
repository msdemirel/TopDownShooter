using UnityEngine;

public enum SkillType
{
    Dash,       // hareket yönüne kısa süreli atılma
    AreaBlast,  // oyuncunun etrafındaki tüm düşmanlara anlık hasar
    Shield,     // süreli dokunulmazlık
    PulseWave,  // oyuncu merkezli, aralıklı N adet dışa büyüyen halka (her halka değince hasar)
    Burst,      // karakterin etrafındaki N noktadan dışa mermi fırlatır (fire spell gibi)
    // Yeniler SONA eklenir: asset'ler enum'u sayı olarak saklar, araya eklemek eskileri bozar.
    Heal,           // anında can yeniler
    FrostNova,      // etraftaki düşmanlara hasar + süreli yavaşlatma
    Overdrive,      // süreli hasar ve atış hızı bonusu
    ChainLightning, // en yakın düşmandan başlayıp zincirleme sekiyor
}

// Bir skill seviyesinin sayıları. Yalnızca skillType'a ait alanlar kullanılır.
[System.Serializable]
public class SkillLevelStats
{
    [Tooltip("Kullanımlar arası bekleme (saniye).")]
    public float cooldown = 5f;

    [Header("Dash")]
    public float dashSpeed = 18f;
    public float dashDuration = 0.15f;

    [Header("Alan Hasarı (AreaBlast)")]
    public float blastDamage = 20f;
    public float blastRadius = 3f;

    [Header("Kalkan (Shield)")]
    public float shieldDuration = 2f;

    [Header("Pulse Dalgası (PulseWave)")]
    public int waveCount = 3;            // kaç halka atılacak
    public float waveInterval = 0.35f;   // halkalar arası bekleme
    public float waveDamage = 15f;       // her halkanın değdiği düşmana verdiği hasar
    public float waveRadius = 4f;        // halkanın büyüyeceği son yarıçap
    public float waveExpandTime = 0.3f;  // bir halkanın 0'dan son yarıçapa büyüme süresi

    [Header("Burst (radyal mermi — fire spell gibi)")]
    public int burstCount = 6;           // etrafta kaç noktadan atılacak
    public float burstDamage = 12f;      // mermi başına hasar
    public float burstSpeed = 8f;        // mermi hızı

    [Header("Can Yenileme (Heal)")]
    public float healAmount = 10f;

    [Header("Buz Dalgası (FrostNova)")]
    public float frostDamage = 8f;
    public float frostRadius = 3f;
    [Range(0f, 0.9f)] public float slowPercent = 0.5f;   // 0.5 = düşman %50 yavaşlar
    public float slowDuration = 2.5f;

    [Header("Overdrive (süreli güç)")]
    public float overdriveDuration = 4f;
    public float overdriveDamageBonus = 0.3f;     // 0.3 = +%30 hasar
    public float overdriveFireRateBonus = 0.3f;   // 0.3 = +%30 atış hızı

    [Header("Zincir Şimşek (ChainLightning)")]
    public float chainDamage = 15f;      // her sekmede verilen hasar
    public int chainCount = 3;           // toplam kaç düşman vurulur
    public float chainRange = 4f;        // ilk hedef ve sekmeler için maks mesafe
}

// Oyuncuya bir AKTİF yetenek kazandırır ve KADEMELİ geliştirir (EnemyTypeData.levels kalıbı):
// panelde ilk seçiliş skill'i verir (Index 0), sonraki seçilişler aynı skill'i bir üst
// seviyeye çıkarır. Kademeler sırayla gelir; hepsi alınınca artık teklif edilmez.
// İlk alım boş slot ister; geliştirmeler istemez (skill zaten slotunda duruyor).
[CreateAssetMenu(menuName = "TopDownShooter/Upgrades/Skill", fileName = "NewSkillUpgrade")]
public class SkillUpgradeData : UpgradeData
{
    [Header("Skill")]
    public SkillType skillType = SkillType.Dash;

    [Tooltip("Index 0 = skill'in ilk hali, Index 1 = ilk geliştirme... " +
             "Her seviyede istediğin alanı iyileştir (ör. cooldown düşür, hasar artır). " +
             "Tek eleman bırakırsan skill geliştirilemez olur.")]
    public SkillLevelStats[] levels = new SkillLevelStats[1];

    [Header("Efekt (opsiyonel)")]
    [Tooltip("Kullanınca oyuncunun üstünde beliren görsel. " +
             "Shield'de süre boyunca oyuncuya yapışık durur, diğerlerinde 1 sn sonra silinir. " +
             "AreaBlast'te patlamanın merkezinde büyüyüp sönen çekirdek olarak kullanılır (bkz. Effect Visual Scale).")]
    public GameObject effectPrefab;

    [Tooltip("SADECE AreaBlast: merkezdeki ateş topu çekirdeğinin boyut çarpanı. " +
             "Hasar alanını ETKİLEMEZ — patlamanın kenarı her zaman tam blastRadius'tur.")]
    [Range(0.2f, 2f)] public float effectVisualScale = 1f;

    [Header("Burst mermisi")]
    [Tooltip("Burst skillinde etrafa fırlatılacak mermi prefab'ı (Projectile + Rigidbody2D + Collider2D). " +
             "Sprite'ı SAĞA (+X) baksın; kod gidiş yönüne döndürür.")]
    public GameObject burstProjectile;
    [Tooltip("Mermiler karakterden bu kadar uzakta doğar (halka yarıçapı).")]
    public float burstSpawnRadius = 0.6f;

    [Header("Otomatik Seviye Üretimi (editor)")]
    [Tooltip("Seviye Sayısı artırılınca yeni seviyeler bir öncekinden bu oranda İYİLEŞİR: " +
             "0.05 = değerler %5 artar, cooldown %5 düşer. Üretilen değerleri elle düzeltebilirsin.")]
    [Range(0f, 0.9f)] public float autoImprovePerLevel = 0.05f;

    // Kademe sayısı = seviye sayısı. Başlık otomatik numaralanır ("Dash 2" gibi).
    public override int TierCount => levels != null ? levels.Length : 0;

    // İstenen seviyenin verisini güvenli şekilde döndürür (taşmayı klamplar).
    public SkillLevelStats GetLevel(int level)
    {
        if (levels == null || levels.Length == 0) return new SkillLevelStats();
        return levels[Mathf.Clamp(level, 0, levels.Length - 1)];
    }

    // tier 0 = skill'i almak: slot boşsa direkt yerleşir, doluysa oyuncu bir skill'i
    // değiştirir (swap, bkz. UpgradeManager). tier 1+ = geliştirmek: skill slotta olmalı.
    public override bool CanOffer(PlayerContext ctx, int tier)
        => base.CanOffer(ctx, tier)
           && ctx.skills != null
           && ctx.skills.SlotCount > 0
           && (tier == 0 ? !ctx.skills.HasSkill(this) : ctx.skills.HasSkill(this));

    public override void Apply(PlayerContext ctx, int tier)
    {
        if (ctx.skills != null) ctx.skills.AddOrUpgrade(this, tier);
    }

    // Açıklamanın altına kazanımları renkli ekler:
    // ilk alımda başlangıç değerleri, geliştirmede bir önceki seviyeye göre FARKLAR.
    public const string SynergyColor = "#C78BFF";   // mor: yeşil (kazanım) ve sarı (swap notu) ile karışmasın

    public override string GetDescription(int tier)
    {
        string text = Loc.T(description);   // açıklama çevrilir, skill ADI çevrilmez
        string bonus = BuildBonusText(tier);
        if (!string.IsNullOrEmpty(bonus)) text = string.IsNullOrEmpty(text) ? bonus : text + "\n" + bonus;

        // İlk alımda: slottaki bir skill'le açılacak sinerjiler (en fazla 2, kart taşmasın)
        string syn = tier == 0 ? BuildSynergyText(2) : "";
        if (!string.IsNullOrEmpty(syn)) text = string.IsNullOrEmpty(text) ? syn : text + "\n" + syn;
        return text;
    }

    string BuildSynergyText(int max)
    {
        var owner = PlayerSkills.Current;
        if (owner == null || owner.HasSkillType(skillType)) return "";

        var lines = new System.Collections.Generic.List<string>();
        foreach (var d in SkillSynergies.For(skillType))
        {
            if (lines.Count >= max) break;
            if (!owner.HasSkillType(d.Partner(skillType))) continue;
            lines.Add($"<color={SynergyColor}>{Loc.F("SYNERGY: {0}", d.name)}</color>\n<size=80%><color={SynergyColor}>{Loc.T(d.description)}</color></size>");
        }
        return string.Join("\n", lines);
    }

    // HUD bildirimi: ilk alımda skill adı, geliştirmede sadece farklar.
    public override string GetBonusSummary(int tier)
        => tier == 0 ? Green(Loc.F("{0} acquired!", title)) : BuildDiffText(tier);

    string BuildBonusText(int tier)
        => tier == 0 ? BuildBaseStatsText() : BuildDiffText(tier);

    // İlk alım kartı: skill'in başlangıç değerlerini listele.
    string BuildBaseStatsText()
    {
        SkillLevelStats cur = GetLevel(0);
        var lines = new System.Collections.Generic.List<string>();

        lines.Add(Green(Loc.F("Cooldown: {0}s", Num(cur.cooldown))));
        switch (skillType)
        {
            case SkillType.Dash:
                lines.Add(Green(Loc.F("Speed: {0}  Duration: {1}s", Num(cur.dashSpeed), Num(cur.dashDuration))));
                break;
            case SkillType.AreaBlast:
                lines.Add(Green(Loc.F("Damage: {0}  Radius: {1}", Num(cur.blastDamage), Num(cur.blastRadius))));
                break;
            case SkillType.Shield:
                lines.Add(Green(Loc.F("Duration: {0}s", Num(cur.shieldDuration))));
                break;
            case SkillType.PulseWave:
                lines.Add(Green(Loc.F("{0}x Waves  Damage: {1}  Radius: {2}", cur.waveCount, Num(cur.waveDamage), Num(cur.waveRadius))));
                break;
            case SkillType.Burst:
                lines.Add(Green(Loc.F("{0}x Shots  Damage: {1}", cur.burstCount, Num(cur.burstDamage))));
                break;
            case SkillType.Heal:
                lines.Add(Green(Loc.F("Heal: {0} HP", Num(cur.healAmount))));
                break;
            case SkillType.FrostNova:
                lines.Add(Green(Loc.F("Damage: {0}  Radius: {1}", Num(cur.frostDamage), Num(cur.frostRadius))));
                lines.Add(Green(Loc.F("Slow: {0}  for {1}s", Pct(cur.slowPercent), Num(cur.slowDuration))));
                break;
            case SkillType.Overdrive:
                lines.Add(Green(Loc.F("+{0} Damage  +{1} Fire Rate", Pct(cur.overdriveDamageBonus), Pct(cur.overdriveFireRateBonus))));
                lines.Add(Green(Loc.F("Duration: {0}s", Num(cur.overdriveDuration))));
                break;
            case SkillType.ChainLightning:
                lines.Add(Green(Loc.F("{0}x Chains  Damage: {1}", cur.chainCount, Num(cur.chainDamage))));
                lines.Add(Green(Loc.F("Range: {0}", Num(cur.chainRange))));
                break;
        }
        return string.Join("\n", lines);
    }

    // Geliştirme: bir önceki seviyeye göre sadece DEĞİŞEN değerlerin farkı.
    string BuildDiffText(int tier)
    {
        SkillLevelStats cur = GetLevel(tier);
        SkillLevelStats prev = GetLevel(tier - 1);
        var lines = new System.Collections.Generic.List<string>();

        AddDiff(lines, prev.cooldown, cur.cooldown, "Cooldown", "s", lowerIsBetter: true);
        switch (skillType)
        {
            case SkillType.Dash:
                AddDiff(lines, prev.dashSpeed, cur.dashSpeed, "Speed");
                AddDiff(lines, prev.dashDuration, cur.dashDuration, "Duration", "s");
                break;
            case SkillType.AreaBlast:
                AddDiff(lines, prev.blastDamage, cur.blastDamage, "Damage");
                AddDiff(lines, prev.blastRadius, cur.blastRadius, "Radius");
                break;
            case SkillType.Shield:
                AddDiff(lines, prev.shieldDuration, cur.shieldDuration, "Duration", "s");
                break;
            case SkillType.PulseWave:
                AddDiff(lines, prev.waveDamage, cur.waveDamage, "Damage");
                AddDiff(lines, prev.waveRadius, cur.waveRadius, "Radius");
                AddDiff(lines, prev.waveCount, cur.waveCount, "Waves");
                break;
            case SkillType.Burst:
                AddDiff(lines, prev.burstDamage, cur.burstDamage, "Damage");
                AddDiff(lines, prev.burstSpeed, cur.burstSpeed, "Speed");
                AddDiff(lines, prev.burstCount, cur.burstCount, "Shots");
                break;
            case SkillType.Heal:
                AddDiff(lines, prev.healAmount, cur.healAmount, "Heal");
                break;
            case SkillType.FrostNova:
                AddDiff(lines, prev.frostDamage, cur.frostDamage, "Damage");
                AddDiff(lines, prev.frostRadius, cur.frostRadius, "Radius");
                AddDiff(lines, prev.slowPercent * 100f, cur.slowPercent * 100f, "Slow", "%");
                AddDiff(lines, prev.slowDuration, cur.slowDuration, "Slow Duration", "s");
                break;
            case SkillType.Overdrive:
                AddDiff(lines, prev.overdriveDuration, cur.overdriveDuration, "Duration", "s");
                AddDiff(lines, prev.overdriveDamageBonus * 100f, cur.overdriveDamageBonus * 100f, "Damage", "%");
                AddDiff(lines, prev.overdriveFireRateBonus * 100f, cur.overdriveFireRateBonus * 100f, "Fire Rate", "%");
                break;
            case SkillType.ChainLightning:
                AddDiff(lines, prev.chainDamage, cur.chainDamage, "Damage");
                AddDiff(lines, prev.chainCount, cur.chainCount, "Chains");
                AddDiff(lines, prev.chainRange, cur.chainRange, "Range");
                break;
        }
        return string.Join("\n", lines);
    }

    // 0.3 -> "30%"
    static string Pct(float v) => $"{Num(v * 100f)}%";

    // İki seviye arasındaki farkı "+4 Hasar" / "-1 sn Cooldown" biçiminde ekler.
    // İyileşme yeşil, kötüleşme kırmızı (cooldown'da azalmak iyidir).
    static void AddDiff(System.Collections.Generic.List<string> lines,
                        float from, float to, string label, string unit = "",
                        bool lowerIsBetter = false)
    {
        float d = to - from;
        // Yuvarlanınca 0 görünen farkı yazma ("+0s Duration" gibi boş satır çıkmasın)
        if (Num(Mathf.Abs(d)) == "0") return;

        bool good = lowerIsBetter ? d < 0f : d > 0f;
        string text = $"{(d > 0f ? "+" : "")}{Num(d)}{unit} {Loc.T(label)}";
        lines.Add(good ? Green(text) : Red(text));
    }
}
