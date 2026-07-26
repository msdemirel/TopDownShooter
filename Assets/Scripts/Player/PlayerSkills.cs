using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Oyuncunun AKTİF yeteneklerini yönetir: slotlar, tuşlar, cooldown'lar ve etkiler.
// Skiller upgrade panelinden kazanılır ve geliştirilir (SkillUpgradeData.Apply ->
// AddOrUpgrade); sırayla slotlara oturur, her slotun kendi tuşu vardır (aşağıdaki listeden).
// SkillHUD, slot/cooldown bilgisini her karede buradan okur (WaveHUD kalıbı).
public class PlayerSkills : MonoBehaviour
{
    [Header("Slot tuşları")]
    [Tooltip("Her eleman bir skill slotudur (0. skill 0. tuşa bağlanır). " +
             "Listenin uzunluğu = toplam slot sayısı. Örn. Space ve E için 2 eleman.")]
    [SerializeField] InputActionReference[] slotActions;

    [Header("Pulse Wave halka görünümü")]
    [SerializeField] Color pulseWaveColor = new Color(0.4f, 0.85f, 1f, 0.9f);
    [Tooltip("Halka çizgisinin kalınlığı (dünya birimi).")]
    [SerializeField] float pulseWaveThickness = 0.12f;
    [Tooltip("Düşman sprite'larından önde çizilsin diye yüksek tut.")]
    [SerializeField] int pulseWaveSortingOrder = 50;

    PlayerMovement movement;
    Health health;

    readonly List<SkillUpgradeData> skills = new List<SkillUpgradeData>();
    readonly List<int> skillLevels = new List<int>();   // slot bazında: skill'in mevcut seviyesi (0-tabanlı)
    float[] readyTimes;      // slot bazında: bu zamandan önce tekrar kullanılamaz
    int shieldCount;         // iç içe kalkanlar birbirinin dokunulmazlığını bozmasın

    // HUD yeni skill eklenince ikonunu göstermek için dinler
    public event Action<int, SkillUpgradeData> OnSkillAdded;

    public int SlotCount => slotActions != null ? slotActions.Length : 0;
    public bool HasFreeSlot => skills.Count < SlotCount;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        health = GetComponent<Health>();
        readyTimes = new float[SlotCount];
    }

    void OnEnable()
    {
        if (slotActions == null) return;
        foreach (var a in slotActions)
            if (a != null) a.action.Enable();
    }

    void OnDisable()
    {
        if (slotActions == null) return;
        foreach (var a in slotActions)
            if (a != null) a.action.Disable();
    }

    void Update()
    {
        // Oyun donukken (upgrade paneli / game over) tuşlar işlenmesin
        if (Time.timeScale == 0f) return;
        if (health != null && health.IsDead) return;

        for (int i = 0; i < skills.Count; i++)
        {
            if (slotActions[i] == null) continue;

            if (slotActions[i].action.WasPressedThisFrame() && Time.time >= readyTimes[i])
                UseSkill(i);
        }
    }

    // SkillUpgradeData.Apply çağırır: tier 0 = skill'i ver, tier 1+ = seviyesini yükselt.
    // (Kademe sırasını UpgradeManager garanti eder; burada sadece uygularız.)
    public void AddOrUpgrade(SkillUpgradeData skill, int tier)
    {
        if (skill == null) return;

        int slot = skills.IndexOf(skill);
        if (slot >= 0)
        {
            skillLevels[slot] = tier;   // geliştirme: yeni değerler bir sonraki kullanımda geçerli
            return;
        }

        if (!HasFreeSlot) return;

        skills.Add(skill);
        skillLevels.Add(tier);
        readyTimes[skills.Count - 1] = 0f;   // yeni skill hemen kullanılabilir

        OnSkillAdded?.Invoke(skills.Count - 1, skill);
    }

    // ---- HUD'un okuduğu bilgiler ----
    public SkillUpgradeData GetSkill(int slot)
        => slot >= 0 && slot < skills.Count ? skills[slot] : null;

    // 1 = az önce kullanıldı, 0 = hazır. (Cooldown overlay'in fillAmount'ı için.)
    public float GetCooldownNormalized(int slot)
    {
        SkillUpgradeData s = GetSkill(slot);
        if (s == null) return 0f;

        float cooldown = s.GetLevel(skillLevels[slot]).cooldown;
        if (cooldown <= 0f) return 0f;

        float remaining = readyTimes[slot] - Time.time;
        return Mathf.Clamp01(remaining / cooldown);
    }

    // Kalan cooldown süresi (saniye). Hazırsa 0. (HUD'daki sayaç yazısı için.)
    public float GetCooldownRemaining(int slot)
    {
        if (GetSkill(slot) == null) return 0f;
        return Mathf.Max(0f, readyTimes[slot] - Time.time);
    }

    // Slotun tuşunun ekranda gösterilecek adı ("Space", "E"...).
    public string GetKeyName(int slot)
    {
        if (slot < 0 || slot >= SlotCount || slotActions[slot] == null) return "";
        return slotActions[slot].action.GetBindingDisplayString();
    }

    // ---- Skill etkileri ----
    void UseSkill(int slot)
    {
        SkillUpgradeData s = skills[slot];
        SkillLevelStats lv = s.GetLevel(skillLevels[slot]);   // mevcut seviyenin değerleri

        readyTimes[slot] = Time.time + Mathf.Max(0.1f, lv.cooldown);

        switch (s.skillType)
        {
            case SkillType.Dash:
                Vector2 dashDir = Vector2.right;
                if (movement != null) dashDir = movement.StartDash(lv.dashSpeed, lv.dashDuration);
                SpawnDirectedEffect(s, dashDir);   // iz dash yönüne dönük, arkada kalır
                break;

            case SkillType.AreaBlast:
                DoBlast(lv);
                SpawnEffect(s, 1f);
                break;

            case SkillType.Shield:
                StartCoroutine(ShieldRoutine(s, lv));
                break;

            case SkillType.PulseWave:
                StartCoroutine(PulseWaveRoutine(lv));
                break;

            case SkillType.Burst:
                DoBurst(s, lv);
                SpawnEffect(s, 1f);   // opsiyonel merkez efekti (effectPrefab)
                break;
        }
    }

    // Karakterin etrafındaki N noktadan dışa doğru eşit açılı mermi fırlatır (fire spell gibi).
    void DoBurst(SkillUpgradeData s, SkillLevelStats lv)
    {
        if (s.burstProjectile == null) return;

        int n = Mathf.Max(1, lv.burstCount);
        float step = 360f / n;

        for (int i = 0; i < n; i++)
        {
            float deg = step * i;
            float rad = deg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            // Karakterden 'burstSpawnRadius' uzakta, o yöne dönük doğ
            Vector3 pos = transform.position + (Vector3)(dir * s.burstSpawnRadius);
            GameObject go = Instantiate(s.burstProjectile, pos, Quaternion.Euler(0f, 0f, deg));

            if (go.TryGetComponent<Rigidbody2D>(out var prb))
                prb.linearVelocity = dir * lv.burstSpeed;

            // Player takımına ata: düşmanlara hasar verir, oyuncuya değil
            if (go.TryGetComponent<Projectile>(out var proj))
            {
                proj.team = Team.Player;
                proj.damage = lv.burstDamage;
            }
        }
    }

    // Aralıklı olarak waveCount adet dışa büyüyen halka üretir (oyuncu merkezli).
    IEnumerator PulseWaveRoutine(SkillLevelStats lv)
    {
        int count = Mathf.Max(1, lv.waveCount);
        for (int i = 0; i < count; i++)
        {
            // Her halka, O ANKİ konumda doğar ve orada sabit kalır. Oyuncu hareket ederse
            // her pulse farklı yerde bırakılır (arkanda bir dizi halka).
            ShockwaveRing.Spawn(transform.position, lv.waveRadius, lv.waveExpandTime, lv.waveDamage,
                                pulseWaveColor, pulseWaveThickness, pulseWaveSortingOrder);

            if (i < count - 1)
                yield return new WaitForSeconds(lv.waveInterval);
        }
    }

    // Yarıçap içindeki tüm düşmanlara hasar. Sondan başa dönüyoruz çünkü ölen
    // düşman kendini EnemyRegistry listesinden siler (öne doğru dönseydik atlama olurdu).
    void DoBlast(SkillLevelStats lv)
    {
        float radiusSqr = lv.blastRadius * lv.blastRadius;
        var alive = EnemyRegistry.Alive;

        for (int i = alive.Count - 1; i >= 0; i--)
        {
            EnemyBase e = alive[i];
            if (e == null || e.IsDead) continue;

            float sqr = ((Vector2)e.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (sqr > radiusSqr) continue;

            if (e.TryGetComponent<Health>(out var h))
                h.TakeDamage(lv.blastDamage);
        }
    }

    IEnumerator ShieldRoutine(SkillUpgradeData s, SkillLevelStats lv)
    {
        // Sayaçla yönetiyoruz: iki kalkan üst üste binerse ilki biterken
        // ikincisinin dokunulmazlığını kapatmasın
        shieldCount++;
        if (health != null) health.Invulnerable = true;

        GameObject fx = SpawnEffect(s, 0f, attach: true);   // süre boyunca üstünde dursun

        yield return new WaitForSeconds(lv.shieldDuration); // timeScale'e uyar: panelde durur

        if (fx != null) Destroy(fx);

        shieldCount--;
        if (shieldCount <= 0 && health != null) health.Invulnerable = false;
    }

    // Yönlü efekt (dash izi gibi): oyuncunun konumunda, verilen yöne DÖNÜK doğar.
    // Efekt prefab'ı +X eksenine (sağa) bakacak şekilde çizilmiş varsayılır; iz arkaya
    // uzanacak biçimde tasarlandığı için oyuncunun sırtından geliyormuş gibi görünür.
    void SpawnDirectedEffect(SkillUpgradeData s, Vector2 dir)
    {
        if (s.effectPrefab == null) return;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        GameObject fx = Instantiate(s.effectPrefab, transform.position, Quaternion.Euler(0f, 0f, ang));
        Destroy(fx, 1f);   // SpriteAnimation kendini yok etmezse (loop) emniyet
    }

    // Skill'in görsel efektini oyuncunun üstünde doğurur.
    // attach: true -> child olur (oyuncuyla gezer, çağıran yok eder).
    // attach: false -> yerinde kalır ve lifetime sonra kendini yok eder.
    GameObject SpawnEffect(SkillUpgradeData s, float lifetime, bool attach = false)
    {
        if (s.effectPrefab == null) return null;

        GameObject fx = Instantiate(s.effectPrefab, transform.position, Quaternion.identity,
                                    attach ? transform : null);
        if (!attach && lifetime > 0f) Destroy(fx, lifetime);
        return fx;
    }
}
