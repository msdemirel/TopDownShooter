using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ana menüdeki mağaza: Core ile kalıcı upgrade alma (UPGRADES sekmesi) ve kilitli
// içeriklerin koşul/ilerlemesi (UNLOCKS sekmesi). İçerik MetaProgress.Catalog'dan her
// açılışta kodla kurulur: kataloğa yeni upgrade/kilit eklenince ekran kendiliğinden güncellenir.
//
// Kurulum: Menü > TopDownShooter > UI > Mağazayı Kur (MainMenu sahnesi açıkken)
public class MetaShopUI : MonoBehaviour
{
    [Header("Referanslar (kurulum menüsü bağlar)")]
    [SerializeField] RectTransform upgradesContent;
    [SerializeField] RectTransform unlocksContent;
    [SerializeField] TMP_Text coreText;
    [SerializeField] Button upgradesTab;
    [SerializeField] Button unlocksTab;

    [Header("Görünüm")]
    [SerializeField] Sprite cardSprite;
    [SerializeField] Sprite buttonSprite;
    [SerializeField] Sprite coreIcon;
    [SerializeField] Sprite checkIcon;
    [SerializeField] TMP_FontAsset font;

    [Header("Yerleşim")]
    [SerializeField] int upgradeColumns = 4;
    [SerializeField] Vector2 upgradeCardSize = new Vector2(400f, 244f);
    [SerializeField] int unlockColumns = 2;
    [SerializeField] Vector2 unlockRowSize = new Vector2(800f, 76f);
    [SerializeField] Vector2 spacing = new Vector2(20f, 16f);

    static readonly Color White = new Color32(0xF4, 0xF4, 0xF4, 0xFF);
    static readonly Color Muted = new Color32(0x94, 0xB0, 0xC2, 0xFF);
    static readonly Color Gold = new Color32(0xFF, 0xCD, 0x75, 0xFF);
    static readonly Color Cyan = new Color32(0x73, 0xEF, 0xF7, 0xFF);
    static readonly Color Green = new Color32(0x5E, 0xDC, 0x5E, 0xFF);
    static readonly Color Red = new Color(1f, 0.4f, 0.4f);
    static readonly Color PipOff = new Color(0.16f, 0.19f, 0.3f);
    static readonly Color BarBack = new Color(0.07f, 0.08f, 0.14f);

    // Satın alma sonrası tazelenecek kart parçaları
    class UpgradeCard
    {
        public MetaUpgradeData data;
        public RectTransform root;
        public Image[] pips;
        public TMP_Text effect, cost;
        public Button buy;
        public Image costIcon;
    }

    readonly List<UpgradeCard> cards = new List<UpgradeCard>();
    bool showingUnlocks;

    void OnEnable()
    {
        if (upgradesTab != null) { upgradesTab.onClick.RemoveAllListeners(); upgradesTab.onClick.AddListener(() => ShowTab(false)); }
        if (unlocksTab != null) { unlocksTab.onClick.RemoveAllListeners(); unlocksTab.onClick.AddListener(() => ShowTab(true)); }

        BuildUpgrades();
        BuildUnlocks();
        ShowTab(showingUnlocks);
        RefreshAll();
    }

    public void ShowTab(bool unlocks)
    {
        showingUnlocks = unlocks;
        if (upgradesContent != null) upgradesContent.gameObject.SetActive(!unlocks);
        if (unlocksContent != null) unlocksContent.gameObject.SetActive(unlocks);
        TintTab(upgradesTab, !unlocks);
        TintTab(unlocksTab, unlocks);
    }

    static void TintTab(Button tab, bool active)
    {
        if (tab == null) return;
        if (tab.targetGraphic != null) tab.targetGraphic.color = active ? Cyan : new Color(1f, 1f, 1f, 0.55f);
        var label = tab.GetComponentInChildren<TMP_Text>();
        if (label != null) label.color = active ? White : Muted;
    }

    // ================= UPGRADES =================
    void BuildUpgrades()
    {
        cards.Clear();
        if (upgradesContent == null) return;
        Clear(upgradesContent);
        var catalog = MetaProgress.Catalog;
        if (catalog == null) { EmptyNote(upgradesContent, "Run TopDownShooter > Meta > Setup first."); return; }

        int n = catalog.upgrades.Count;
        for (int i = 0; i < n; i++)
        {
            var data = catalog.upgrades[i];
            if (data == null) continue;
            var card = new UpgradeCard { data = data };
            card.root = Box($"Upgrade_{data.id}", upgradesContent, cardSprite,
                            GridPos(i, n, upgradeColumns, upgradeCardSize), upgradeCardSize);

            Pic(card.root, "Icon", data.icon, new Vector2(20f, -20f), new Vector2(72f, 72f));
            // Uzun başlık ("STARTING FUNDS") karta sığmazsa küçülür
            AutoSize(Label(card.root, "Title", data.title, 28f, White, new Vector2(108f, -20f),
                           new Vector2(upgradeCardSize.x - 108f - 16f, 34f)), 18f);

            // Seviye göstergesi: dolu = alınmış
            card.pips = new Image[data.MaxLevel];
            float pipW = Mathf.Min(34f, (upgradeCardSize.x - 128f - 6f * (data.MaxLevel - 1)) / Mathf.Max(1, data.MaxLevel));
            for (int p = 0; p < data.MaxLevel; p++)
                card.pips[p] = Pic(card.root, $"Pip{p}", null, new Vector2(108f + p * (pipW + 6f), -64f), new Vector2(pipW, 12f));

            // Açıklama iki satıra kayar; yine sığmazsa küçülür (kartın dışına taşmasın)
            var desc = Label(card.root, "Desc", data.description, 18f, Muted, new Vector2(20f, -86f),
                             new Vector2(upgradeCardSize.x - 40f, 48f));
            desc.textWrappingMode = TextWrappingModes.Normal;
            desc.alignment = TextAlignmentOptions.TopLeft;
            AutoSize(desc, 13f);
            // Etki satırı: sağ alttaki satın alma butonunun ÜSTÜNDE kalır
            card.effect = AutoSize(Label(card.root, "Effect", "", 20f, Gold, new Vector2(20f, -140f),
                                         new Vector2(upgradeCardSize.x - 40f, 26f)), 14f);

            // Satın alma butonu (sağ alt): fiyat + Core ikonu, ya da MAXED
            var btnRt = Box("Buy", card.root, buttonSprite, Vector2.zero, new Vector2(170f, 48f));
            btnRt.anchorMin = btnRt.anchorMax = btnRt.pivot = new Vector2(1f, 0f);
            btnRt.anchoredPosition = new Vector2(-16f, 14f);
            var btnImg = btnRt.GetComponent<Image>();
            btnImg.pixelsPerUnitMultiplier = 0.5f;
            btnImg.raycastTarget = true;
            card.buy = btnRt.gameObject.AddComponent<Button>();
            card.buy.targetGraphic = btnImg;
            // Buton olduğu belli olsun: üstüne gelince belirgin aydınlanır ve büyür, basınca kararır
            var colors = card.buy.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.6f, 1.75f, 1.8f, 1f);
            colors.selectedColor = new Color(1.3f, 1.4f, 1.45f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.75f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.5f, 1f);
            colors.fadeDuration = 0.08f;
            card.buy.colors = colors;
            btnRt.gameObject.AddComponent<UIHoverScale>();
            var captured = card;
            card.buy.onClick.AddListener(() => Buy(captured));
            card.cost = AutoSize(Label(btnRt, "Cost", "", 24f, White, new Vector2(14f, -8f), new Vector2(100f, 32f)), 16f);
            card.cost.alignment = TextAlignmentOptions.MidlineRight;
            card.costIcon = Pic(btnRt, "CoreIcon", coreIcon, new Vector2(122f, -10f), new Vector2(28f, 28f));

            cards.Add(card);
        }
    }

    void Buy(UpgradeCard card)
    {
        if (!MetaProgress.TryBuy(card.data)) return;
        AudioManager.PlaySkill();
        RefreshAll();
        StartCoroutine(Pop(card.root));
    }

    void RefreshAll()
    {
        if (coreText != null) coreText.text = MetaProgress.Core.ToString("N0");

        foreach (var c in cards)
        {
            var d = c.data;
            for (int p = 0; p < c.pips.Length; p++) c.pips[p].color = p < d.Level ? Cyan : PipOff;

            c.effect.text = d.IsMaxed
                ? $"{d.FormatValue(d.TotalValue)}  <color=#94B0C2>(MAX)</color>"
                : d.Level == 0
                    ? $"NEXT {d.FormatValue(d.valuePerLevel)}"
                    : $"NOW {d.FormatValue(d.TotalValue)}  <color=#94B0C2>-></color>  {d.FormatValue(d.TotalValue + d.valuePerLevel)}";

            if (d.IsMaxed)
            {
                // İkon yok: yazı butonun tamamını kullanır, ortalanır (dar fiyat kutusundan taşmasın)
                SetCostLayout(c, full: true);
                c.cost.text = "MAXED";
                c.cost.color = Gold;
                c.costIcon.enabled = false;
                c.buy.interactable = false;
            }
            else
            {
                SetCostLayout(c, full: false);
                bool affordable = MetaProgress.Core >= d.NextCost;
                c.cost.text = d.NextCost.ToString();
                c.cost.color = affordable ? White : Red;
                c.costIcon.enabled = true;
                c.buy.interactable = affordable;
            }
        }
    }

    // Fiyat yazısı: normalde ikonun solunda sağa hizalı, MAXED'de butonun tamamında ortalı.
    static void SetCostLayout(UpgradeCard c, bool full)
    {
        var rt = c.cost.rectTransform;
        var btn = (RectTransform)c.buy.transform;
        if (full)
        {
            rt.anchoredPosition = new Vector2(8f, -8f);
            rt.sizeDelta = new Vector2(btn.sizeDelta.x - 16f, 32f);
            c.cost.alignment = TextAlignmentOptions.Center;
        }
        else
        {
            rt.anchoredPosition = new Vector2(14f, -8f);
            rt.sizeDelta = new Vector2(100f, 32f);
            c.cost.alignment = TextAlignmentOptions.MidlineRight;
        }
    }

    // ================= UNLOCKS =================
    void BuildUnlocks()
    {
        if (unlocksContent == null) return;
        Clear(unlocksContent);
        var catalog = MetaProgress.Catalog;
        if (catalog == null) return;

        // Açılanlar en sonda: önce yapılacaklar görünsün
        var list = new List<UpgradeData>();
        foreach (var u in catalog.lockables) if (u != null && !u.IsMetaUnlocked) list.Add(u);
        foreach (var u in catalog.lockables) if (u != null && u.IsMetaUnlocked) list.Add(u);
        if (list.Count == 0) { EmptyNote(unlocksContent, "Nothing to unlock."); return; }

        for (int i = 0; i < list.Count; i++)
        {
            var u = list[i];
            bool open = u.IsMetaUnlocked;
            var row = Box($"Unlock_{u.name}", unlocksContent, cardSprite,
                          GridPos(i, list.Count, unlockColumns, unlockRowSize), unlockRowSize);

            var icon = Pic(row, "Icon", u.icon, new Vector2(14f, -12f), new Vector2(52f, 52f));
            icon.color = open ? Color.white : new Color(0.22f, 0.25f, 0.34f, 1f);   // kilitliyken silüet

            string kind = u is WeaponUpgradeData ? "WEAPON" : u is SkillUpgradeData ? "SKILL" : "UPGRADE";
            // Sağdaki ilerleme çubuğuna/UNLOCKED yazısına binmesin: sığmazsa küçülür
            float textW = unlockRowSize.x - 82f - 290f;
            AutoSize(Label(row, "Name", $"{DisplayName(u)}  <size=65%><color=#94B0C2>{kind}</color></size>", 24f,
                           open ? White : Muted, new Vector2(82f, -10f), new Vector2(textW, 30f)), 16f);
            AutoSize(Label(row, "Condition", MetaProgress.Describe(u.unlockCondition, u.unlockThreshold), 18f,
                           open ? Muted : Gold, new Vector2(82f, -42f), new Vector2(textW, 24f)), 13f);

            if (open)
            {
                // Tik + yazı sağ kenardan içeride biter (yazı ~150 birim, sığmazsa küçülür)
                Pic(row, "Check", checkIcon, new Vector2(unlockRowSize.x - 222f, -20f), new Vector2(36f, 36f));
                var status = AutoSize(Label(row, "Status", "UNLOCKED", 22f, Green,
                                            new Vector2(unlockRowSize.x - 180f, -23f), new Vector2(160f, 30f)), 14f);
                status.alignment = TextAlignmentOptions.MidlineLeft;
            }
            else
            {
                float p = Mathf.Clamp01(MetaProgress.GetProgress(u.unlockCondition) / Mathf.Max(0.0001f, u.unlockThreshold));
                Pic(row, "BarBack", null, new Vector2(unlockRowSize.x - 270f, -22f), new Vector2(250f, 12f)).color = BarBack;
                if (p > 0f)
                    Pic(row, "BarFill", null, new Vector2(unlockRowSize.x - 270f, -22f), new Vector2(250f * p, 12f)).color = Cyan;
                var prog = Label(row, "Progress", MetaProgress.ProgressText(u.unlockCondition, u.unlockThreshold), 18f,
                                 White, new Vector2(unlockRowSize.x - 270f, -40f), new Vector2(250f, 24f));
                prog.alignment = TextAlignmentOptions.MidlineRight;
            }
        }
    }

    static string DisplayName(UpgradeData u)
        => u is WeaponUpgradeData w && w.weapon != null ? w.weapon.weaponName : u.title;

    // ================= Yapı taşları =================
    // Izgarada i. öğenin merkezi (içerik alanının ortasına göre, satırlar ortalanır).
    Vector2 GridPos(int i, int count, int columns, Vector2 size)
    {
        columns = Mathf.Max(1, columns);
        int rows = Mathf.CeilToInt(count / (float)columns);
        int r = i / columns, c = i % columns;
        int inRow = Mathf.Min(columns, count - r * columns);   // son satır eksikse ortala
        float x = (c - (inRow - 1) * 0.5f) * (size.x + spacing.x);
        float y = ((rows - 1) * 0.5f - r) * (size.y + spacing.y);
        return new Vector2(x, y);
    }

    RectTransform Box(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = false;
        if (sprite == null) img.color = new Color(0.1f, 0.12f, 0.2f, 0.9f);
        return rt;
    }

    // Sol üst köşeye göre yerleşen görsel (pos: sol üstten, y aşağı eksi)
    Image Pic(RectTransform parent, string name, Sprite sprite, Vector2 topLeft, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = topLeft;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = sprite != null;
        img.raycastTarget = false;
        return img;
    }

    TMP_Text Label(RectTransform parent, string name, string text, float size, Color color, Vector2 topLeft, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = topLeft;
        rt.sizeDelta = box;
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    // Kutuya sığmazsa yazı min boyuta kadar küçülür (mevcut boyut en büyük değer olur)
    static TMP_Text AutoSize(TMP_Text t, float min)
    {
        t.fontSizeMax = t.fontSize;
        t.fontSizeMin = min;
        t.enableAutoSizing = true;
        t.overflowMode = TextOverflowModes.Ellipsis;   // en küçükte bile sığmazsa "..." ile kesilir
        return t;
    }

    void EmptyNote(RectTransform parent, string text)
    {
        var t = Label(parent, "Empty", text, 26f, Muted, Vector2.zero, new Vector2(900f, 40f));
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        t.alignment = TextAlignmentOptions.Center;
    }

    static void Clear(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
    }

    static IEnumerator Pop(RectTransform rt)
    {
        for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.22f;
            rt.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        rt.localScale = Vector3.one;
    }
}
