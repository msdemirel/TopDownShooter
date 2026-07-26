using UnityEngine;

// Pickup'a "cila" katar: sprite hafifçe yukarı-aşağı süzülür, nabız gibi büyüyüp
// küçülür ve arkasında yumuşak bir parıltı (glow) yanıp söner. Böylece yerdeki
// para/eşya daha belirgin ve toplanası görünür.
//
// ÖNEMLİ: Süzülme, KÖK transform'a değil sprite'ın kendi transform'una uygulanır.
// Çünkü PickupMagnet kökü oyuncuya doğru çeker; ikisi aynı transform'u oynatırsa çakışır.
// Bu yüzden sprite'ın pickup kökünün ALTINDA bir child obje olması gerekir (aşağıya bak).
public class PickupGlow : MonoBehaviour
{
    [Header("Görsel (boşsa child'daki SpriteRenderer bulunur)")]
    [Tooltip("Hareket ettirilecek sprite'ın transform'u. Pickup kökünün child'ı olmalı.")]
    [SerializeField] Transform visual;

    [Header("Süzülme (bob)")]
    [SerializeField] bool bob = true;
    [Tooltip("Yukarı-aşağı mesafe (dünya birimi).")]
    [SerializeField] float bobHeight = 0.08f;
    [SerializeField] float bobSpeed = 3f;

    [Header("Nabız (scale)")]
    [SerializeField] bool pulse = true;
    [Tooltip("Büyüyüp küçülme miktarı (0.08 = ±%8).")]
    [SerializeField] float pulseAmount = 0.08f;
    [SerializeField] float pulseSpeed = 3f;

    [Header("Parıltı (glow)")]
    [SerializeField] bool glow = true;
    [SerializeField] Color glowColor = new Color(1f, 0.9f, 0.3f);  // altın sarısı
    [Tooltip("Parıltının SPRITE'a göre boyutu. 1 = sprite kadar, 1.5 = %50 taşarak etrafını sarar.")]
    [SerializeField] float glowScale = 1.5f;
    [SerializeField] float glowMinAlpha = 0.25f;
    [SerializeField] float glowMaxAlpha = 0.55f;
    [SerializeField] float glowSpeed = 3f;

    Vector3 baseLocalPos;
    Vector3 baseScale;
    SpriteRenderer visualSr;   // paranın sprite'ı (yanıp sönmeyi buradan izleriz)
    SpriteRenderer haloSr;
    float phaseOffset;   // her pickup farklı fazda başlasın (hepsi senkron atmasın)

    void Awake()
    {
        if (visual == null)
        {
            visualSr = GetComponentInChildren<SpriteRenderer>();
            if (visualSr != null) visual = visualSr.transform;
        }
        else visual.TryGetComponent(out visualSr);

        if (visual == null)
        {
            Debug.LogWarning("[PickupGlow] Sprite bulunamadı (visual atanmamış).", this);
            enabled = false;
            return;
        }

        baseLocalPos = visual.localPosition;
        baseScale = visual.localScale;
        phaseOffset = Random.value * 10f;

        if (glow) CreateHalo();
    }

    // Parıltı: sprite'ın ARKASINA, onunla birlikte süzülsün diye child olarak koyar.
    void CreateHalo()
    {
        var go = new GameObject("Glow");
        go.transform.SetParent(visual, false);   // sprite ile birlikte süzülür + nabız atar
        go.transform.localPosition = Vector3.zero;

        // Boyutu paranın SPRITE boyutuna göre ayarla: hep etrafını sarsın
        float spriteSize = 1f;
        if (visualSr != null && visualSr.sprite != null)
        {
            Vector3 b = visualSr.sprite.bounds.size;   // yerel birim (transform scale hariç)
            spriteSize = Mathf.Max(b.x, b.y);
        }
        go.transform.localScale = Vector3.one * (spriteSize * glowScale);

        haloSr = go.AddComponent<SpriteRenderer>();
        haloSr.sprite = RuntimeSprite.Glow;
        haloSr.color = glowColor;

        // Sprite'ın hemen ARKASINA çiz (aynı katman, bir alt sıra)
        if (visualSr != null)
        {
            haloSr.sortingLayerID = visualSr.sortingLayerID;
            haloSr.sortingOrder = visualSr.sortingOrder - 1;
        }
    }

    void Update()
    {
        float t = Time.time + phaseOffset;

        // Süzülme: sprite'ın yerel y'sini baz konumun etrafında sallar
        if (bob)
        {
            Vector3 p = baseLocalPos;
            p.y += Mathf.Sin(t * bobSpeed) * bobHeight;
            visual.localPosition = p;
        }

        // Nabız: baz ölçeğin etrafında hafif büyüyüp küçül
        if (pulse)
        {
            float k = 1f + Mathf.Sin(t * pulseSpeed) * pulseAmount;
            visual.localScale = baseScale * k;
        }

        // Parıltı
        if (glow && haloSr != null)
        {
            // Ömrü dolarken PickupLifetime para sprite'ının enabled'ını aç/kapatarak
            // yanıp söndürüyor. Glow da onunla birlikte sönsün diye aynısını izleriz.
            if (visualSr != null) haloSr.enabled = visualSr.enabled;

            // Alfa nabzı (0..1 arası sin'i alfa aralığına eşle)
            float a = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, (Mathf.Sin(t * glowSpeed) + 1f) * 0.5f);
            Color c = glowColor;
            c.a = a;
            haloSr.color = c;
        }
    }
}
