using System.Collections.Generic;
using UnityEngine;

// Dışa doğru büyüyen tek bir hasar halkası. Doğduğu andaki konumda SABİT kalır —
// oyuncu uzaklaşsa bile halka orada büyümeye devam eder (patlama dalgası gibi).
// Görselini KODDA çizer (LineRenderer ile daire) — prefab/sprite gerekmez.
// PlayerSkills.PulseWaveRoutine bunu aralıklı olarak birden çok kez üretir.
//
// Hasar modeli: halkanın ön kenarı bir düşmana ULAŞINCA (mesafe <= mevcut yarıçap) o
// düşmana BİR kez hasar verir (HashSet ile tekrar engellenir). Yarıçap büyüdükçe halka
// dışa süpürür; yakın düşman erken, uzak düşman geç vurulur.
public class ShockwaveRing : MonoBehaviour
{
    const int Segments = 48;   // dairenin köşe sayısı (yüksek = daha pürüzsüz)

    float maxRadius;
    float expandTime;
    float damage;
    float age;
    Color baseColor;
    LineRenderer line;

    readonly HashSet<EnemyBase> hitEnemies = new HashSet<EnemyBase>();

    public static void Spawn(Vector3 position, float maxRadius, float expandTime, float damage,
                             Color color, float thickness, int sortingOrder)
    {
        var go = new GameObject("ShockwaveRing");
        // Parent YOK: doğuş konumunda sabit kalır, oyuncuyla hareket etmez
        go.transform.position = position;

        var r = go.AddComponent<ShockwaveRing>();
        r.maxRadius = Mathf.Max(0.1f, maxRadius);
        r.expandTime = Mathf.Max(0.05f, expandTime);
        r.damage = damage;
        r.baseColor = color;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;   // local: obje zaten sabit, daire objenin etrafında çizilir
        lr.loop = true;
        lr.positionCount = Segments;
        lr.startWidth = lr.endWidth = thickness;
        lr.numCornerVertices = 2;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = color;
        lr.sortingOrder = sortingOrder;
        r.line = lr;
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / expandTime);
        float radius = maxRadius * t;

        DrawCircle(radius);
        ApplyDamage(radius);

        // Sona doğru sol (halka dağılıyormuş gibi)
        Color c = baseColor;
        c.a = baseColor.a * (1f - t);
        line.startColor = line.endColor = c;

        if (age >= expandTime) Destroy(gameObject);
    }

    void DrawCircle(float radius)
    {
        for (int i = 0; i < Segments; i++)
        {
            float a = (i / (float)Segments) * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
    }

    // Sondan başa: ölen düşman EnemyRegistry'den kendini siler.
    void ApplyDamage(float radius)
    {
        var alive = EnemyRegistry.Alive;
        Vector2 c = transform.position;   // sabit doğuş konumu (halka merkezi)

        for (int i = alive.Count - 1; i >= 0; i--)
        {
            EnemyBase e = alive[i];
            if (e == null || e.IsDead || hitEnemies.Contains(e)) continue;

            float d = ((Vector2)e.transform.position - c).magnitude;
            if (d <= radius)
            {
                if (e.TryGetComponent<Health>(out var h)) h.TakeDamage(damage);
                hitEnemies.Add(e);   // bu halka bu düşmanı bir daha vurmasın
            }
        }
    }
}
