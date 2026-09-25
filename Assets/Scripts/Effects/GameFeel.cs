using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Oyun "hissi" için kısa zaman efektleri. Kendini kurar, sahneler arası yaşar.
//
// HitStop: vuruş anında oyunu çok kısa neredeyse durdurur (tam 0 değil: 0 "duraklatıldı"
// demek — cursor, skill girişi, paneller ona bakıyor). Bu sırada biri oyunu gerçekten
// duraklatırsa (upgrade paneli, pause) ona dokunmaz.
public class GameFeel : MonoBehaviour
{
    const float StopScale = 0.05f;
    const float Cooldown = 0.25f;   // üst üste hasarda oyun takılır gibi olmasın

    static GameFeel instance;
    float activeUntil;
    float lastStop = -999f;
    Coroutine running;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("GameFeel");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GameFeel>();
    }

    // Gamepad titreşimi (low = ağır motor, high = hafif motor). Gamepad yoksa hiçbir şey yapmaz.
    public static void Rumble(float low, float high, float seconds)
    {
        if (instance == null || Gamepad.current == null || !InputMode.UsingGamepad) return;
        instance.StartCoroutine(instance.RumbleRoutine(Gamepad.current, low, high, seconds));
    }

    IEnumerator RumbleRoutine(Gamepad pad, float low, float high, float seconds)
    {
        pad.SetMotorSpeeds(low, high);
        yield return new WaitForSecondsRealtime(seconds);
        if (pad != null) pad.SetMotorSpeeds(0f, 0f);
    }

    // Uygulama kapanırken motor açık kalmasın
    void OnApplicationQuit() { if (Gamepad.current != null) Gamepad.current.ResetHaptics(); }

    public static void HitStop(float seconds)
    {
        if (instance == null || seconds <= 0f) return;
        instance.DoHitStop(seconds);
    }

    void DoHitStop(float seconds)
    {
        float now = Time.unscaledTime;
        // Normal oyunda değilsek (duraklatılmış / başka yavaşlatma) karışma
        bool stopping = running != null;
        if (!stopping && (Time.timeScale != 1f || now - lastStop < Cooldown)) return;

        lastStop = now;
        activeUntil = Mathf.Max(activeUntil, now + seconds);   // uzun olan kazanır (boss ölümü)
        if (running == null) running = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        Time.timeScale = StopScale;
        while (Time.unscaledTime < activeUntil)
        {
            if (Time.timeScale != StopScale) { running = null; yield break; }   // biri duraklattı
            yield return null;
        }
        if (Time.timeScale == StopScale) Time.timeScale = 1f;
        running = null;
    }
}
