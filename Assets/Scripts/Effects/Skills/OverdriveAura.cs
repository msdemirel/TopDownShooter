using UnityEngine;

// Overdrive süresince oyuncunun üstünde: nabız gibi atan turuncu aura, ayak altında güç çemberi,
// yükselen korlar ve hareket ederken arkada kalan turuncu gölgeler (hız/güç hissi).
// Son WarnTime saniyede aura titrer: oyuncu buff'ın bitmek üzere olduğunu görür.
// PlayerSkills.OverdriveRoutine Attach ile başlatır, süre bitince Stop çağırır.
public class OverdriveAura : MonoBehaviour
{
    const float WarnTime = 0.75f;
    const float EmberInterval = 0.05f;
    const float GhostInterval = 0.07f;
    const float GhostMinMove = 0.06f;

    Color color;
    float duration, age;
    SpriteRenderer body;          // oyuncunun kendi sprite'ı (gölge kopyası için)
    Transform root;
    SpriteRenderer aura, circle;
    float emberTimer, ghostTimer;
    Vector3 lastGhostPos;
    float stopAge = -1f;

    public static OverdriveAura Attach(GameObject owner, float duration, Color color)
    {
        var a = owner.AddComponent<OverdriveAura>();
        a.color = color;
        a.duration = duration;
        a.body = owner.GetComponent<SpriteRenderer>();
        if (a.body == null) a.body = owner.GetComponentInChildren<SpriteRenderer>();
        a.Build();
        return a;
    }

    void Build()
    {
        Vector3 p = transform.position;
        int bodyOrder = body != null ? body.sortingOrder : 13;

        // Başlangıç patlaması
        SkillVfx.Flash(p, color, 2f, 0.3f);
        VfxSprite.Spawn(VfxSprites.Ring, p, SkillVfx.A(SkillVfx.Light(color, 0.3f), 0.9f), 0.35f, SkillVfx.TopOrder).Scale(0.3f, 3.2f, true);
        SkillVfx.SparkBurst(p, color, 12, 6f, 0.35f, 0.45f);

        root = new GameObject("OverdriveAura").transform;
        root.SetParent(transform, false);

        aura = MakeLayer(RuntimeSprite.Glow, bodyOrder - 1);          // oyuncunun hemen arkası
        circle = MakeLayer(VfxSprites.ThinRing, SkillVfx.GroundOrder); // ayak altı güç çemberi
        circle.transform.localPosition = new Vector3(0f, -0.35f, 0f);
        lastGhostPos = p;
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

    public void Stop()
    {
        if (stopAge >= 0f) return;
        stopAge = 0f;
        VfxSprite.Spawn(VfxSprites.ThinRing, transform.position, SkillVfx.A(color, 0.7f), 0.3f, SkillVfx.TopOrder).Scale(1.6f, 0.6f, true);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        float fade = 1f;
        if (stopAge >= 0f)
        {
            stopAge += dt;
            fade = 1f - stopAge / 0.25f;
            if (fade <= 0f) { Destroy(root.gameObject); Destroy(this); return; }
        }

        // Nabız + bitişe yakın titreme
        float pulse = Mathf.Sin(age * 10f);
        float warn = duration - age < WarnTime && Mathf.Repeat(age * 12f, 1f) < 0.5f ? 0.35f : 1f;
        float fadeIn = Mathf.Clamp01(age / 0.15f);

        aura.transform.localScale = Vector3.one * (1.7f + pulse * 0.12f);
        aura.color = SkillVfx.A(color, (0.5f + pulse * 0.1f) * warn * fade * fadeIn);

        circle.transform.localScale = new Vector3(1.4f, 0.7f, 1f) * (1f + pulse * 0.05f);   // yere yatık elips
        circle.transform.Rotate(0f, 0f, 90f * dt);
        circle.color = SkillVfx.A(SkillVfx.Light(color, 0.3f), 0.8f * warn * fade * fadeIn);

        if (stopAge >= 0f) return;

        // Yükselen korlar (oyuncuyu takip etmez: hareket edince arkada iz kalır)
        emberTimer -= dt;
        if (emberTimer <= 0f)
        {
            emberTimer = EmberInterval;
            Vector3 pos = transform.position + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.45f, 0f), 0f);
            Color c = Random.value < 0.3f ? new Color(1f, 0.85f, 0.45f, 1f) : color;
            VfxSprite.Spawn(VfxSprites.Spark, pos, SkillVfx.A(c, 0.95f), Random.Range(0.35f, 0.6f), SkillVfx.TopOrder)
                     .Scale(Random.Range(0.14f, 0.24f), 0f)
                     .Move(new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(1.2f, 2f), 0f), drag: 1.5f);
        }

        // Hareket gölgeleri
        ghostTimer -= dt;
        if (ghostTimer <= 0f && body != null && body.sprite != null)
        {
            ghostTimer = GhostInterval;
            Vector3 bp = body.transform.position;
            if ((bp - lastGhostPos).sqrMagnitude >= GhostMinMove * GhostMinMove)
            {
                lastGhostPos = bp;
                Vector3 ls = body.transform.lossyScale;
                var g = VfxSprite.Spawn(body.sprite, bp, SkillVfx.A(color, 0.55f), 0.25f, body.sortingOrder - 2)
                                 .Scale(new Vector2(ls.x, ls.y), new Vector2(ls.x, ls.y));
                g.Renderer.flipX = body.flipX;
                g.Renderer.flipY = body.flipY;
            }
        }
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }
}
