using UnityEngine;

// Tek bir enemy türünün seviyelere göre istatistikleri.
// Davranış prefab'taki component (MeleeEnemy / RangedEnemy / ExploderEnemy) tarafından belirlenir;
// burada sadece sayısal değerler tutulur.
[System.Serializable]
public class EnemyLevelStats
{
    [Header("Genel")]
    public float maxHealth = 10f;
    public float moveSpeed = 2f;

    [Header("Saldırı")]
    public float attackDamage = 5f;     // melee vuruş / ranged mermi / exploder patlama hasarı
    public float attackRange = 1.5f;    // bu mesafede saldırır (exploder bunu kullanmaz)
    public float attackCooldown = 1f;   // saldırılar arası süre

    [Header("Ranged'e özel")]
    public float projectileSpeed = 8f;

    [Header("Exploder'a özel")]
    public float explosionRadius = 1.2f;  // bu mesafeye girince patlar + hasar yarıçapı

    [Header("Ödül")]
    [Tooltip("Ölünce LootDropper'ın zarı kaç kez atılır. Güçlü seviyeler daha çok exp/coin versin diye " +
             "artır (ör. Lv1-2 = 1, Lv3-4 = 2, Lv5+ = 3). 0 veya boş = 1.")]
    public int lootRolls = 1;
}

// Bir enemy türünü tanımlar: prefab + seviye seviye istatistikler.
[CreateAssetMenu(menuName = "TopDownShooter/Enemy Type", fileName = "NewEnemyType")]
public class EnemyTypeData : ScriptableObject
{
    public string typeName = "Enemy";
    public GameObject prefab;

    [Tooltip("Index 0 = Level 1, Index 1 = Level 2 ...")]
    public EnemyLevelStats[] levels = new EnemyLevelStats[1];

    // İstenen seviyenin verisini güvenli şekilde döndürür (taşmayı klamplar).
    public EnemyLevelStats GetLevel(int level)
    {
        if (levels == null || levels.Length == 0)
            return new EnemyLevelStats();

        int idx = Mathf.Clamp(level - 1, 0, levels.Length - 1);
        return levels[idx];
    }
}
