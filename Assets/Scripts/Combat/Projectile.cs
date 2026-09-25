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

    // Atış anında silah açabilir (WeaponData.pierce); prefab'daki ayarı kapatmaz.
    public void EnablePierce() => pierce = true;

    bool spent;   // normal mermi isabet etti mi (Destroy kare sonunda çalışır; o ana kadar ikinci isabeti engeller)

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (spent) return;

        // Hasar alabilen bir şeye çarptıysak
        if (other.TryGetComponent<Health>(out var hp))
        {
            if (hp.Team == team) return;  // aynı takım: içinden geç, yok olma

            // Normal mermi: hasardan ÖNCE kendini harca ve yok et. Böylece hasar olaylarını
            // dinleyen bir script hata fırlatsa bile mermi düşmanın içinden geçip gitmez.
            // Pierce açıksa yok olma, yoluna devam et (her düşmana OnTriggerEnter bir kez tetiklenir)
            if (!pierce)
            {
                spent = true;
                if (TryGetComponent<Collider2D>(out var col)) col.enabled = false;
                if (TryGetComponent<Rigidbody2D>(out var rb)) rb.linearVelocity = Vector2.zero;
                Destroy(gameObject);
            }

            SpawnHit();
            hp.TakeDamage(damage, isCrit);
        }
    }

    void SpawnHit()
    {
        if (hitEffect != null)
            Instantiate(hitEffect, transform.position, Quaternion.identity);
    }
}
