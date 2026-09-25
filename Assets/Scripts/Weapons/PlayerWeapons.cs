using System;
using UnityEngine;

// Oyuncunun silah slotları. Slotlar oyuncunun etrafında eşit açıyla çembere dizilir.
// Oyun 1 silahla başlar; upgrade sistemi AddWeapon ile boş slotlara silah ekler.
//
// BİRLEŞTİRME: Aynı silahtan aynı kademede iki tane olunca otomatik birleşir (I + I -> II),
// zincirleme devam eder (II + II -> III), en fazla WeaponTiers.MaxTier. Birleşme bir slot
// boşaltır; slotlar doluyken sahip olunan bir silah alınırsa swap yerine birleşir.
public class PlayerWeapons : MonoBehaviour
{
    [Header("Slotlar")]
    [Tooltip("Toplam silah slotu sayısı.")]
    [SerializeField] int slotCount = 6;
    [Tooltip("Silahların çemberin merkezinden uzaklığı.")]
    [SerializeField] float orbitRadius = 0.6f;
    [Tooltip("Çemberin merkezi (oyuncunun merkezine göre local X / Y). Silah halkasını yukarı/aşağı kaydırmak için.")]
    [SerializeField] Vector2 centerOffset = Vector2.zero;
    [Tooltip("İlk slotun açısı (derece). 90 = üstten başlar.")]
    [SerializeField] float firstSlotAngle = 90f;

    [Header("Referanslar")]
    [Tooltip("Weapon component'i olan silah prefab'ı. Tüm silahlar bunu kullanır; görsel WeaponData'dan gelir.")]
    [SerializeField] GameObject weaponPrefab;
    [Tooltip("Oyunun başında verilecek silah.")]
    [SerializeField] WeaponData startingWeapon;

    Transform[] slots;
    Weapon[] weapons;

    // Upgrade kartları "bu silah birleşir" yazabilsin diye sahnedeki oyuncunun silahları
    public static PlayerWeapons Current { get; private set; }

    public int SlotCount => slotCount;
    public int WeaponCount { get; private set; }
    public bool HasFreeSlot => WeaponCount < slotCount;

    // ---- Upgrade'lerle büyüyen global çarpanlar ----
    // Tüm silahlar bunları okur; tek tek WeaponData'ya dokunmayız
    // (WeaponData bir asset — üstünde oynasak değişiklik kalıcı olurdu).
    public float DamageMultiplier { get; private set; } = 1f;
    public float FireRateMultiplier { get; private set; } = 1f;

    // 0.1 = %10 artış. Alt sınır: çarpan 0 veya eksiye düşüp silahı kilitlemesin.
    public void AddDamageMultiplier(float amount)
        => DamageMultiplier = Mathf.Max(0.1f, DamageMultiplier + amount);

    public void AddFireRateMultiplier(float amount)
        => FireRateMultiplier = Mathf.Max(0.1f, FireRateMultiplier + amount);

    // Silahların kendi kritik şansına EKLENEN global bonus (0.05 = +%5).
    public float CritChanceBonus { get; private set; }

    public void AddCritChance(float amount)
        => CritChanceBonus = Mathf.Clamp01(CritChanceBonus + amount);

    // i. slottaki silahın verisi (boşsa null). HUD slot ikonlarını buradan okur.
    public WeaponData GetSlotData(int index)
        => weapons != null && index >= 0 && index < weapons.Length && weapons[index] != null
            ? weapons[index].Data : null;

    // Upgrade paneli / HUD bunu dinleyebilir
    public event Action<Weapon> OnWeaponAdded;

    // İki silah birleşip kademe atlayınca: (yükselen silah, boşalan slot). HUD + bildirim dinler.
    public event Action<Weapon, int> OnWeaponMerged;

    void Awake()
    {
        BuildSlots();
        Current = this;
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Start()
    {
        // Seçili karakterin başlangıç silahı (yoksa prefab'daki)
        var character = CharacterSelection.Current;
        WeaponData start = character != null && character.startingWeapon != null ? character.startingWeapon : startingWeapon;
        if (start != null) AddWeapon(start);
    }

    // Slot objelerini oyuncunun child'ı olarak çembere dizer.
    void BuildSlots()
    {
        slotCount = Mathf.Max(1, slotCount);
        slots = new Transform[slotCount];
        weapons = new Weapon[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            var go = new GameObject($"WeaponSlot_{i}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = SlotLocalPosition(i);
            slots[i] = go.transform;
        }
    }

    // Çember üzerindeki i. slotun oyuncuya göre local pozisyonu.
    Vector3 SlotLocalPosition(int index)
    {
        float angle = (firstSlotAngle + index * (360f / slotCount)) * Mathf.Deg2Rad;
        Vector2 onCircle = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * orbitRadius;
        return centerOffset + onCircle;
    }

    // İlk boş slota silahı takar. Boş slot yoksa false döner.
    public bool AddWeapon(WeaponData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[PlayerWeapons] WeaponData boş.", this);
            return false;
        }
        if (weaponPrefab == null)
        {
            Debug.LogWarning("[PlayerWeapons] Weapon Prefab atanmamış.", this);
            return false;
        }

        // Önce birleşme: aynı silahtan 1. kademede bir tane varsa yeni slot kullanılmaz
        int partner = FindMergePartner(data, 1, -1);
        if (partner >= 0)
        {
            MergeInto(partner, -1);
            return true;
        }

        for (int i = 0; i < slotCount; i++)
            if (weapons[i] == null) return PlaceWeapon(i, data);

        return false;  // tüm slotlar dolu
    }

    // Bu silah alınırsa mevcut bir kopyasıyla birleşir mi? (kart notu + swap gerekip gerekmediği)
    public bool CanMerge(WeaponData data) => FindMergePartner(data, 1, -1) >= 0;

    // Birleşince ulaşılacak kademe (zincirleme dahil). Birleşmezse 1.
    public int MergeResultTier(WeaponData data)
    {
        int tier = 1;
        int skip = -1;
        while (tier < WeaponTiers.MaxTier)
        {
            int p = FindMergePartner(data, tier, skip);
            if (p < 0) break;
            tier++;
            skip = p;   // aynı silahı iki kez sayma (kendisiyle birleşmesin)
        }
        return tier;
    }

    public int GetSlotTier(int index)
        => weapons != null && index >= 0 && index < weapons.Length && weapons[index] != null ? weapons[index].Tier : 0;

    // data + tier eşleşen, 'except' dışındaki ilk slot (yoksa -1). Maks kademedekiler birleşmez.
    int FindMergePartner(WeaponData data, int tier, int except)
    {
        if (data == null || tier >= WeaponTiers.MaxTier) return -1;
        for (int i = 0; i < slotCount; i++)
            if (i != except && weapons[i] != null && weapons[i].Data == data && weapons[i].Tier == tier)
                return i;
        return -1;
    }

    // 'index'teki silah bir kademe atlar; aynı kademede başka bir kopyası varsa onunla da
    // birleşir (zincirleme). freedSlot: bu birleşmede boşalan slot (yeni alınan silahsa -1).
    void MergeInto(int index, int freedSlot)
    {
        Weapon w = weapons[index];
        w.SetTier(w.Tier + 1);

        // Birleşme efekti: silahın üstünde kademe renginde flaş + kıvılcım
        Color c = WeaponTiers.Color(w.Tier);
        SkillVfx.Flash(w.transform.position, c, 1.2f, 0.3f);
        SkillVfx.SparkBurst(w.transform.position, c, 12, 4f, 0.3f, 0.4f);

        OnWeaponMerged?.Invoke(w, freedSlot);

        int next = FindMergePartner(w.Data, w.Tier, index);
        if (next >= 0)
        {
            Destroy(weapons[next].gameObject);
            weapons[next] = null;
            WeaponCount--;
            MergeInto(index, next);
        }
    }

    // Boş i. slota silahı takar.
    bool PlaceWeapon(int i, WeaponData data)
    {
        GameObject go = Instantiate(weaponPrefab, slots[i]);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.name = $"Weapon_{data.weaponName}";

        if (!go.TryGetComponent<Weapon>(out var w))
        {
            Debug.LogWarning("[PlayerWeapons] Weapon Prefab'ında Weapon component'i yok.", this);
            Destroy(go);
            return false;
        }

        w.SetData(data);
        weapons[i] = w;
        WeaponCount++;
        OnWeaponAdded?.Invoke(w);
        return true;
    }

    // Slotlar doluyken yeni silah alınınca (swap): o slottaki silahı söküp yenisini takar.
    // Silah sayısı değişmez. HUD OnWeaponAdded ile güncellenir.
    public bool ReplaceWeapon(int index, WeaponData data)
    {
        if (data == null || weaponPrefab == null || index < 0 || index >= slotCount) return false;

        // Swap ekranına ancak birleşme yoksa gelinir; yine de varsa birleştir (slot kaybetme)
        if (CanMerge(data)) return AddWeapon(data);

        if (weapons[index] != null)
        {
            Destroy(weapons[index].gameObject);
            weapons[index] = null;
            WeaponCount--;
        }
        return PlaceWeapon(index, data);
    }

    // Slot dizilimini Scene view'da göster (oyun çalışmıyorken de).
    void OnDrawGizmosSelected()
    {
        int n = Mathf.Max(1, slotCount);

        // Çemberin merkezi
        Vector3 center = transform.position + (Vector3)centerOffset;
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(center, 0.04f);

        for (int i = 0; i < n; i++)
        {
            float angle = (firstSlotAngle + i * (360f / n)) * Mathf.Deg2Rad;
            Vector3 p = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * orbitRadius;

            // İlk slot yeşil: başlangıç silahı oraya gelir. Diğerleri sarı.
            Gizmos.color = i == 0 ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(p, 0.12f);
        }
    }

#if UNITY_EDITOR
    // Oyun çalışırken değerleri değiştirince slotlar anında yerine gitsin (canlı ayar için).
    void OnValidate()
    {
        if (!Application.isPlaying || slots == null) return;

        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null) slots[i].localPosition = SlotLocalPosition(i);
    }
#endif
}
