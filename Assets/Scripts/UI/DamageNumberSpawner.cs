using UnityEngine;

// Health.AnyDamaged'i dinler ve hasar alan objenin üstünde hasar yazısı doğurur.
// Sahneye BİR tane koymak yeterli — enemy prefab'larına hiçbir şey eklenmez.
public class DamageNumberSpawner : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("DamageNumber component'li yazı prefab'ı.")]
    [SerializeField] DamageNumber prefab;

    [Header("Kimler için gösterilsin")]
    [SerializeField] bool showEnemyDamage = true;
    [Tooltip("Oyuncunun aldığı hasar da yazsın mı (kırmızı).")]
    [SerializeField] bool showPlayerDamage = false;

    [Header("Görünüm")]
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color critColor = new Color(1f, 0.6f, 0.1f);   // turuncu
    [SerializeField] Color playerDamageColor = new Color(1f, 0.25f, 0.25f);
    [Tooltip("Kritik yazının boyut çarpanı (normal = 1).")]
    [SerializeField] float critScale = 1.4f;
    [Tooltip("Kritik vuruşta sayı yerine gösterilecek metin.")]
    [SerializeField] string critText = "Crit!";

    [Header("Konum")]
    [Tooltip("Yazı, vurulanın bu kadar üstünde belirir.")]
    [SerializeField] float spawnHeight = 0.6f;
    [Tooltip("Üst üste binmesin diye eklenen rastgele kaydırma yarıçapı.")]
    [SerializeField] float randomOffset = 0.25f;

    void OnEnable() { Health.AnyDamaged += HandleDamaged; }
    void OnDisable() { Health.AnyDamaged -= HandleDamaged; }

    void HandleDamaged(Health target, float amount, bool isCrit)
    {
        if (prefab == null || target == null) return;

        bool isPlayer = target.Team == Team.Player;
        if (isPlayer && !showPlayerDamage) return;
        if (!isPlayer && !showEnemyDamage) return;

        Vector3 pos = target.transform.position
                    + Vector3.up * spawnHeight
                    + (Vector3)(Random.insideUnitCircle * randomOffset);

        DamageNumber dn = Instantiate(prefab, pos, Quaternion.identity);

        // Kritikte sayı yerine "Crit!" yazılır; normalde yuvarlanmış hasar
        string label = isCrit ? critText : Mathf.Max(1, Mathf.RoundToInt(amount)).ToString();
        Color color = isPlayer ? playerDamageColor : (isCrit ? critColor : normalColor);
        dn.Init(label, color, isCrit ? critScale : 1f);
    }
}
