using UnityEngine;

// Tek bir efekt parçası: sprite + hareket + ölçek + solma + dönme. Tüm skill efektleri
// bu parçalardan kurulur (prefab gerekmez, DeathParticle kalıbı).
//
// Kullanım (zincirleme):
//   VfxSprite.Spawn(VfxSprites.Spark, pos, color, 0.4f, order)
//            .Scale(0.3f, 0f).Move(dir * 5f, drag: 4f).Spin(180f);
//
// Zaman Time.deltaTime ile ilerler: oyun donunca (upgrade paneli) efektler de durur.
public class VfxSprite : MonoBehaviour
{
    SpriteRenderer sr;
    Color color;
    float life, age, delay;
    Vector2 scaleFrom = Vector2.one, scaleTo = Vector2.one;
    bool easeOut;
    float scaleOver = 1f;             // ölçek animasyonu ömrün bu oranında tamamlanır
    float fadeIn, fadeOutStart;       // ömrün oranı olarak (0..1)
    Vector3 velocity;
    float drag;
    float spin;
    Transform follow;
    bool following;
    Vector3 followOffset;

    public SpriteRenderer Renderer => sr;

    public static VfxSprite Spawn(Sprite sprite, Vector3 pos, Color color, float life, int sortingOrder)
    {
        var go = new GameObject("Vfx");
        go.transform.position = pos;
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.color = color;
        r.sortingOrder = sortingOrder;
        r.material = VfxMaterial;

        var v = go.AddComponent<VfxSprite>();
        v.sr = r;
        v.color = color;
        v.life = Mathf.Max(0.01f, life);
        v.Apply(0f);
        return v;
    }

    // Işıktan etkilenmeyen sprite materyali (URP 2D'de Sprite-Lit ışığa göre kararır).
    static Material vfxMaterial;
    public static Material VfxMaterial => vfxMaterial != null ? vfxMaterial
        : vfxMaterial = new Material(Shader.Find("Sprites/Default"));

    // ---- Ayarlar (zincirlenebilir) ----
    // over: ölçek animasyonu ömrün bu oranında biter, sonra son boyutta kalır (ör. hızla açılıp bekleyen kırağı).
    public VfxSprite Scale(float from, float to, bool easeOut = false, float over = 1f)
        => Scale(Vector2.one * from, Vector2.one * to, easeOut, over);
    public VfxSprite Scale(Vector2 from, Vector2 to, bool easeOut = false, float over = 1f)
    {
        scaleFrom = from; scaleTo = to; this.easeOut = easeOut; scaleOver = Mathf.Max(0.01f, over); Apply(0f); return this;
    }
    // fadeIn: görünür olma süresi (ömür oranı). fadeOutStart: solmanın başladığı an (ömür oranı).
    public VfxSprite Fade(float fadeIn, float fadeOutStart)
    {
        this.fadeIn = Mathf.Clamp01(fadeIn); this.fadeOutStart = Mathf.Clamp01(fadeOutStart); Apply(0f); return this;
    }
    public VfxSprite Move(Vector3 velocity, float drag = 0f) { this.velocity = velocity; this.drag = drag; return this; }
    public VfxSprite Spin(float degPerSec) { spin = degPerSec; return this; }
    public VfxSprite Rotate(float deg) { transform.rotation = Quaternion.Euler(0f, 0f, deg); return this; }
    public VfxSprite Delay(float seconds) { delay = seconds; sr.enabled = delay <= 0f; return this; }
    // Hedefi takip eder (oyuncuyla birlikte hareket eden efektler). Hedef yok olunca efekt de biter.
    public VfxSprite Follow(Transform target)
    {
        follow = target;
        following = target != null;
        if (following) followOffset = transform.position - target.position;
        return this;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (delay > 0f)
        {
            delay -= dt;
            if (delay > 0f) return;
            sr.enabled = true;
        }

        age += dt;
        if (age >= life) { Destroy(gameObject); return; }

        if (following)
        {
            if (follow == null) { Destroy(gameObject); return; }   // takip edilen obje yok oldu
            followOffset += velocity * dt;
            transform.position = follow.position + followOffset;
        }
        else
        {
            transform.position += velocity * dt;
        }

        if (drag > 0f) velocity *= Mathf.Max(0f, 1f - drag * dt);
        if (spin != 0f) transform.Rotate(0f, 0f, spin * dt);

        Apply(age / life);
    }

    void Apply(float t)
    {
        float st = Mathf.Clamp01(t / scaleOver);
        float s = easeOut ? 1f - (1f - st) * (1f - st) : st;
        Vector2 sc = Vector2.LerpUnclamped(scaleFrom, scaleTo, s);
        transform.localScale = new Vector3(sc.x, sc.y, 1f);

        float a = 1f;
        if (fadeIn > 0f && t < fadeIn) a = t / fadeIn;
        if (t > fadeOutStart) a *= 1f - (t - fadeOutStart) / Mathf.Max(0.0001f, 1f - fadeOutStart);
        Color c = color; c.a *= Mathf.Clamp01(a);
        sr.color = c;
    }
}
