using UnityEngine;

// Yavaşlatılmış düşmanın görseli (Frost Nova): ayağının altında buz halkası, ayaklarına
// saplanmış buz kristalleri ve üstüne yağan kar. Yavaşlama bitince erir.
// EnemyBase.ApplySlow çağırır; prefab'a eklemek gerekmez.
//
// Not: Düşman sprite'ının rengine DOKUNMUYORUZ — HitFlash orijinal rengi kendisi saklıyor,
// tint verirsek kalıcı olarak yanlış renge dönebilir.
public class SlowVisual : MonoBehaviour
{
    static readonly Color Ice = new Color(0.65f, 0.95f, 1f, 1f);
    const float FadeTime = 0.3f;

    EnemyBase enemy;
    float until;
    float size;                 // düşmanın kabaca genişliği (collider'dan)
    float feetY;                // ayak hizası (local)
    Transform root;
    SpriteRenderer ground;
    SpriteRenderer[] crystals;
    float alpha, snowTimer;

    public static void Refresh(EnemyBase e, float duration)
    {
        if (e == null) return;
        var v = e.GetComponent<SlowVisual>();
        if (v == null) { v = e.gameObject.AddComponent<SlowVisual>(); v.enemy = e; v.Build(); }
        v.until = Mathf.Max(v.until, Time.time + duration);
        v.enabled = true;
        v.root.gameObject.SetActive(true);
    }

    void Build()
    {
        var col = GetComponent<Collider2D>();
        Bounds b = col != null ? col.bounds : new Bounds(transform.position, Vector3.one * 0.5f);
        size = Mathf.Max(0.45f, Mathf.Max(b.size.x, b.size.y));
        feetY = b.min.y - transform.position.y;

        var body = GetComponentInChildren<SpriteRenderer>();
        int o = body != null ? body.sortingOrder : 0;

        root = new GameObject("SlowVisual").transform;
        root.SetParent(transform, false);

        ground = MakeLayer(VfxSprites.HexBubble, SkillVfx.GroundOrder + 2);
        ground.transform.localPosition = new Vector3(0f, feetY, 0f);
        ground.transform.localScale = new Vector3(size * 1.6f, size * 0.8f, 1f);   // yere yatık elips

        // Ayaklara saplanmış kristaller: yukarı-dışa bakan kısa sivriler
        float[] angles = { 115f, 80f, 60f, 100f };
        float[] xs = { -0.35f, -0.05f, 0.3f, 0.12f };
        crystals = new SpriteRenderer[angles.Length];
        for (int i = 0; i < angles.Length; i++)
        {
            var c = MakeLayer(VfxSprites.Shard, o + 1);
            float len = size * Random.Range(0.45f, 0.7f);
            c.transform.localPosition = new Vector3(xs[i] * size, feetY + 0.02f, 0f);
            c.transform.localRotation = Quaternion.Euler(0f, 0f, angles[i]);
            c.transform.localScale = new Vector3(len, len, 1f);
            crystals[i] = c;
        }
    }

    SpriteRenderer MakeLayer(Sprite sprite, int order)
    {
        var go = new GameObject("Layer");
        go.transform.SetParent(root, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        sr.material = VfxSprite.VfxMaterial;
        return sr;
    }

    void Update()
    {
        bool active = enemy != null && !enemy.IsDead && Time.time < until;
        float target = active ? 1f : 0f;
        alpha = Mathf.MoveTowards(alpha, target, Time.deltaTime / FadeTime);

        float shimmer = 0.85f + Mathf.Sin(Time.time * 6f) * 0.15f;
        ground.color = SkillVfx.A(Ice, 0.55f * alpha * shimmer);
        for (int i = 0; i < crystals.Length; i++)
            crystals[i].color = SkillVfx.A(i % 2 == 0 ? Color.white : Ice, 0.9f * alpha);

        if (active)
        {
            snowTimer -= Time.deltaTime;
            if (snowTimer <= 0f)
            {
                snowTimer = Random.Range(0.18f, 0.3f);
                Vector3 p = transform.position + new Vector3(Random.Range(-0.5f, 0.5f) * size, size * 0.7f, 0f);
                VfxSprite.Spawn(VfxSprites.Snowflake, p, SkillVfx.A(Ice, 0.9f), 0.6f, SkillVfx.TopOrder - 5)
                         .Scale(0.16f, 0.08f).Fade(0.2f, 0.6f)
                         .Move(new Vector3(Random.Range(-0.2f, 0.2f), -size * 1.2f, 0f))
                         .Spin(Random.Range(-90f, 90f));
            }
        }
        else if (alpha <= 0f)
        {
            root.gameObject.SetActive(false);
            enabled = false;   // bir sonraki Refresh'te yeniden açılır
        }
    }
}
