using UnityEngine;

// Dash izi: dash boyunca oyuncunun arkasında soluklaşan hayalet kopyalar (afterimage),
// gövdenin ortasından uzanan parlak hız çizgisi (TrailRenderer) ve başlangıçta
// geriye savrulan kıvılcımlar. PlayerSkills dash başlarken Play çağırır.
//
// Oyuncuya geçici component olarak eklenir; dash bitince hız çizgisi söner, component
// bir sonraki dash için yerinde kalır (her dash'te yeniden eklenmez).
public class DashTrail : MonoBehaviour
{
    const float GhostInterval = 0.022f;  // iki hayalet kopya arası süre (küçük = daha sık iz)
    const float GhostLife = 0.3f;
    const float StreakTime = 0.22f;      // hız çizgisinin kuyruk uzunluğu (saniye)

    SpriteRenderer source;
    Color color;
    Vector2 dir;
    float timeLeft;
    float ghostTimer;
    TrailRenderer outer, core;

    public static void Play(GameObject player, Vector2 dir, float duration, Color color)
    {
        if (!player.TryGetComponent(out DashTrail trail))
            trail = player.AddComponent<DashTrail>();
        trail.Begin(dir, duration, color);
    }

    void Begin(Vector2 direction, float duration, Color c)
    {
        StopStreak();   // önceki dash bitmeden yenisi başladıysa eski çizgiler sönsün
        source = GetComponent<SpriteRenderer>();
        color = c;
        dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        timeLeft = Mathf.Max(0.05f, duration);
        ghostTimer = 0f;

        Vector3 center = BodyCenter();
        SkillVfx.Flash(center, color, 1.1f, 0.18f);

        // Geriye savrulan hız kıvılcımları
        for (int i = 0; i < 7; i++)
        {
            Vector2 back = Quaternion.Euler(0f, 0f, Random.Range(-35f, 35f)) * -dir;
            VfxSprite.Spawn(VfxSprites.Spark, center, SkillVfx.Light(color, Random.Range(0.2f, 0.7f)),
                            Random.Range(0.18f, 0.3f), SkillVfx.TopOrder)
                     .Scale(Random.Range(0.25f, 0.4f), 0f)
                     .Move(back * Random.Range(5f, 9f), drag: 6f)
                     .Spin(Random.Range(-360f, 360f));
        }

        StartStreak();
    }

    void Update()
    {
        if (timeLeft <= 0f) return;

        float dt = Time.deltaTime;
        timeLeft -= dt;
        ghostTimer -= dt;

        if (ghostTimer <= 0f)
        {
            SpawnGhost();
            ghostTimer = GhostInterval;
        }

        if (timeLeft <= 0f) EndDash();
    }

    // Oyuncunun o anki karesinin renklendirilmiş, sönen kopyası (karakterin arkasında).
    void SpawnGhost()
    {
        if (source == null || source.sprite == null) return;

        Vector3 s = source.transform.lossyScale;
        var g = VfxSprite.Spawn(source.sprite, source.transform.position,
                                SkillVfx.A(SkillVfx.Light(color, 0.25f), 0.6f), GhostLife, source.sortingOrder - 1)
                         .Scale(new Vector2(s.x, s.y), new Vector2(s.x, s.y) * 0.92f)
                         .Fade(0f, 0f);
        g.Renderer.flipX = source.flipX;
        g.Renderer.flipY = source.flipY;
        g.Renderer.sortingLayerID = source.sortingLayerID;
    }

    void EndDash()
    {
        SpawnGhost();
        VfxSprite.Spawn(RuntimeSprite.Glow, BodyCenter(), SkillVfx.A(color, 0.5f), 0.2f, SkillVfx.TopOrder)
                 .Scale(0.4f, 1.2f, true);

        StopStreak();
    }

    // Çizgiler yeni nokta eklemeyi bırakır; mevcut kuyruk StreakTime içinde söner
    void StopStreak()
    {
        if (outer != null) { outer.emitting = false; Destroy(outer.gameObject, StreakTime + 0.05f); outer = null; }
        if (core != null) { core.emitting = false; Destroy(core.gameObject, StreakTime + 0.05f); core = null; }
    }

    // İki katmanlı hız çizgisi: geniş yarı saydam renkli dış + ince beyaz çekirdek.
    void StartStreak()
    {
        float h = source != null && source.sprite != null ? source.bounds.size.y : 1f;
        outer = MakeTrail("DashStreak", h * 0.55f, SkillVfx.A(color, 0.55f), source != null ? source.sortingOrder - 2 : 0);
        core = MakeTrail("DashStreakCore", h * 0.18f, SkillVfx.A(Color.white, 0.85f), source != null ? source.sortingOrder - 1 : 0);
    }

    TrailRenderer MakeTrail(string name, float width, Color head, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = BodyCenter();

        var tr = go.AddComponent<TrailRenderer>();
        tr.time = StreakTime;
        tr.minVertexDistance = 0.05f;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        tr.widthMultiplier = width;
        tr.numCapVertices = 4;
        tr.material = VfxSprite.VfxMaterial;
        tr.sortingOrder = order;
        if (source != null) tr.sortingLayerID = source.sortingLayerID;

        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(head, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(head.a, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = grad;
        tr.Clear();   // doğduğu yerden başlasın (eski konumdan çizgi çekmesin)
        return tr;
    }

    // Pivot ayaklarda olabilir: çizgiler ve flaş gövdenin ortasından çıksın.
    Vector3 BodyCenter()
        => source != null && source.sprite != null ? source.bounds.center : transform.position;
}
