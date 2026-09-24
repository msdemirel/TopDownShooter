using System.Collections.Generic;
using UnityEngine;

// Area Blast: merkezden dışa büyüyen ateş patlaması. Hasar, ateş cephesi düşmana
// ULAŞTIĞINDA verilir (her düşmana bir kez) — görünen kenar = hasar kenarı.
// ShockwaveRing kalıbı: doğduğu yerde sabit kalır, görselini kodda çizer.
//
// Katmanlar: merkezde Fire Ball animasyonu (opsiyonel corePrefab), sıcak şok halkası,
// alev dolgusu, dışa fırlayan alev dilleri, korlar, duman ve zeminde soğuyan yanık izi.
public class BlastWave : MonoBehaviour
{
    // Ateş cephesinin düşman gövdesine "değme" payı (merkez değil gövde kenarı sayılsın)
    const float BodyMargin = 0.2f;

    static readonly Color Hot = new Color(1f, 0.9f, 0.45f, 1f);
    static readonly Color Fire = new Color(1f, 0.5f, 0.12f, 1f);
    static readonly Color Deep = new Color(0.85f, 0.22f, 0.08f, 1f);

    float maxRadius, expandTime, damage, age;
    readonly HashSet<EnemyBase> hit = new HashSet<EnemyBase>();
    SpriteRenderer ring, fill, hotFill;
    Transform core;
    SpriteRenderer coreSr;
    float coreBaseScale;
    float coreScale = 1f;

    public static void Spawn(Vector3 pos, float radius, float expandTime, float damage,
                             GameObject corePrefab = null, float coreScale = 1f)
    {
        var go = new GameObject("BlastWave");
        go.transform.position = pos;
        var b = go.AddComponent<BlastWave>();
        b.maxRadius = Mathf.Max(0.2f, radius);
        b.expandTime = Mathf.Max(0.05f, expandTime);
        b.damage = damage;
        b.coreScale = Mathf.Max(0.1f, coreScale);
        b.Build(corePrefab);
    }

    void Build(GameObject corePrefab)
    {
        Vector3 p = transform.position;
        float R = maxRadius;

        SkillVfx.Flash(p, Fire, R * 1.4f, 0.25f);
        CameraShake.Shake(0.22f, 0.25f);

        // Büyüyen katmanlar (Update'te yarıçapla birlikte ölçeklenir)
        fill = MakeLayer(VfxSprites.Disc, SkillVfx.GroundOrder + 3);
        hotFill = MakeLayer(RuntimeSprite.Glow, SkillVfx.TopOrder - 2);
        ring = MakeLayer(VfxSprites.Ring, SkillVfx.TopOrder);

        // Merkezde dönen ateş topu: büyür ve söner
        if (corePrefab != null)
        {
            core = Instantiate(corePrefab, p, Quaternion.identity).transform;
            coreSr = core.GetComponentInChildren<SpriteRenderer>();
            if (coreSr != null)
            {
                coreSr.sortingOrder = SkillVfx.TopOrder - 1;
                // Görünen top texture'ın ~%66'sı: çapı R'ye oturtmak için bounds'a göre ölçekle
                float ext = coreSr.sprite != null ? coreSr.sprite.bounds.extents.x : 0.5f;
                coreBaseScale = 1f / Mathf.Max(0.01f, ext * 0.66f);
            }
            Destroy(core.gameObject, expandTime + 0.4f);
        }

        // Alev dilleri: cepheyle birlikte dışa fırlar (mesafe ≈ hız / drag)
        int n = Mathf.Clamp(Mathf.RoundToInt(R * 6f), 12, 30);
        float drag = 3f / expandTime;
        for (int i = 0; i < n; i++)
        {
            float ang = (i / (float)n) * 360f + Random.Range(-8f, 8f);
            Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
            float dist = R * Random.Range(0.7f, 0.95f);
            float len = R * Random.Range(0.3f, 0.5f);
            Color c = i % 3 == 0 ? Hot : (i % 3 == 1 ? Fire : Deep);
            VfxSprite.Spawn(VfxSprites.Shard, transform.position, c, expandTime + Random.Range(0.2f, 0.35f), SkillVfx.TopOrder - 1)
                     .Rotate(ang)
                     .Scale(new Vector2(len * 0.3f, len * 0.9f), new Vector2(len, len * 0.6f), true)
                     .Fade(0f, 0.45f)
                     .Move(dir * dist * drag, drag);
        }

        // Korlar: kenarın biraz dışına savrulur
        for (int i = 0; i < 22; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            VfxSprite.Spawn(VfxSprites.Spark, p, i % 2 == 0 ? Hot : Fire, Random.Range(0.45f, 0.75f), SkillVfx.TopOrder + 1)
                     .Scale(Random.Range(0.18f, 0.32f), 0f)
                     .Move(dir * R * Random.Range(3.5f, 5.5f), drag: 4f)
                     .Spin(Random.Range(-360f, 360f));
        }

        // Duman: patlamadan sonra yükselir ve dağılır
        for (int i = 0; i < 9; i++)
        {
            Vector2 off = Random.insideUnitCircle * R * 0.7f;
            float s = R * Random.Range(0.5f, 0.8f);
            VfxSprite.Spawn(RuntimeSprite.Glow, p + (Vector3)off, new Color(0.22f, 0.2f, 0.22f, 0.45f), Random.Range(0.8f, 1.1f), SkillVfx.TopOrder - 3)
                     .Scale(s * 0.6f, s * 1.3f).Fade(0.2f, 0.4f)
                     .Move(new Vector3(off.x * 0.3f, Random.Range(0.4f, 0.8f), 0f))
                     .Delay(expandTime * 0.7f);
        }

        // Zeminde yanık izi + soğuyan kor kenarı
        VfxSprite.Spawn(VfxSprites.Disc, p, new Color(0.1f, 0.08f, 0.08f, 0.45f), 2.2f, SkillVfx.GroundOrder)
                 .Scale(R * 2f, R * 2f).Fade(0.1f, 0.5f).Delay(expandTime * 0.5f);
        VfxSprite.Spawn(VfxSprites.ThinRing, p, SkillVfx.A(Deep, 0.8f), 1.4f, SkillVfx.GroundOrder + 1)
                 .Scale(R * 2f, R * 2.05f).Fade(0f, 0.2f).Delay(expandTime);
    }

    SpriteRenderer MakeLayer(Sprite sprite, int order)
    {
        var go = new GameObject("Layer");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        sr.material = VfxSprite.VfxMaterial;
        return sr;
    }

    // Cephe hızlı çıkar, kenara yaklaşınca yavaşlar (patlama hissi). Hasar da aynı eğriyi kullanır.
    float RadiusAt(float t) => maxRadius * (1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f));

    void Update()
    {
        age += Time.deltaTime;
        float t = age / expandTime;
        float r = RadiusAt(t);

        if (t <= 1f) ApplyDamage(r);

        float d = r * 2f;
        float after = Mathf.Clamp01((age - expandTime) / 0.3f);   // cephe durduktan sonra sönme

        ring.transform.localScale = Vector3.one * d;
        ring.color = SkillVfx.A(Hot, 1f - after);
        fill.transform.localScale = Vector3.one * d * 0.97f;
        fill.color = SkillVfx.A(Fire, 0.5f * (1f - after));
        hotFill.transform.localScale = Vector3.one * d * 0.9f;
        hotFill.color = SkillVfx.A(Hot, 0.75f * (1f - Mathf.Clamp01(t * 0.8f)));

        if (core != null && coreSr != null)
        {
            float cs = Mathf.Lerp(0.35f, 0.75f, Mathf.Clamp01(t)) * maxRadius * coreBaseScale * coreScale;
            core.localScale = Vector3.one * cs;
            coreSr.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((age - expandTime * 0.6f) / 0.35f));
        }

        if (after >= 1f) Destroy(gameObject);
    }

    // Sondan başa: ölen düşman EnemyRegistry'den kendini siler.
    void ApplyDamage(float radius)
    {
        var alive = EnemyRegistry.Alive;
        Vector2 c = transform.position;

        for (int i = alive.Count - 1; i >= 0; i--)
        {
            EnemyBase e = alive[i];
            if (e == null || e.IsDead || hit.Contains(e)) continue;

            float dist = ((Vector2)e.transform.position - c).magnitude;
            if (dist > maxRadius) continue;                    // hasar alanı dışı (pay kenarı büyütmez)
            if (dist - BodyMargin > radius) continue;           // cephe henüz değmedi

            hit.Add(e);
            SkillVfx.Flash(e.transform.position, Fire, 0.8f, 0.15f);
            SkillVfx.SparkBurst(e.transform.position, Fire, 5, 3.5f, 0.28f, 0.3f);
            if (e.TryGetComponent<Health>(out var h)) h.TakeDamage(damage);
        }
    }
}
