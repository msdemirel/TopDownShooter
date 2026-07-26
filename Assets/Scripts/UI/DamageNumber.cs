using TMPro;
using UnityEngine;

// Tek bir hasar yazısı: yukarı süzülür, sona doğru solar, süresi bitince kendini yok eder.
// DamageNumberSpawner doğurur ve Init ile ayarlar.
// Prefab: boş obje + child'ında TextMeshPro (WORLD-SPACE olan, "UI" olmayan).
public class DamageNumber : MonoBehaviour
{
    [Header("Referans")]
    [Tooltip("Boş bırakılırsa child'larda aranır.")]
    [SerializeField] TMP_Text text;

    [Header("Hareket")]
    [Tooltip("Yukarı süzülme hızı (birim/sn).")]
    [SerializeField] float floatSpeed = 1.5f;
    [Tooltip("Toplam ömür (saniye).")]
    [SerializeField] float lifetime = 0.7f;
    [Tooltip("Bu andan itibaren solmaya başlar (lifetime'dan küçük olmalı).")]
    [SerializeField] float fadeStart = 0.35f;

    float age;
    Color baseColor;

    void Awake()
    {
        if (text == null) text = GetComponentInChildren<TMP_Text>();
    }

    // Spawner çağırır: gösterilecek metin (sayı ya da "Crit!"), renk ve boyut.
    public void Init(string label, Color color, float scale)
    {
        if (text == null) return;

        text.text = label;
        text.color = color;
        baseColor = color;
        transform.localScale = Vector3.one * scale;
    }

    void Update()
    {
        age += Time.deltaTime;
        transform.position += Vector3.up * (floatSpeed * Time.deltaTime);

        // Solma: fadeStart'tan ömrün sonuna doğru alfa 1 -> 0
        if (text != null && age >= fadeStart && lifetime > fadeStart)
        {
            float alpha = 1f - (age - fadeStart) / (lifetime - fadeStart);
            text.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }

        if (age >= lifetime)
            Destroy(gameObject);
    }
}
