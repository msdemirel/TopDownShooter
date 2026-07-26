using System.Collections.Generic;
using UnityEngine;

// Enemy prefab'ına eklenir. Ölünce yüzdelere göre SADECE BİR pickup düşürür (ya da hiçbir şey).
// Slider'lar birbirini dengeler: toplam (drop'lar + Nothing Chance) her zaman 100'de kalır.
public class LootDropper : MonoBehaviour
{
    [System.Serializable]
    public class Drop
    {
        public GameObject pickupPrefab;
        [Range(0f, 100f)] public float dropChance = 50f;  // düşme olasılığı (%)
        public int minCount = 1;
        public int maxCount = 1;
    }

    [SerializeField] List<Drop> drops = new List<Drop>();

    [Tooltip("Hiçbir şey düşmeme olasılığı (%).")]
    [Range(0f, 100f)] [SerializeField] float nothingChance = 0f;

    [SerializeField] float scatterRadius = 0.3f;  // pickup biraz dağılsın

    // EnemyBase ölüm anında çağırır. Yüzdelere göre tek bir sonuç seçer.
    public void DropLoot()
    {
        float roll = Random.value * 100f;  // 0-100 arası tek zar
        float cumulative = 0f;

        foreach (var d in drops)
        {
            if (d.pickupPrefab == null) continue;

            cumulative += d.dropChance;
            if (roll < cumulative)
            {
                Spawn(d);
                return;
            }
        }
        // Buraya düştüyse: hiçbir şey düşmez (nothingChance dilimi)
    }

    void Spawn(Drop drop)
    {
        int count = Random.Range(drop.minCount, drop.maxCount + 1);
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * scatterRadius;
            Instantiate(drop.pickupPrefab, transform.position + (Vector3)offset, Quaternion.identity);
        }
    }

#if UNITY_EDITOR
    // ---- Inspector'da otomatik dengeleme (toplam = 100) ----
    // Değerler: index [0..drops.Count-1] = drop yüzdeleri, son index = nothingChance
    float[] prevValues;

    void OnValidate()
    {
        int n = drops.Count + 1;

        // Yapı değiştiyse (element eklendi/silindi/ilk açılış): topluca 100'e normalize et
        if (prevValues == null || prevValues.Length != n)
        {
            NormalizeTo100(n);
            CacheValues(n);
            return;
        }

        // Hangi slider değişti bul
        int changed = -1;
        for (int i = 0; i < n; i++)
        {
            if (!Mathf.Approximately(GetValue(i), prevValues[i])) { changed = i; break; }
        }
        if (changed == -1) return;

        // Tek slider varsa (hiç drop yoksa) onu 100 yap
        if (n == 1) { SetValue(0, 100f); CacheValues(n); return; }

        float changedVal = Mathf.Clamp(GetValue(changed), 0f, 100f);
        SetValue(changed, changedVal);
        float remaining = 100f - changedVal;

        // Diğerlerinin mevcut toplamı (yalnızca 'changed' değiştiği için diğerleri = prevValues)
        float sumOthers = 0f;
        for (int i = 0; i < n; i++)
            if (i != changed) sumOthers += GetValue(i);

        if (sumOthers <= 0.0001f)
        {
            // Diğerleri sıfırsa: kalanı eşit dağıt
            float each = remaining / (n - 1);
            for (int i = 0; i < n; i++)
                if (i != changed) SetValue(i, each);
        }
        else
        {
            // Kalanı diğerlerine mevcut oranlarına göre dağıt
            for (int i = 0; i < n; i++)
                if (i != changed) SetValue(i, GetValue(i) * remaining / sumOthers);
        }

        CacheValues(n);
    }

    void NormalizeTo100(int n)
    {
        float total = 0f;
        for (int i = 0; i < n; i++) total += GetValue(i);

        if (total <= 0.0001f)
        {
            float each = 100f / n;
            for (int i = 0; i < n; i++) SetValue(i, each);
        }
        else
        {
            for (int i = 0; i < n; i++) SetValue(i, GetValue(i) * 100f / total);
        }
    }

    void CacheValues(int n)
    {
        prevValues = new float[n];
        for (int i = 0; i < n; i++) prevValues[i] = GetValue(i);
    }

    float GetValue(int i) => i < drops.Count ? drops[i].dropChance : nothingChance;

    void SetValue(int i, float v)
    {
        if (i < drops.Count) drops[i].dropChance = v;
        else nothingChance = v;
    }
#endif
}
