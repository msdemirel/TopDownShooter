using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Metin taşmalarına TOPLU çözüm: oyundaki her UI yazısı (sahnede duran ya da kodla üretilen, her dilde)
// kutusuna sığmazsa yazı boyutu kendiliğinden küçültülür.
//   * Tek satır (NoWrap) yazılar: genişliğe sığacak kadar küçülür (çok satırlılar yüksekliğe de bakar).
//   * Kaydırmalı (wrap) yazılar: satırlar kutunun yüksekliğine sığana kadar küçülür.
// Yazı değişince asıl boyuttan yeniden hesaplanır (kısa metin tekrar büyür). En fazla %50 küçültür.
// Kendi boyutunu yöneten yazılara dokunmaz: Auto Size açık olanlar, ContentSizeFitter / Layout Group
// ile boyutlananlar ve KeepFontSize bileşeni eklenmiş objeler. Dünya yazıları (hasar sayıları) etkilenmez.
// Kendini kurar, sahneler arası yaşar.
public class TextFitter : MonoBehaviour
{
    const float MinScale = 0.5f;

    class State
    {
        public float baseSize;    // tasarlanan (asıl) boyut
        public float applied;     // en son bizim verdiğimiz boyut
        public string text;
        public Vector2 rect;
    }

    readonly Dictionary<TMP_Text, State> states = new Dictionary<TMP_Text, State>();
    readonly HashSet<TMP_Text> queue = new HashSet<TMP_Text>();
    readonly List<TMP_Text> work = new List<TMP_Text>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<TextFitter>() != null) return;
        var go = new GameObject("TextFitter");
        DontDestroyOnLoad(go);
        go.AddComponent<TextFitter>();
    }

    void OnEnable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Açılışta zaten çizilmiş yazılar
        foreach (var t in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Exclude)) queue.Add(t);
    }

    void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        var dead = new List<TMP_Text>();
        foreach (var t in states.Keys) if (t == null) dead.Add(t);
        foreach (var t in dead) states.Remove(t);
    }

    // TMP bir yazının mesh'ini yeniden kurunca çağrılır (çizim sırasında): burada boyut
    // değiştirilemez, sıraya alıp LateUpdate'te işliyoruz.
    void OnTextChanged(Object o)
    {
        if (o is TextMeshProUGUI t) queue.Add(t);
    }

    void LateUpdate()
    {
        if (queue.Count == 0) return;
        work.Clear();
        work.AddRange(queue);
        queue.Clear();
        foreach (var t in work)
            if (t != null && t.isActiveAndEnabled) Fit(t);
    }

    void Fit(TMP_Text t)
    {
        if (t.enableAutoSizing || !Eligible(t)) return;

        var r = t.rectTransform.rect;
        float w = r.width - t.margin.x - t.margin.z;
        float h = r.height - t.margin.y - t.margin.w;
        if (w < 4f) return;

        if (!states.TryGetValue(t, out var s))
        {
            s = new State { baseSize = t.fontSize, applied = -1f };
            states[t] = s;
        }
        else if (!Mathf.Approximately(t.fontSize, s.applied))
            s.baseSize = t.fontSize;   // boyutu kod değiştirmiş: yeni asıl boyut bu

        string text = t.text;
        if (s.text == text && s.rect == r.size && Mathf.Approximately(t.fontSize, s.applied)) return;
        s.text = text;
        s.rect = r.size;

        float size = s.baseSize;
        t.fontSize = size;
        if (!string.IsNullOrEmpty(text))
        {
            bool noWrap = t.textWrappingMode == TextWrappingModes.NoWrap ||
                          t.textWrappingMode == TextWrappingModes.PreserveWhitespaceNoWrap;
            if (noWrap)
            {
                float pw = t.GetPreferredValues().x;
                if (pw > w + 1f) size *= w / pw * 0.98f;
                // Çok satırlı etiket yükseklikten de taşabilir (kutu satırlara göre tasarlandıysa)
                if (text.IndexOf('\n') >= 0 && h >= s.baseSize)
                {
                    t.fontSize = size;
                    float ph = t.GetPreferredValues().y;
                    if (ph > h + 1f) size *= h / ph;
                }
            }
            else if (h >= s.baseSize)
            {
                // Satır kırılımı boyutla değiştiği için adım adım küçült
                for (int i = 0; i < 10; i++)
                {
                    t.fontSize = size;
                    if (t.GetPreferredValues(w, 0f).y <= h + 1f) break;
                    size *= 0.92f;
                }
            }
        }

        size = Mathf.Max(size, s.baseSize * MinScale);
        t.fontSize = size;
        s.applied = size;
    }

    // Boyutu başka bir sistemin belirlediği yazılar: küçültmek kutuyu da küçültür, döngüye girer
    static bool Eligible(TMP_Text t)
    {
        if (t.GetComponent<KeepFontSize>() != null) return false;
        if (t.GetComponent<ContentSizeFitter>() != null) return false;
        var parent = t.transform.parent;
        if (parent != null && parent.TryGetComponent<HorizontalOrVerticalLayoutGroup>(out var g) &&
            (g.childControlWidth || g.childControlHeight)) return false;
        return true;
    }
}
