using UnityEngine;

// Tek bir ölüm parçacığı: rastgele yöne savrulur, küçülüp solarak kaybolur.
// DeathEffectSpawner, Spawn ile üretir — prefab gerekmez.
public class DeathParticle : MonoBehaviour
{
    Vector2 velocity;
    float lifetime;
    float age;
    Vector3 startScale;
    SpriteRenderer sr;
    Color baseColor;

    public static void Spawn(Vector3 pos, Color color, float size, float speed,
                             float lifetime, int sortingOrder)
    {
        var go = new GameObject("DeathParticle");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * size;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeSprite.White;
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        var p = go.AddComponent<DeathParticle>();
        p.sr = sr;
        p.baseColor = color;
        p.startScale = go.transform.localScale;
        p.lifetime = lifetime;
        // Yön tamamen rastgele; hız da biraz rastgele ki parçacıklar tekdüze savrulmasın
        p.velocity = Random.insideUnitCircle.normalized * (speed * Random.Range(0.5f, 1f));
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime) { Destroy(gameObject); return; }

        transform.position += (Vector3)(velocity * Time.deltaTime);
        velocity *= 1f - 4f * Time.deltaTime;   // sürtünme: gittikçe yavaşlar

        // Küçül + sol
        float t = 1f - age / lifetime;          // 1 -> 0
        transform.localScale = startScale * t;
        sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * t);
    }
}
