using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Son kullanılan giriş cihazı (klavye/fare mi, gamepad mi) + ortak gamepad kısayolları.
// Ekrandaki tuş etiketleri ("Space" / "A"), imleç ve tutorial metinleri buna göre değişir.
// Kendini kurar, sahneler arası yaşar.
//
// Gamepad düzeni (Xbox adlarıyla; PlayStation'da aynı konumdaki tuşlar):
//   Hareket: sol analog / d-pad   Skill 1-4: A / X / Y / RB   Pause: Start
//   Geri / iptal: B               Reroll (level-up panelinde): Y
public class InputMode : MonoBehaviour
{
    public static bool UsingGamepad { get; private set; }
    public static event Action Changed;

    const float StickDeadzone = 0.35f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<InputMode>() != null) return;
        var go = new GameObject("InputMode");
        DontDestroyOnLoad(go);
        go.AddComponent<InputMode>();
    }

    void Update()
    {
        var pad = Gamepad.current;
        bool padUsed = pad != null && (pad.wasUpdatedThisFrame && PadActive(pad));
        bool kbUsed = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                      || (Mouse.current != null && (Mouse.current.delta.ReadValue().sqrMagnitude > 4f
                                                    || Mouse.current.leftButton.wasPressedThisFrame));

        if (padUsed && !UsingGamepad) Set(true);
        else if (kbUsed && UsingGamepad) Set(false);
    }

    static bool PadActive(Gamepad p)
    {
        foreach (var c in p.allControls)
            if (c is UnityEngine.InputSystem.Controls.ButtonControl b && b.wasPressedThisFrame) return true;
        return p.leftStick.ReadValue().magnitude > StickDeadzone || p.rightStick.ReadValue().magnitude > StickDeadzone;
    }

    static void Set(bool gamepad)
    {
        UsingGamepad = gamepad;
        Changed?.Invoke();
    }

    // ---- Ortak kısayollar (bu karede basıldı mı?) ----
    public static bool BackPressed => Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
    public static bool ConfirmPressed => Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
    public static bool StartPressed => Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
    public static bool RerollPressed => Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame;

    // Sağ/sol "adım" (d-pad ya da analogun yeni itilmesi): seçim ekranlarında kart değiştirme
    static float lastStickX;
    static int stickFrame = -1;
    static int stickStep;
    public static int HorizontalStep
    {
        get
        {
            var p = Gamepad.current;
            if (p == null) return 0;
            if (p.dpad.left.wasPressedThisFrame) return -1;
            if (p.dpad.right.wasPressedThisFrame) return 1;
            if (stickFrame != Time.frameCount)   // aynı karede birden çok okuyan olabilir
            {
                stickFrame = Time.frameCount;
                float x = p.leftStick.ReadValue().x;
                stickStep = Mathf.Abs(lastStickX) < 0.5f && Mathf.Abs(x) >= 0.5f ? (int)Mathf.Sign(x) : 0;
                lastStickX = x;
            }
            return stickStep;
        }
    }

    // Ekranda gösterilecek tuş adı: klavyede verilen metin, gamepad'de gamepad tuşu
    public static string Key(string keyboard, string gamepad) => UsingGamepad ? gamepad : keyboard;
}
