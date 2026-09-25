using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// PLAY'e basınca açılan zorluk seçimi: katalogdaki her zorluk için bir kart (çarpanlar,
// Core bonusu, o zorluktaki en iyi dalga). Kilitliler karartılır ve koşulu yazar.
// Karta tıkla ya da ←/→ + ENTER: seçilir ve oyun başlar. ESC: MainMenuUI ana ekrana döner.
//
// Kurulum: Menü > TopDownShooter > UI > Zorluk Seçimini Kur (MainMenu sahnesi açıkken)
public class DifficultySelectUI : MonoBehaviour
{
    [SerializeField] MainMenuUI menu;
    [SerializeField] RectTransform cardsParent;
    [SerializeField] Sprite cardSprite;
    [SerializeField] TMP_FontAsset font;
    [SerializeField] Vector2 cardSize = new Vector2(460f, 540f);
    [SerializeField] float cardSpacing = 40f;
    [Tooltip("Seçili kartın büyüklüğü.")]
    [SerializeField] float selectedScale = 1.06f;

    static readonly Color Muted = new Color32(0x94, 0xB0, 0xC2, 0xFF);
    static readonly Color Gold = new Color32(0xFF, 0xCD, 0x75, 0xFF);
    static readonly Color White = new Color32(0xF4, 0xF4, 0xF4, 0xFF);

    class Card
    {
        public DifficultyData data;
        public RectTransform root;
        public Image frame;
    }

    readonly List<Card> cards = new List<Card>();
    int selected;
    bool starting;
    int openedFrame;   // paneli açan ENTER aynı karede kartı da seçmesin

    void OnDisable() => Loc.Changed -= Rebuild;
    void Rebuild() { Build(); Highlight(); }

    void OnEnable()
    {
        Loc.Changed -= Rebuild;
        Loc.Changed += Rebuild;
        starting = false;
        openedFrame = Time.frameCount;
        Build();
        var cur = Difficulty.Current;
        selected = Mathf.Max(0, cards.FindIndex(c => c.data == cur));
        Highlight();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (starting || cards.Count == 0 || Time.frameCount <= openedFrame) return;
        if (kb == null) { int st = InputMode.HorizontalStep; if (st != 0) Move(st); return; }

        int padStep = InputMode.HorizontalStep;   // gamepad d-pad / sol analog
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame || padStep < 0) Move(-1);
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame || padStep > 0) Move(1);
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            Choose(selected);

        // Seçili kart hafif büyük dursun (unscaled: menüde zaman akıyor ama yine de güvenli)
        for (int i = 0; i < cards.Count; i++)
        {
            float target = i == selected ? selectedScale : 1f;
            var rt = cards[i].root;
            rt.localScale = Vector3.one * Mathf.MoveTowards(rt.localScale.x, target, Time.unscaledDeltaTime * 1.5f);
        }
    }

    // Sadece açık zorluklar arasında gezinir
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
        if (starting || i < 0 || i >= cards.Count || !cards[i].data.IsUnlocked) return;
        starting = true;
        selected = i;
        Highlight();
        Difficulty.Select(cards[i].data);
        if (menu != null) menu.StartGame();
    }

    void Highlight()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            bool on = i == selected;
            c.frame.color = !c.data.IsUnlocked ? new Color(0.35f, 0.37f, 0.45f, 1f)
                          : on ? Color.Lerp(Color.white, c.data.color, 0.55f) : Color.white;
        }
        if (EventSystem.current != null && selected < cards.Count)
            EventSystem.current.SetSelectedGameObject(cards[selected].root.gameObject);
    }

    void Build()
    {
        cards.Clear();
        if (cardsParent == null) return;
        for (int i = cardsParent.childCount - 1; i >= 0; i--) Destroy(cardsParent.GetChild(i).gameObject);

        var list = MetaProgress.Catalog != null ? MetaProgress.Catalog.difficulties : null;
        if (list == null) return;

        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (d == null) continue;
            bool open = d.IsUnlocked;
            float x = (i - (list.Count - 1) * 0.5f) * (cardSize.x + cardSpacing);

            var go = new GameObject($"Difficulty_{d.id}", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(cardsParent, false);
            rt.sizeDelta = cardSize;
            rt.anchoredPosition = new Vector2(x, 0f);
            var img = go.GetComponent<Image>();
            img.sprite = cardSprite;
            img.type = cardSprite != null && cardSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;   // renk Highlight'ta yönetiliyor
            btn.interactable = open;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };   // gezinmeyi biz yönetiyoruz (kart atlamasın)
            int idx = cards.Count;
            btn.onClick.AddListener(() => Choose(idx));
            // Üstüne gelince seç (klavye ve fare aynı kartı göstersin)
            var trigger = go.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (d.IsUnlocked && !starting) { selected = idx; Highlight(); } });
            trigger.triggers.Add(enter);

            float y = cardSize.y * 0.5f - 60f;
            Label(rt, "Title", Loc.T(d.title), 52f, open ? d.color : Muted, TextAlignmentOptions.Center, new Vector2(0f, y), new Vector2(cardSize.x - 40f, 64f));
            var desc = Label(rt, "Desc", Loc.T(d.description), 20f, Muted, TextAlignmentOptions.Top, new Vector2(0f, y - 78f), new Vector2(cardSize.x - 60f, 60f));
            desc.textWrappingMode = TextWrappingModes.Normal;

            string mods =
                $"{Loc.T("Enemy health")}  <color=#FFFFFF>x{d.enemyHealth:0.##}</color>\n" +
                $"{Loc.T("Enemy damage")}  <color=#FFFFFF>x{d.enemyDamage:0.##}</color>\n" +
                $"{Loc.T("Enemy speed")}  <color=#FFFFFF>x{d.enemySpeed:0.##}</color>";
            var modText = Label(rt, "Modifiers", mods, 24f, Muted, TextAlignmentOptions.Center, new Vector2(0f, -10f), new Vector2(cardSize.x - 40f, 120f));
            modText.lineSpacing = 12f;
            Label(rt, "Core", $"{Loc.T("CORE")} x{d.coreMultiplier:0.##}", 30f, Gold, TextAlignmentOptions.Center,
                  new Vector2(0f, -110f), new Vector2(cardSize.x - 40f, 40f));

            if (open)
            {
                int best = Difficulty.BestWave(d);
                Label(rt, "Best", best > 0 ? Loc.F("BEST WAVE {0}", best) : Loc.T("NOT PLAYED YET"), 22f, White,
                      TextAlignmentOptions.Center, new Vector2(0f, -cardSize.y * 0.5f + 50f), new Vector2(cardSize.x - 40f, 30f));
            }
            else
            {
                // Karartma + kilit metni + ilerleme
                var shade = new GameObject("Locked", typeof(RectTransform), typeof(Image));
                var srt = (RectTransform)shade.transform;
                srt.SetParent(rt, false);
                srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
                srt.offsetMin = new Vector2(10f, 10f); srt.offsetMax = new Vector2(-10f, -10f);
                var simg = shade.GetComponent<Image>();
                simg.color = new Color(0.02f, 0.03f, 0.07f, 0.55f);
                simg.raycastTarget = false;

                int have = d.requires != null ? Difficulty.BestWave(d.requires, orHarder: true) : 0;
                // Başlık ve koşul ayrı: uzun koşul (her dilde) iki satıra kırılır, CORE satırına binmez
                float bottom = -cardSize.y * 0.5f;
                Label(rt, "Lock", Loc.T("LOCKED"), 34f, White, TextAlignmentOptions.Center,
                      new Vector2(0f, bottom + 118f), new Vector2(cardSize.x - 40f, 40f));
                var cond = Label(rt, "LockCondition", $"<color=#FFCD75>{d.LockText}</color>\n<color=#94B0C2>{Mathf.Min(have, d.requiredWave)}/{d.requiredWave}</color>",
                                 21f, White, TextAlignmentOptions.Top, new Vector2(0f, bottom + 55f), new Vector2(cardSize.x - 60f, 76f));
                cond.textWrappingMode = TextWrappingModes.Normal;
            }

            cards.Add(new Card { data = d, root = rt, frame = img });
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
        return t;
    }
}
