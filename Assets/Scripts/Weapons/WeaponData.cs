using UnityEngine;

public enum WeaponType
{
    Ranged,  // mermi fırlatır (pistol, shotgun...)
    Melee,   // yakın menzilde yay çizerek biçer (kılıç...)
}

// Tek bir silah türünün özellikleri. Her silah için bir asset oluşturulur
// (Create > TopDownShooter > Weapon). Upgrade sistemi ileride bu asset'leri dağıtacak.
[CreateAssetMenu(menuName = "TopDownShooter/Weapon", fileName = "NewWeapon")]
public class WeaponData : ScriptableObject
{
    [Header("Tanım")]
    public string weaponName = "Weapon";
    [Tooltip("Silahın görseli. Slota takılınca silah prefab'ının SpriteRenderer'ına yazılır.")]
    public Sprite sprite;
    [Tooltip("Ranged = mermi fırlatır. Melee = yakın menzilde yay çizerek biçer (kılıç).")]
    public WeaponType weaponType = WeaponType.Ranged;

    [Header("Ortak")]
    [Tooltip("Vuruş başına hasar (mermi ya da kılıç darbesi).")]
    public float damage = 1f;
    [Tooltip("Saniyede kaç vuruş. 2 = saniyede 2 atış/darbe.")]
    public float fireRate = 2f;
    [Tooltip("Ranged: ateş menzili. Melee: kılıcın erişim mesafesi.")]
    public float range = 5f;

    [Header("Ranged (mermi)")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 12f;

    [Header("Melee (kılıç)")]
    [Tooltip("Vuruş konisinin açısı (derece). 120 = önündeki geniş bir yayı biçer. " +
             "360 = etrafındaki her şey.")]
    public float meleeArc = 120f;
    [Tooltip("Kılıcın hedefe doğru ne kadar ileri atılacağı (dünya birimi) — Brotato dürtme hissi.")]
    public float lungeDistance = 0.5f;
    [Tooltip("İleri-geri atılma (dürtme) süresi — sadece görsel.")]
    public float swingTime = 0.15f;

    [Header("Çoklu Atış (Ranged)")]
    [Tooltip("Tek atışta çıkan mermi sayısı (shotgun için artır).")]
    public int projectileCount = 1;
    [Tooltip("Mermilerin SABİT yayılma açısı (derece) — shotgun yelpazesi. Tek mermide etkisiz.")]
    public float spreadAngle = 0f;

    [Header("Kritik Vuruş")]
    [Tooltip("Mermi başına kritik şansı (0.05 = %5). Üstüne oyuncunun global kritik bonusu eklenir.")]
    [Range(0f, 1f)] public float critChance = 0.05f;
    [Tooltip("Kritik vuruşta hasar bu katsayıyla çarpılır (2 = çift hasar).")]
    public float critMultiplier = 2f;

    [Header("İsabet")]
    [Tooltip("Her mermiye eklenen RASTGELE sapma (± derece). 0 = kusursuz nişan (lazer gibi). " +
             "3-8 arası gerçekçi durur; büyüttükçe silah dağıtır.")]
    public float inaccuracyAngle = 5f;

    // İki atış arasındaki süre (saniye).
    public float FireCooldown => fireRate > 0f ? 1f / fireRate : float.MaxValue;
}
