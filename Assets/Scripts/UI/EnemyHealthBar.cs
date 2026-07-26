using UnityEngine;

// Düşmanın üstündeki mini can barı. Görselini (arka plan + dolum) KODDA kendisi kurar —
// prefab, sprite, elle dizilim GEREKMEZ; böylece kurulum hatası da olamaz.
// EnemyHealthBarSpawner, düşman ilk hasarı alınca Create ile düşmana takar.
public class EnemyHealthBar : MonoBehaviour
{
    Health health;
    Transform fill;
    Vector3 fullScale;   // dolumun "tam can" ölçüsü

    // Barı oluşturur, düşmana child olarak takar ve Health'i izlemeye başlar.
    public static EnemyHealthBar Create(Health target, Vector3 offset, float width, float height,
                                        Color bgColor, Color fillColor, int sortingOrder)
    {
        var root = new GameObject("EnemyHealthBar");
        root.transform.SetParent(target.transform, false);
        root.transform.localPosition = offset;

        var bar = root.AddComponent<EnemyHealthBar>();

        // Arka plan tam boy; dolum hafif küçük (kenarlardan BG görünüp çerçeve etkisi versin)
        bar.NewPart("BG", bgColor, sortingOrder, new Vector3(width, height, 1f));
        bar.fill = bar.NewPart("Fill", fillColor, sortingOrder + 1,
                               new Vector3(width * 0.94f, height * 0.6f, 1f));
        bar.fullScale = bar.fill.localScale;

        bar.Attach(target);
        return bar;
    }

    Transform NewPart(string name, Color color, int order, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = scale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeSprite.White;   // ortak 1x1 beyaz sprite (Effects/RuntimeSprite.cs)
        sr.color = color;
        sr.sortingOrder = order;

        return go.transform;
    }

    void Attach(Health h)
    {
        health = h;
        health.OnHealthChanged += UpdateBar;
        health.OnDeath += HandleDeath;
        UpdateBar(health);
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.OnHealthChanged -= UpdateBar;
            health.OnDeath -= HandleDeath;
        }
    }

    void UpdateBar(Health h)
    {
        if (fill == null) return;

        float n = h.Normalized;

        // Genişliği cana göre ölçekle; SOL kenar sabit kalsın, bar sağdan erisin
        fill.localScale = new Vector3(fullScale.x * n, fullScale.y, fullScale.z);
        fill.localPosition = new Vector3(-fullScale.x * (1f - n) * 0.5f, 0f, 0f);
    }

    // Ölünce bar hemen kalksın (death animasyonu oynarken boş bar sallanmasın)
    void HandleDeath(Health h) => Destroy(gameObject);
}
