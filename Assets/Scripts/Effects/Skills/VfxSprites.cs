using UnityEngine;

// Skill efektlerinin KODDA üretilen dokuları (RuntimeSprite kalıbı). Hepsi beyaz;
// renk SpriteRenderer.color ile verilir. Hepsi 1 dünya birimi = yarıçap 0.5 olacak
// şekilde üretilir, yani transform scale = ÇAP (Ring/Disc için scale = 2 * yarıçap).
// Yumuşak (bilinear) stil: README'deki "efekt katmanı yumuşak" kuralıyla uyumlu.
public static class VfxSprites
{
    static Sprite ring, thinRing, disc, shard, snowflake, plus, spark, hex;

    // Şok dalgası bandı: dış kenar keskin ve parlak, içe doğru yumuşakça söner (arkada iz bırakır).
    public static Sprite Ring => ring ??= Make(256, r =>
    {
        float outer = Mathf.Clamp01((0.99f - r) / 0.02f);       // dış kenarda 1-2 piksel yumuşatma
        float trail = Mathf.Clamp01((r - 0.55f) / 0.42f);         // 0.55 -> 0.97 arası iz
        return outer * trail * trail * trail;
    });

    // İnce, iki tarafı yumuşak halka (kalkan kenarı, iyileşme halkası).
    public static Sprite ThinRing => thinRing ??= Make(256, r =>
    {
        float d = Mathf.Abs(r - 0.93f) / 0.06f;
        return Mathf.Clamp01(1f - d * d);
    });

    // Kenarı yumuşak dolu disk (zemin kırağısı, kalkan dolgusu).
    public static Sprite Disc => disc ??= Make(128, r => Mathf.Clamp01((1f - r) / 0.15f));

    // Uzun elmas (buz sivrisi / kalkan kırığı). +X yönüne bakar.
    public static Sprite Shard => shard ??= MakeXY(128, 32, (x, y) =>
    {
        float u = x;                       // 0 (kök) .. 1 (uç)
        float half = u < 0.25f ? u / 0.25f : (1f - u) / 0.75f;  // kök kısa, uç uzun
        float d = Mathf.Abs(y) / Mathf.Max(0.001f, half);       // 0 merkez çizgisi .. 1 kenar
        if (d > 1f) return 0f;
        return Mathf.Lerp(1f, 0.55f, d) * Mathf.Clamp01((1f - d) / 0.15f);
    });

    // 6 kollu kar tanesi.
    public static Sprite Snowflake => snowflake ??= MakeXY(64, 64, (x, y) =>
    {
        float best = 0f;
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 p = new Vector2(x * 2f - 1f, y);
            float along = Vector2.Dot(p, dir);
            if (along < 0f || along > 0.95f) continue;
            float across = Mathf.Abs(p.x * dir.y - p.y * dir.x);
            best = Mathf.Max(best, Mathf.Clamp01(1f - across / 0.07f));
            // kolun ortasındaki küçük V dalları
            float branch = Mathf.Abs(Mathf.Abs(across) - (along - 0.45f) * 0.8f);
            if (along > 0.45f && along < 0.75f) best = Mathf.Max(best, Mathf.Clamp01(1f - branch / 0.06f));
        }
        return best;
    }, centered: true);

    // Yuvarlak köşeli artı (iyileşme).
    public static Sprite Plus => plus ??= MakeXY(64, 64, (x, y) =>
    {
        float px = Mathf.Abs(x * 2f - 1f), py = Mathf.Abs(y);
        float bar = 0.24f, len = 0.85f;
        float h = Mathf.Max(py - bar, px - len);
        float v = Mathf.Max(px - bar, py - len);
        float d = Mathf.Min(h, v);
        return Mathf.Clamp01(-d / 0.06f);
    }, centered: true);

    // 4 köşeli parıltı yıldızı (kıvılcım / kor).
    public static Sprite Spark => spark ??= MakeXY(64, 64, (x, y) =>
    {
        float px = Mathf.Abs(x * 2f - 1f), py = Mathf.Abs(y);
        float star = Mathf.Clamp01(1f - (px * py * 18f + Mathf.Max(px, py) * 0.9f));
        float core = Mathf.Clamp01(1f - Mathf.Sqrt(px * px + py * py) / 0.35f);
        return Mathf.Clamp01(star + core * core);
    }, centered: true);

    // Altıgen ızgaralı baloncuk: kenara yakın daha görünür (küre hissi).
    public static Sprite HexBubble => hex ??= MakeXY(256, 256, (x, y) =>
    {
        Vector2 p = new Vector2(x * 2f - 1f, y);
        float r = p.magnitude;
        if (r > 0.97f) return 0f;

        // Altıgen döşeme: iki kaydırılmış ızgaradan yakın olan hücre merkezi seçilir,
        // altıgen mesafesi 0.5'e (hücre kenarı) yaklaştıkça çizgi belirir.
        const float size = 0.2f;
        Vector2 q = p / size;
        Vector2 s = new Vector2(1f, 1.732f);
        Vector2 a = Mod(q, s) - s * 0.5f;
        Vector2 b = Mod(q - s * 0.5f, s) - s * 0.5f;
        Vector2 gv = a.sqrMagnitude < b.sqrMagnitude ? a : b;
        float edge = Mathf.Max(Mathf.Abs(gv.x), Mathf.Abs(gv.x) * 0.5f + Mathf.Abs(gv.y) * 0.866f);
        float line = Mathf.Clamp01((edge - 0.42f) / 0.06f);

        float fresnel = Mathf.Pow(r, 2.2f);                      // kenarda güçlü
        return line * (0.25f + 0.75f * fresnel) * Mathf.Clamp01((0.97f - r) / 0.03f);
    }, centered: true);

    // Negatif sayılarda da pozitif kalan mod (C# % işareti korur).
    static Vector2 Mod(Vector2 v, Vector2 m)
        => new Vector2(v.x - m.x * Mathf.Floor(v.x / m.x), v.y - m.y * Mathf.Floor(v.y / m.y));

    // ---- Üreticiler ----
    // Radyal doku: f(r), r = 0 merkez .. 1 kenar.
    static Sprite Make(int size, System.Func<float, float> f)
        => MakeXY(size, size, (x, y) => f(new Vector2(x * 2f - 1f, y).magnitude), centered: true);

    // x: 0..1 (soldan sağa). y: centered ise -1..1 (kare dokularda x ile aynı ölçek), değilse -1..1 yükseklik.
    static Sprite MakeXY(int w, int h, System.Func<float, float, float> f, bool centered = false)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color32[w * h];
        for (int j = 0; j < h; j++)
        for (int i = 0; i < w; i++)
        {
            float x = (i + 0.5f) / w;
            float y = ((j + 0.5f) / h) * 2f - 1f;
            byte a = (byte)(Mathf.Clamp01(f(x, y)) * 255f);
            px[j * w + i] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(px);
        tex.Apply();
        // PPU = genişlik -> sprite 1 birim genişliğinde; scale ile boyutlandırılır
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
    }
}
