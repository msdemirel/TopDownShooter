using UnityEngine;

// Anlık skill efektleri (tek seferlik): Pulse Wave, Frost Nova, Second Wind.
// Süreli olanlar ayrı component: OverdriveAura, ShieldBubble, SlowVisual.
// Hepsi VfxSprite parçalarından kurulur; boyutlar skill'in GERÇEK etki alanına göre verilir.
public static class SkillVfx
{
    // Sorting: zemin katmanları (Floor) Default'un altında. Düşmanlar 0, oyuncu 13.
    public const int GroundOrder = -5;   // zemin üstü, karakterlerin altı (kırağı, halka izi)
    public const int TopOrder = 50;      // her şeyin üstü (patlama, kıvılcım)

    // ---- Ortak yardımcılar ----
    public static Color A(Color c, float a) { c.a = a; return c; }
    public static Color Light(Color c, float t) { float a = c.a; c = Color.Lerp(c, Color.white, t); c.a = a; return c; }

    // Parlak merkez flaşı: renkli dış hale + beyaz çekirdek.
    public static void Flash(Vector3 pos, Color color, float size, float life = 0.25f, int order = TopOrder)
    {
        VfxSprite.Spawn(RuntimeSprite.Glow, pos, A(Light(color, 0.3f), 0.9f), life, order).Scale(size * 0.5f, size, true);
        VfxSprite.Spawn(RuntimeSprite.Glow, pos, A(Color.white, 0.85f), life * 0.6f, order + 1).Scale(size * 0.4f, size * 0.1f);
    }

    // Her yöne saçılan kıvılcımlar.
    public static void SparkBurst(Vector3 pos, Color color, int count, float speed, float size, float life, int order = TopOrder)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir == Vector2.zero) dir = Vector2.up;
            VfxSprite.Spawn(VfxSprites.Spark, pos, Light(color, Random.Range(0f, 0.6f)), life * Random.Range(0.7f, 1f), order)
                     .Scale(size * Random.Range(0.7f, 1.2f), 0f)
                     .Move(dir * speed * Random.Range(0.5f, 1f), drag: 5f)
                     .Spin(Random.Range(-360f, 360f));
        }
    }

    // ================= PULSE WAVE =================
    // Halka doğarken: merkez flaşı + halkanın kenarını süren kıvılcımlar (halkayla aynı hızda).
    public static void PulseEmit(Vector3 pos, float radius, float expandTime, Color color)
    {
        Flash(pos, color, 1.8f, 0.3f);
        VfxSprite.Spawn(VfxSprites.Disc, pos, A(color, 0.35f), 0.35f, GroundOrder).Scale(0.4f, 1.6f, true);   // zemin vuruşu

        int n = 18;
        float speed = radius / Mathf.Max(0.05f, expandTime);
        for (int i = 0; i < n; i++)
        {
            float ang = (i / (float)n) * 360f + Random.Range(-6f, 6f);
            Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
            VfxSprite.Spawn(VfxSprites.Spark, pos, Light(color, 0.5f), expandTime, TopOrder + 1)
                     .Scale(0.4f, 0.15f).Fade(0f, 0.6f)
                     .Move(dir * speed)
                     .Spin(Random.Range(-200f, 200f));
        }
    }

    // Halka bir düşmana çarpınca.
    public static void PulseHit(Vector3 pos, Color color)
    {
        Flash(pos, color, 0.8f, 0.15f);
        SparkBurst(pos, color, 5, 3.5f, 0.28f, 0.3f);
    }

    // ================= FROST NOVA =================
    // Tamamı etki yarıçapına göre: kırağı diski + kenar halkası = hasar/yavaşlatma alanı.
    public static void FrostNova(Vector3 pos, float radius, Color color)
    {
        Color pale = Light(color, 0.6f);
        float d = radius * 2f;

        Flash(pos, color, 2.2f, 0.3f);

        // Zemin kırağısı: 0.2 sn'de açılır, bir süre kalır, yavaşça erir
        VfxSprite.Spawn(VfxSprites.Disc, pos, A(color, 0.28f), 1.8f, GroundOrder).Scale(d * 0.2f, d, true, over: 0.12f).Fade(0f, 0.4f);
        VfxSprite.Spawn(VfxSprites.HexBubble, pos, A(pale, 0.35f), 1.8f, GroundOrder + 1).Scale(d * 0.2f, d, true, over: 0.12f).Fade(0f, 0.3f);

        // Soğuk şok dalgası + keskin kenar
        VfxSprite.Spawn(VfxSprites.Ring, pos, A(pale, 0.85f), 0.35f, TopOrder).Scale(0.3f, d, true);
        VfxSprite.Spawn(VfxSprites.ThinRing, pos, A(Color.white, 0.9f), 0.6f, TopOrder).Scale(0.3f, d, true, over: 0.35f).Fade(0f, 0.4f);

        // Buz sivrileri: kenara doğru fırlar, saplanıp kalır, erir
        int n = Mathf.Clamp(Mathf.RoundToInt(radius * 5f), 10, 26);
        for (int i = 0; i < n; i++)
        {
            float ang = (i / (float)n) * 360f + Random.Range(-7f, 7f);
            Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
            float dist = radius * Random.Range(0.55f, 0.95f);
            float len = Random.Range(0.45f, 0.85f);
            const float drag = 14f;   // mesafe ≈ hız / drag
            VfxSprite.Spawn(VfxSprites.Shard, pos + (Vector3)(dir * 0.3f), A(i % 3 == 0 ? Color.white : pale, 0.95f),
                            Random.Range(0.9f, 1.2f), TopOrder - 1)
                     .Rotate(ang)
                     .Scale(new Vector2(len * 0.2f, len * 0.5f), new Vector2(len, len), true, over: 0.2f)
                     .Fade(0f, 0.55f)
                     .Move(dir * (dist - 0.3f) * drag, drag);
        }

        // Kar taneleri: yavaşça dağılır, döner
        for (int i = 0; i < 16; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            VfxSprite.Spawn(VfxSprites.Snowflake, pos + (Vector3)(dir * 0.3f), A(pale, 0.95f), Random.Range(0.9f, 1.4f), TopOrder)
                     .Scale(Random.Range(0.2f, 0.34f), 0.06f).Fade(0.1f, 0.5f)
                     .Move(dir * radius * Random.Range(0.9f, 1.7f), drag: 2.5f)
                     .Spin(Random.Range(-140f, 140f));
        }
    }

    // ================= SECOND WIND (iyileşme) =================
    // "Toplan, sonra patla": önce enerji oyuncuya çöker, sonra yeşil ışık yükselir. Oyuncuyu takip eder.
    public static void Heal(Transform target, Color color)
    {
        Vector3 p = target.position;
        Color pale = Light(color, 0.5f);
        const float release = 0.25f;   // çökme bitince açılma anı

        // İçe çöken halka + içe akan kıvılcımlar
        VfxSprite.Spawn(VfxSprites.ThinRing, p, A(color, 0.9f), release + 0.05f, TopOrder).Scale(3.2f, 0.5f, true).Fade(0.25f, 0.8f).Follow(target);
        for (int i = 0; i < 10; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float dist = Random.Range(1.2f, 1.7f);
            VfxSprite.Spawn(VfxSprites.Spark, p + (Vector3)(dir * dist), pale, release, TopOrder)
                     .Scale(0.25f, 0.1f).Fade(0.3f, 0.8f)
                     .Move(-dir * dist / release).Follow(target);
        }

        // Açılma: ışık sütunu + hale + zemin halkası
        VfxSprite.Spawn(RuntimeSprite.Glow, p + Vector3.up * 0.9f, A(pale, 0.55f), 0.75f, TopOrder - 1)
                 .Scale(new Vector2(0.5f, 3.4f), new Vector2(1.1f, 3.8f), true).Fade(0.15f, 0.3f).Follow(target).Delay(release);
        VfxSprite.Spawn(RuntimeSprite.Glow, p, A(color, 0.6f), 0.8f, TopOrder - 2).Scale(1f, 2.4f, true).Fade(0.1f, 0.3f).Follow(target).Delay(release);
        VfxSprite.Spawn(VfxSprites.Ring, p, A(pale, 0.8f), 0.45f, GroundOrder).Scale(0.4f, 2.6f, true).Follow(target).Delay(release);

        // Yükselen artılar
        for (int i = 0; i < 10; i++)
        {
            Vector2 off = Random.insideUnitCircle * 0.55f + Vector2.down * 0.2f;
            VfxSprite.Spawn(VfxSprites.Plus, p + (Vector3)off, i % 3 == 0 ? A(Color.white, 0.95f) : A(pale, 0.95f),
                            Random.Range(0.7f, 1f), TopOrder)
                     .Scale(Random.Range(0.17f, 0.28f), 0.08f).Fade(0.2f, 0.6f)
                     .Move(Vector3.up * Random.Range(1.2f, 1.9f))
                     .Follow(target)
                     .Delay(release + Random.Range(0f, 0.35f));
        }
    }
}
