using TMPro;
using UnityEngine;

// Dalga bilgisini ekranda gösterir: dalga no, dalganın kalan süresi ve
// dalganın spawn'ı bitmeye yaklaşınca çıkan geri sayım (5 4 3 2 1) —
// oyuncuya "yeni dalga geliyor" uyarısı.
// WaveManager'ın değerlerini her karede okur — sayaç için event'ten daha basit.
public class WaveHUD : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Boşsa sahnede aranır.")]
    [SerializeField] WaveManager waveManager;

    [Header("UI (hepsi opsiyonel)")]
    [SerializeField] TMP_Text waveText;        // "Wave 3"
    [SerializeField] string waveFormat = "Wave {0}";
    [SerializeField] TMP_Text waveTimerText;   // dalganın kalan süresi (sürekli görünür)
    [SerializeField] TMP_Text countdownText;   // 5 4 3 2 1 (sadece dalga sonunda)

    void Awake()
    {
        if (waveManager == null) waveManager = FindFirstObjectByType<WaveManager>();
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (waveManager == null) return;

        // Dalga numarası (0 = henüz ilk dalga başlamadı)
        if (waveText != null)
            waveText.text = waveManager.CurrentWave > 0
                ? string.Format(waveFormat, waveManager.CurrentWave)
                : "";

        float left = waveManager.WaveTimeLeft;

        // Dalganın kalan süresi — dalga sürerken
        if (waveTimerText != null)
        {
            waveTimerText.gameObject.SetActive(waveManager.WaveActive);
            if (waveManager.WaveActive)
                waveTimerText.text = Mathf.CeilToInt(left).ToString();
        }

        // Geri sayım — dalganın spawn'ı bitmeye yaklaşınca: "yeni dalga geliyor"
        if (countdownText != null)
        {
            bool show = waveManager.WaveActive && left > 0f && left <= waveManager.CountdownSeconds;

            countdownText.gameObject.SetActive(show);
            if (show) countdownText.text = Mathf.CeilToInt(left).ToString();
        }
    }
}
