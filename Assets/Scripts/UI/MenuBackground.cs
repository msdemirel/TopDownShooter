using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Ana menünün hareketli arka planı:
//   - Oyunun zemin tile'ı yavaşça çapraz kayar (Image tipi Tiled olmalı)
//   - Yukarı doğru süzülen, yanıp sönen ışık parçacıkları (kodda üretilir)
//   - Opsiyonel: nabız gibi atan parıltı (başlığın arkasındaki glow)
// Hepsi unscaledDeltaTime ile çalışır: oyun donuk (timeScale 0) gelse bile hareket eder.
public class MenuBackground : MonoBehaviour
{
    [Header("Kayan zemin")]
    [Tooltip("Tiled tipinde zemin Image'ı. Ekrandan en az bir tile büyük olmalı (kayarken kenar görünmesin).")]
    [SerializeField] RectTransform scrollingFloor;
    [Tooltip("Kayma hızı (canvas birimi / sn).")]
    [SerializeField] Vector2 scrollSpeed = new Vector2(-18f, -10f);

    [Header("Parçacıklar")]
    [Tooltip("Parçacıkların doğacağı alan (genelde tam ekran bir obje).")]
    [SerializeField] RectTransform particleArea;
    [SerializeField] Sprite particleSprite;
    [SerializeField] int particleCount = 28;
    [SerializeField] Vector2 particleSize = new Vector2(10f, 34f);
    [SerializeField] Vector2 particleSpeed = new Vector2(12f, 45f);
    [SerializeField] Color[] particleColors =
    {
        new Color(0.45f, 0.94f, 0.97f, 0.55f),   // cyan
        new Color(1f, 0.45f, 0.65f, 0.45f),      // pembe
        new Color(1f, 0.8f, 0.46f, 0.4f),        // sarı
    };

    [Header("Parıltı (opsiyonel)")]
    [SerializeField] Graphic pulseGlow;
    [SerializeField] float pulseSpeed = 1.3f;
    [SerializeField] Vector2 pulseAlpha = new Vector2(0.25f, 0.55f);

    class Particle
    {
        public RectTransform rt;
        public Image img;
        public float speed, drift, phase, baseAlpha;
    }

    readonly List<Particle> particles = new List<Particle>();
    Vector2 floorStart;
    Vector2 scrollOffset;
    float tileSize;

    void Start()
    {
        if (scrollingFloor != null)
        {
            floorStart = scrollingFloor.anchoredPosition;
            tileSize = ComputeTileSize(scrollingFloor.GetComponent<Image>());
        }

        if (particleArea != null && particleSprite != null)
            for (int i = 0; i < particleCount; i++)
                particles.Add(SpawnParticle(randomY: true));
    }

    // Tiled Image'da bir tile'ın canvas birimi cinsinden boyutu (kaydırmayı buna göre sarıyoruz,
    // böylece zemin sonsuz kayıyormuş gibi görünür).
    static float ComputeTileSize(Image img)
    {
        if (img == null || img.sprite == null || img.canvas == null) return 0f;
        float ppu = img.sprite.pixelsPerUnit * img.pixelsPerUnitMultiplier / img.canvas.referencePixelsPerUnit;
        return ppu > 0f ? img.sprite.rect.width / ppu : 0f;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (scrollingFloor != null && tileSize > 0f)
        {
            scrollOffset += scrollSpeed * dt;
            scrollOffset.x = Mathf.Repeat(scrollOffset.x, tileSize);
            scrollOffset.y = Mathf.Repeat(scrollOffset.y, tileSize);
            scrollingFloor.anchoredPosition = floorStart + scrollOffset;
        }

        if (particles.Count > 0) UpdateParticles(dt);

        if (pulseGlow != null)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
            Color c = pulseGlow.color;
            c.a = Mathf.Lerp(pulseAlpha.x, pulseAlpha.y, k);
            pulseGlow.color = c;
        }
    }

    void UpdateParticles(float dt)
    {
        Rect area = particleArea.rect;
        float t = Time.unscaledTime;

        foreach (var p in particles)
        {
            Vector2 pos = p.rt.anchoredPosition;
            pos.y += p.speed * dt;
            pos.x += Mathf.Sin(t * 0.7f + p.phase) * p.drift * dt;

            // Üstten çıkınca alttan yeniden doğ
            if (pos.y > area.yMax + 40f) { Respawn(p, randomY: false); continue; }
            p.rt.anchoredPosition = pos;

            // Yanıp sönme
            Color c = p.img.color;
            c.a = p.baseAlpha * (0.55f + 0.45f * Mathf.Sin(t * 2.1f + p.phase));
            p.img.color = c;
        }
    }

    Particle SpawnParticle(bool randomY)
    {
        var go = new GameObject("Particle", typeof(RectTransform), typeof(Image));
        var p = new Particle { rt = (RectTransform)go.transform, img = go.GetComponent<Image>() };
        p.rt.SetParent(particleArea, false);
        p.rt.anchorMin = p.rt.anchorMax = new Vector2(0.5f, 0.5f);
        p.img.sprite = particleSprite;
        p.img.raycastTarget = false;
        Respawn(p, randomY);
        return p;
    }

    void Respawn(Particle p, bool randomY)
    {
        Rect area = particleArea.rect;
        float size = Random.Range(particleSize.x, particleSize.y);
        p.rt.sizeDelta = new Vector2(size, size);
        p.speed = Random.Range(particleSpeed.x, particleSpeed.y);
        p.drift = Random.Range(8f, 30f);
        p.phase = Random.Range(0f, Mathf.PI * 2f);

        Color c = particleColors.Length > 0 ? particleColors[Random.Range(0, particleColors.Length)] : Color.white;
        p.baseAlpha = c.a;
        p.img.color = c;

        float y = randomY ? Random.Range(area.yMin, area.yMax) : area.yMin - 40f;
        p.rt.anchoredPosition = new Vector2(Random.Range(area.xMin, area.xMax), y);
    }
}
