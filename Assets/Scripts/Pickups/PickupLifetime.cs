using UnityEngine;

// Yere düşen Pickup'a (exp / ammo / health) takılır.
// Belli süre sonra kaybolur; son saniyelerde yanıp sönerek "almazsan gideceğim" uyarısı verir.
// Player toplayınca Pickup zaten objeyi Destroy ediyor; bu script de onunla gider.
public class PickupLifetime : MonoBehaviour
{
    [Header("Ömür")]
    [SerializeField] float lifeTime = 8f;         // Kaç saniye sonra kaybolsun

    [Header("Yanıp Sönme")]
    [SerializeField] float blinkDuration = 2f;    // Son kaç saniye yanıp sönsün
    [SerializeField] float blinkInterval = 0.15f; // Sönme hızı (küçük = daha hızlı)

    SpriteRenderer sr;
    float timer;
    float blinkTimer;

    void Awake()
    {
        // Sprite bu objede yoksa child'larda ara.
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        timer += Time.deltaTime;

        // Süre doldu -> yok ol.
        if (timer >= lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        // Son 'blinkDuration' saniyede yanıp sön.
        float timeLeft = lifeTime - timer;
        if (timeLeft <= blinkDuration && sr != null)
        {
            blinkTimer += Time.deltaTime;
            if (blinkTimer >= blinkInterval)
            {
                sr.enabled = !sr.enabled;   // görünürlüğü aç/kapa
                blinkTimer = 0f;
            }
        }
    }

    void OnDisable()
    {
        // Emniyet: kapanırken sprite görünür kalsın.
        if (sr != null) sr.enabled = true;
    }
}
