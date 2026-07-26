using System;
using System.Collections.Generic;
using UnityEngine;

// Level atlayınca açılan seçim paneli. UpgradeManager açar/kapatır.
public class UpgradePanel : MonoBehaviour
{
    [Tooltip("Açılıp kapanacak panel kökü. Boşsa bu objenin kendisi kullanılır.")]
    [SerializeField] GameObject root;

    [Tooltip("Paneldeki seçenek kartları. Kaç tane varsa o kadar seçenek gösterilebilir.")]
    [SerializeField] UpgradeButton[] buttons;

    Action<UpgradeChoice> onChosen;

    // Awake'e güvenmiyoruz: panel sahnede kapalı başlarsa Awake hiç çalışmaz.
    GameObject Root => root != null ? root : gameObject;

    public void Show(List<UpgradeChoice> choices, int money, Action<UpgradeChoice> chosenCallback)
    {
        onChosen = chosenCallback;
        Root.SetActive(true);

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;

            if (i < choices.Count)
            {
                buttons[i].gameObject.SetActive(true);
                buttons[i].Setup(choices[i], money, Choose);
            }
            else
            {
                // Yeterli seçenek yoksa fazla kartı gizle
                buttons[i].gameObject.SetActive(false);
            }
        }
    }

    public void Hide()
    {
        onChosen = null;
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
