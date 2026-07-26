using UnityEngine;

// Basit flipbook animasyonu: sprite karelerini sırayla oynatır.
// Sprite-sheet'i (Sprite Mode = Multiple) grid'e bölüp, oluşan kareleri
// aşağıdaki 'frames' dizisine ata. Loop kapalıysa animasyon bitince obje
// kendini yok edebilir (tek seferlik patlama/vuruş efektleri için).
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAnimation : MonoBehaviour
{
    [Tooltip("Sıralı kareler (sprite-sheet'ten dilimlenmiş).")]
    [SerializeField] Sprite[] frames;
    [Tooltip("Saniyedeki kare sayısı.")]
    [SerializeField] float fps = 12f;
    [Tooltip("Bitince başa dönsün mü (kalkan/plazma gibi süreklilere aç).")]
    [SerializeField] bool loop = false;
    [Tooltip("Loop KAPALIYKEN animasyon bitince objeyi yok et (patlama/vuruş).")]
    [SerializeField] bool destroyOnEnd = true;

    SpriteRenderer sr;
    float t;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    void OnEnable()
    {
        t = 0f;
        if (frames != null && frames.Length > 0) sr.sprite = frames[0];
    }

    void Update()
    {
        if (frames == null || frames.Length == 0 || fps <= 0f) return;

        t += Time.deltaTime * fps;
        int i = (int)t;

        if (i >= frames.Length)
        {
            if (loop) { t = 0f; i = 0; }
            else if (destroyOnEnd) { Destroy(gameObject); return; }
            else { i = frames.Length - 1; enabled = false; }
        }

        sr.sprite = frames[i];
    }
}
