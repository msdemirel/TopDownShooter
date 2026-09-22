using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Ekranın köşesinde daire şeklinde, oyuncu merkezli radar/minimap.
// Oyuncu ortada, düşmanlar kırmızı nokta; EnemyRegistry her karede okunur (canlı).
//
// Kurulum: Canvas altında boş bir obje oluştur, bu component'i ekle. Görseller
// (çerçeve, arka plan, maske, noktalar) Awake'te kodla üretilir; sprite gerekmez.
// "Anchor Bottom Left" açıksa kendini sol alt köşeye yerleştirir.
//
// Neden kamera + RenderTexture değil: düşmanları nokta olarak göstermek için ikinci
// bir kamera gereksiz maliyet. Noktalar havuzdan gelir; her karede yeni obje üretilmez.
[RequireComponent(typeof(RectTransform))]
public class MinimapUI : MonoBehaviour
{
    [Header("Yerleşim")]
    [Tooltip("Açıksa Awake'te kendini sol alt köşeye yerleştirir (aşağıdaki boyut/boşlukla). " +
             "Kapalıysa RectTransform'u elle konumlandırırsın.")]
    [SerializeField] bool anchorBottomLeft = true;
    [SerializeField] float diameter = 220f;
    [SerializeField] Vector2 margin = new Vector2(20f, 20f);

    [Header("Ölçek")]
    [Tooltip("Minimap'in yarıçapı dünyada kaç birimi kapsar. Büyütürsen daha uzağı görürsün, noktalar sıklaşır.")]
    [SerializeField] float worldRadius = 15f;
    [Tooltip("Açıksa menzil dışındaki düşmanlar kenarda (yönünü gösteren) soluk nokta olarak görünür. " +
             "Kapalıysa hiç görünmez.")]
    [SerializeField] bool clampToEdge = true;
    [Range(0f, 1f)] [SerializeField] float edgeAlpha = 0.45f;

    [Header("Görünüm")]
    [SerializeField] Color backgroundColor = new Color(0.05f, 0.07f, 0.1f, 0.7f);
    [SerializeField] Color borderColor = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] float borderWidth = 3f;
    [SerializeField] Color playerColor = new Color(0.35f, 0.9f, 1f);
    [SerializeField] float playerDotSize = 12f;
    [SerializeField] Color enemyColor = new Color(1f, 0.2f, 0.2f);
    [SerializeField] float enemyDotSize = 8f;

    Transform player;
    RectTransform content;   // maskeli arka plan; noktalar bunun child'ı
    readonly List<Image> enemyDots = new List<Image>();

    void Awake()
    {
        var rt = (RectTransform)transform;
        if (anchorBottomLeft)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = margin;
            rt.sizeDelta = new Vector2(diameter, diameter);
        }

        BuildVisuals(rt);
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    // Çerçeve (bu obje) > maskeli arka plan > [düşman noktaları, oyuncu noktası]
    void BuildVisuals(RectTransform rt)
    {
        var border = GetComponent<Image>();
        if (border == null) border = gameObject.AddComponent<Image>();
        border.sprite = RuntimeSprite.Circle;
        border.color = borderColor;
        border.raycastTarget = false;

        var bg = CreateImage("Background", rt, backgroundColor);
        content = bg.rectTransform;
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(borderWidth, borderWidth);
        content.offsetMax = new Vector2(-borderWidth, -borderWidth);

        // Daire dışına taşan noktalar kırpılsın
        bg.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        // Oyuncu en son eklenir -> düşman noktalarının üstünde çizilir (SetAsLastSibling ile korunur)
        var playerDot = CreateImage("Player", content, playerColor);
        playerDot.rectTransform.sizeDelta = new Vector2(playerDotSize, playerDotSize);
    }

    Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.sprite = RuntimeSprite.Circle;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // LateUpdate: düşmanlar bu karedeki hareketlerini bitirdikten sonra çiz
    void LateUpdate()
    {
        if (player == null || content == null) return;

        float mapRadius = content.rect.width * 0.5f;
        float scale = mapRadius / Mathf.Max(0.01f, worldRadius);   // dünya birimi -> UI pikseli
        float edge = mapRadius - enemyDotSize * 0.5f;              // kenar noktası tam görünsün

        Vector2 center = player.position;
        var alive = EnemyRegistry.Alive;
        int used = 0;

        for (int i = 0; i < alive.Count; i++)
        {
            EnemyBase e = alive[i];
            if (e == null || e.IsDead) continue;

            Vector2 pos = ((Vector2)e.transform.position - center) * scale;
            float alpha = 1f;

            if (pos.magnitude > edge)
            {
                if (!clampToEdge) continue;
                pos = pos.normalized * edge;
                alpha = edgeAlpha;
            }

            Image dot = GetDot(used++);
            dot.rectTransform.anchoredPosition = pos;
            Color c = enemyColor;
            c.a *= alpha;
            dot.color = c;
        }

        // Bu karede kullanılmayan noktaları gizle (yok etme; havuzda kalsın)
        for (int i = used; i < enemyDots.Count; i++)
            if (enemyDots[i].gameObject.activeSelf) enemyDots[i].gameObject.SetActive(false);
    }

    Image GetDot(int index)
    {
        if (index >= enemyDots.Count)
        {
            var img = CreateImage("Enemy", content, enemyColor);
            img.rectTransform.sizeDelta = new Vector2(enemyDotSize, enemyDotSize);
            img.transform.SetSiblingIndex(0);   // oyuncu noktasının altında kalsın
            enemyDots.Add(img);
        }

        Image dot = enemyDots[index];
        if (!dot.gameObject.activeSelf) dot.gameObject.SetActive(true);
        return dot;
    }
}
