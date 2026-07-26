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
    [Tooltip("Bedava upgrade'de \"GET\", parayla alınan weapon/skill'de fiyat yazar.")]
    [SerializeField] TMP_Text costText;
    [Tooltip("Bedava upgrade'de gösterilecek yazı.")]
    [SerializeField] string freeLabel = "GET";
    [SerializeField] Color affordColor = Color.white;
    [Tooltip("Para yetmediğinde fiyat yazısının rengi.")]
    [SerializeField] Color cantAffordColor = new Color(1f, 0.4f, 0.4f);

    UpgradeChoice choice;
    Action<UpgradeChoice> onClick;

    // Panel her açıldığında UpgradeManager -> UpgradePanel bunu çağırır.
    // money: oyuncunun mevcut parası (fiyat gösterimi ve alınabilirlik için).
    public void Setup(UpgradeChoice newChoice, int money, Action<UpgradeChoice> callback)
    {
        choice = newChoice;
        onClick = callback;

        if (titleText != null) titleText.text = choice.data.GetTitle(choice.tier);
        if (descriptionText != null) descriptionText.text = choice.data.GetDescription(choice.tier);

        if (icon != null)
        {
            icon.sprite = choice.data.icon;
            icon.enabled = choice.data.icon != null;   // ikon yoksa boş kare görünmesin
        }

        // Yazılar/ikon tıklamayı ENGELLEMESIN: sadece kartın arka planı (Button) tıklanır,
        // böylece kartın her yerine (metnin üstü dahil) basılabilir.
        DisableRaycast(titleText); DisableRaycast(descriptionText);
        DisableRaycast(costText);  DisableRaycast(icon);

        // Fiyat / "GET" ve alınabilirlik
        int cost = choice.data.GetCost(choice.tier);
        bool affordable = cost <= 0 || money >= cost;

        if (costText != null)
        {
            costText.text = cost <= 0 ? freeLabel : cost.ToString();
            costText.color = affordable ? affordColor : cantAffordColor;
        }

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

    // Bir grafiğin raycast hedefini kapatır (tıklamayı Button'a geçirsin, engellemesin).
    static void DisableRaycast(Graphic g)
    {
        if (g != null) g.raycastTarget = false;
    }
}
