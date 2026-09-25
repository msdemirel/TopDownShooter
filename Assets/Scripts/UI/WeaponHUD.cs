using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Sağ altta oyuncunun silah slotları: dolu slotta silah ikonu, boşta sönük yuva,
// başlıkta "WEAPONS 2/6". Yeni silah takılınca o slot kısa bir "pop" yapar.
//
// SWAP: Slotlar doluyken silah kartı seçilince UpgradeManager BeginSwap çağırır (SkillHUD
// kalıbı). Slotlar tıklanabilir olur ve nabız gibi atar; oyuncu slota tıklar ya da 1-9
// tuşuna basar. Esc / İptal butonu seçim paneline geri döner.
//
// Görseller Awake'te kodla kurulur (MinimapUI kalıbı); sprite alanları boşsa düz renkli
// kutular çizilir. Kurulum: Menü > TopDownShooter > UI > Silah Slotlarını Kur.
[RequireComponent(typeof(RectTransform))]
public class WeaponHUD : MonoBehaviour
{
    [Header("Yerleşim")]
    [Tooltip("Açıksa Awake'te kendini sağ alt köşeye yerleştirir. Kapalıysa RectTransform'u elle konumlandırırsın.")]
    [SerializeField] bool anchorBottomRight = true;
    [Tooltip("Sağ kenardan / alt kenardan boşluk.")]
    [SerializeField] Vector2 margin = new Vector2(16f, 0f);
    [SerializeField] int columns = 3;
    [SerializeField] float slotSize = 56f;
    [SerializeField] float spacing = 6f;
    [Tooltip("Panelin iç boşluğu (sol, sağ, üst, alt).")]
    [SerializeField] Vector4 padding = new Vector4(14f, 14f, 16f, 12f);
    [SerializeField] float headerHeight = 22f;

    [Header("Görünüm")]
    [Tooltip("Arka panel (9-slice). skill_bar_frame ile aynı stil.")]
    [SerializeField] Sprite panelSprite;
    [Tooltip("Panel kenarlarının kalınlık çarpanı (0.5 = sprite pikseli 2 UI birimi).")]
    [SerializeField] float panelPixelScale = 0.5f;
    [SerializeField] Sprite slotSprite;
    [Range(0f, 1f)] [SerializeField] float emptySlotAlpha = 0.35f;
    [Tooltip("İkonun slot içindeki boyutu (slot boyutuna oranla).")]
    [Range(0.3f, 1f)] [SerializeField] float iconScale = 0.72f;
    [SerializeField] TMP_FontAsset font;
    [SerializeField] string title = "WEAPONS";
    [SerializeField] Color titleColor = new Color(0.58f, 0.69f, 0.76f);
    [SerializeField] Color countColor = new Color(0.45f, 0.94f, 0.97f);
    [SerializeField] float fontSize = 15f;

    [Header("Yeni silah animasyonu")]
    [SerializeField] float popScale = 1.35f;
    [SerializeField] float popDuration = 0.3f;

    [Header("Swap ekranı")]
    [SerializeField] float swapPulseScale = 1.12f;
    [SerializeField] Color swapPromptBackground = new Color(0.1f, 0.11f, 0.17f, 0.92f);

    PlayerWeapons weapons;
    Image[] frames;
    Image[] icons;
    float[] popTime;
    WeaponData[] shown;
    TMP_Text countText;

    // Swap durumu
    Action<int> onSwapPick;
    Action onSwapCancel;
    GameObject swapPrompt;
    Image swapIcon;
    TMP_Text swapTitle;

    public bool IsSwapping => onSwapPick != null;

    void Awake()
    {
        if (font == null)
        {
            // Oyunun fontu: aynı Canvas'taki ilk TMP yazısından al
            var any = GetComponentInParent<Canvas>(true)?.GetComponentInChildren<TMP_Text>(true);
            if (any != null) font = any.font;
        }
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) weapons = p.GetComponent<PlayerWeapons>();
        if (weapons == null)
        {
            Debug.LogWarning("[WeaponHUD] Player'da PlayerWeapons bulunamadı.", this);
            enabled = false;
            return;
        }

        Build(weapons.SlotCount);
        weapons.OnWeaponAdded += HandleWeaponAdded;
        Refresh(animate: false);   // başlangıç silahı bu Start'tan önce takılmış olabilir
    }

    void OnDestroy()
    {
        if (weapons != null) weapons.OnWeaponAdded -= HandleWeaponAdded;
    }

    void HandleWeaponAdded(Weapon w) => Refresh(animate: true);

    // Slotları PlayerWeapons'tan okuyup ikonları günceller; değişen slot pop yapar.
    void Refresh(bool animate)
    {
        for (int i = 0; i < frames.Length; i++)
        {
            WeaponData d = weapons.GetSlotData(i);
            bool filled = d != null;

            frames[i].color = new Color(1f, 1f, 1f, filled ? 1f : emptySlotAlpha);
            icons[i].enabled = filled && d.Icon != null;
            if (filled) icons[i].sprite = d.Icon;

            if (animate && d != shown[i] && filled) popTime[i] = popDuration;
            shown[i] = d;
        }
        if (countText != null) countText.text = $"{weapons.WeaponCount}/{weapons.SlotCount}";
    }

    // Oyun donukken de (silah upgrade panelinden alınır, timeScale 0) animasyon oynasın
    void Update()
    {
        if (popTime == null) return;
        if (IsSwapping) { UpdateSwap(); return; }
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < popTime.Length; i++)
        {
            if (popTime[i] <= 0f) continue;
            popTime[i] = Mathf.Max(0f, popTime[i] - dt);
            float t = 1f - popTime[i] / popDuration;                  // 0 -> 1
            float k = 1f + (popScale - 1f) * (1f - t) * (1f - t);      // büyükten normale yumuşak iniş
            frames[i].rectTransform.localScale = Vector3.one * k;
        }
    }

    // ---- Swap ----
    // Oyun donukken çağrılır (upgrade paneli yerine). Seçim onPick(slot), vazgeçme onCancel ile döner.
    public void BeginSwap(WeaponUpgradeData incoming, Action<int> onPick, Action onCancel)
    {
        onSwapPick = onPick;
        onSwapCancel = onCancel;

        BuildSwapPrompt();
        swapPrompt.SetActive(true);
        swapPrompt.transform.SetAsLastSibling();   // diğer UI'ın önünde
        string name = incoming.weapon != null ? incoming.weapon.weaponName : incoming.title;
        swapTitle.text = $"Weapon slots full! Replace a weapon with <color=#FFCD75>{name}</color>\n" +
                         $"<size=70%>Click a weapon below-right or press its number (1-{frames.Length}).</size>";
        Sprite icon = incoming.weapon != null ? incoming.weapon.Icon : incoming.icon;
        swapIcon.sprite = icon;
        swapIcon.enabled = icon != null;

        for (int i = 0; i < frames.Length; i++) SetSlotClickable(i, weapons.GetSlotData(i) != null);
    }

    void EndSwap()
    {
        onSwapPick = null;
        onSwapCancel = null;
        if (swapPrompt != null) swapPrompt.SetActive(false);

        for (int i = 0; i < frames.Length; i++)
        {
            SetSlotClickable(i, false);
            frames[i].rectTransform.localScale = Vector3.one;
        }
    }

    void PickSlot(int slot)
    {
        Action<int> pick = onSwapPick;
        EndSwap();
        pick?.Invoke(slot);
    }

    void CancelSwap()
    {
        Action cancel = onSwapCancel;
        EndSwap();
        cancel?.Invoke();
    }

    // Esc / rakam tuşları + dolu slotların nabzı. Oyun donuk: unscaledTime.
    void UpdateSwap()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.escapeKey.wasPressedThisFrame) { CancelSwap(); return; }
            for (int i = 0; i < frames.Length && i < 9; i++)
            {
                var key = kb[Key.Digit1 + i];
                var pad = kb[Key.Numpad1 + i];
                if ((key.wasPressedThisFrame || pad.wasPressedThisFrame) && weapons.GetSlotData(i) != null)
                {
                    PickSlot(i);
                    return;
                }
            }
        }

        float k = 1f + (swapPulseScale - 1f) * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f));
        for (int i = 0; i < frames.Length; i++)
            if (weapons.GetSlotData(i) != null) frames[i].rectTransform.localScale = Vector3.one * k;
    }

    void SetSlotClickable(int i, bool on)
    {
        Image frame = frames[i];
        frame.raycastTarget = on;

        var btn = frame.GetComponent<Button>();
        if (btn == null)
        {
            if (!on) return;
            btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            int slot = i;   // closure için kopya
            btn.onClick.AddListener(() => PickSlot(slot));
        }
        btn.enabled = on;
    }

    // İstem kutusunu bir kez kodda kurar: gelen silahın ikonu + yazı + İptal butonu.
    void BuildSwapPrompt()
    {
        if (swapPrompt != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.rootCanvas.transform : transform;

        swapPrompt = new GameObject("WeaponSwapPrompt", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)swapPrompt.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(760f, 170f);
        rt.anchoredPosition = new Vector2(0f, 60f);
        var bg = swapPrompt.GetComponent<Image>();
        if (panelSprite != null)
        {
            bg.sprite = panelSprite;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = panelPixelScale;
        }
        else bg.color = swapPromptBackground;

        swapIcon = MakeImage("Icon", rt, null, new Vector2(96f, 96f));
        var irt = swapIcon.rectTransform;
        irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(28f, 12f);
        swapIcon.preserveAspect = true;

        swapTitle = MakePromptText("Title", rt, 26f, TextAlignmentOptions.Left);
        var trt = swapTitle.rectTransform;
        trt.anchorMin = new Vector2(0f, 0.35f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = new Vector2(140f, 0f);
        trt.offsetMax = new Vector2(-24f, -14f);

        // İptal butonu (sağ alt)
        var cancelGo = new GameObject("Cancel", typeof(RectTransform), typeof(Image), typeof(Button));
        var crt = (RectTransform)cancelGo.transform;
        crt.SetParent(rt, false);
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 0f);
        crt.sizeDelta = new Vector2(190f, 44f);
        crt.anchoredPosition = new Vector2(-18f, 16f);
        cancelGo.GetComponent<Image>().color = new Color(0.69f, 0.24f, 0.33f, 1f);
        cancelGo.GetComponent<Button>().onClick.AddListener(CancelSwap);

        var ct = MakePromptText("Label", crt, 22f, TextAlignmentOptions.Center);
        ct.text = "Cancel (Esc)";
        ct.rectTransform.anchorMin = Vector2.zero;
        ct.rectTransform.anchorMax = Vector2.one;
        ct.rectTransform.offsetMin = ct.rectTransform.offsetMax = Vector2.zero;

        swapPrompt.SetActive(false);
    }

    TMP_Text MakePromptText(string name, Transform parent, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.richText = true;
        return t;
    }

    void Build(int slotCount)
    {
        int cols = Mathf.Max(1, Mathf.Min(columns, slotCount));
        int rows = Mathf.CeilToInt(slotCount / (float)cols);
        Vector2 grid = new Vector2(cols * slotSize + (cols - 1) * spacing, rows * slotSize + (rows - 1) * spacing);
        Vector2 size = new Vector2(padding.x + padding.y + grid.x, padding.z + headerHeight + grid.y + padding.w);

        var rt = (RectTransform)transform;
        if (anchorBottomRight)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-margin.x, margin.y);
        }
        rt.sizeDelta = size;

        if (panelSprite != null)
        {
            var bg = gameObject.GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.sprite = panelSprite;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = panelPixelScale;
            bg.raycastTarget = false;
        }

        // Başlık: solda "WEAPONS", sağda "2/6"
        // Yazılar panel köşesindeki süs çizgilerine değmesin diye yanlardan biraz içeride
        const float textInset = 6f;
        float headerY = -padding.z - headerHeight * 0.5f;
        var textPos = new Vector2(padding.x + textInset, headerY);
        float textWidth = grid.x - textInset * 2f;
        MakeText("Title", title, titleColor, TextAlignmentOptions.MidlineLeft, textPos, textWidth);
        countText = MakeText("Count", "", countColor, TextAlignmentOptions.MidlineRight, textPos, textWidth);

        frames = new Image[slotCount];
        icons = new Image[slotCount];
        popTime = new float[slotCount];
        shown = new WeaponData[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            int c = i % cols, r = i / cols;
            var pos = new Vector2(padding.x + c * (slotSize + spacing) + slotSize * 0.5f,
                                  -(padding.z + headerHeight + r * (slotSize + spacing) + slotSize * 0.5f));

            var frame = MakeImage($"Slot ({i})", transform, slotSprite, Vector2.one * slotSize);
            var frt = frame.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0f, 1f);   // sol üstten dizilir
            frt.anchoredPosition = pos;
            if (slotSprite == null) frame.color = new Color(0.1f, 0.12f, 0.2f, 0.8f);

            var icon = MakeImage("Icon", frt, null, Vector2.one * slotSize * iconScale);
            icon.preserveAspect = true;
            icon.enabled = false;

            frames[i] = frame;
            icons[i] = icon;
        }
    }

    static Image MakeImage(string name, Transform parent, Sprite sprite, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    TMP_Text MakeText(string name, string text, Color color, TextAlignmentOptions align, Vector2 topLeft, float width)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = topLeft;
        rt.sizeDelta = new Vector2(width, headerHeight);

        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }
}
