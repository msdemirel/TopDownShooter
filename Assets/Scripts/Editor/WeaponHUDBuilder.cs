using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Sağ alttaki silah slotu panelini (WeaponHUD) kurar ve Generated_v2 sprite'larını bağlar:
//   - HUD Canvas'ına "WeaponHUD" objesi (varsa eskisi silinip yeniden kurulur)
//   - Silah asset'lerine HUD ikonları (WeaponData.icon) — Sword II ayrı renkte
//   - SkillHUD'un Bar Frame alanı boşsa skill bar çerçevesi
//
// Kullanım: MainGame sahnesi açıkken Menü > TopDownShooter > UI > Silah Slotlarını Kur
public static class WeaponHUDBuilder
{
    const string V2 = "Assets/Art/Generated_v2/";

    static readonly (string weapon, string icon)[] WeaponIcons =
    {
        ("Assets/Prefabs/Data/Guns/Sword.asset", "Icons/weapon_sword.png"),
        ("Assets/Prefabs/Data/Guns/Sword II.asset", "Icons/weapon_sword2.png"),
        ("Assets/Prefabs/Data/Guns/Pistol.asset", "Icons/weapon_pistol.png"),
    };

    [MenuItem("TopDownShooter/UI/Silah Slotlarını Kur")]
    static void Build()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Silah Slotları", "Önce Play Mode'dan çık.", "Tamam");
            return;
        }

        var skillHud = Object.FindAnyObjectByType<SkillHUD>(FindObjectsInactive.Include);
        Canvas canvas = skillHud != null ? skillHud.GetComponentInParent<Canvas>(true)?.rootCanvas : null;
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Silah Slotları",
                "HUD Canvas'ı bulunamadı (SkillHUD'un Canvas'ı). Önce MainGame sahnesini aç.", "Tamam");
            return;
        }

        // Eski panel varsa sil (tekrar çalıştırılabilsin)
        foreach (var old in Object.FindObjectsByType<WeaponHUD>(FindObjectsInactive.Include))
            Undo.DestroyObjectImmediate(old.gameObject);

        var go = new GameObject("WeaponHUD", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Silah Slotlarını Kur");
        go.transform.SetParent(canvas.transform, false);
        go.layer = canvas.gameObject.layer;
        var hud = go.AddComponent<WeaponHUD>();

        var so = new SerializedObject(hud);
        so.FindProperty("panelSprite").objectReferenceValue = Sprite("HUD/skill_bar_frame.png");
        so.FindProperty("slotSprite").objectReferenceValue = Sprite("HUD/skill_slot.png");
        var anyText = canvas.GetComponentInChildren<TMP_Text>(true);
        if (anyText != null) so.FindProperty("font").objectReferenceValue = anyText.font;
        so.ApplyModifiedProperties();

        // Skill bar çerçevesi elle atanmadıysa aynı stilde bağla
        if (skillHud != null)
        {
            var sso = new SerializedObject(skillHud);
            var bar = sso.FindProperty("barFrame");
            if (bar != null && bar.objectReferenceValue == null)
            {
                bar.objectReferenceValue = Sprite("HUD/skill_bar_frame.png");
                sso.ApplyModifiedProperties();
            }
        }

        // Silah ikonları (asset'e yazılır)
        foreach (var (weaponPath, iconPath) in WeaponIcons)
        {
            var data = AssetDatabase.LoadAssetAtPath<WeaponData>(weaponPath);
            var icon = Sprite(iconPath);
            if (data == null || icon == null) continue;
            var wso = new SerializedObject(data);
            wso.FindProperty("icon").objectReferenceValue = icon;
            wso.ApplyModifiedProperties();
        }
        AssetDatabase.SaveAssets();

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log("[WeaponHUDBuilder] Silah slotları kuruldu. Sahneyi kaydetmeyi unutma (Ctrl+S).");
    }

    static Sprite Sprite(string rel)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(V2 + rel);
        if (s == null) Debug.LogWarning($"[WeaponHUDBuilder] Sprite bulunamadı: {V2 + rel}");
        return s;
    }
}
