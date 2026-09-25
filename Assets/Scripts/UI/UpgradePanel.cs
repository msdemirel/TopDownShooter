using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Level atlayınca açılan seçim paneli. UpgradeManager açar/kapatır.
public class UpgradePanel : MonoBehaviour
{
    [Tooltip("Açılıp kapanacak panel kökü. Boşsa bu objenin kendisi kullanılır.")]
    [SerializeField] GameObject root;

    [Tooltip("Paneldeki seçenek kartları. Kaç tane varsa o kadar seçenek gösterilebilir.")]
    [SerializeField] UpgradeButton[] buttons;

    [Tooltip("Paralı kartlarda fiyatın yanında gösterilecek coin ikonu (tüm kartlar bunu kullanır).")]
    [SerializeField] Sprite coinSprite;

    [Tooltip("Slotlar doluyken yeni skill kartının ikonunun köşesinde gösterilen swap rozeti.")]
    [SerializeField] Sprite swapSprite;

    [Header("Reroll (opsiyonel)")]
    [Tooltip("Kartları parayla yenileme butonu. Boşsa reroll yok. Kurulum: TopDownShooter > UI > Reroll Butonunu Kur")]
    [SerializeField] Button rerollButton;
    [Tooltip("Butondaki fiyat yazısı.")]
    [SerializeField] TMP_Text rerollCostText;
    [SerializeField] Color rerollAffordColor = Color.white;
    [SerializeField] Color rerollCantAffordColor = new Color(1f, 0.4f, 0.4f);
    [Tooltip("Fiyat 0 iken (bedava reroll hakkı) yazılacak metin.")]
    [SerializeField] string rerollFreeText = "FREE";

    Action<UpgradeChoice> onChosen;
    Action onReroll;

    // Awake'e güvenmiyoruz: panel sahnede kapalı başlarsa Awake hiç çalışmaz.
    GameObject Root => root != null ? root : gameObject;

    // rerollCost < 0 ya da rerollCallback null ise reroll butonu gizlenir.
    public void Show(List<UpgradeChoice> choices, int money, Action<UpgradeChoice> chosenCallback,
                     int rerollCost = -1, Action rerollCallback = null)
    {
        onChosen = chosenCallback;
        Root.SetActive(true);
        SetupReroll(money, rerollCost, rerollCallback);
        StartCoroutine(SelectFirstCard());

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;

            if (i < choices.Count)
            {
                buttons[i].gameObject.SetActive(true);
                buttons[i].Setup(choices[i], money, coinSprite, swapSprite, Choose);
            }
            else
            {
                // Yeterli seçenek yoksa fazla kartı gizle
                buttons[i].gameObject.SetActive(false);
            }
        }
    }

    void SetupReroll(int money, int cost, Action callback)
    {
        onReroll = null;
        if (rerollButton == null) return;

        bool show = callback != null && cost >= 0;
        rerollButton.gameObject.SetActive(show);
        if (!show) return;

        bool affordable = money >= cost;
        onReroll = affordable ? callback : null;
        rerollButton.interactable = affordable;
        rerollButton.onClick.RemoveAllListeners();
        rerollButton.onClick.AddListener(Reroll);

        // Butondaki kısayol: klavyede (R), gamepad'de (Y)
        var label = rerollButton.transform.Find("Label")?.GetComponent<TMP_Text>();
        if (label != null)
            label.text = $"{Loc.T("REROLL")} <size=70%><color=#94B0C2>({InputMode.Key("R", "Y")})</color></size>";

        if (rerollCostText != null)
        {
            rerollCostText.text = cost <= 0 ? Loc.T(rerollFreeText) : cost.ToString();
            rerollCostText.color = affordable ? rerollAffordColor : rerollCantAffordColor;
        }
    }

    void Reroll()
    {
        // Callback'i sıfırla: tek tıklamada iki reroll olmasın (Show yeniden atar)
        Action callback = onReroll;
        onReroll = null;
        callback?.Invoke();
    }

    // Klavye kısayolu: R (oyun donuk ama UI Update'i çalışır)
    void Update()
    {
        if (onReroll == null) return;
        var kb = Keyboard.current;
        if ((kb != null && kb.rKey.wasPressedThisFrame) || InputMode.RerollPressed) Reroll();
    }

    // Gamepad ile gezilebilsin: ilk kart seçili gelsin (bir kare sonra: kartlar yeni açıldı)
    System.Collections.IEnumerator SelectFirstCard()
    {
        yield return null;
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null) yield break;
        foreach (var b in buttons)
            if (b != null && b.gameObject.activeInHierarchy)
            {
                var btn = b.GetComponentInChildren<Button>();
                if (btn != null && btn.IsInteractable()) { es.SetSelectedGameObject(btn.gameObject); yield break; }
            }
    }

    public void Hide()
    {
        onChosen = null;
        onReroll = null;
        Root.SetActive(false);
    }

    void Choose(UpgradeChoice choice)
    {
        // Callback'i sıfırla: aynı panelde ikinci bir tıklama işlenmesin
        Action<UpgradeChoice> callback = onChosen;
        onChosen = null;
        callback?.Invoke(choice);
    }
}
