using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// PLAY'e basınca açılan karakter seçimi: katalogdaki her karakter için bir kart (yürüyen
// portre, başlangıç silahı, pasif bonuslar). Kilitliler silüet + koşul/ilerleme gösterir.
// Karta tıkla ya da ←/→ + ENTER: seçilir, sonra zorluk ekranı açılır (MainMenuUI.OpenDifficulty).
//
// Kurulum: Menü > TopDownShooter > UI > Karakter Seçimini Kur (MainMenu sahnesi açıkken)
public class CharacterSelectUI : MonoBehaviour
{
    [SerializeField] MainMenuUI menu;
    [SerializeField] RectTransform cardsParent;
    [SerializeField] Sprite cardSprite;
    [SerializeField] TMP_FontAsset font;
    [SerializeField] Vector2 cardSize = new Vector2(380f, 580f);
    [SerializeField] float cardSpacing = 30f;
    [SerializeField] float selectedScale = 1.06f;
    [Tooltip("Portre animasyonunun kare hızı.")]
    [SerializeField] float previewFps = 8f;

    static readonly Color Muted = new Color32(0x94, 0xB0, 0xC2, 0xFF);
    static readonly Color White = new Color32(0xF4, 0xF4, 0xF4, 0xFF);
    static readonly Color Silhouette = new Color(0.08f, 0.09f, 0.14f, 1f);

    class Card
    {
        public CharacterData data;
        public RectTransform root;
        public Image frame, portrait;
    }

    readonly List<Card> cards = new List<Card>();
    int selected;
    bool choosing;
    int openedFrame;   // paneli açan ENTER aynı karede kartı da seçmesin

    void OnEnable()
    {
        choosing = false;
        openedFrame = Time.frameCount;
        Build();
        var cur = CharacterSelection.Current;
        selected = Mathf.Max(0, cards.FindIndex(c => c.data == cur));
        Highlight();
    }

    void Update()
    {
        if (cards.Count == 0) return;

        // Portreler yürür (seçili olan her zaman, diğerleri de; kilitliler silüet olarak)
        int frame = Mathf.FloorToInt(Time.unscaledTime * previewFps);
        foreach (var c in cards)
        {
            var f = c.data.previewFrames;
            if (f != null && f.Length > 0) c.portrait.sprite = f[frame % f.Length];
        }

        for (int i = 0; i < cards.Count; i++)
        {
            float target = i == selected ? selectedScale : 1f;
            var rt = cards[i].root;
            rt.localScale = Vector3.one * Mathf.MoveTowards(rt.localScale.x, target, Time.unscaledDeltaTime * 1.5f);
        }

        var kb = Keyboard.current;
        if (kb == null || choosing || Time.frameCount <= openedFrame) return;
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Move(-1);
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Move(1);
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            Choose(selected);
    }

    void Move(int dir)
    {
        for (int step = 1; step <= cards.Count; step++)
        {
            int i = (selected + dir * step + cards.Count * 4) % cards.Count;
            if (cards[i].data.IsUnlocked) { selected = i; Highlight(); return; }
        }
    }

    void Choose(int i)
    {
        if (choosing || i < 0 || i >= cards.Count || !cards[i].data.IsUnlocked) return;
        choosing = true;
        selected = i;
        CharacterSelection.Select(cards[i].data);
        if (menu != null) menu.OpenDifficulty();
    }

    void Highlight()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            c.frame.color = !c.data.IsUnlocked ? new Color(0.35f, 0.37f, 0.45f, 1f)
                          : i == selected ? Color.Lerp(Color.white, c.data.color, 0.55f) : Color.white;
        }
        if (EventSystem.current != null && selected < cards.Count)
            EventSystem.current.SetSelectedGameObject(cards[selected].root.gameObject);
    }

    void Build()
    {
        cards.Clear();
        if (cardsParent == null) return;
        for (int i = cardsParent.childCount - 1; i >= 0; i--) Destroy(cardsParent.GetChild(i).gameObject);

        var list = MetaProgress.Catalog != null ? MetaProgress.Catalog.characters : null;
        if (list == null) return;

        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (d == null) continue;
            bool open = d.IsUnlocked;
            float x = (i - (list.Count - 1) * 0.5f) * (cardSize.x + cardSpacing);

            var go = new GameObject($"Character_{d.id}", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(cardsParent, false);
            rt.sizeDelta = cardSize;
            rt.anchoredPosition = new Vector2(x, 0f);
            var img = go.GetComponent<Image>();
            img.sprite = cardSprite;
            img.type = cardSprite != null && cardSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;   // renk Highlight'ta
            btn.interactable = open;
            int idx = cards.Count;
            btn.onClick.AddListener(() => Choose(idx));
            var trigger = go.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (d.IsUnlocked && !choosing) { selected = idx; Highlight(); } });
            trigger.triggers.Add(enter);

            float top = cardSize.y * 0.5f;

            // Portre: pixel art keskin kalsın diye tam kat büyütme (32px kare -> 160)
            var pgo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            var prt = (RectTransform)pgo.transform;
            prt.SetParent(rt, false);
            prt.anchoredPosition = new Vector2(0f, top - 120f);
            prt.sizeDelta = new Vector2(160f, 160f);
            var portrait = pgo.GetComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.color = open ? Color.white : Silhouette;
            if (d.previewFrames != null && d.previewFrames.Length > 0) portrait.sprite = d.previewFrames[0];

            Label(rt, "Title", d.title, 40f, open ? d.color : Muted, TextAlignmentOptions.Center,
                  new Vector2(0f, top - 232f), new Vector2(cardSize.x - 30f, 48f)).enableAutoSizing = false;
            var desc = Label(rt, "Desc", d.description, 18f, Muted, TextAlignmentOptions.Top,
                             new Vector2(0f, top - 285f), new Vector2(cardSize.x - 40f, 48f));
            desc.textWrappingMode = TextWrappingModes.Normal;

            if (open)
            {
                // Başlangıç silahı: ikon + ad
                if (d.startingWeapon != null)
                {
                    var wgo = new GameObject("WeaponIcon", typeof(RectTransform), typeof(Image));
                    var wrt = (RectTransform)wgo.transform;
                    wrt.SetParent(rt, false);
                    wrt.anchoredPosition = new Vector2(-cardSize.x * 0.5f + 60f, top - 345f);
                    wrt.sizeDelta = new Vector2(44f, 44f);
                    var wimg = wgo.GetComponent<Image>();
                    wimg.sprite = d.startingWeapon.Icon;
                    wimg.preserveAspect = true;
                    wimg.raycastTarget = false;
                    var wl = Label(rt, "Weapon", $"<size=70%><color=#94B0C2>STARTS WITH</color></size>\n{d.startingWeapon.weaponName}",
                                   22f, White, TextAlignmentOptions.MidlineLeft,
                                   new Vector2(40f, top - 345f), new Vector2(cardSize.x - 130f, 52f));
                    wl.lineSpacing = -10f;
                }
                var bonus = Label(rt, "Bonuses", d.BonusText(), 20f, White, TextAlignmentOptions.Top,
                                  new Vector2(0f, top - 450f), new Vector2(cardSize.x - 40f, 110f));
                bonus.lineSpacing = 6f;
            }
            else
            {
                Label(rt, "Lock", $"LOCKED\n<size=60%><color=#FFCD75>{MetaProgress.Describe(d.unlockCondition, d.unlockThreshold)}</color>\n" +
                                  $"<color=#94B0C2>{MetaProgress.ProgressText(d.unlockCondition, d.unlockThreshold)}</color></size>",
                      34f, White, TextAlignmentOptions.Center, new Vector2(0f, top - 420f), new Vector2(cardSize.x - 30f, 140f));
            }

            cards.Add(new Card { data = d, root = rt, frame = img, portrait = portrait });
        }
    }

    TMP_Text Label(RectTransform parent, string name, string text, float size, Color color,
                   TextAlignmentOptions align, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        t.raycastTarget = false;
        // Kart dışına taşmasın: sığmazsa küçülsün
        t.fontSizeMax = size;
        t.fontSizeMin = size * 0.6f;
        t.enableAutoSizing = true;
        return t;
    }
}
