using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Dalga Görevleri: startWave'den itibaren her dalganın başında rastgele küçük bir görev gelir.
// Dalga bitmeden (bir sonraki dalga başlamadan) tamamlanırsa anında ödül verilir; bitmezse kaybolur.
//   Görevler: N düşman öldür / aşırı yüklenmiş ızgara alanında N öldür / N blok aşırı yükle /
//             X sn hasar almadan dayan / N altın topla
//   Ödüller : altın / EXP (seviyenin yarısı) / can (%35)
// Hedefler dalga numarasıyla büyür. HUD: sağ üstte görev kutusu (ReactorDefense kutusu bunun altına iner).
// Kendini kurar: WaveManager olan sahnede otomatik oluşur (sahneye eklemek gerekmez).
public class WaveQuests : MonoBehaviour
{
    [Tooltip("İlk görev bu dalgada gelir (1. dalga oyunu öğrenmek için boş).")]
    [SerializeField] int startWave = 2;
    [Tooltip("Hasar almadan dayanma görevinin süresi (sn).")]
    [SerializeField] float noHitSeconds = 15f;
    [Tooltip("Can ödülü: max canın bu oranı.")]
    [SerializeField] float healFraction = 0.35f;

    enum Kind { Kill, GridKill, Overload, NoHit, Coins }
    enum Reward { Coins, Exp, Heal }

    // ReactorDefense kutusunu buna göre aşağı kaydırır
    public static bool PanelVisible { get; private set; }
    public const float PanelHeight = 108f;

    Kind kind;
    Reward reward;
    int target, progress, rewardAmount;
    float noHitTimer, lastHitTime = -10f;
    bool active, done;
    int questWave;
    float resultTimer;   // "BAŞARISIZ" yazısının ekranda kalma süresi

    WaveManager waves;
    PlayerStats stats;
    Health playerHealth;
    Transform player;
    int lastMoney;
    TMP_FontAsset font;

    RectTransform hud, barFill;
    Image barImg, icon;
    TMP_Text titleText, bodyText, rewardText;
    float pop;

    static readonly Color Gold = new Color32(0xFF, 0xCD, 0x75, 0xFF);
    static readonly Color Green = new Color32(0xA7, 0xF0, 0x70, 0xFF);
    static readonly Color Red = new Color32(0xE5, 0x46, 0x58, 0xFF);
    static readonly Color Cyan = new Color32(0x73, 0xEF, 0xF7, 0xFF);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => PanelVisible = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded += (s, m) => TryCreate();
        TryCreate();
    }

    static void TryCreate()
    {
        if (FindAnyObjectByType<WaveQuests>() != null) return;
        if (FindAnyObjectByType<WaveManager>() == null) return;   // sadece oyun sahnesi
        new GameObject("WaveQuests").AddComponent<WaveQuests>();
    }

    void Start()
    {
        waves = FindAnyObjectByType<WaveManager>();
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            stats = p.GetComponent<PlayerStats>();
            playerHealth = p.GetComponent<Health>();
        }
        if (stats != null) { lastMoney = stats.Money; stats.OnMoneyChanged += OnMoney; }
        var ui = FindAnyObjectByType<TextMeshProUGUI>();
        if (ui != null) font = ui.font;

        EnemyBase.AnyKilled += OnKill;
        Health.AnyDamaged += OnDamaged;
        FloorGrid.ZoneOverloaded += OnOverload;
        BuildHud();
    }

    void OnDestroy()
    {
        PanelVisible = false;
        EnemyBase.AnyKilled -= OnKill;
        Health.AnyDamaged -= OnDamaged;
        FloorGrid.ZoneOverloaded -= OnOverload;
        if (stats != null) stats.OnMoneyChanged -= OnMoney;
    }

    // ================================================================ akış
    void Update()
    {
        if (waves == null) return;

        // Yeni dalga: eski görev bitmediyse önce kısa bir "BAŞARISIZ" gösterilir, sonra yenisi gelir
        if (waves.CurrentWave != questWave && waves.CurrentWave >= startWave && resultTimer <= 0f)
        {
            if (active && !done) Fail();
            else NewQuest(waves.CurrentWave);
        }

        if (active && !done && kind == Kind.NoHit && playerHealth != null && !playerHealth.IsDead)
        {
            noHitTimer += Time.deltaTime;
            SetProgress(Mathf.FloorToInt(noHitTimer));
        }

        if (resultTimer > 0f) resultTimer -= Time.deltaTime;
        UpdateHud();
    }

    void NewQuest(int wave)
    {
        questWave = wave;
        active = true;
        done = false;
        progress = 0;
        noHitTimer = 0f;
        resultTimer = 0f;

        kind = (Kind)Random.Range(0, 5);
        target = kind switch
        {
            Kind.Kill => Mathf.Min(50, 6 + 2 * wave),
            Kind.GridKill => Mathf.Min(15, 3 + wave / 2),
            Kind.Overload => Mathf.Min(3, 1 + wave / 6),
            Kind.NoHit => Mathf.RoundToInt(noHitSeconds),
            _ => Mathf.Min(40, 5 + wave),
        };

        reward = (Reward)Random.Range(0, 3);
        // Can dolu iken can ödülü anlamsız: altına çevir
        if (reward == Reward.Heal && playerHealth != null && playerHealth.Normalized > 0.9f) reward = Reward.Coins;
        rewardAmount = reward switch
        {
            Reward.Coins => 10 + 3 * wave,
            Reward.Exp => stats != null ? Mathf.Max(1, stats.ExpToNextLevel / 2) : 10,
            _ => playerHealth != null ? Mathf.RoundToInt(playerHealth.Max * healFraction) : 20,
        };

        icon.sprite = LoadIcon(kind);
        pop = 1f;
        AudioManager.Play(SfxId.UiHover);
        TutorialHints.Request("quest",
            Loc.T("Complete the <color=#FFCD75>WAVE QUEST</color> before the wave ends to earn a bonus!"));
    }

    void SetProgress(int value)
    {
        if (!active || done) return;
        progress = Mathf.Min(target, value);
        if (progress >= target) Complete();
    }

    void Complete()
    {
        done = true;
        switch (reward)
        {
            case Reward.Coins: if (stats != null) stats.AddMoney(rewardAmount); break;
            case Reward.Exp: if (stats != null) stats.AddExp(rewardAmount); break;
            case Reward.Heal: if (playerHealth != null) playerHealth.Heal(rewardAmount); break;
        }
        pop = 1f;
        AudioManager.Play(SfxId.Unlock);
        if (player != null)
        {
            Announce($"{Loc.T("QUEST COMPLETE!")}  {RewardText()}", Green);
            VfxSprite.Spawn(VfxSprites.Ring, player.position, WithAlpha(Gold, 0.8f), 0.5f, 60).Scale(0.4f, 4f, true).Fade(0f, 0.3f);
        }
    }

    void Fail()
    {
        active = false;
        resultTimer = 1.5f;
        AudioManager.Play(SfxId.UiError, 0.6f);
    }

    // ================================================================ olaylar
    void OnKill(EnemyBase e)
    {
        if (!active || done || e == null) return;
        if (kind == Kind.Kill) SetProgress(progress + 1);
        else if (kind == Kind.GridKill && FloorGrid.IsInActiveZone(e.transform.position)) SetProgress(progress + 1);
    }

    void OnOverload(Rect area)
    {
        if (active && !done && kind == Kind.Overload) SetProgress(progress + 1);
    }

    void OnDamaged(Health h, float amount, bool crit)
    {
        if (h != playerHealth || !active || done || kind != Kind.NoHit) return;
        noHitTimer = 0f;   // vuruldu: sayaç baştan
        progress = 0;
        lastHitTime = Time.time;
    }

    void OnMoney(int money)
    {
        int gained = money - lastMoney;
        lastMoney = money;
        if (gained > 0 && active && !done && kind == Kind.Coins) SetProgress(progress + gained);
    }

    // ================================================================ metinler
    string QuestText() => kind switch
    {
        Kind.Kill => Loc.F("Kill {0} enemies", target),
        Kind.GridKill => Loc.F("Kill {0} enemies in an overloaded zone", target),
        Kind.Overload => target == 1 ? Loc.T("Overload a floor block") : Loc.F("Overload {0} floor blocks", target),
        Kind.NoHit => Loc.F("Take no damage for {0}s", target),
        _ => Loc.F("Collect {0} coins", target),
    };

    string RewardText() => reward switch
    {
        Reward.Coins => $"<color=#FFCD75>+{rewardAmount} {Loc.T("COINS")}</color>",
        Reward.Exp => $"<color=#E54658>+{rewardAmount} EXP</color>",
        _ => $"<color=#A7F070>+{rewardAmount} {Loc.T("HP")}</color>",
    };

    static Sprite LoadIcon(Kind k)
    {
        string id = k switch { Kind.Kill => "kill", Kind.GridKill => "gridkill", Kind.Overload => "overload", Kind.NoHit => "nohit", _ => "coins" };
        var all = Resources.LoadAll<Sprite>("Quests/" + id);   // ikonlar çoklu sprite modunda olabilir
        return all != null && all.Length > 0 ? all[0] : null;
    }

    // ================================================================ HUD
    void BuildHud()
    {
        // HUD canvas'ının ölçekleme ayarlarını kopyalayan ayrı bir overlay canvas (ReactorDefense ile aynı).
        var go = new GameObject("QuestHUD", typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1;   // Game Over / upgrade panelleri üstte kalsın
        var scaler = go.GetComponent<CanvasScaler>();
        var waveHud = FindAnyObjectByType<WaveHUD>();
        var hudScaler = waveHud != null ? waveHud.GetComponentInParent<CanvasScaler>() : null;
        if (hudScaler != null)
        {
            scaler.uiScaleMode = hudScaler.uiScaleMode;
            scaler.referenceResolution = hudScaler.referenceResolution;
            scaler.matchWidthOrHeight = hudScaler.matchWidthOrHeight;
            scaler.scaleFactor = hudScaler.scaleFactor;
        }
        else
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        var box = new GameObject("Quest", typeof(RectTransform), typeof(Image));
        hud = (RectTransform)box.transform;
        hud.SetParent(go.transform, false);
        hud.anchorMin = hud.anchorMax = hud.pivot = new Vector2(1f, 1f);
        hud.anchoredPosition = new Vector2(-20f, -20f);
        hud.sizeDelta = new Vector2(400f, PanelHeight);
        var bg = box.GetComponent<Image>();
        bg.raycastTarget = false;
        var frame = Resources.Load<Sprite>("Reactor/hud_frame");
        if (frame != null) { bg.sprite = frame; bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 0.5f; }
        else bg.color = new Color(0.05f, 0.07f, 0.1f, 0.85f);

        icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var irt = icon.rectTransform;
        irt.SetParent(hud, false);
        irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(18f, 4f);
        irt.sizeDelta = new Vector2(52f, 52f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        titleText = MakeText(18f, TextAlignmentOptions.Left, new Vector2(84f, -14f), new Vector2(-18f, 24f));
        rewardText = MakeText(18f, TextAlignmentOptions.Right, new Vector2(84f, -14f), new Vector2(-18f, 24f));
        bodyText = MakeText(24f, TextAlignmentOptions.Left, new Vector2(84f, -40f), new Vector2(-18f, 32f));

        var barBg = new GameObject("Bar", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        barBg.SetParent(hud, false);
        barBg.anchorMin = new Vector2(0f, 0f); barBg.anchorMax = new Vector2(1f, 0f);
        barBg.offsetMin = new Vector2(84f, 16f); barBg.offsetMax = new Vector2(-18f, 28f);
        var bbi = barBg.GetComponent<Image>();
        bbi.color = new Color(0f, 0f, 0f, 0.5f);
        bbi.raycastTarget = false;
        barFill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        barFill.SetParent(barBg, false);
        barFill.anchorMin = Vector2.zero; barFill.anchorMax = new Vector2(0f, 1f);
        barFill.offsetMin = new Vector2(2f, 2f); barFill.offsetMax = new Vector2(-2f, -2f);
        barImg = barFill.GetComponent<Image>();
        barImg.raycastTarget = false;

        hud.gameObject.SetActive(false);
    }

    // offsetMin.x: soldan (ikon payı), top.y: üstten; right.x: sağdan boşluk (negatif), right.y: yükseklik
    TMP_Text MakeText(float size, TextAlignmentOptions align, Vector2 top, Vector2 right)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        var t = go.GetComponent<TextMeshProUGUI>();
        var rt = t.rectTransform;
        rt.SetParent(hud, false);
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(top.x, top.y - right.y);
        rt.offsetMax = new Vector2(right.x, top.y);
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = size * 0.55f;   // uzun çeviriler sığsın
        t.richText = true;
        t.raycastTarget = false;
        go.AddComponent<KeepFontSize>();
        return t;
    }

    void UpdateHud()
    {
        bool on = active || resultTimer > 0f;
        PanelVisible = on;
        if (hud.gameObject.activeSelf != on) hud.gameObject.SetActive(on);
        if (!on) return;

        pop = Mathf.MoveTowards(pop, 0f, Time.unscaledDeltaTime * 4f);
        hud.localScale = Vector3.one * (1f + 0.12f * pop * pop);

        float k = target > 0 ? (float)progress / target : 0f;
        titleText.text = Loc.F("WAVE {0} QUEST", questWave);
        rewardText.text = RewardText();
        barFill.anchorMax = new Vector2(Mathf.Clamp01(k), 1f);

        if (done)
        {
            titleText.color = Green;
            bodyText.text = $"<color=#A7F070>{Loc.T("QUEST COMPLETE!")}</color>";
            barImg.color = Green;
            icon.color = Green;
        }
        else if (resultTimer > 0f && !active)
        {
            titleText.color = Red;
            bodyText.text = $"<color=#E54658>{Loc.T("QUEST FAILED")}</color>";
            barImg.color = Red;
        }
        else
        {
            titleText.color = Gold;
            string unit = kind == Kind.NoHit ? "s" : "";
            bodyText.text = $"{QuestText()}  <color=#94B0C2>{progress}{unit}/{target}{unit}</color>";
            // Hasar almadan dayan: vurulunca bar sıfırlanır, kırmızı yanıp söner
            barImg.color = kind == Kind.NoHit && Time.time - lastHitTime < 0.4f ? Red : Cyan;
            icon.color = Color.white;
        }
    }

    // Oyuncunun üstünde kısa duyuru (dünya yazısı)
    void Announce(string text, Color color)
    {
        var go = new GameObject("QuestAnnounce");
        go.transform.position = player.position + Vector3.up * 1.4f;
        var t = go.AddComponent<TextMeshPro>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = 4.5f;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.outlineWidth = 0.2f;
        t.outlineColor = new Color32(10, 10, 20, 255);
        t.sortingOrder = 110;
        go.AddComponent<Floater>();
    }

    class Floater : MonoBehaviour
    {
        const float Life = 2f;
        TMP_Text t;
        Color baseColor;
        float age;

        void Start() { t = GetComponent<TMP_Text>(); baseColor = t.color; }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= Life) { Destroy(gameObject); return; }
            transform.position += Vector3.up * (0.5f * Time.deltaTime);
            transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(age / 0.15f));
            float k = age / Life;
            t.color = WithAlpha(baseColor, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
        }
    }

    static Color WithAlpha(Color c, float a) { c.a = a; return c; }
}
