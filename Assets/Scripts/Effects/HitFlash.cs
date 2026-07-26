using System.Collections;
using UnityEngine;

// Tek bir objenin vuruş flaşı: sprite'ını kısa süreliğine renklendirir, sonra
// eski rengine döndürür. HitFlashSpawner ilk hasarda otomatik ekler — elle ekleme.
public class HitFlash : MonoBehaviour
{
    SpriteRenderer sr;
    Color originalColor;
    Coroutine running;

    void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) originalColor = sr.color;
    }

    public void Flash(Color color, float duration)
    {
        if (sr == null) return;

        // Üst üste vuruşta önceki flaşı iptal et: süre tazelensin, renk orijinale
        // yanlış anda dönmesin
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(FlashRoutine(color, duration));
    }

    IEnumerator FlashRoutine(Color color, float duration)
    {
        sr.color = color;
        yield return new WaitForSeconds(duration);
        sr.color = originalColor;
        running = null;
    }
}
