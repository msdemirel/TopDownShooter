using UnityEngine;

public enum SkillType
{
    Dash,       // hareket yönüne kısa süreli atılma
    AreaBlast,  // oyuncunun etrafındaki tüm düşmanlara anlık hasar
    Shield,     // süreli dokunulmazlık
    PulseWave,  // oyuncu merkezli, aralıklı N adet dışa büyüyen halka (her halka değince hasar)
    Burst,      // karakterin etrafındaki N noktadan dışa mermi fırlatır (fire spell gibi)
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
             "AreaBlast'te blastRadius'a göre otomatik ölçeklenir (görselin kenarı = hasar alanı).")]
    public GameObject effectPrefab;

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

    // tier 0 = skill'i almak (boş slot şart); tier 1+ = geliştirmek (slot şartı yok).
    public override bool CanOffer(PlayerContext ctx, int tier)
        => base.CanOffer(ctx, tier)
           && ctx.skills != null
           && (tier > 0 || ctx.skills.HasFreeSlot);

    public override void Apply(PlayerContext ctx, int tier)
    {
        if (ctx.skills != null) ctx.skills.AddOrUpgrade(this, tier);
    }

    // Açıklamanın altına kazanımları renkli ekler:
    // ilk alımda başlangıç değerleri, geliştirmede bir önceki seviyeye göre FARKLAR.
    public override string GetDescription(int tier)
    {
        string bonus = BuildBonusText(tier);
        if (string.IsNullOrEmpty(bonus)) return description;
        return string.IsNullOrEmpty(description) ? bonus : description + "\n" + bonus;
    }

    // HUD bildirimi: ilk alımda skill adı, geliştirmede sadece farklar.
    public override string GetBonusSummary(int tier)
        => tier == 0 ? Green($"{title} acquired!") : BuildDiffText(tier);

    string BuildBonusText(int tier)
        => tier == 0 ? BuildBaseStatsText() : BuildDiffText(tier);

    // İlk alım kartı: skill'in başlangıç değerlerini listele.
    string BuildBaseStatsText()
    {
        SkillLevelStats cur = GetLevel(0);
        var lines = new System.Collections.Generic.List<string>();

        lines.Add(Green($"Cooldown: {Num(cur.cooldown)}s"));
        switch (skillType)
        {
            case SkillType.Dash:
                lines.Add(Green($"Speed: {Num(cur.dashSpeed)}  Duration: {Num(cur.dashDuration)}s"));
                break;
            case SkillType.AreaBlast:
                lines.Add(Green($"Damage: {Num(cur.blastDamage)}  Radius: {Num(cur.blastRadius)}"));
                break;
            case SkillType.Shield:
                lines.Add(Green($"Duration: {Num(cur.shieldDuration)}s"));
                break;
            case SkillType.PulseWave:
                lines.Add(Green($"{cur.waveCount}x Waves  Damage: {Num(cur.waveDamage)}  Radius: {Num(cur.waveRadius)}"));
                break;
            case SkillType.Burst:
                lines.Add(Green($"{cur.burstCount}x Shots  Damage: {Num(cur.burstDamage)}"));
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
        }
        return string.Join("\n", lines);
    }

    // İki seviye arasındaki farkı "+4 Hasar" / "-1 sn Cooldown" biçiminde ekler.
    // İyileşme yeşil, kötüleşme kırmızı (cooldown'da azalmak iyidir).
    static void AddDiff(System.Collections.Generic.List<string> lines,
                        float from, float to, string label, string unit = "",
                        bool lowerIsBetter = false)
    {
        float d = to - from;
        if (Mathf.Approximately(d, 0f)) return;

        bool good = lowerIsBetter ? d < 0f : d > 0f;
        string text = $"{(d > 0f ? "+" : "")}{Num(d)}{unit} {label}";
        lines.Add(good ? Green(text) : Red(text));
    }
}
