using UnityEngine;

// Kalkan süresince oyuncuyu saran altıgen desenli enerji baloncuğu.
//  - Açılış: küçükten büyüyüp hafifçe taşar ("pop").
//  - Engellenen her vuruşta: kenar beyazlar, dışa bir dalga yayılır (Health.OnDamageBlocked).
//  - Son WarnTime saniyede yanıp söner: oyuncu kalkanın bitmek üzere olduğunu görür.
//  - Bitiş: parçalara ayrılıp dağılır.
// PlayerSkills.ShieldRoutine Attach ile başlatır, süre bitince Stop çağırır.
public class ShieldBubble : MonoBehaviour
{
    const float Diameter = 1.5f;
    const float WarnTime = 0.7f;

    Color color;
    float duration, age, hitFlash;
    Health health;
    Transform root;
    SpriteRenderer back, fill, hex, rim;
    bool stopped;

    public static ShieldBubble Attach(GameObject owner, float duration, Color color)
    {
        var b = owner.AddComponent<ShieldBubble>();
        b.color = color;
        b.duration = duration;
        b.health = owner.GetComponent<Health>();
        if (b.health != null) b.health.OnDamageBlocked += b.HandleBlocked;
        b.Build();
        return b;
    }

    void Build()
    {
        var body = GetComponent<SpriteRenderer>();
        int o = body != null ? body.sortingOrder : 13;

        root = new GameObject("ShieldBubble").transform;
        root.SetParent(transform, false);

        back = MakeLayer(RuntimeSprite.Glow, o - 1);        // arkadaki yumuşak hale
        fill = MakeLayer(VfxSprites.Disc, o + 1);           // hafif dolgu
        hex  = MakeLayer(VfxSprites.HexBubble, o + 2);      // altıgen desen
        rim  = MakeLayer(VfxSprites.ThinRing, o + 3);       // parlak kenar

        SkillVfx.Flash(transform.position, color, 1.6f, 0.25f);
        Apply();
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

    void HandleBlocked(Health h, float amount)
    {
        if (stopped) return;
        hitFlash = 1f;
        VfxSprite.Spawn(VfxSprites.ThinRing, transform.position, SkillVfx.A(Color.white, 0.9f), 0.25f, SkillVfx.TopOrder)
                 .Scale(Diameter, Diameter * 1.4f, true).Follow(transform);
        SkillVfx.SparkBurst(transform.position, color, 4, 3f, 0.22f, 0.25f);
    }

    public void Stop()
    {
        if (stopped) return;
        stopped = true;

        // Kırılma: kenardan dışa fırlayan parçalar + son bir dalga
        Vector3 p = transform.position;
        Color pale = SkillVfx.Light(color, 0.5f);
        for (int i = 0; i < 12; i++)
        {
            float ang = i * 30f + Random.Range(-10f, 10f);
            Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
            VfxSprite.Spawn(VfxSprites.Shard, p + (Vector3)(dir * Diameter * 0.45f), SkillVfx.A(pale, 0.9f), 0.45f, SkillVfx.TopOrder)
                     .Rotate(ang).Scale(0.35f, 0.15f).Fade(0f, 0.3f)
                     .Move(dir * Random.Range(3f, 5f), drag: 4f).Spin(Random.Range(-300f, 300f));
        }
        VfxSprite.Spawn(VfxSprites.Ring, p, SkillVfx.A(pale, 0.8f), 0.3f, SkillVfx.TopOrder).Scale(Diameter, Diameter * 1.7f, true);

        Destroy(root.gameObject);
        Destroy(this);
    }

    void Update()
    {
        age += Time.deltaTime;
        hitFlash = Mathf.Max(0f, hitFlash - Time.deltaTime * 5f);
        hex.transform.Rotate(0f, 0f, 20f * Time.deltaTime);
        Apply();
    }

    void Apply()
    {
        // Pop: 0 -> 1.15 -> 1, sonra hafif nefes alma
        float s;
        if (age < 0.12f) s = Mathf.Lerp(0f, 1.15f, age / 0.12f);
        else if (age < 0.22f) s = Mathf.Lerp(1.15f, 1f, (age - 0.12f) / 0.1f);
        else s = 1f + Mathf.Sin(age * 4f) * 0.025f;
        root.localScale = Vector3.one * (Diameter * s);

        float blink = duration - age < WarnTime && Mathf.Repeat(age * 10f, 1f) < 0.5f ? 0.3f : 1f;
        Color pale = SkillVfx.Light(color, 0.4f);

        back.transform.localScale = Vector3.one * 1.35f;
        back.color = SkillVfx.A(color, 0.3f * blink);
        fill.color = SkillVfx.A(color, (0.14f + hitFlash * 0.2f) * blink);
        hex.color  = SkillVfx.A(Color.Lerp(pale, Color.white, hitFlash), (0.5f + hitFlash * 0.4f) * blink);
        rim.color  = SkillVfx.A(Color.Lerp(pale, Color.white, hitFlash), 0.95f * blink);
    }

    void OnDestroy()
    {
        if (health != null) health.OnDamageBlocked -= HandleBlocked;
        if (root != null) Destroy(root.gameObject);
    }
}
