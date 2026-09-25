using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Oyuncunun AKTİF yeteneklerini yönetir: slotlar, tuşlar, cooldown'lar ve etkiler.
// Skiller upgrade panelinden kazanılır ve geliştirilir (SkillUpgradeData.Apply ->
// AddOrUpgrade); sırayla slotlara oturur, her slotun kendi tuşu vardır (aşağıdaki listeden).
// SkillHUD, slot/cooldown bilgisini her karede buradan okur (WaveHUD kalıbı).
// Slotlar doluyken yeni skill seçilirse UpgradeManager oyuncuya hangi slotu
// değiştireceğini sorar ve ReplaceSkill çağırır (swap).
public class PlayerSkills : MonoBehaviour
{
    [Header("Slot tuşları")]
    [Tooltip("Her eleman bir skill slotudur (0. skill 0. tuşa bağlanır). " +
             "Listeden sonra aynı input asset'inde 'Skill{N}' adlı aksiyonlar varsa " +
             "(ör. Skill4) onlar da otomatik slot olarak eklenir.")]
    [SerializeField] InputActionReference[] slotActions;

    [Tooltip("Listedekilerden sonra input asset'inde aranacak aksiyon adı kalıbı ({0} = slot numarası, 1'den başlar).")]
    [SerializeField] string extraSlotActionFormat = "Skill{0}";

    [Header("Pulse Wave halka görünümü")]
    [SerializeField] Color pulseWaveColor = new Color(0.4f, 0.85f, 1f, 0.9f);
    [Tooltip("Halka çizgisinin kalınlığı (dünya birimi).")]
    [SerializeField] float pulseWaveThickness = 0.12f;
    [Tooltip("Düşman sprite'larından önde çizilsin diye yüksek tut.")]
    [SerializeField] int pulseWaveSortingOrder = 50;

    [Header("Yeni skill renkleri (halka / şimşek)")]
    [SerializeField] Color healColor = new Color(0.45f, 1f, 0.5f, 0.9f);
    [SerializeField] Color frostColor = new Color(0.65f, 0.95f, 1f, 0.95f);
    [SerializeField] Color overdriveColor = new Color(1f, 0.55f, 0.25f, 0.9f);
    [SerializeField] Color lightningColor = new Color(1f, 0.95f, 0.45f, 1f);
    [SerializeField] Color shieldColor = new Color(0.35f, 0.75f, 1f, 1f);
    [Tooltip("Dash izinin rengi (hayalet kopyalar + hız çizgisi).")]
    [SerializeField] Color dashColor = new Color(0.4f, 0.92f, 1f, 1f);

    [Header("Area Blast")]
    [Tooltip("Patlama cephesinin merkezden blastRadius'a ulaşma süresi (sn). Kısa = sert patlama.")]
    [SerializeField] float blastExpandTime = 0.3f;
    [Tooltip("Şimşek çizgisinin kalınlığı (dünya birimi).")]
    [SerializeField] float lightningThickness = 0.08f;

    PlayerMovement movement;
    Health health;
    PlayerWeapons weapons;

    readonly List<SkillUpgradeData> skills = new List<SkillUpgradeData>();
    readonly List<int> skillLevels = new List<int>();   // slot bazında: skill'in mevcut seviyesi (0-tabanlı)
    float[] readyTimes;      // slot bazında: bu zamandan önce tekrar kullanılamaz
    int shieldCount;         // iç içe kalkanlar birbirinin dokunulmazlığını bozmasın
    InputAction[] actions;   // slot tuşları: referanslar + asset'te adıyla bulunan ek slotlar
    int ignoreInputFrame = -1;   // swap ekranında slot tuşuyla seçim yapılan kare

    // HUD yeni skill eklenince ikonunu göstermek için dinler
    public event Action<int, SkillUpgradeData> OnSkillAdded;

    // Bir slot skill değiştirince (swap) HUD ikonu güncellemek için dinler
    public event Action<int, SkillUpgradeData> OnSkillReplaced;

    public int SlotCount => Actions.Length;
    public int SkillCount => skills.Count;
    public bool HasFreeSlot => skills.Count < SlotCount;

    // Awake'ten önce (başka bir scriptin Awake'i) sorulursa da doğru cevap versin diye tembel kurulur.
    InputAction[] Actions => actions ??= ResolveActions();

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        health = GetComponent<Health>();
        weapons = GetComponentInChildren<PlayerWeapons>();
        readyTimes = new float[SlotCount];
    }

    // Inspector'daki referanslar + aynı asset'te sıradaki "Skill{N}" aksiyonları.
    // Böylece yeni slot için sadece .inputactions'a aksiyon eklemek yeter, prefab değişmez.
    InputAction[] ResolveActions()
    {
        var list = new List<InputAction>();
        InputActionAsset asset = null;

        if (slotActions != null)
            foreach (var r in slotActions)
            {
                list.Add(r != null ? r.action : null);
                if (asset == null && r != null && r.action != null) asset = r.action.actionMap?.asset;
            }

        if (asset != null && !string.IsNullOrEmpty(extraSlotActionFormat))
        {
            while (true)
            {
                InputAction next = asset.FindAction(string.Format(extraSlotActionFormat, list.Count + 1));
                if (next == null || list.Contains(next)) break;
                list.Add(next);
            }
        }
        return list.ToArray();
    }

    void OnEnable()
    {
        foreach (var a in Actions)
            if (a != null) a.Enable();
    }

    void OnDisable()
    {
        foreach (var a in Actions)
            if (a != null) a.Disable();
    }

    // Swap ekranı: bu karede basılan slot tuşu (yoksa -1). Oyun donukken de çalışır.
    public int GetPressedSlot()
    {
        for (int i = 0; i < Actions.Length; i++)
            if (Actions[i] != null && Actions[i].WasPressedThisFrame()) return i;
        return -1;
    }

    void Update()
    {
        // Oyun donukken (upgrade paneli / game over) tuşlar işlenmesin
        if (Time.timeScale == 0f) return;
        if (health != null && health.IsDead) return;
        if (Time.frameCount == ignoreInputFrame) return;   // swap tuşu bu karede skill kullanmasın

        for (int i = 0; i < skills.Count; i++)
        {
            if (Actions[i] == null) continue;

            if (Actions[i].WasPressedThisFrame() && Time.time >= readyTimes[i])
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

    // Swap: slottaki skill'i yenisiyle (ilk seviyesiyle) değiştirir. Eski skill'in
    // seviyesi kaybolur; ilerlemeyi sıfırlamak UpgradeManager'ın işi.
    // Değiştirilen skill'in sürmekte olan etkisi (kalkan, overdrive) süresini doldurur.
    public void ReplaceSkill(int slot, SkillUpgradeData skill)
    {
        if (skill == null || slot < 0 || slot >= skills.Count) return;
        if (skills.Contains(skill)) return;   // zaten slotta (normalde teklif edilmez)

        skills[slot] = skill;
        skillLevels[slot] = 0;
        readyTimes[slot] = 0f;   // yeni skill hemen kullanılabilir
        ignoreInputFrame = Time.frameCount;   // seçim tuşu (Space/E/R/Q) aynı karede skill'i ateşlemesin

        OnSkillReplaced?.Invoke(slot, skill);
    }

    // ---- HUD'un okuduğu bilgiler ----
    public bool HasSkill(SkillUpgradeData skill) => skills.Contains(skill);

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
    // Input System tuş adlarını klavye düzeninden alır; boşluk tuşu bu yüzden bazı
    // dillerde farklı yazılıyor (TR'de "Boşluk" gibi). Space'i her düzende sabit
    // tutuyoruz, diğer tuşlar Input System'in verdiği adla kalsın.
    public string GetKeyName(int slot)
    {
        if (slot < 0 || slot >= SlotCount || Actions[slot] == null) return "";

        InputAction action = Actions[slot];

        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            if (bindings[i].isComposite || bindings[i].isPartOfComposite) continue;
            if (IsSpacePath(bindings[i].effectivePath)) return "Space";
        }

        return action.GetBindingDisplayString();
    }

    // "<Keyboard>/space" gibi bir yol boşluk tuşunu mu gösteriyor?
    static bool IsSpacePath(string path)
        => !string.IsNullOrEmpty(path) && path.EndsWith("/space", StringComparison.OrdinalIgnoreCase);

    // ---- Skill etkileri ----
    void UseSkill(int slot)
    {
        SkillUpgradeData s = skills[slot];
        SkillLevelStats lv = s.GetLevel(skillLevels[slot]);   // mevcut seviyenin değerleri

        readyTimes[slot] = Time.time + Mathf.Max(0.1f, lv.cooldown);
        AudioManager.PlaySkill();

        switch (s.skillType)
        {
            case SkillType.Dash:
                Vector2 dashDir = Vector2.right;
                if (movement != null) dashDir = movement.StartDash(lv.dashSpeed, lv.dashDuration);
                DashTrail.Play(gameObject, dashDir, lv.dashDuration, dashColor);
                SpawnDirectedEffect(s, dashDir);   // iz dash yönüne dönük, arkada kalır
                break;

            case SkillType.AreaBlast:
                // Dışa büyüyen ateş patlaması: cephe düşmana değince hasar verir (görsel kenar = hasar kenarı).
                // effectPrefab (Fire Ball animasyonu) merkezde büyüyüp söner.
                BlastWave.Spawn(transform.position, lv.blastRadius, blastExpandTime, lv.blastDamage,
                                s.effectPrefab, s.effectVisualScale);
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

            case SkillType.Heal:
                if (health != null) health.Heal(lv.healAmount);
                SkillVfx.Heal(transform, healColor);
                SpawnEffect(s, 1f);
                break;

            case SkillType.FrostNova:
                DoFrostNova(lv);
                SkillVfx.FrostNova(transform.position, lv.frostRadius, frostColor);   // görsel = etki alanı
                SpawnEffect(s, 1f);
                break;

            case SkillType.Overdrive:
                StartCoroutine(OverdriveRoutine(s, lv));
                break;

            case SkillType.ChainLightning:
                DoChainLightning(lv);
                break;
        }
    }

    // Yarıçaptaki düşmanlara hasar + yavaşlatma. Önce yavaşlat, sonra vur:
    // hasar düşmanı öldürürse listeden silinir (sondan başa dönme sebebi, bkz. BlastWave.ApplyDamage).
    void DoFrostNova(SkillLevelStats lv)
    {
        float radiusSqr = lv.frostRadius * lv.frostRadius;
        var alive = EnemyRegistry.Alive;

        for (int i = alive.Count - 1; i >= 0; i--)
        {
            EnemyBase e = alive[i];
            if (e == null || e.IsDead) continue;

            float sqr = ((Vector2)e.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (sqr > radiusSqr) continue;

            e.ApplySlow(lv.slowPercent, lv.slowDuration);
            if (e.TryGetComponent<Health>(out var h))
                h.TakeDamage(lv.frostDamage);
        }
    }

    // Süreli güç: PlayerWeapons çarpanlarına bonus ekler, süre sonunda aynı miktarı geri alır.
    // Çarpanlar toplamsal olduğu için üst üste binen Overdrive'lar ve stat upgrade'leri bozulmaz.
    IEnumerator OverdriveRoutine(SkillUpgradeData s, SkillLevelStats lv)
    {
        if (weapons == null) yield break;

        float dmg = lv.overdriveDamageBonus;
        float rate = lv.overdriveFireRateBonus;
        weapons.AddDamageMultiplier(dmg);
        weapons.AddFireRateMultiplier(rate);

        var aura = OverdriveAura.Attach(gameObject, lv.overdriveDuration, overdriveColor);
        GameObject fx = SpawnEffect(s, 0f, attach: true);   // opsiyonel ek prefab

        yield return new WaitForSeconds(lv.overdriveDuration);   // timeScale'e uyar: panelde durur

        if (aura != null) aura.Stop();
        if (fx != null) Destroy(fx);
        weapons.AddDamageMultiplier(-dmg);
        weapons.AddFireRateMultiplier(-rate);
    }

    // Oyuncuya en yakın düşmandan başlar, her seferinde henüz vurulmamış en yakın
    // düşmana seker (chainRange içinde). Hedefler ÖNCE toplanır, hasar SONRA verilir:
    // ölen düşman EnemyRegistry listesini değiştirir, arama sırasında bu olmasın.
    readonly List<EnemyBase> chainTargets = new List<EnemyBase>();

    void DoChainLightning(SkillLevelStats lv)
    {
        chainTargets.Clear();
        Vector2 from = transform.position;
        int count = Mathf.Max(1, lv.chainCount);

        for (int i = 0; i < count; i++)
        {
            EnemyBase next = FindNearestEnemy(from, lv.chainRange, chainTargets);
            if (next == null) break;
            chainTargets.Add(next);
            from = next.transform.position;
        }

        if (chainTargets.Count == 0) return;

        var points = new Vector3[chainTargets.Count + 1];
        points[0] = transform.position;
        for (int i = 0; i < chainTargets.Count; i++)
            points[i + 1] = chainTargets[i].transform.position;
        LightningArc.Spawn(points, lightningColor, lightningThickness, 0.25f, pulseWaveSortingOrder);

        foreach (var e in chainTargets)
            if (e != null && !e.IsDead && e.TryGetComponent<Health>(out var h))
                h.TakeDamage(lv.chainDamage);
    }

    static EnemyBase FindNearestEnemy(Vector2 from, float range, List<EnemyBase> exclude)
    {
        EnemyBase best = null;
        float bestSqr = range * range;
        var alive = EnemyRegistry.Alive;

        for (int i = 0; i < alive.Count; i++)
        {
            EnemyBase e = alive[i];
            if (e == null || e.IsDead || exclude.Contains(e)) continue;

            float sqr = ((Vector2)e.transform.position - from).sqrMagnitude;
            if (sqr <= bestSqr) { bestSqr = sqr; best = e; }
        }
        return best;
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

    IEnumerator ShieldRoutine(SkillUpgradeData s, SkillLevelStats lv)
    {
        // Sayaçla yönetiyoruz: iki kalkan üst üste binerse ilki biterken
        // ikincisinin dokunulmazlığını kapatmasın
        shieldCount++;
        if (health != null) health.Invulnerable = true;

        var bubble = ShieldBubble.Attach(gameObject, lv.shieldDuration, shieldColor);
        GameObject fx = SpawnEffect(s, 0f, attach: true);   // opsiyonel ek prefab

        yield return new WaitForSeconds(lv.shieldDuration); // timeScale'e uyar: panelde durur

        if (bubble != null) bubble.Stop();
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
