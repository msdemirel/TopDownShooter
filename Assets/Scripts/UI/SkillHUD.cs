using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Kazanılan skillerin ikonlarını, tuşlarını ve cooldown durumunu gösterir.
// Slotlar başta gizlidir; skill kazanıldıkça sırayla görünür olur.
// Cooldown overlay'i her karede PlayerSkills'ten okunur (WaveHUD kalıbı).
//
// PlayerSkills'te sahnedeki kutulardan fazla slot varsa (ör. 4. tuş eklendi) eksik
// kutular sonuncunun KOPYASI olarak otomatik üretilir ve aynı aralıkla dizilir.
//
// SWAP: Slotlar doluyken yeni skill seçilince UpgradeManager BeginSwap çağırır.
// Kutular tıklanabilir olur ve nabız gibi atar; oyuncu kutuya tıklar ya da slotun
// tuşuna basar. Esc / İptal butonu seçim paneline geri döner.
public class SkillHUD : MonoBehaviour
{
    // Ekrandaki tek bir skill kutusu.
    [System.Serializable]
    public class SlotUI
    {
        [Tooltip("Kutunun tamamı (skill kazanılana kadar gizlenir).")]
        public GameObject root;
        [Tooltip("Skill ikonu (SkillUpgradeData.icon buraya basılır).")]
        public Image icon;
        [Tooltip("Cooldown karartması: Image tipi 'Filled' olmalı. 1 = az önce kullanıldı.")]
        public Image cooldownOverlay;
        [Tooltip("Kalan cooldown saniyesini gösteren yazı (ör. \"3\"). Hazırken boşalır. Opsiyonel.")]
        public TMP_Text cooldownText;
        [Tooltip("Tuş etiketi (\"Space\", \"E\"...). Opsiyonel.")]
        public TMP_Text keyText;
    }

    [Header("Referanslar")]
    [Tooltip("Boş bırakılırsa 'Player' tag'inden bulunur.")]
    [SerializeField] PlayerSkills playerSkills;

    [Tooltip("Sıra PlayerSkills'teki slot tuşlarıyla aynı olmalı (0. kutu 0. tuş).")]
    [SerializeField] SlotUI[] slots;

    [Header("Swap ekranı")]
    [Tooltip("Swap sırasında kutuların nabız büyüklüğü (1.1 = %10 büyür).")]
    [SerializeField] float swapPulseScale = 1.12f;
    [SerializeField] Color swapPromptBackground = new Color(0.1f, 0.11f, 0.17f, 0.92f);

    // Swap durumu
    Action<int> onSwapPick;
    Action onSwapCancel;
    Vector3[] slotBaseScales;
    GameObject swapPrompt;
    Image swapIcon;
    TMP_Text swapTitle;

    public bool IsSwapping => onSwapPick != null;

    void Start()
    {
        if (playerSkills == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerSkills = p.GetComponent<PlayerSkills>();
        }

        if (playerSkills == null)
        {
            Debug.LogWarning("[SkillHUD] Player'da PlayerSkills bulunamadı.", this);
            enabled = false;
            return;
        }

        EnsureSlotCount(playerSkills.SlotCount);
        slotBaseScales = new Vector3[slots.Length];
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && slots[i].root != null) slotBaseScales[i] = slots[i].root.transform.localScale;

        playerSkills.OnSkillAdded += HandleSkillAdded;
        playerSkills.OnSkillReplaced += HandleSkillReplaced;

        // Başta tüm kutular gizli (skill kazanılınca açılır)
        foreach (var s in slots)
            if (s != null && s.root != null) s.root.SetActive(false);
    }

    void OnDestroy()
    {
        if (playerSkills == null) return;
        playerSkills.OnSkillAdded -= HandleSkillAdded;
        playerSkills.OnSkillReplaced -= HandleSkillReplaced;
    }

    // Sahnedeki kutu sayısı slot sayısından azsa, son kutuyu kopyalayıp aynı aralıkla dizer.
    void EnsureSlotCount(int count)
    {
        if (slots == null || slots.Length == 0 || slots.Length >= count) return;

        SlotUI last = slots[slots.Length - 1];
        if (last == null || last.root == null) return;

        // Kutular arası adım: son iki kutunun farkı (tek kutu varsa genişliği kadar sola)
        Vector3 step;
        if (slots.Length >= 2 && slots[slots.Length - 2] != null && slots[slots.Length - 2].root != null)
            step = last.root.transform.localPosition - slots[slots.Length - 2].root.transform.localPosition;
        else
            step = new Vector3(-((RectTransform)last.root.transform).rect.width, 0f, 0f);

        var list = new System.Collections.Generic.List<SlotUI>(slots);
        while (list.Count < count)
        {
            GameObject clone = Instantiate(last.root, last.root.transform.parent);
            clone.name = $"SkillSlot ({list.Count})";
            clone.transform.localPosition = last.root.transform.localPosition + step * (list.Count - slots.Length + 1);

            list.Add(new SlotUI
            {
                root = clone,
                icon = Twin(last.icon, last.root, clone),
                cooldownOverlay = Twin(last.cooldownOverlay, last.root, clone),
                cooldownText = Twin(last.cooldownText, last.root, clone),
                keyText = Twin(last.keyText, last.root, clone),
            });
        }
        slots = list.ToArray();
    }

    // Kaynak kutudaki bileşenin, kopyadaki karşılığını (aynı hiyerarşi yolu) bulur.
    static T Twin<T>(T source, GameObject sourceRoot, GameObject cloneRoot) where T : Component
    {
        if (source == null) return null;
        if (source.gameObject == sourceRoot) return cloneRoot.GetComponent<T>();

        string path = source.name;
        for (Transform t = source.transform.parent; t != null && t.gameObject != sourceRoot; t = t.parent)
            path = t.name + "/" + path;

        Transform found = cloneRoot.transform.Find(path);
        return found != null ? found.GetComponent<T>() : null;
    }

    void HandleSkillAdded(int slot, SkillUpgradeData skill)
    {
        if (slot < 0 || slot >= slots.Length || slots[slot] == null) return;

        SlotUI ui = slots[slot];
        if (ui.root != null) ui.root.SetActive(true);
        if (ui.icon != null) ui.icon.sprite = skill.icon;
        if (ui.keyText != null) ui.keyText.text = playerSkills.GetKeyName(slot);
    }

    void HandleSkillReplaced(int slot, SkillUpgradeData skill) => HandleSkillAdded(slot, skill);

    // ---- Swap ----
    // Oyun donukken çağrılır (upgrade paneli yerine). Seçim onPick(slot), vazgeçme onCancel ile döner.
    public void BeginSwap(SkillUpgradeData incoming, Action<int> onPick, Action onCancel)
    {
        onSwapPick = onPick;
        onSwapCancel = onCancel;

        BuildSwapPrompt();
        swapPrompt.SetActive(true);
        swapPrompt.transform.SetAsLastSibling();   // diğer UI'ın önünde
        swapTitle.text = $"Slots full! Replace a skill with <color=#FFCD75>{incoming.title}</color>\n" +
                         "<size=70%>Click a skill below or press its key.  Its level will be lost.</size>";
        swapIcon.sprite = incoming.icon;
        swapIcon.enabled = incoming.icon != null;

        for (int i = 0; i < slots.Length; i++)
            SetSlotClickable(i, playerSkills.GetSkill(i) != null);
    }

    void EndSwap()
    {
        onSwapPick = null;
        onSwapCancel = null;
        if (swapPrompt != null) swapPrompt.SetActive(false);

        for (int i = 0; i < slots.Length; i++)
        {
            SetSlotClickable(i, false);
            if (slots[i] != null && slots[i].root != null) slots[i].root.transform.localScale = slotBaseScales[i];
        }
    }

    void PickSlot(int slot)
    {
        if (!IsSwapping || playerSkills.GetSkill(slot) == null) return;
        Action<int> pick = onSwapPick;
        EndSwap();
        pick(slot);
    }

    void CancelSwap()
    {
        if (!IsSwapping) return;
        Action cancel = onSwapCancel;
        EndSwap();
        cancel?.Invoke();
    }

    // Kutuya (ilk seferde) Button ekler ve swap süresince tıklanabilir yapar.
    void SetSlotClickable(int i, bool on)
    {
        SlotUI ui = slots[i];
        if (ui == null || ui.root == null) return;

        var btn = ui.root.GetComponent<Button>();
        if (btn == null)
        {
            if (!on) return;
            btn = ui.root.AddComponent<Button>();
            btn.targetGraphic = ui.icon;
            int slot = i;   // closure için kopya
            btn.onClick.AddListener(() => PickSlot(slot));
        }
        btn.enabled = on;
    }

    // İstem kutusunu bir kez kodda kurar (prefab gerekmez): ikon + yazı + İptal butonu.
    // Yazı tipi HUD'daki tuş etiketinden alınır ki oyunun fontuyla aynı olsun.
    void BuildSwapPrompt()
    {
        if (swapPrompt != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.rootCanvas.transform : transform;
        TMP_FontAsset font = null;
        foreach (var s in slots)
            if (s != null && s.keyText != null) { font = s.keyText.font; break; }

        swapPrompt = new GameObject("SkillSwapPrompt", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)swapPrompt.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(760f, 170f);
        rt.anchoredPosition = new Vector2(0f, 60f);
        swapPrompt.GetComponent<Image>().color = swapPromptBackground;

        // Gelen skill'in ikonu (solda)
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var irt = (RectTransform)iconGo.transform;
        irt.SetParent(rt, false);
        irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0f, 0.5f);
        irt.sizeDelta = new Vector2(96f, 96f);
        irt.anchoredPosition = new Vector2(24f, 12f);
        swapIcon = iconGo.GetComponent<Image>();
        swapIcon.preserveAspect = true;
        swapIcon.raycastTarget = false;

        swapTitle = MakeText("Title", rt, font, 28f, TextAlignmentOptions.Left);
        var trt = swapTitle.rectTransform;
        trt.anchorMin = new Vector2(0f, 0.35f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = new Vector2(140f, 0f);
        trt.offsetMax = new Vector2(-20f, -10f);

        // İptal butonu (sağ alt)
        var cancelGo = new GameObject("Cancel", typeof(RectTransform), typeof(Image), typeof(Button));
        var crt = (RectTransform)cancelGo.transform;
        crt.SetParent(rt, false);
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 0f);
        crt.sizeDelta = new Vector2(190f, 44f);
        crt.anchoredPosition = new Vector2(-16f, 14f);
        cancelGo.GetComponent<Image>().color = new Color(0.69f, 0.24f, 0.33f, 1f);
        cancelGo.GetComponent<Button>().onClick.AddListener(CancelSwap);

        var ct = MakeText("Label", crt, font, 22f, TextAlignmentOptions.Center);
        ct.text = "Cancel (Esc)";
        ct.rectTransform.anchorMin = Vector2.zero;
        ct.rectTransform.anchorMax = Vector2.one;
        ct.rectTransform.offsetMin = ct.rectTransform.offsetMax = Vector2.zero;
    }

    static TMP_Text MakeText(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions align)
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

    void Update()
    {
        if (IsSwapping) UpdateSwap();

        // Sadece dolu slotları güncelle
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || playerSkills.GetSkill(i) == null) continue;

            if (slots[i].cooldownOverlay != null)
                slots[i].cooldownOverlay.fillAmount = playerSkills.GetCooldownNormalized(i);

            if (slots[i].cooldownText != null)
            {
                float remaining = playerSkills.GetCooldownRemaining(i);
                // Yukarı yuvarla: "3,2,1" gibi görünsün; hazırken (0) boş kalsın
                slots[i].cooldownText.text = remaining > 0.05f
                    ? Mathf.CeilToInt(remaining).ToString()
                    : "";
            }
        }
    }

    // Swap sırasında: slot tuşu / Esc dinlenir, dolu kutular nabız gibi atar.
    // Oyun donuk (timeScale 0) olduğu için unscaledTime kullanılır.
    void UpdateSwap()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { CancelSwap(); return; }

        int pressed = playerSkills.GetPressedSlot();
        if (pressed >= 0 && playerSkills.GetSkill(pressed) != null) { PickSlot(pressed); return; }

        float k = 1f + (swapPulseScale - 1f) * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f));
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && slots[i].root != null && playerSkills.GetSkill(i) != null)
                slots[i].root.transform.localScale = slotBaseScales[i] * k;
    }
}
