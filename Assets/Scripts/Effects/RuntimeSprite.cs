using UnityEngine;

// Kodda üretilen görseller (can barı, ölüm partikülü...) için ortak 1x1 beyaz sprite.
// Renk SpriteRenderer.color ile verilir; boyut transform scale ile.
public static class RuntimeSprite
{
    static Sprite white;
    static Sprite glow;
    static Sprite circle;

    // Kenarı yumuşatılmış (anti-aliased) dolu beyaz daire. UI Image'larında (minimap) kullanılır;
    // rengi Image.color ile, boyutu RectTransform ile verilir.
    public static Sprite Circle
    {
        get
        {
            if (circle == null)
            {
                const int size = 128;
                var tex = new Texture2D(size, size) { wrapMode = TextureWrapMode.Clamp };
                float r = size * 0.5f;

                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d);   // kenarda 1 piksellik yumuşak geçiş
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            return circle;
        }
    }

    public static Sprite White
    {
        get
        {
            // == null kontrolü Play bitince yok edilen eski sprite'ı da yakalar (Unity null'u)
            if (white == null)
            {
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                // pixelsPerUnit = 1 -> sprite tam 1x1 dünya birimi
                white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            return white;
        }
    }

    // Merkezi opak, kenarlara doğru saydamlaşan yumuşak yuvarlak parıltı.
    // Rengi SpriteRenderer.color ile verilir; boyutu transform scale ile.
    public static Sprite Glow
    {
        get
        {
            if (glow == null)
            {
                const int size = 64;
                var tex = new Texture2D(size, size) { wrapMode = TextureWrapMode.Clamp };
                float r = size * 0.5f;

                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - r) / r;
                    float dy = (y + 0.5f - r) / r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);   // 0 merkez .. 1 kenar
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;                                     // yumuşak sönüm
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                tex.Apply();
                // pixelsPerUnit = size -> sprite 1x1 dünya birimi; boyut scale ile ayarlanır
                glow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            return glow;
        }
    }
}
