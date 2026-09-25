using UnityEngine;

// Bir zorluk seviyesi: düşman çarpanları + Core ödül çarpanı + kilit koşulu.
// Kurulum menüsü (TopDownShooter > Meta > Kurulumu Yap) Normal/Hard/Nightmare'i üretip
// MetaCatalog.difficulties'e ekler; değerleri Inspector'dan değiştirebilirsin.
[CreateAssetMenu(menuName = "TopDownShooter/Meta/Difficulty", fileName = "NewDifficulty")]
public class DifficultyData : ScriptableObject
{
    [Tooltip("Kayıt anahtarı (seçim ve zorluk bazlı en iyi dalga bununla saklanır). SONRADAN DEĞİŞTİRME.")]
    public string id = "normal";
    public string title = "NORMAL";
    [TextArea] public string description = "";
    public Color color = Color.white;

    [Header("Düşman çarpanları")]
    [Min(0.1f)] public float enemyHealth = 1f;
    [Min(0.1f)] public float enemyDamage = 1f;
    [Min(0.1f)] public float enemySpeed = 1f;

    [Header("Ödül")]
    [Tooltip("Oyun sonunda kazanılan Core bu oranla çarpılır.")]
    [Min(0f)] public float coreMultiplier = 1f;

    [Header("Kilit")]
    [Tooltip("Boşsa baştan açık. Doluysa: bu zorlukta (ya da daha zorunda) Required Wave'e ulaşmak gerekir.")]
    public DifficultyData requires;
    [Min(0)] public int requiredWave = 10;

    public bool IsUnlocked => requires == null || Difficulty.BestWave(requires, orHarder: true) >= requiredWave;

    public string LockText => requires == null ? "" : Loc.F("Reach wave {0} on {1}", requiredWave, Loc.T(requires.title));
}
