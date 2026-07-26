using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Player'ın can / exp / level / para değerlerini ekranda gösterir.
// Değerler event'lere abone olarak canlı güncellenir (yerden toplama dahil).
public class PlayerHUD : MonoBehaviour
{
    [Header("Player References (boş bırakılırsa 'Player' tag'inden bulunur)")]
    [SerializeField] Health playerHealth;
    [SerializeField] PlayerStats playerStats;

    [Header("Health UI")]
    [SerializeField] TMP_Text healthText;
    [SerializeField] Slider healthBar;    // opsiyonel (0-1 arası)
    [SerializeField] Image healthFill;    // opsiyonel (Image fillAmount, 0-1)
    [SerializeField] string healthFormat = "{0}/{1}";

    [Header("Exp / Level UI")]
    [SerializeField] TMP_Text expText;
    [SerializeField] string expFormat = "{0}/{1}";
    [SerializeField] Image expFill;       // opsiyonel exp barı (Image fillAmount)
    [SerializeField] TMP_Text levelText;
    [SerializeField] string levelFormat = "LV {0}";

    [Header("Money UI")]
    [SerializeField] TMP_Text moneyText;
    [SerializeField] string moneyFormat = "{0}";

    void Awake()
    {
        // Referanslar boşsa Player'dan otomatik bul
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            if (playerHealth == null) playerHealth = p.GetComponent<Health>();
            if (playerStats == null) playerStats = p.GetComponent<PlayerStats>();
        }
    }

    void OnEnable()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged += UpdateHealth;
        if (playerStats != null)
        {
            playerStats.OnExpChanged += UpdateExp;
            playerStats.OnLevelUp += UpdateLevel;
            playerStats.OnMoneyChanged += UpdateMoney;
        }
    }

    void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged -= UpdateHealth;
        if (playerStats != null)
        {
            playerStats.OnExpChanged -= UpdateExp;
            playerStats.OnLevelUp -= UpdateLevel;
            playerStats.OnMoneyChanged -= UpdateMoney;
        }
    }

    void Start()
    {
        // Başlangıç değerlerini ekrana bas
        if (playerHealth != null) UpdateHealth(playerHealth);
        if (playerStats != null)
        {
            UpdateExp(playerStats.Exp, playerStats.ExpToNextLevel);
            UpdateLevel(playerStats.Level);
            UpdateMoney(playerStats.Money);
        }
    }

    void UpdateHealth(Health h)
    {
        if (healthText != null)
            healthText.text = string.Format(healthFormat, Mathf.CeilToInt(h.Current), Mathf.CeilToInt(h.Max));
        if (healthBar != null) healthBar.value = h.Normalized;
        if (healthFill != null) healthFill.fillAmount = h.Normalized;
    }

    void UpdateExp(int exp, int needed)
    {
        if (expText != null) expText.text = string.Format(expFormat, exp, needed);
        if (expFill != null) expFill.fillAmount = needed > 0 ? (float)exp / needed : 0f;
    }

    void UpdateLevel(int level)
    {
        if (levelText != null) levelText.text = string.Format(levelFormat, level);
    }

    void UpdateMoney(int money)
    {
        if (moneyText != null) moneyText.text = string.Format(moneyFormat, money);
    }
}
