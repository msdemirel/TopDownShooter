using UnityEngine;

// Health.AnyDamaged'i dinler: bir düşman İLK kez hasar alınca üstüne can barı takar.
// Sahneye BİR tane koymak yeterli. Bar görseli kodda üretilir — PREFAB GEREKMEZ;
// görünüm aşağıdaki ayarlarla değiştirilir.
// (Hasar almamış düşmanda bar yok; bar ölümle birlikte kendini yok eder.)
public class EnemyHealthBarSpawner : MonoBehaviour
{
    [Header("Konum")]
    [Tooltip("Barın düşman merkezine göre konumu (genelde biraz üstü).")]
    [SerializeField] Vector3 offset = new Vector3(0f, 0.8f, 0f);

    [Header("Görünüm")]
    [Tooltip("Barın genişliği (dünya birimi).")]
    [SerializeField] float width = 1f;
    [Tooltip("Barın yüksekliği (dünya birimi).")]
    [SerializeField] float height = 0.12f;
    [SerializeField] Color bgColor = new Color(0f, 0f, 0f, 0.7f);
    [SerializeField] Color fillColor = new Color(0.9f, 0.15f, 0.15f);
    [Tooltip("Düşman sprite'larından önde çizilmesi için yüksek tut.")]
    [SerializeField] int sortingOrder = 100;

    void OnEnable() { Health.AnyDamaged += HandleDamaged; }
    void OnDisable() { Health.AnyDamaged -= HandleDamaged; }

    void HandleDamaged(Health target, float amount, bool isCrit)
    {
        if (target == null || target.Team != Team.Enemy || target.IsDead) return;

        // Zaten barı varsa ikinciyi takma
        if (target.GetComponentInChildren<EnemyHealthBar>() != null) return;

        EnemyHealthBar.Create(target, offset, width, height, bgColor, fillColor, sortingOrder);
    }
}
