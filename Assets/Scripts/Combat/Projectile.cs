using UnityEngine;

// Hem player hem enemy mermisi için ortak script.
// team alanı sayesinde mermi kendi takımına hasar vermez (friendly-fire kapalı).
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [Header("Runtime (atış anında set edilir)")]
    public Team team = Team.Enemy;
    public float damage = 1f;
    public bool isCrit;   // hasar yazısının rengi için taşınır; hasar zaten çarpılmış gelir

    [Header("Settings")]
    [SerializeField] float lifetime = 3f;
    [Tooltip("Açıksa: değdiği düşmana hasar verir ama YOK OLMAZ, lifetime bitene kadar delip geçer. " +
             "Kapalıysa ilk isabette yok olur (normal mermi).")]
    [SerializeField] bool pierce = false;
    [Tooltip("Bir şeye isabet edince o noktada beliren efekt (opsiyonel). Tek seferlik animasyon prefab'ı.")]
    [SerializeField] GameObject hitEffect;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Hasar alabilen bir şeye çarptıysak
        if (other.TryGetComponent<Health>(out var hp))
        {
            if (hp.Team == team) return;  // aynı takım: içinden geç, yok olma

            hp.TakeDamage(damage, isCrit);
            SpawnHit();

            // Pierce açıksa yok olma, yoluna devam et (her düşmana OnTriggerEnter bir kez tetiklenir)
            if (!pierce) Destroy(gameObject);
        }
    }

    void SpawnHit()
    {
        if (hitEffect != null)
            Instantiate(hitEffect, transform.position, Quaternion.identity);
    }
}
