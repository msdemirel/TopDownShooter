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

    [Header("Level Saati (level yazısının arkasında dolan daire)")]
    [Tooltip("Açıksa level yazısının arkasında, bir sonraki levele kalan exp'i saat gibi " +
             "(tepeden saat yönünde) dolduran yarı saydam bir daire oluşturulur.")]
    [SerializeField] bool levelClock = true;
    [Tooltip("Dairenin çapı = level yazısının genişliği + bu değer.")]
    [SerializeField] float clockPadding = 24f;
    [SerializeField] Color clockBackColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] Color clockFillColor = new Color(0.4f, 0.85f, 1f, 0.45f);
    [Tooltip("Dolumun hedefe yetişme hızı (saniyede). 0 = anında.")]
    [SerializeField] float clockFillSpeed = 2f;
    [Tooltip("Level atlayınca daire bu oranda büyüyüp geri döner (1 = kapalı).")]
    [SerializeField] float levelUpPulse = 1.25f;

    RectTransform clockRoot;
    Image clockFill;
    float clockTarget;     // gerçek exp oranı (0-1)
    float pulseTime;       // >0 iken level atlama nabzı sürüyor
    const float PulseDuration = 0.35f;

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

        if (levelClock && levelText != null) BuildLevelClock();
    }

    // Level yazısının hemen ARKASINA (bir önceki kardeş olarak) iki daire koyar:
    // yarı saydam zemin + saat gibi dolan radial dolgu. Yazı üstte kalır.
    void BuildLevelClock()
    {
        RectTransform textRt = levelText.rectTransform;

        var root = new GameObject("LevelClock", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        root.GetComponent<LayoutElement>().ignoreLayout = true;   // yazı bir layout grubundaysa düzeni bozmasın
        clockRoot = (RectTransform)root.transform;
        clockRoot.SetParent(textRt.parent, false);
        clockRoot.SetSiblingIndex(textRt.GetSiblingIndex());      // yazının hemen arkası
        clockRoot.anchorMin = clockRoot.anchorMax = clockRoot.pivot = new Vector2(0.5f, 0.5f);

        var back = root.GetComponent<Image>();
        back.sprite = RuntimeSprite.Circle;
        back.color = clockBackColor;
        back.raycastTarget = false;

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        var fillRt = (RectTransform)fillGo.transform;
        fillRt.SetParent(clockRoot, false);
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;

        clockFill = fillGo.GetComponent<Image>();
        clockFill.sprite = RuntimeSprite.Circle;
        clockFill.color = clockFillColor;
        clockFill.raycastTarget = false;
        clockFill.type = Image.Type.Filled;
        clockFill.fillMethod = Image.FillMethod.Radial360;
        clockFill.fillOrigin = (int)Image.Origin360.Top;   // saat 12'den başla
        clockFill.fillClockwise = true;
        clockFill.fillAmount = 0f;
    }

    // Daireyi yazının GERÇEKTE çizildiği alanın ortasına oturtur; boyutu yazıya göre ayarlar.
    // Level yazısı değişince çağrılır ("LV 9" -> "LV 10" genişler).
    void FitLevelClock()
    {
        if (clockRoot == null || levelText == null) return;

        levelText.ForceMeshUpdate();
        Bounds b = levelText.textBounds;
        RectTransform textRt = levelText.rectTransform;

        float size = Mathf.Max(b.size.x, b.size.y) + clockPadding;
        clockRoot.sizeDelta = new Vector2(size, size);
        clockRoot.position = textRt.TransformPoint(b.center);
    }

    void Update()
    {
        if (clockFill == null) return;

        // unscaledDeltaTime: upgrade paneli oyunu dondurunca da daire dolmaya devam etsin
        float dt = Time.unscaledDeltaTime;
        clockFill.fillAmount = clockFillSpeed <= 0f
            ? clockTarget
            : Mathf.MoveTowards(clockFill.fillAmount, clockTarget, clockFillSpeed * dt);

        if (pulseTime > 0f)
        {
            pulseTime = Mathf.Max(0f, pulseTime - dt);
            float t = 1f - pulseTime / PulseDuration;                          // 0 -> 1
            float s = Mathf.Lerp(1f, levelUpPulse, Mathf.Sin(t * Mathf.PI));  // 1 -> tepe -> 1
            clockRoot.localScale = new Vector3(s, s, 1f);
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
        clockTarget = needed > 0 ? Mathf.Clamp01((float)exp / needed) : 0f;
    }

    void UpdateLevel(int level)
    {
        if (levelText != null) levelText.text = Loc.F(levelFormat, level);
        FitLevelClock();

        // Level atlandı: saat sıfırdan yeniden dolsun + kısa bir büyüme nabzı.
        // (Start'taki ilk çağrıda level 1'dir; nabız atmasın.)
        if (clockFill != null && level > 1)
        {
            clockFill.fillAmount = 0f;
            pulseTime = PulseDuration;
        }
    }

    void UpdateMoney(int money)
    {
        if (moneyText != null) moneyText.text = string.Format(moneyFormat, money);
    }
}
