using UnityEngine;

// Başlangıç karakteri: görünüm (sprite sheet varyantı) + başlangıç silahı + pasif bonuslar
// + kalıcı kilit. Kurulum menüsü (TopDownShooter > Meta > Kurulumu Yap) 4 karakteri üretip
// MetaCatalog.characters'a ekler; değerleri Inspector'dan değiştirebilirsin.
[CreateAssetMenu(menuName = "TopDownShooter/Meta/Character", fileName = "NewCharacter")]
public class CharacterData : ScriptableObject
{
    [Tooltip("Kayıt anahtarı (seçim bununla saklanır). SONRADAN DEĞİŞTİRME.")]
    public string id = "vanguard";
    public string title = "VANGUARD";
    [TextArea] public string description = "";
    public Color color = Color.white;

    [Header("Görünüm")]
    [Tooltip("Hedef sprite sheet'in TÜM kareleri. Boşsa oyuncunun orijinal görünümü kullanılır.")]
    public Sprite[] skinSprites;
    [Tooltip("Orijinal karenin sheet'teki konumuna eklenecek kayma (piksel). Aynı sheet'teki " +
             "ikinci tasarım (robot) için (0, -224); renk varyantı sheet'lerinde (0, 0).")]
    public Vector2Int cellOffset;
    [Tooltip("Kayma'dan ÖNCE uygulanan kare eşlemesi (orijinal sheet konumları, piksel). " +
             "Ör. Juggernaut: silahlı yürüme kareleri -> aynı satırdaki 'no gun' kareleri.")]
    public CellRemap[] cellRemap;
    [Tooltip("Seçim ekranında oynatılan kareler (yürüme animasyonu).")]
    public Sprite[] previewFrames;

    [System.Serializable]
    public struct CellRemap
    {
        public Vector2Int from;
        public Vector2Int to;
    }

    // Orijinal karenin konumu -> karakterin sheet'indeki konum (eşleme + kayma)
    public Vector2Int MapCell(Vector2Int cell)
    {
        if (cellRemap != null)
            foreach (var r in cellRemap)
                if (r.from == cell) { cell = r.to; break; }
        return cell + cellOffset;
    }

    [Header("Başlangıç")]
    [Tooltip("Boşsa Player prefab'ındaki başlangıç silahı.")]
    public WeaponData startingWeapon;

    [Header("Pasif bonuslar (0 = etkisiz)")]
    public float maxHealth;        // düz: +50 / -15
    public float damage;           // oran: 0.15 = +%15
    public float fireRate;         // oran
    public float moveSpeed;        // oran: -0.12 = -%12
    public float critChance;       // eklenen: 0.1 = +%10
    public float magnetRange;      // oran

    [Header("Kilit")]
    public UnlockCondition unlockCondition = UnlockCondition.None;
    public float unlockThreshold;

    public bool IsUnlocked => MetaProgress.IsMet(unlockCondition, unlockThreshold);

    // Seçim ekranı için "+%15 Move Speed" gibi satırlar (renkli: artı yeşil, eksi kırmızı)
    public string BonusText()
    {
        var sb = new System.Text.StringBuilder();
        Line(sb, maxHealth, "Max Health", false);
        Line(sb, damage, "Damage", true);
        Line(sb, fireRate, "Fire Rate", true);
        Line(sb, moveSpeed, "Move Speed", true);
        Line(sb, critChance, "Crit Chance", true);
        Line(sb, magnetRange, "Pickup Range", true);
        return sb.Length > 0 ? sb.ToString().TrimEnd('\n') : "<color=#94B0C2>Balanced, no modifiers</color>";
    }

    static void Line(System.Text.StringBuilder sb, float v, string label, bool percent)
    {
        if (Mathf.Approximately(v, 0f)) return;
        string num = percent ? $"{(v * 100f):+0;-0}%" : $"{v:+0;-0}";
        sb.Append(v > 0f ? "<color=#5EDC5E>" : "<color=#E5533C>").Append(num).Append(' ').Append(label).Append("</color>\n");
    }
}
