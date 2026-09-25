using UnityEngine;

// Ana menü mağazasındaki kalıcı upgrade'ler. Yeniler SONA eklenir (asset'ler sayı olarak saklar).
public enum MetaEffect
{
    MaxHealth,    // +değer max can
    Damage,       // +değer hasar çarpanı (0.05 = %5)
    MoveSpeed,    // +değer hareket hızı çarpanı (0.04 = %4)
    ExpGain,      // +değer exp çarpanı (0.1 = %10)
    StartMoney,   // oyun başında +değer para
    FreeRerolls,  // her oyunda +değer bedava reroll
    MagnetRange,  // +değer toplama menzili çarpanı (0.15 = %15)
    ExtraLife,    // +değer dirilme hakkı (%50 canla)
}

// Mağazadaki tek bir kalıcı upgrade (Core ile alınır, oyunlar arası kalır).
// Seviye MetaProgress'te (PlayerPrefs) tutulur — asset'e yazılmaz.
[CreateAssetMenu(menuName = "TopDownShooter/Meta/Upgrade", fileName = "NewMetaUpgrade")]
public class MetaUpgradeData : ScriptableObject
{
    [Tooltip("Kayıt anahtarı. SONRADAN DEĞİŞTİRME: oyuncunun aldığı seviyeler bu isimle saklanır.")]
    public string id = "upgrade";
    public string title = "Upgrade";
    [Tooltip("{0} = mevcut toplam etki, {1} = bir sonraki seviyedeki toplam etki (biçimli).")]
    [TextArea] public string description = "";
    public Sprite icon;

    public MetaEffect effect;
    [Tooltip("Seviye başına etki. Yüzdeli etkilerde oran: 0.05 = %5.")]
    public float valuePerLevel = 1f;
    [Tooltip("Her seviyenin fiyatı (Core). Eleman sayısı = maks seviye.")]
    public int[] costs = { 20 };

    public int MaxLevel => costs != null ? costs.Length : 0;
    public int Level => Mathf.Min(MetaProgress.GetLevel(id), MaxLevel);
    public bool IsMaxed => Level >= MaxLevel;
    public int NextCost => IsMaxed ? -1 : costs[Level];
    public float TotalValue => Level * valuePerLevel;

    // Yüzdeli etkiler oran olarak tutulur, ekranda yüzde gösterilir.
    public bool IsPercent => effect == MetaEffect.Damage || effect == MetaEffect.MoveSpeed
                          || effect == MetaEffect.ExpGain || effect == MetaEffect.MagnetRange;

    public string FormatValue(float v)
        => IsPercent ? $"+{(v * 100f).ToString("0.#")}%" : $"+{v.ToString("0.#")}";

    public string GetDescription()
        => string.Format(description, FormatValue(TotalValue), FormatValue(TotalValue + valuePerLevel));
}
