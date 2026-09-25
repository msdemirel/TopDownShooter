using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Mouse üstüne gelince UI elemanını yumuşakça büyütür, çekilince eski boyutuna döndürür.
// Upgrade kartlarına takılır ki hangi kartın seçilebilir olduğu belirgin olsun.
//
// ÖNEMLİ: Upgrade paneli açıkken oyun donuyor (Time.timeScale = 0). Bu yüzden ölçek
// animasyonu unscaledDeltaTime ile yürür — yoksa donuk ekranda hiç büyümez.
//
// Kurulum: Button/Image'ı olan kart objesine ekle (raycast'i o alır). Kartın pivotu
// ORTADA olmalı ki merkezinden büyüsün.
[RequireComponent(typeof(RectTransform))]
public class UIHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Normal (üstünde değilken) ölçek.")]
    [SerializeField] float normalScale = 1f;
    [Tooltip("Mouse üstündeyken ölçek. 1.1 = %10 büyür.")]
    [SerializeField] float hoverScale = 1.1f;
    [Tooltip("Büyüme/küçülme hızı. Yüksek = daha hızlı oturur.")]
    [SerializeField] float speed = 12f;

    [Tooltip("Üstüne gelince kart en öne gelsin (kenarları komşuların altında kalmasın).")]
    [SerializeField] bool bringToFront = true;

    RectTransform rect;
    float target;      // gidilmek istenen ölçek
    int baseSiblingIndex;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        target = normalScale;
    }

    // Kart her gösterildiğinde (panel yeniden açılınca) hover'da kalmış ölçeği sıfırla
    void OnEnable()
    {
        target = normalScale;
        rect.localScale = Vector3.one * normalScale;
        baseSiblingIndex = rect.GetSiblingIndex();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Pasif buton/kart (ör. parası yetmiyor) büyümesin: tıklanabilir sanılmasın
        if (TryGetComponent<Selectable>(out var s) && !s.IsInteractable()) return;
        target = hoverScale;
        if (bringToFront) rect.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        target = normalScale;
        if (bringToFront) rect.SetSiblingIndex(baseSiblingIndex);
    }

    void Update()
    {
        float current = rect.localScale.x;
        // unscaledDeltaTime: panel oyunu dondursa da animasyon akar
        float next = Mathf.Lerp(current, target, speed * Time.unscaledDeltaTime);
        rect.localScale = Vector3.one * next;
    }
}
