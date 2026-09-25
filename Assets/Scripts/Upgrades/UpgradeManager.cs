using System.Collections.Generic;
using UnityEngine;

// Level atlayınca oyunu dondurup 3 seçenekli upgrade panelini açar.
// Aynı anda birden fazla level atlanırsa seçimler sırayla yaptırılır.
//
// KADEME TAKİBİ: Her upgrade'in kaç kez alındığı buradaki sözlükte tutulur
// (asset'te DEĞİL — asset'e yazılsaydı ilerleme Play bitince de kalırdı).
// Bir upgrade her seçilişinde bir sonraki kademesi teklif edilir.
//
// SKILL SWAP: Slotlar doluyken yeni bir skill seçilirse panel gizlenir ve SkillHUD
// oyuncuya hangi skill'i değiştireceğini sorar. Değiştirilen skill'in ilerlemesi
// sıfırlanır (ileride yeniden 1. seviyeden teklif edilebilir). Vazgeçerse aynı
// seçenekler tekrar gösterilir.
//
// SİLAH SWAP: Silah slotları doluyken silah kartı seçilirse aynı akış WeaponHUD ile
// işler: oyuncu bırakacağı silahı seçer, para ancak seçimden sonra ödenir.
public class UpgradeManager : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] UpgradePanel panel;
    [Tooltip("Slotlar doluyken skill değiştirme ekranı. Boşsa sahnede aranır.")]
    [SerializeField] SkillHUD skillHud;
    [Tooltip("Silah slotları doluyken silah değiştirme ekranı. Boşsa sahnede aranır.")]
    [SerializeField] WeaponHUD weaponHud;

    [Tooltip("Teklif edilebilecek tüm upgrade'ler. Panel bunlar arasından rastgele seçer.")]
    [SerializeField] List<UpgradeData> pool = new List<UpgradeData>();

    [Header("Ayarlar")]
    [Tooltip("Panelde kaç seçenek gösterilsin.")]
    [SerializeField] int choiceCount = 3;
    [Tooltip("Bir panelde paralı (silah) kart çıkma ihtimali. 1 = her seferinde (alınabilir silah varsa). " +
             "Düşük = silaha ulaşmak zorlaşır.")]
    [Range(0f, 1f)] [SerializeField] float weaponCardChance = 0.45f;

    [Header("Reroll")]
    [Tooltip("Kartları yenilemenin ilk fiyatı. -1 = reroll kapalı.")]
    [SerializeField] int rerollBaseCost = 5;
    [Tooltip("Aynı seçimde her reroll'dan sonra fiyata eklenen miktar. Yeni level'da fiyat sıfırlanır.")]
    [SerializeField] int rerollCostIncrease = 5;

    PlayerContext ctx;
    PlayerStats stats;

    int pendingLevelUps;   // seçim bekleyen level sayısı
    bool panelOpen;
    int rerollsThisPanel;  // bu seçimde kaç kez reroll yapıldı (fiyatı artırır)
    int freeRerolls;       // oyun boyu bedava reroll hakkı (kalıcı "Lucky Dice" upgrade'i)

    int RerollCost => rerollBaseCost < 0 ? -1
                    : freeRerolls > 0 ? 0
                    : rerollBaseCost + rerollsThisPanel * rerollCostIncrease;

    public void AddFreeRerolls(int count) => freeRerolls += Mathf.Max(0, count);

    // upgrade -> kaç kez alındı (bir sonraki teklif edilecek kademe)
    readonly Dictionary<UpgradeData, int> timesTaken = new Dictionary<UpgradeData, int>();

    // Bir upgrade uygulandığında tetiklenir (HUD bildirimi dinler).
    public event System.Action<UpgradeData, int> OnUpgradeApplied;

    // Her seçimde yeni liste ayırmamak için tekrar kullanılan tamponlar.
    // Adaylar bedava/paralı diye ayrı tutulur: panelde en fazla 1 paralı kart olsun.
    readonly List<UpgradeChoice> freeCandidates = new List<UpgradeChoice>();
    readonly List<UpgradeChoice> paidCandidates = new List<UpgradeChoice>();
    readonly List<UpgradeChoice> choices = new List<UpgradeChoice>();

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p == null)
        {
            Debug.LogWarning("[UpgradeManager] 'Player' tag'li obje bulunamadı.", this);
            enabled = false;
            return;
        }

        ctx = new PlayerContext(p);
        stats = ctx.stats;

        if (stats == null)
        {
            Debug.LogWarning("[UpgradeManager] Player'da PlayerStats yok.", this);
            enabled = false;
            return;
        }
        if (panel == null)
        {
            Debug.LogWarning("[UpgradeManager] Upgrade Panel atanmamış.", this);
            enabled = false;
            return;
        }

        if (skillHud == null) skillHud = FindAnyObjectByType<SkillHUD>();
        if (weaponHud == null) weaponHud = FindAnyObjectByType<WeaponHUD>();

        stats.OnLevelUp += HandleLevelUp;
        panel.Hide();
    }

    void OnDestroy()
    {
        if (stats != null) stats.OnLevelUp -= HandleLevelUp;
    }

    void HandleLevelUp(int newLevel)
    {
        // Oyuncu öldüyse panel açma (Game Over ekranıyla çakışmasın)
        if (ctx.health != null && ctx.health.IsDead) return;

        pendingLevelUps++;
        TryOpenNext();
    }

    // Bekleyen level varsa paneli açar; yoksa oyunu devam ettirir.
    void TryOpenNext()
    {
        if (panelOpen) return;

        while (pendingLevelUps > 0)
        {
            PickChoices();

            if (choices.Count > 0)
            {
                panelOpen = true;
                rerollsThisPanel = 0;
                Time.timeScale = 0f;          // oyunu dondur (UI zamandan etkilenmez)
                ShowPanel();
                return;
            }

            // Teklif edilebilecek upgrade kalmadı (ör. tüm kademeler alındı ve slotlar dolu):
            // bu level'ı sessizce geç, oyun kilitlenmesin.
            pendingLevelUps--;
        }

        // Sıra bitti. Oyuncu bu arada öldüyse donuk kalsın (Game Over ekranı açık).
        if (ctx.health == null || !ctx.health.IsDead)
            Time.timeScale = 1f;
    }

    void OnChosen(UpgradeChoice chosen)
    {
        // Panel zaten kapandıysa (çift tıklama) ikinci kez uygulama
        if (!panelOpen) return;

        // Slotlar dolu ve yeni skill seçildi: önce hangi skill'in gideceğini sor
        if (NeedsSwap(chosen))
        {
            panel.Hide();
            skillHud.BeginSwap((SkillUpgradeData)chosen.data,
                               slot => FinishSwap(chosen, slot),
                               ReshowPanel);
            return;
        }

        // Silah slotları dolu: önce hangi silahın bırakılacağını sor
        if (NeedsWeaponSwap(chosen.data))
        {
            panel.Hide();
            weaponHud.BeginSwap((WeaponUpgradeData)chosen.data,
                                slot => FinishWeaponSwap(chosen, slot),
                                ReshowPanel);
            return;
        }

        if (chosen.data != null)
        {
            if (!TryPay(chosen)) return;

            chosen.data.Apply(ctx, chosen.tier);
            timesTaken[chosen.data] = chosen.tier + 1;   // bir sonraki kademeye ilerle
            OnUpgradeApplied?.Invoke(chosen.data, chosen.tier);
        }

        CloseAndContinue();
    }

    bool NeedsSwap(UpgradeChoice c) => NeedsSwap(c.data, c.tier);

    bool NeedsSwap(UpgradeData data, int tier)
        => data is SkillUpgradeData && tier == 0
           && ctx.skills != null && !ctx.skills.HasFreeSlot
           && skillHud != null;

    // Slotlar dolu VE sahip olunan bir kopyayla birleşmiyorsa swap gerekir
    bool NeedsWeaponSwap(UpgradeData data)
        => data is WeaponUpgradeData w && ctx.weapons != null && !ctx.weapons.HasFreeSlot
           && !ctx.weapons.CanMerge(w.weapon) && weaponHud != null;

    // Parası varsa öde; yetmezse seçimi işleme (normalde buton pasif olduğu için buraya gelinmez)
    bool TryPay(UpgradeChoice c)
    {
        int cost = c.data.GetCost(c.tier);
        return cost <= 0 || (stats != null && stats.TrySpendMoney(cost));
    }

    void FinishSwap(UpgradeChoice chosen, int slot)
    {
        if (!panelOpen) return;
        if (!TryPay(chosen)) { ReshowPanel(); return; }

        var skill = (SkillUpgradeData)chosen.data;
        SkillUpgradeData old = ctx.skills.GetSkill(slot);

        ctx.skills.ReplaceSkill(slot, skill);
        if (old != null) timesTaken.Remove(old);   // eski skill ileride baştan teklif edilebilir
        timesTaken[skill] = 1;
        OnUpgradeApplied?.Invoke(skill, 0);

        CloseAndContinue();
    }

    void FinishWeaponSwap(UpgradeChoice chosen, int slot)
    {
        if (!panelOpen) return;
        if (!TryPay(chosen)) { ReshowPanel(); return; }

        var up = (WeaponUpgradeData)chosen.data;
        ctx.weapons.ReplaceWeapon(slot, up.weapon);
        OnUpgradeApplied?.Invoke(up, chosen.tier);

        CloseAndContinue();
    }

    // Swap'tan vazgeçildi: aynı seçenekleri tekrar göster (oyun donuk kalır).
    void ReshowPanel()
    {
        if (!panelOpen) return;
        ShowPanel();
    }

    void ShowPanel()
    {
        int money = stats != null ? stats.Money : 0;
        panel.Show(choices, money, OnChosen, RerollCost, Reroll);
    }

    // Parayla yeni kartlar çek. Aynı seçimde her reroll bir öncekinden pahalı.
    void Reroll()
    {
        if (!panelOpen) return;
        int cost = RerollCost;
        if (cost < 0) return;
        if (freeRerolls > 0) freeRerolls--;                 // bedava hak: fiyat artmaz
        else if (cost > 0 && (stats == null || !stats.TrySpendMoney(cost))) return;
        else rerollsThisPanel++;
        PickChoices();
        ShowPanel();
    }

    void CloseAndContinue()
    {
        panelOpen = false;
        pendingLevelUps--;
        panel.Hide();

        TryOpenNext();   // başka bekleyen level varsa devam eder, yoksa oyunu çalıştırır
    }

    // Havuzdan, şu an teklif edilebilir olanlar arasından rastgele ve TEKRARSIZ seç.
    // Her upgrade için sıradaki kademesi teklif edilir.
    // KURAL: Panelde EN FAZLA 1 paralı (weapon) kart olur, o da weaponCardChance ihtimalle;
    // diğerleri bedava (stat/skill). Bedavalar yetmezse panel paralılarla tamamlanır.
    void PickChoices()
    {
        freeCandidates.Clear();
        paidCandidates.Clear();

        for (int i = 0; i < pool.Count; i++)
        {
            UpgradeData u = pool[i];
            if (u == null) continue;

            timesTaken.TryGetValue(u, out int tier);   // hiç alınmadıysa 0
            if (!u.CanOffer(ctx, tier)) continue;

            var choice = new UpgradeChoice
            {
                data = u, tier = tier,
                replacesSkill = NeedsSwap(u, tier),
                replacesWeapon = NeedsWeaponSwap(u),
            };
            if (u.GetCost(tier) > 0) paidCandidates.Add(choice);
            else freeCandidates.Add(choice);
        }

        Shuffle(freeCandidates);
        Shuffle(paidCandidates);

        choices.Clear();

        // En fazla 1 paralı kart, o da her panelde değil (silaha ulaşmak kolay olmasın)
        bool offerWeapon = paidCandidates.Count > 0 && Random.value < weaponCardChance;
        if (offerWeapon)
            choices.Add(paidCandidates[0]);

        // Kalanları bedavalardan doldur
        for (int i = 0; i < freeCandidates.Count && choices.Count < choiceCount; i++)
            choices.Add(freeCandidates[i]);

        // Bedava yetmediyse (hepsi alınmış/slot dolu) kalan paralılarla tamamla — panel eksik kalmasın
        for (int i = offerWeapon ? 1 : 0; i < paidCandidates.Count && choices.Count < choiceCount; i++)
            choices.Add(paidCandidates[i]);

        // Paralı kart hep aynı sırada görünmesin diye son bir karıştırma
        Shuffle(choices);
    }

    // Fisher-Yates karıştırma
    void Shuffle(List<UpgradeChoice> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            UpgradeChoice temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
