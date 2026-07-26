using UnityEngine;
using UnityEngine.InputSystem;

// Oyun sırasında canlı performans/durum overlay'i (sol üstte).
// FPS, en düşük FPS, sahnedeki düşman/mermi/pickup sayısı ve timeScale gösterir.
// Bir sahne objesine ekle; F3 ile aç/kapat. OnGUI kullanır — Canvas kurulumu gerekmez.
//
// Nasıl okunur: FPS ani düşüyorsa VE düşman/mermi sayısı fırlıyorsa, o an çok obje
// var demektir (spawn/efekt taşması). Sayılar sürekli artıp hiç düşmüyorsa bir şey
// yok edilmiyordur (sızıntı).
public class DebugOverlay : MonoBehaviour
{
    [SerializeField] Key toggleKey = Key.F3;
    [SerializeField] bool visible = true;
    [Tooltip("FPS bu değerin altına düşerse kırmızı gösterilir.")]
    [SerializeField] int lowFpsThreshold = 45;
    [Tooltip("Ağır sayımları (mermi/pickup) kaç saniyede bir yenile — her kare yapmak pahalı olur.")]
    [SerializeField] float refreshInterval = 0.5f;

    float fps;
    float minFps = float.MaxValue;
    float refreshTimer;
    int projectiles, pickups;
    GUIStyle style;

    void Update()
    {
        // FPS — unscaled: oyun donsa (upgrade paneli) bile gerçek kare süresini ölçer
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f)
        {
            float cur = 1f / dt;
            fps = fps <= 0f ? cur : Mathf.Lerp(fps, cur, 0.1f);   // yumuşat
            if (cur < minFps) minFps = cur;
        }

        // Ağır sayımları seyrek yap (her kare FindObjects pahalı)
        refreshTimer -= dt;
        if (refreshTimer <= 0f)
        {
            refreshTimer = refreshInterval;
            projectiles = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length;
            pickups = Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None).Length;
        }

        var kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame)
        {
            visible = !visible;
            if (visible) minFps = float.MaxValue;   // yeniden açınca min'i sıfırla
        }
    }

    void OnGUI()
    {
        if (!visible) return;

        if (style == null)
            style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, padding = new RectOffset(8, 8, 6, 6) };

        int enemies = EnemyRegistry.Count;
        string fpsCol = fps < lowFpsThreshold ? "#ff5a5a" : "#5dff9b";
        string minCol = minFps < lowFpsThreshold ? "#ff5a5a" : "#c8d0dc";

        string txt =
            "<b>DEBUG</b>  <size=11>(F3)</size>\n" +
            $"FPS: <color={fpsCol}><b>{fps:0}</b></color>   min: <color={minCol}>{minFps:0}</color>\n" +
            $"Enemies:     {enemies}\n" +
            $"Projectiles: {projectiles}\n" +
            $"Pickups:     {pickups}\n" +
            $"TimeScale:   {Time.timeScale:0.0}";

        var box = new Rect(8, 8, 190, 128);
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(box, txt, style);
    }
}
