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
    // Dalga yazısı ve süre kutularını doldurur; uzun çevirilerde (ONDATA 12, 第 12 波) küçülerek sığar
    [SerializeField] float waveFontMax = 36f;
    [SerializeField] float waveFontMin = 16f;
    [SerializeField] TMP_Text waveTimerText;   // dalganın kalan süresi (sürekli görünür)
    [SerializeField] TMP_Text countdownText;   // 5 4 3 2 1 (sadece dalga sonunda)
    int lastCountdown = -1;

    void Awake()
    {
        if (waveManager == null) waveManager = FindFirstObjectByType<WaveManager>();
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        FitToBox(waveText);
        FitToBox(waveTimerText);
    }

    void FitToBox(TMP_Text t)
    {
        if (t == null) return;
        t.textWrappingMode = TextWrappingModes.NoWrap;   // alt satıra kaymasın, küçülsün
        t.enableAutoSizing = true;
        t.fontSizeMax = waveFontMax;
        t.fontSizeMin = waveFontMin;
    }

    void Update()
    {
        if (waveManager == null) return;

        // Dalga numarası (0 = henüz ilk dalga başlamadı)
        if (waveText != null)
            waveText.text = waveManager.CurrentWave > 0
                ? Loc.F(waveFormat, waveManager.CurrentWave)
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
            if (show)
            {
                int n = Mathf.CeilToInt(left);
                countdownText.text = n.ToString();
                // Her saniye bir tik; son saniyelerde perde yükselir (gerilim)
                if (n != lastCountdown) AudioManager.Play(SfxId.CountdownTick, 1f, 1f + 0.08f * (waveManager.CountdownSeconds - n));
                lastCountdown = n;
            }
            else lastCountdown = -1;
        }
    }
}
