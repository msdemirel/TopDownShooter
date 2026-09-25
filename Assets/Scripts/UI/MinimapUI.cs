using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Ekranın köşesinde daire şeklinde, oyuncu merkezli radar/minimap.
// Oyuncu ortada, düşmanlar kırmızı nokta; EnemyRegistry her karede okunur (canlı).
//
// Kurulum: Canvas altında boş bir obje oluştur, bu component'i ekle. Görseller
// (çerçeve, arka plan, maske, noktalar) Awake'te kodla üretilir. Sprite alanları
// opsiyoneldir: boş bırakılanlar düz daire olarak çizilir.
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

    [Header("Sprite'lar (opsiyonel)")]
    [Tooltip("Arka planın ÜSTÜNE çizilen halka çerçeve. Atanırsa Border Color/Width yerine bu kullanılır.")]
    [SerializeField] Sprite frameSprite;
    [Tooltip("Çerçeve sprite'ı varken iç dairenin kenardan içeri boşluğu (halkanın kalınlığı).")]
    [SerializeField] float frameInset = 14f;
    [Tooltip("İç daire (radar zemini). Maske olarak da kullanılır: alfası daire olmalı.")]
    [SerializeField] Sprite backgroundSprite;
    [Tooltip("Oyuncu işareti. Yukarı bakan çizilmeli; oyuncunun gittiği yöne döner.")]
    [SerializeField] Sprite playerSprite;
    [SerializeField] Sprite enemySprite;
    [Tooltip("Menzil dışındaki düşmanlar için kenarda yön oku (yukarı bakan çizilmeli). Boşsa soluk nokta.")]
    [SerializeField] Sprite edgeSprite;
    [SerializeField] float edgeArrowSize = 12f;

    [Header("Radar Taraması (opsiyonel)")]
    [Tooltip("Dönen tarama dilimi. Boşsa tarama olmaz.")]
    [SerializeField] Sprite sweepSprite;
    [Tooltip("Derece/saniye.")]
    [SerializeField] float sweepSpeed = 120f;
    [SerializeField] Color sweepColor = new Color(1f, 1f, 1f, 0.8f);

    Transform player;
    RectTransform content;   // maskeli arka plan; noktalar bunun child'ı
    RectTransform sweep;
    RectTransform playerMarker;
    Vector2 lastPlayerPos;
    float playerAngle;
    readonly List<Image> enemyDots = new List<Image>();

    // ---- Ek işaretler (reaktörler vb.) ----
    // Başka sistemler kendi işaretlerini ekler, her karede pos/color/visible'ı günceller.
    // Menzil dışındaki işaret her zaman kenara yapışır (yönü gösterir). Düşman noktalarının üstünde çizilir.
    public class Marker
    {
        public Vector3 pos;
        public Sprite sprite;
        public Color color = Color.white;
        public float size = 16f;
        public bool visible = true;
        public float pulse;   // > 0: bu hızla büyüyüp küçülür (dikkat çekmesi gerekenler)
    }

    static readonly List<Marker> markers = new List<Marker>();
    readonly List<Image> markerImages = new List<Image>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => markers.Clear();

    public static Marker AddMarker(Vector3 pos, Sprite sprite, Color color, float size)
    {
        var m = new Marker { pos = pos, sprite = sprite, color = color, size = size };
        markers.Add(m);
        return m;
    }

    public static void RemoveMarker(Marker m) => markers.Remove(m);

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
        if (p != null)
        {
            player = p.transform;
            lastPlayerPos = player.position;
        }
    }

    // Sprite'sız:  daire kenar (bu obje) > maskeli arka plan > [tarama, düşmanlar, oyuncu]
    // Sprite'lı:   maskeli arka plan > [tarama, düşmanlar, oyuncu]  +  en üstte çerçeve halkası
    void BuildVisuals(RectTransform rt)
    {
        var border = GetComponent<Image>();
        if (border == null) border = gameObject.AddComponent<Image>();
        border.sprite = RuntimeSprite.Circle;
        border.color = borderColor;
        border.raycastTarget = false;
        border.enabled = frameSprite == null;   // çerçeve sprite'ı varsa düz kenar gerekmez

        float inset = frameSprite != null ? frameInset : borderWidth;
        var bg = CreateImage("Background", rt, backgroundColor, backgroundSprite);
        content = bg.rectTransform;
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(inset, inset);
        content.offsetMax = new Vector2(-inset, -inset);

        // Daire dışına taşan noktalar kırpılsın
        bg.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        if (sweepSprite != null)
        {
            var sw = CreateImage("Sweep", content, sweepColor, sweepSprite);
            sweep = sw.rectTransform;
            sweep.anchorMin = Vector2.zero;
            sweep.anchorMax = Vector2.one;
            sweep.offsetMin = sweep.offsetMax = Vector2.zero;
        }

        // Oyuncu en son eklenir -> düşman noktalarının üstünde çizilir (düşmanlar hep 0. sıraya girer)
        var playerDot = CreateImage("Player", content, playerColor, playerSprite);
        playerMarker = playerDot.rectTransform;
        playerMarker.sizeDelta = new Vector2(playerDotSize, playerDotSize);

        if (frameSprite != null)
        {
            var frame = CreateImage("Frame", rt, Color.white, frameSprite);
            frame.rectTransform.anchorMin = Vector2.zero;
            frame.rectTransform.anchorMax = Vector2.one;
            frame.rectTransform.offsetMin = frame.rectTransform.offsetMax = Vector2.zero;
        }
    }

    Image CreateImage(string name, Transform parent, Color color, Sprite sprite = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.sprite = sprite != null ? sprite : RuntimeSprite.Circle;
        img.preserveAspect = true;   // kare olmayan sprite'lar (kenar oku) basılmasın
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // LateUpdate: düşmanlar bu karedeki hareketlerini bitirdikten sonra çiz
    void LateUpdate()
    {
        if (player == null || content == null) return;

        if (sweep != null) sweep.Rotate(0f, 0f, -sweepSpeed * Time.deltaTime);   // saat yönünde
        UpdatePlayerMarker();

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
            bool outside = pos.magnitude > edge;

            if (outside)
            {
                if (!clampToEdge) continue;
                pos = pos.normalized * edge;
            }

            Image dot = GetDot(used++);
            RectTransform drt = dot.rectTransform;
            drt.anchoredPosition = pos;
            Color c = enemyColor;

            if (outside && edgeSprite != null)
            {
                // Kenar oku: düşmanın yönünü göstersin (sprite yukarı bakıyor)
                SetDotLook(dot, edgeSprite, edgeArrowSize);
                drt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(pos.y, pos.x) * Mathf.Rad2Deg - 90f);
            }
            else
            {
                SetDotLook(dot, enemySprite != null ? enemySprite : RuntimeSprite.Circle, enemyDotSize);
                drt.localRotation = Quaternion.identity;
            }

            if (outside) c.a *= edgeAlpha;
            dot.color = c;
        }

        // Bu karede kullanılmayan noktaları gizle (yok etme; havuzda kalsın)
        for (int i = used; i < enemyDots.Count; i++)
            if (enemyDots[i].gameObject.activeSelf) enemyDots[i].gameObject.SetActive(false);

        DrawMarkers(center, scale, mapRadius);
    }

    void DrawMarkers(Vector2 center, float scale, float mapRadius)
    {
        int used = 0;
        for (int i = 0; i < markers.Count; i++)
        {
            var m = markers[i];
            if (!m.visible) continue;

            float size = m.size * (m.pulse > 0f ? 1f + 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * m.pulse)) : 1f);
            Vector2 pos = ((Vector2)m.pos - center) * scale;
            float edge = mapRadius - size * 0.5f;
            if (pos.magnitude > edge) pos = pos.normalized * edge;   // menzil dışı: kenarda, yönünü gösterir

            if (used >= markerImages.Count)
            {
                var img = CreateImage("Marker", content, Color.white);
                img.preserveAspect = true;
                markerImages.Add(img);
            }
            var im = markerImages[used++];
            if (!im.gameObject.activeSelf) im.gameObject.SetActive(true);
            im.sprite = m.sprite != null ? m.sprite : RuntimeSprite.Circle;
            im.color = m.color;
            im.rectTransform.sizeDelta = new Vector2(size, size);
            im.rectTransform.anchoredPosition = pos;
        }
        for (int i = used; i < markerImages.Count; i++)
            if (markerImages[i].gameObject.activeSelf) markerImages[i].gameObject.SetActive(false);

        // Yeni işaret/düşman noktaları sona eklenir: oyuncu noktası hep en üstte kalsın
        if (playerMarker != null && playerMarker.GetSiblingIndex() != content.childCount - 1) playerMarker.SetAsLastSibling();
    }

    // Oyuncu işaretini son hareket yönüne yumuşakça çevirir (durunca son yönde kalır).
    void UpdatePlayerMarker()
    {
        if (playerMarker == null || playerSprite == null) return;   // düz dairede yön yok

        Vector2 now = player.position;
        Vector2 delta = now - lastPlayerPos;
        lastPlayerPos = now;
        if (delta.sqrMagnitude > 0.000001f)
            playerAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;

        float z = Mathf.LerpAngle(playerMarker.localEulerAngles.z, playerAngle, 12f * Time.deltaTime);
        playerMarker.localRotation = Quaternion.Euler(0f, 0f, z);
    }

    // Sprite ve boyutu sadece değiştiyse ata (her karede gereksiz layout yenilemesi olmasın).
    static void SetDotLook(Image dot, Sprite sprite, float size)
    {
        if (dot.sprite != sprite) dot.sprite = sprite;
        Vector2 sz = new Vector2(size, size);
        if (dot.rectTransform.sizeDelta != sz) dot.rectTransform.sizeDelta = sz;
    }

    Image GetDot(int index)
    {
        if (index >= enemyDots.Count)
        {
            var img = CreateImage("Enemy", content, enemyColor, enemySprite);
            img.rectTransform.sizeDelta = new Vector2(enemyDotSize, enemyDotSize);
            img.transform.SetSiblingIndex(0);   // oyuncu noktasının altında kalsın
            if (sweep != null) sweep.SetAsFirstSibling();   // tarama en altta, noktaları boyamasın
            enemyDots.Add(img);
        }

        Image dot = enemyDots[index];
        if (!dot.gameObject.activeSelf) dot.gameObject.SetActive(true);
        return dot;
    }
}
