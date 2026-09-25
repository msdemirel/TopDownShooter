using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Upgrade seçildikten sonra HUD'da kısa süreliğine kazanımı gösterir:
// sadece DEĞİŞİM yazılır ("+1 Hasar" gibi, UpgradeData.GetBonusSummary'den gelir).
// Arka arkaya birden fazla seçim olursa bildirimler sırayla gösterilir.
// Zamanlama unscaled: upgrade paneli oyunu dondururken de akar.
public class UpgradeToastUI : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Boş bırakılırsa sahnede aranır.")]
    [SerializeField] UpgradeManager upgradeManager;

    [Tooltip("Bildirim metni. Rich Text açık olmalı (renkler için).")]
    [SerializeField] TMP_Text text;

    [Tooltip("Yumuşak kaybolma için. Boş bırakılırsa metin fade'siz açılıp kapanır.")]
    [SerializeField] CanvasGroup group;

    [Header("Ayarlar")]
    [Tooltip("Bildirimin tam görünür kaldığı süre (saniye).")]
    [SerializeField] float showTime = 1.5f;
    [Tooltip("Kaybolma süresi (saniye).")]
    [SerializeField] float fadeTime = 0.5f;

    readonly Queue<string> pending = new Queue<string>();
    bool showing;

    void Start()
    {
        if (upgradeManager == null)
            upgradeManager = FindFirstObjectByType<UpgradeManager>();

        if (upgradeManager == null || text == null)
        {
            Debug.LogWarning("[UpgradeToastUI] UpgradeManager veya text atanmamış.", this);
            enabled = false;
            return;
        }

        upgradeManager.OnUpgradeApplied += HandleUpgradeApplied;
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (upgradeManager != null)
            upgradeManager.OnUpgradeApplied -= HandleUpgradeApplied;
    }

    void HandleUpgradeApplied(UpgradeData data, int tier) => Show(data.GetBonusSummary(tier));

    // Dışarıdan bildirim (ör. "Second Chance!" dirilme mesajı). Sıraya eklenir.
    public void Show(string msg)
    {
        if (string.IsNullOrEmpty(msg) || text == null) return;

        pending.Enqueue(msg);
        if (!showing) StartCoroutine(ShowQueue());
    }

    IEnumerator ShowQueue()
    {
        showing = true;

        while (pending.Count > 0)
        {
            text.text = pending.Dequeue();
            SetVisible(true);

            // Görünür bekle (unscaledDeltaTime: timeScale 0 iken de ilerler)
            for (float t = 0f; t < showTime; t += Time.unscaledDeltaTime)
                yield return null;

            // Yumuşak kaybolma (CanvasGroup yoksa direkt kapanır)
            if (group != null)
            {
                for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
                {
                    group.alpha = 1f - t / fadeTime;
                    yield return null;
                }
            }

            SetVisible(false);
        }

        showing = false;
    }

    void SetVisible(bool visible)
    {
        // GameObject'i KAPATMIYORUZ: script metinle aynı objedeyse SetActive(false)
        // coroutine'i de durdururdu. Component'i kapatmak yeterli ve güvenli.
        text.enabled = visible;
        if (group != null) group.alpha = visible ? 1f : 0f;
    }
}
