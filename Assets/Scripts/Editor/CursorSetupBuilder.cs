using UnityEditor;
using UnityEngine;

// Özel cursor'ı kurar: Resources/CursorSettings.asset (CursorManager okur).
// Kullanım: Menü > TopDownShooter > UI > Cursor'ı Kur. Tekrar çalıştırmak güvenli
// (sadece doku boşsa atanır; Inspector'daki ayarların korunur).
public static class CursorSetupBuilder
{
    const string SettingsPath = "Assets/Resources/CursorSettings.asset";
    const string PointerPath = "Assets/Art/Generated_v2/Cursors/cursor_pointer.png";

    [MenuItem("TopDownShooter/UI/Cursor'ı Kur")]
    static void Setup()
    {
        System.IO.Directory.CreateDirectory("Assets/Resources");
        var settings = AssetDatabase.LoadAssetAtPath<CursorSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<CursorSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        if (settings.pointer == null)
            settings.pointer = AssetDatabase.LoadAssetAtPath<Texture2D>(PointerPath);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        if (settings.pointer == null)
            Debug.LogWarning($"[CursorSetup] {PointerPath} bulunamadı (Tools/SpriteGen/gen_hud_v2.py ile üretilir).");
        else
            Debug.Log("[CursorSetup] Cursor hazır. Play'de menülerde görünür, oyun akarken gizlenir.");
        Selection.activeObject = settings;
    }
}
