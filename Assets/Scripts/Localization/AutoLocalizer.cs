using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Sahnedeki SABİT yazıları (menü butonları, başlıklar, etiketler) sahneleri düzenlemeden çevirir:
// her TMP yazısının İngilizce hali hatırlanır; tabloda karşılığı varsa seçili dile çevrilir.
// Sahne yüklenince, dil değişince ve (sonradan açılan/kopyalanan objeler için) saniyede bir tarar.
// Kodda üretilen yazılar bunu beklemez, doğrudan Loc.T / Loc.F kullanır.
// Kendini kurar, sahneler arası yaşar.
public class AutoLocalizer : MonoBehaviour
{
    // yazı -> (İngilizce asıl, en son yazdığımız çeviri)
    readonly Dictionary<TMP_Text, (string original, string applied)> known = new Dictionary<TMP_Text, (string, string)>();
    float timer;
    static AutoLocalizer instance;

    // Kodun bir kez yazdığı sabit etiket: dil değişince de güncel kalsın (bkz. Loc.Bind)
    public static void Track(TMP_Text t, string english)
    {
        if (t == null) return;
        string tr = Loc.T(english);
        t.text = tr;
        if (instance != null) instance.known[t] = (english, tr);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<AutoLocalizer>() != null) return;
        var go = new GameObject("AutoLocalizer");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoLocalizer>();
    }

    void OnEnable()
    {
        instance = this;
        Loc.Changed += ScanAll;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ScanAll();
    }

    void OnDisable()
    {
        Loc.Changed -= ScanAll;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // Eski sahnenin yok olan yazılarını at (yeni sahnenin Awake'te Bind edilenleri kalsın)
        var dead = new List<TMP_Text>();
        foreach (var t in known.Keys) if (t == null) dead.Add(t);
        foreach (var t in dead) known.Remove(t);
        ScanAll();
    }

    void Update()
    {
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = 1f;
        ScanAll();
    }

    void ScanAll()
    {
        foreach (var t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            Apply(t);
    }

    void Apply(TMP_Text t)
    {
        if (t == null) return;
        string cur = t.text;

        if (known.TryGetValue(t, out var k))
        {
            if (cur == k.applied)
            {
                // Bizim yazdığımız çeviri duruyor: sadece dil değiştiyse güncelle
                string again = Loc.T(k.original);
                if (again != cur) { t.text = again; known[t] = (k.original, again); }
                return;
            }
            known.Remove(t);   // kod yazıyı değiştirmiş: yeni metni yeniden değerlendir
        }

        if (!Loc.Has(cur)) return;
        string tr = Loc.T(cur);
        known[t] = (cur, tr);
        if (tr != cur) t.text = tr;
    }
}
