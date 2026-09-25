using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// İlk kez oynayan için bağlama göre ipuçları: oyuncu bir şeyle İLK karşılaştığında kısa bir ipucu.
// Her ipucu bir kez gösterilir (PlayerPrefs'te hatırlanır); deneyimli oyuncuyu rahatsız etmez.
// Ekranın üst ortasında, HUD stilinde kutu; aynı anda tek ipucu, gerisi sıraya girer.
// Oyun donukken (upgrade paneli) de çalışır. Kendini kurar: sahneye eklemek gerekmez.
//
// Test: Menü > TopDownShooter > Tutorial > İpuçlarını Sıfırla
public class TutorialHints : MonoBehaviour
{
    const string KeyPrefix = "tutorial_seen_";
    public static readonly string[] AllIds =
        { "move", "exp", "levelup", "coins", "skill", "merge", "pause", "boss", "core" };

    // Bir ipucu: metin + (opsiyonel) ne zaman kapanacağı + en uzun süre
    class Hint
    {
        public string id, text;
        public System.Func<bool> doneWhen;
        public float maxTime = 7f;
    }

    readonly Queue<Hint> queue = new Queue<Hint>();
    readonly HashSet<string> queued = new HashSet<string>();
    Hint current;
    float shownFor;

    RectTransform box;
    TMP_Text label;
    CanvasGroup group;
    float baseX = 0f;
    float baseY = -150f;   // oyunda: üst orta (dalga panelinin altı). Menüde: sağ alt

    // oyun sahnesi
    PlayerStats stats;
    PlayerSkills skills;
    UpgradeManager upgrades;
    WaveManager waves;
    Transform player;
    Vector3 startPos;
    bool gameScene;
    bool bossSeen;
    int skillSlotToWatch = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded += (s, m) => TryCreate();
        TryCreate();
    }

    static void TryCreate()
    {
        if (FindAnyObjectByType<TutorialHints>() != null) return;
        if (AllSeen()) return;   // hepsi görüldüyse hiç kurulma
        new GameObject("TutorialHints").AddComponent<TutorialHints>();
    }

    public static bool Seen(string id) => PlayerPrefs.GetInt(KeyPrefix + id, 0) == 1;
    static void MarkSeen(string id) { PlayerPrefs.SetInt(KeyPrefix + id, 1); PlayerPrefs.Save(); }
    static bool AllSeen() { foreach (var id in AllIds) if (!Seen(id)) return false; return true; }

    public static void ResetAll()
    {
        foreach (var id in AllIds) PlayerPrefs.DeleteKey(KeyPrefix + id);
        PlayerPrefs.Save();
    }

    // ================================================================ kurulum
    IEnumerator Start()
    {
        yield return null;   // HUD / oyuncu / diğer sistemler kurulsun
        BuildBox();
        if (box == null) { Destroy(gameObject); yield break; }

        var p = GameObject.FindGameObjectWithTag("Player");
        gameScene = p != null;
        if (gameScene) SetupGame(p);
        else
        {
            // Menüde solda butonlar, üstte başlık var: kutu sağ altta, rekor kartının altında dursun
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(1f, 0f);
            box.sizeDelta = new Vector2(640f, 78f);
            baseX = -40f;
            baseY = 110f;
            SetupMenu();
        }
    }

    void SetupGame(GameObject p)
    {
        player = p.transform;
        startPos = player.position;
        stats = p.GetComponent<PlayerStats>();
        skills = p.GetComponent<PlayerSkills>();
        upgrades = FindAnyObjectByType<UpgradeManager>();
        waves = FindAnyObjectByType<WaveManager>();

        Enqueue("move", Loc.F("<b>{0}</b> to move. Your weapons attack <color=#73EFF7>automatically</color>.",
                              InputMode.Key("WASD", Loc.T("Left stick"))),
                () => player != null && (player.position - startPos).sqrMagnitude > 4f, 10f);

        EnemyBase.AnyKilled += OnKill;
        PlayerCollector.AnyCollected += OnCollected;
        if (stats != null) stats.OnLevelUp += OnLevelUp;
        if (skills != null) skills.OnSkillAdded += OnSkillAdded;
        if (upgrades != null) upgrades.OnUpgradeApplied += OnUpgradeApplied;
    }

    // Ana menü: ilk oyundan sonra Core'un mağazada harcanabileceğini göster
    void SetupMenu()
    {
        if (BestRecords.TotalRuns < 1 || MetaProgress.Core <= 0) return;
        Enqueue("core", Loc.T("You earned <color=#73EFF7>CORE</color>! Spend it in <color=#73EFF7>UPGRADES</color> for permanent bonuses."),
                () => IsActive("ShopPanel"), 12f);
    }

    static bool IsActive(string name)
    {
        var go = GameObject.Find(name);
        return go != null && go.activeInHierarchy;
    }

    void OnDestroy()
    {
        EnemyBase.AnyKilled -= OnKill;
        PlayerCollector.AnyCollected -= OnCollected;
        if (stats != null) stats.OnLevelUp -= OnLevelUp;
        if (skills != null) skills.OnSkillAdded -= OnSkillAdded;
        if (upgrades != null) upgrades.OnUpgradeApplied -= OnUpgradeApplied;
    }

    // ================================================================ tetikleyiciler
    void OnKill(EnemyBase e)
        => Enqueue("exp", Loc.T("Collect the <color=#E54658>red orbs</color> for EXP. Level up to pick upgrades."));

    void OnCollected(PickupType t)
    {
        if (t == PickupType.Money)
            Enqueue("coins", Loc.T("<color=#FFCD75>Coins</color> buy weapons on level-up cards. Save them up!"));
    }

    // Upgrade paneli açıkken gösterilir (oyun donuk: süre gerçek zamanla işler), seçim yapılınca kapanır
    void OnLevelUp(int level)
    {
        int applied = appliedCount;
        Show("levelup", Loc.F("Pick <b>one</b> upgrade. Weapon cards cost <color=#FFCD75>coins</color> - press <b>{0}</b> to reroll the cards.",
                              InputMode.Key("R", "Y")), () => appliedCount > applied, 30f);
    }

    int appliedCount;
    void OnUpgradeApplied(UpgradeData data, int tier)
    {
        appliedCount++;
        if (data is WeaponUpgradeData)
            Enqueue("merge", Loc.T("Buy the <b>same weapon</b> again to <color=#C78BFF>MERGE</color> it into a stronger tier!"), null, 9f);
    }

    void OnSkillAdded(int slot, SkillUpgradeData skill)
    {
        if (Seen("skill") || skills == null) return;
        skillSlotToWatch = slot;
        string key = skills.GetKeyName(slot);
        Enqueue("skill", Loc.F("Press <b>{0}</b> to use <color=#73EFF7>{1}</color>. Skills recharge over time.", key, skill.title),
                () => skills.GetCooldownRemaining(skillSlotToWatch) > 0f, 15f);
    }

    // Olay olmayan durumlar: dalga ve boss (hafif yoklama)
    void CheckGameState()
    {
        if (waves != null && waves.CurrentWave >= 2)
            Enqueue("pause", Loc.F("<b>{0}</b> pauses. <color=#73EFF7>SAVE & QUIT</color> lets you continue this run later.",
                                   InputMode.Key("ESC", "START")));

        if (!bossSeen && !Seen("boss"))
        {
            var alive = EnemyRegistry.Alive;
            for (int i = 0; i < alive.Count; i++)
                if (alive[i] != null && alive[i].IsBoss)
                {
                    bossSeen = true;
                    Enqueue("boss", Loc.T("<color=#E5533C>Boss!</color> It hits hard - keep moving and use your skills."));
                    break;
                }
        }
    }

    // ================================================================ gösterim
    void Enqueue(string id, string text, System.Func<bool> doneWhen = null, float maxTime = 7f)
    {
        if (Seen(id) || queued.Contains(id)) return;
        queued.Add(id);
        queue.Enqueue(new Hint { id = id, text = text, doneWhen = doneWhen, maxTime = maxTime });
    }

    // Sırayı beklemeden hemen göster (level-up: panel açıkken anlamlı)
    void Show(string id, string text, System.Func<bool> doneWhen, float maxTime)
    {
        if (Seen(id) || queued.Contains(id)) return;
        queued.Add(id);
        if (current != null) queue.Enqueue(current);   // mevcut ipucu sonra devam etsin
        Begin(new Hint { id = id, text = text, doneWhen = doneWhen, maxTime = maxTime });
    }

    void Begin(Hint h)
    {
        current = h;
        shownFor = 0f;
        label.text = h.text;
        MarkSeen(h.id);   // görüldü say: oyuncu oyundan çıksa da tekrar gösterilmesin
    }

    void Update()
    {
        if (box == null) return;
        float dt = Time.unscaledDeltaTime;
        if (gameScene) CheckGameState();

        if (current == null && queue.Count > 0) Begin(queue.Dequeue());

        float targetAlpha = 0f;
        if (current != null)
        {
            shownFor += dt;
            bool done = shownFor > current.maxTime
                        || (current.doneWhen != null && shownFor > 1.2f && current.doneWhen());
            if (done) current = null;
            else targetAlpha = 1f;
        }

        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, dt * 4f);
        // hafif "belirme" hareketi: yukarıdan kayarak gelir
        box.anchoredPosition = new Vector2(baseX, baseY + (1f - group.alpha) * 20f * Mathf.Sign(-baseY));
    }

    // ================================================================ görünüm
    // HUD'daki bir 9-slice sprite'ı (reroll butonu / panel çerçevesi) ödünç alır: stil aynı olsun
    void BuildBox()
    {
        Canvas canvas = null;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace) { canvas = c; break; }
        if (canvas == null) return;

        // Önce reroll butonunun çerçevesi (HUD stili), yoksa sahnedeki ilk 9-slice görsel
        Image source = null;
        foreach (var img in canvas.GetComponentsInChildren<Image>(true))
        {
            if (img.sprite == null || img.sprite.border == Vector4.zero || img.type != Image.Type.Sliced) continue;
            if (img.gameObject.name == "RerollButton") { source = img; break; }
            if (source == null) source = img;
        }
        TMP_FontAsset font = canvas.GetComponentInChildren<TMP_Text>(true)?.font;

        var go = new GameObject("TutorialHint", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        box = (RectTransform)go.transform;
        box.SetParent(canvas.transform, false);
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 1f);
        box.sizeDelta = new Vector2(860f, 78f);
        box.anchoredPosition = new Vector2(0f, -150f);
        box.SetAsLastSibling();
        var bg = go.GetComponent<Image>();
        bg.raycastTarget = false;
        if (source != null)
        {
            bg.sprite = source.sprite;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;   // kaynağın kenar kalınlığı
        }
        else bg.color = new Color(0.09f, 0.11f, 0.2f, 0.92f);
        group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        // Solda küçük "TIP" etiketi
        var tip = MakeText(box, "Tip", "", 22f, new Color32(0xFF, 0xCD, 0x75, 0xFF), font);
        Loc.Bind(tip, "TIP");
        tip.alignment = TextAlignmentOptions.Center;
        var trt = tip.rectTransform;
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0f, 0.5f);
        trt.anchoredPosition = new Vector2(20f, 0f);
        trt.sizeDelta = new Vector2(70f, 40f);

        label = MakeText(box, "Text", "", 24f, Color.white, font);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.enableAutoSizing = true;
        label.fontSizeMax = 24f;
        label.fontSizeMin = 15f;
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(100f, 8f);
        lrt.offsetMax = new Vector2(-24f, -8f);
    }

    static TMP_Text MakeText(RectTransform parent, string name, string text, float size, Color color, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }
}
