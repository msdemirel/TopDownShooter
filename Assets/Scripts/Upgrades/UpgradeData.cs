using UnityEngine;

// Panelde gösterilen tek bir seçenek: hangi upgrade + kaçıncı kademesi.
// Kademe (tier) 0-tabanlıdır: ilk seçiliş 0, ikinci 1...
public struct UpgradeChoice
{
    public UpgradeData data;
    public int tier;
    public bool replacesSkill;   // slotlar dolu: seçilirse bir skill'in yerine geçer (kartta not + ikon)
}

// Tüm upgrade'lerin ortak temeli.
// Yeni bir upgrade TÜRÜ eklemek için bu sınıftan türet ve Apply'ı yaz.
// (İleride SkillUpgradeData da böyle eklenecek — UpgradeManager'ın değişmesi gerekmez.)
//
// KADEME SİSTEMİ: Bir upgrade birden fazla kademeden oluşabilir (ör. Can: +2, +5, +10).
// Kademeler HER ZAMAN sırayla teklif edilir; oyuncu 1. kademeyi almadan 2.'yi göremez.
// "Kaçıncı kademedeyiz" bilgisi BURADA TUTULMAZ — ScriptableObject bir asset'tir,
// üstüne yazılan her şey Play bitince de kalır. İlerlemeyi UpgradeManager tutar.
public abstract class UpgradeData : ScriptableObject
{
    [Header("Panelde Görünüm")]
    public string title = "Upgrade";
    [Tooltip("Kademeli upgrade'lerde {0} yazarsan o kademenin miktarı yerine geçer. " +
             "Ör: \"Maks canı +{0} artırır\"")]
    [TextArea] public string description = "";
    public Sprite icon;

    [Header("Dalga Kilidi")]
    [Tooltip("Bu dalgadan itibaren teklif edilir (1 = baştan beri). " +
             "Ör: 6 yazarsan 5. dalga bitip 6. dalga başlayınca panelde çıkmaya başlar.")]
    [Min(1)] public int unlockWave = 1;
    [Tooltip("Bu dalgadan SONRA artık teklif edilmez (0 = hiç kapanmaz). " +
             "Zayıf silah sürümlerini emekli etmek için: Sword I -> 8 yazıp Sword II'yi 5'te açmak gibi.")]
    [Min(0)] public int lastWave = 0;

    // Toplam kademe sayısı. 1 = tek seferlik upgrade.
    public virtual int TierCount => 1;

    // Şu anki dalga, bu upgrade'in açık olduğu aralıkta mı?
    public bool IsUnlocked(PlayerContext ctx)
    {
        int wave = ctx.CurrentWave;
        return wave >= unlockWave && (lastWave <= 0 || wave <= lastWave);
    }

    // Bu kademe şu anda teklif edilebilir mi?
    // Varsayılan: dalga kilidi açıksa ve kademeler bitmediyse evet.
    // (Silah upgrade'i kademe yerine slot kontrolü yapar ama dalga kilidine yine uyar.)
    public virtual bool CanOffer(PlayerContext ctx, int tier) => IsUnlocked(ctx) && tier < TierCount;

    // Oyuncu bu kademeyi seçince çalışır.
    public abstract void Apply(PlayerContext ctx, int tier);

    // Panelde görünen başlık: kademeliyse "Can Takviyesi 2" gibi numaralanır.
    public virtual string GetTitle(int tier)
        => TierCount > 1 ? $"{title} {tier + 1}" : title;

    public virtual string GetDescription(int tier) => description;

    // Upgrade SEÇİLDİKTEN sonra HUD'da kısaca gösterilecek özet: sadece DEĞİŞİM
    // yazılır ("+1 Hasar" gibi), tam değer değil. Boş dönerse HUD bir şey göstermez.
    public virtual string GetBonusSummary(int tier) => "";

    // Bu seçimin parası. 0 = bedava (kartta "FREE" yazar). Weapon/Skill ezer ve fiyat gösterir.
    public virtual int GetCost(int tier) => 0;

    // ---- Açıklama yardımcıları (alt sınıflar kullanır) ----
    // Kazanımları panelde renkli göstermek için TMP rich text etiketleri.
    // NOT: UpgradeButton'daki TMP metinlerinde "Rich Text" açık olmalı (varsayılan açık).
    protected static string Green(string s) => $"<color=#5EDC5E>{s}</color>";
    protected static string Red(string s) => $"<color=#E5533C>{s}</color>";

    // Sayıyı gereksiz ondalık olmadan yazar: 18 -> "18", 18.5 -> "18.5"
    protected static string Num(float v) => v.ToString("0.##");
}
