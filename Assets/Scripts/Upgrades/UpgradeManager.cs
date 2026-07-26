using System.Collections.Generic;
using UnityEngine;

// Level atlayınca oyunu dondurup 3 seçenekli upgrade panelini açar.
// Aynı anda birden fazla level atlanırsa seçimler sırayla yaptırılır.
//
// KADEME TAKİBİ: Her upgrade'in kaç kez alındığı buradaki sözlükte tutulur
// (asset'te DEĞİL — asset'e yazılsaydı ilerleme Play bitince de kalırdı).
// Bir upgrade her seçilişinde bir sonraki kademesi teklif edilir.
public class UpgradeManager : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] UpgradePanel panel;

    [Tooltip("Teklif edilebilecek tüm upgrade'ler. Panel bunlar arasından rastgele seçer.")]
    [SerializeField] List<UpgradeData> pool = new List<UpgradeData>();

    [Header("Ayarlar")]
    [Tooltip("Panelde kaç seçenek gösterilsin.")]
    [SerializeField] int choiceCount = 3;

    PlayerContext ctx;
    PlayerStats stats;

    int pendingLevelUps;   // seçim bekleyen level sayısı
    bool panelOpen;

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
                Time.timeScale = 0f;          // oyunu dondur (UI zamandan etkilenmez)
                int money = stats != null ? stats.Money : 0;
                panel.Show(choices, money, OnChosen);
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

        if (chosen.data != null)
        {
            // Parası varsa öde; yetmezse seçimi işleme (normalde buton pasif olduğu için buraya gelinmez)
            int cost = chosen.data.GetCost(chosen.tier);
            if (cost > 0 && (stats == null || !stats.TrySpendMoney(cost)))
                return;

            chosen.data.Apply(ctx, chosen.tier);
            timesTaken[chosen.data] = chosen.tier + 1;   // bir sonraki kademeye ilerle
            OnUpgradeApplied?.Invoke(chosen.data, chosen.tier);
        }

        panelOpen = false;
        pendingLevelUps--;
        panel.Hide();

        TryOpenNext();   // başka bekleyen level varsa devam eder, yoksa oyunu çalıştırır
    }

    // Havuzdan, şu an teklif edilebilir olanlar arasından rastgele ve TEKRARSIZ seç.
    // Her upgrade için sıradaki kademesi teklif edilir.
    // KURAL: Panelde EN FAZLA 1 paralı (weapon) kart olur; diğerleri bedava (stat/skill).
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

            var choice = new UpgradeChoice { data = u, tier = tier };
            if (u.GetCost(tier) > 0) paidCandidates.Add(choice);
            else freeCandidates.Add(choice);
        }

        Shuffle(freeCandidates);
        Shuffle(paidCandidates);

        choices.Clear();

        // En fazla 1 paralı kart
        if (paidCandidates.Count > 0)
            choices.Add(paidCandidates[0]);

        // Kalanları bedavalardan doldur
        for (int i = 0; i < freeCandidates.Count && choices.Count < choiceCount; i++)
            choices.Add(freeCandidates[i]);

        // Bedava yetmediyse (hepsi alınmış/slot dolu) kalan paralılarla tamamla — panel eksik kalmasın
        for (int i = 1; i < paidCandidates.Count && choices.Count < choiceCount; i++)
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
