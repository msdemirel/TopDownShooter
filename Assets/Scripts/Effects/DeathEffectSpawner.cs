using UnityEngine;

// Health.AnyDeath'i dinler: bir DÜŞMAN ölünce olduğu yerde ufak parçacık saçılması
// oluşturur. Sahneye BİR tane koymak yeterli — parçacıklar kodda üretilir, prefab gerekmez.
public class DeathEffectSpawner : MonoBehaviour
{
    [Header("Parçacıklar")]
    [SerializeField] int particleCount = 8;
    [Tooltip("Tek parçacığın boyutu (dünya birimi).")]
    [SerializeField] float particleSize = 0.12f;
    [Tooltip("Savrulma hızı (birim/sn).")]
    [SerializeField] float speed = 3f;
    [SerializeField] float lifetime = 0.4f;
    [SerializeField] Color color = new Color(1f, 1f, 1f, 0.9f);
    [Tooltip("Düşman sprite'larından önde çizilmesi için yüksek tut.")]
    [SerializeField] int sortingOrder = 90;

    [Header("Ek VFX (opsiyonel)")]
    [Tooltip("Ölüm anında spawn edilecek animasyon prefab'ı (ör. duman/patlama). Parçacıklara EK olarak.")]
    [SerializeField] GameObject deathVfx;

    void OnEnable() { Health.AnyDeath += HandleDeath; }
    void OnDisable() { Health.AnyDeath -= HandleDeath; }

    void HandleDeath(Health target)
    {
        if (target == null || target.Team != Team.Enemy) return;

        for (int i = 0; i < particleCount; i++)
            DeathParticle.Spawn(target.transform.position, color, particleSize,
                                speed, lifetime, sortingOrder);

        if (deathVfx != null)
            Instantiate(deathVfx, target.transform.position, Quaternion.identity);
    }
}
