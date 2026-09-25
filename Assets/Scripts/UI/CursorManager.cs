using UnityEngine;

// Özel cursor + görünürlük kuralı. Kendini kurar (sahneye eklemek gerekmez), sahneler arası yaşar.
//   - Menülerde (Player'ın olmadığı sahneler) ve oyun donukken (upgrade paneli, pause, swap,
//     Game Over: hepsi timeScale = 0) cursor GÖRÜNÜR.
//   - Oyun akarken GİZLİ: oyunda fare kullanılmıyor (silahlar otomatik nişan alır), ekranda
//     boşuna durmasın. Resources/CursorSettings'te 'Hide During Gameplay' ile kapatılabilir.
// Ayarlar: Resources/CursorSettings.asset (kurulum: TopDownShooter > UI > Cursor'ı Kur)
public class CursorManager : MonoBehaviour
{
    CursorSettings settings;
    Health player;
    int lastPlayerSearchFrame = -999;
    bool? lastVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<CursorManager>() != null) return;
        var go = new GameObject("CursorManager");
        DontDestroyOnLoad(go);
        go.AddComponent<CursorManager>();
    }

    void Awake()
    {
        settings = Resources.Load<CursorSettings>("CursorSettings");
        if (settings != null && settings.pointer != null)
            Cursor.SetCursor(settings.pointer, settings.hotspot, CursorMode.Auto);
    }

    void Update()
    {
        // Player'ı sahne değişince yeniden bul (sık aramamak için birkaç karede bir)
        if (player == null && Time.frameCount - lastPlayerSearchFrame > 30)
        {
            lastPlayerSearchFrame = Time.frameCount;
            var p = GameObject.FindGameObjectWithTag("Player");
            player = p != null ? p.GetComponent<Health>() : null;
        }

        bool inGameplay = player != null && !player.IsDead && Time.timeScale > 0f;
        bool visible = !(inGameplay && (settings == null || settings.hideDuringGameplay)) && !InputMode.UsingGamepad;

        if (lastVisible != visible)
        {
            lastVisible = visible;
            Cursor.visible = visible;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    // Uygulama odağı geri gelince bazı platformlar cursor'ı sıfırlar: yeniden uygula
    void OnApplicationFocus(bool focus)
    {
        if (!focus) return;
        lastVisible = null;
        if (settings != null && settings.pointer != null)
            Cursor.SetCursor(settings.pointer, settings.hotspot, CursorMode.Auto);
    }
}
