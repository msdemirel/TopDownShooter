using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Upgrade panelindeki tek bir seçenek kartı (ikon + başlık + açıklama).
// Başlık ve açıklama kademeye göre değişir ("Can Takviyesi 2", "+5 can" gibi).
public class UpgradeButton : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] Image icon;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;

    [Header("Aksiyon / Fiyat")]
    [Tooltip("Bedava upgrade'de \"FREE\", parayla alınan weapon'da fiyat + coin ikonu yazar.")]
    [SerializeField] TMP_Text costText;
    // Not: alan adı bilerek eski 'freeLabel' değil — sahnede kayıtlı "GET" değeri geri gelmesin.
    [Tooltip("Bedava upgrade'de gösterilecek yazı.")]
    [SerializeField] string freeText = "FREE";

    [Tooltip("Opsiyonel: fiyatın yanındaki coin ikonu. Boşsa UpgradePanel'deki Coin Sprite ile " +
             "fiyat yazısının sağına otomatik oluşturulur.")]
    [SerializeField] Image coinIcon;
    [Tooltip("Coin ikonu ile fiyat yazısı arasındaki boşluk.")]
    [SerializeField] float coinIconGap = 6f;
    [SerializeField] Color affordColor = Color.white;
    [Tooltip("Para yetmediğinde fiyat yazısının rengi.")]
    [SerializeField] Color cantAffordColor = new Color(1f, 0.4f, 0.4f);

    [Header("Skill Swap")]
    [Tooltip("Slotlar doluyken yeni skill kartında açıklamanın altına eklenen not.")]
    [SerializeField] string replacesNote = "Replaces a skill";
    [SerializeField] Color replacesNoteColor = new Color(1f, 0.8f, 0.46f);   // sarı (#FFCD75)
    [Tooltip("Opsiyonel: ikonun köşesindeki swap rozeti. Boşsa UpgradePanel'deki Swap Sprite ile " +
             "ikonun sağ üst köşesine otomatik oluşturulur.")]
    [SerializeField] Image swapBadge;
    [Tooltip("Rozetin boyutu, kart ikonunun boyutuna oranla.")]
    [Range(0.2f, 0.8f)] [SerializeField] float swapBadgeSize = 0.45f;

    UpgradeChoice choice;
    Action<UpgradeChoice> onClick;

    // Panel her açıldığında UpgradeManager -> UpgradePanel bunu çağırır.
    // money: oyuncunun mevcut parası (fiyat gösterimi ve alınabilirlik için).
    public void Setup(UpgradeChoice newChoice, int money, Sprite coinSprite, Sprite swapSprite,
                      Action<UpgradeChoice> callback)
    {
        choice = newChoice;
        onClick = callback;

        if (titleText != null) titleText.text = choice.data.GetTitle(choice.tier);
        if (descriptionText != null)
        {
            string desc = choice.data.GetDescription(choice.tier);
            if (choice.replacesSkill && !string.IsNullOrEmpty(replacesNote))
            {
                string note = $"<color=#{ColorUtility.ToHtmlStringRGB(replacesNoteColor)}>{replacesNote}</color>";
                desc = string.IsNullOrEmpty(desc) ? note : desc + "\n" + note;
            }
            descriptionText.text = desc;
        }

        if (icon != null)
        {
            icon.sprite = choice.data.icon;
            icon.enabled = choice.data.icon != null;   // ikon yoksa boş kare görünmesin
        }

        // Yazılar/ikon tıklamayı ENGELLEMESIN: sadece kartın arka planı (Button) tıklanır,
        // böylece kartın her yerine (metnin üstü dahil) basılabilir.
        DisableRaycast(titleText); DisableRaycast(descriptionText);
        DisableRaycast(costText);  DisableRaycast(icon);

        // Fiyat / "FREE" ve alınabilirlik
        int cost = choice.data.GetCost(choice.tier);
        bool affordable = cost <= 0 || money >= cost;

        if (costText != null)
        {
            costText.text = cost <= 0 ? freeText : cost.ToString();
            costText.color = affordable ? affordColor : cantAffordColor;
        }

        UpdateCoinIcon(cost > 0, coinSprite);
        UpdateSwapBadge(choice.replacesSkill, swapSprite);

        if (button == null) button = GetComponent<Button>();
        if (button != null)
        {
            button.interactable = affordable;   // param yetmiyorsa kart pasif (gri)

            // Önce temizle: panel her açıldığında listener üst üste binmesin
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClick);
        }
    }

    void HandleClick() => onClick?.Invoke(choice);

    // Paralı kartta fiyatın sağında coin ikonu gösterir, bedavada gizler.
    void UpdateCoinIcon(bool paid, Sprite coinSprite)
    {
        // Elle atanmış ikon yoksa ve sprite verildiyse bir kez oluştur (fiyat yazısının child'ı)
        if (coinIcon == null && coinSprite != null && costText != null)
        {
            var go = new GameObject("CoinIcon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(costText.transform, false);
            coinIcon = go.GetComponent<Image>();
            coinIcon.preserveAspect = true;
            coinIcon.raycastTarget = false;
        }
        if (coinIcon == null) return;

        coinIcon.gameObject.SetActive(paid);
        if (!paid) return;

        if (coinSprite != null) coinIcon.sprite = coinSprite;
        coinIcon.color = costText != null ? costText.color : Color.white;   // yetmiyorsa kırmızımsı olsun

        if (costText == null || coinIcon.transform.parent != costText.transform) return;

        // Yazının GERÇEKTE çizildiği alanın sağ kenarına yerleştir (hizalama ne olursa olsun doğru)
        costText.ForceMeshUpdate();
        Bounds b = costText.textBounds;
        float size = costText.fontSize;

        RectTransform rt = coinIcon.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.localPosition = new Vector3(b.max.x + coinIconGap + size * 0.5f, b.center.y, 0f);
    }

    // Slotlar doluyken yeni skill kartında ikonun sağ üst köşesine swap rozeti koyar.
    void UpdateSwapBadge(bool show, Sprite swapSprite)
    {
        // Elle atanmış rozet yoksa ve sprite verildiyse bir kez oluştur (kart ikonunun child'ı)
        if (swapBadge == null && swapSprite != null && icon != null)
        {
            var go = new GameObject("SwapBadge", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(icon.transform, false);
            swapBadge = go.GetComponent<Image>();
            swapBadge.preserveAspect = true;
            swapBadge.raycastTarget = false;

            // Köşeye oturt, biraz dışarı taşsın (rozet gibi dursun)
            RectTransform rt = swapBadge.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.7f, 0.7f);
            rt.anchoredPosition = Vector2.zero;
            Rect r = icon.rectTransform.rect;
            float size = Mathf.Min(r.width, r.height) * swapBadgeSize;
            rt.sizeDelta = new Vector2(size, size);
        }
        if (swapBadge == null) return;

        if (swapSprite != null) swapBadge.sprite = swapSprite;
        swapBadge.gameObject.SetActive(show && swapBadge.sprite != null);
    }

    // Bir grafiğin raycast hedefini kapatır (tıklamayı Button'a geçirsin, engellemesin).
    static void DisableRaycast(Graphic g)
    {
        if (g != null) g.raycastTarget = false;
    }
}
