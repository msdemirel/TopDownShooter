using System.Collections.Generic;
using UnityEngine;

// Noktalar arasında zikzaklı, kısa süre görünüp sönen şimşek çizgisi.
// Görselini KODDA çizer (LineRenderer) — prefab/sprite gerekmez (ShockwaveRing kalıbı).
// Sadece görseldir; hasarı PlayerSkills verir.
public class LightningArc : MonoBehaviour
{
    const float SegmentLength = 0.35f;   // zikzak kırılımları arası mesafe
    const float Jitter = 0.18f;          // kırılımların yana sapma miktarı
    const float RedrawInterval = 0.04f;  // şimşek titresin diye şekli bu aralıkla yeniden çizilir

    Vector3[] points;
    float lifetime;
    float age;
    float redrawTimer;
    Color baseColor;
    LineRenderer line;

    readonly List<Vector3> buffer = new List<Vector3>();

    public static void Spawn(Vector3[] points, Color color, float thickness, float lifetime, int sortingOrder)
    {
        if (points == null || points.Length < 2) return;

        var go = new GameObject("LightningArc");
        var a = go.AddComponent<LightningArc>();
        a.points = points;
        a.lifetime = Mathf.Max(0.05f, lifetime);
        a.baseColor = color;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;   // noktalar dünya konumunda sabit (düşman ölse bile)
        lr.startWidth = lr.endWidth = thickness;
        lr.numCornerVertices = 1;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = color;
        lr.sortingOrder = sortingOrder;
        a.line = lr;

        a.Redraw();
    }

    void Update()
    {
        age += Time.deltaTime;

        redrawTimer += Time.deltaTime;
        if (redrawTimer >= RedrawInterval)
        {
            redrawTimer = 0f;
            Redraw();
        }

        // Sona doğru sol
        Color c = baseColor;
        c.a = baseColor.a * (1f - Mathf.Clamp01(age / lifetime));
        line.startColor = line.endColor = c;

        if (age >= lifetime) Destroy(gameObject);
    }

    // Her iki nokta arasını kısa parçalara böler, uçlar hariç her kırılımı yana kaydırır.
    void Redraw()
    {
        buffer.Clear();
        buffer.Add(points[0]);

        for (int i = 1; i < points.Length; i++)
        {
            Vector3 from = points[i - 1];
            Vector3 to = points[i];
            Vector3 d = to - from;
            Vector3 normal = new Vector3(-d.y, d.x, 0f).normalized;

            int steps = Mathf.Max(1, Mathf.CeilToInt(d.magnitude / SegmentLength));
            for (int s = 1; s < steps; s++)
                buffer.Add(from + d * (s / (float)steps) + normal * Random.Range(-Jitter, Jitter));

            buffer.Add(to);
        }

        line.positionCount = buffer.Count;
        for (int i = 0; i < buffer.Count; i++)
            line.SetPosition(i, buffer[i]);
    }
}
