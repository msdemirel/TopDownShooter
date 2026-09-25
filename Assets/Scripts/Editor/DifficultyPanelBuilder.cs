using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static UIBuilderKit;

// Ana menüye zorluk seçim ekranını ekler ve PLAY butonunu ona yönlendirir:
//   PLAY -> DifficultyPanel (Normal / Hard / Nightmare kartları) -> kart seçilince oyun başlar.
// Kartları DifficultySelectUI çalışırken katalogdan kurar (TopDownShooter > Meta > Kurulumu Yap).
//
// Kullanım: MainMenu sahnesi açıkken Menü > TopDownShooter > UI > Zorluk Seçimini Kur
// Tekrar çalıştırmak güvenli. "Ana Menüyü Kur" da sonunda bunu çağırır.
public static class DifficultyPanelBuilder
{
    [MenuItem("TopDownShooter/UI/Zorluk Seçimini Kur")]
    static void BuildMenu()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("Zorluk", "Önce Play Mode'dan çık.", "Tamam"); return; }

        var menu = Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
        var canvas = menu != null ? menu.GetComponentInParent<Canvas>() : null;
        var mso = menu != null ? new SerializedObject(menu) : null;
        var main = mso?.FindProperty("mainPanel").objectReferenceValue as GameObject;
        if (canvas == null || main == null)
        {
            EditorUtility.DisplayDialog("Zorluk", "Kurulu ana menü bulunamadı. Önce MainMenu sahnesini aç / 'Ana Menüyü Kur'.", "Tamam");
            return;
        }

        Build(menu, (RectTransform)canvas.transform, (RectTransform)main.transform, mso);
        mso.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        Debug.Log("[DifficultyPanelBuilder] Zorluk seçimi kuruldu. Sahneyi kaydetmeyi unutma (Ctrl+S).");
    }

    public static void Build(MainMenuUI menu, RectTransform root, RectTransform main, SerializedObject mso)
    {
        if (mso.FindProperty("difficultyPanel").objectReferenceValue is GameObject old)
            Undo.DestroyObjectImmediate(old);

        var panel = Panel("DifficultyPanel", root);
        panel.SetSiblingIndex(main.GetSiblingIndex() + 1);
        Undo.RegisterCreatedObjectUndo(panel.gameObject, "Zorluk Seçimi");

        ShadowText("Title", panel, "SELECT DIFFICULTY", 72f, Gold, new Vector2(0f, 400f));
        var cards = Empty("Cards", panel, new Vector2(0f, 0f), new Vector2(1600f, 560f));
        var hint = Text("Hint", panel, "<color=#73EFF7>< ></color> CHOOSE     <color=#73EFF7>ENTER</color> START     <color=#73EFF7>ESC</color> BACK",
                        24f, Muted, TextAlignmentOptions.Center, new Vector2(0f, -330f), new Vector2(1000f, 36f));
        hint.characterSpacing = 4f;
        var back = MakeButton("BackButton", panel, "BACK", MenuIcons + "ui_back.png",
                              new Vector2(0f, -420f), new Vector2(320f, 80f), 30f, White, menu.ShowMain);

        var ui = panel.gameObject.AddComponent<DifficultySelectUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("menu").objectReferenceValue = menu;
        so.FindProperty("cardsParent").objectReferenceValue = cards;
        so.FindProperty("cardSprite").objectReferenceValue = LoadSprite(UIDir + "frame_panel.png");
        so.FindProperty("font").objectReferenceValue = UIBuilderKit.Font;
        so.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(false);

        mso.FindProperty("difficultyPanel").objectReferenceValue = panel.gameObject;

        // PLAY -> önce zorluk ekranı (StartGame yerine Play)
        if (main.Find("PlayButton") is Transform playT && playT.TryGetComponent<Button>(out var play))
        {
            Undo.RecordObject(play, "Zorluk Seçimi");
            UnityEventTools.RemovePersistentListener(play.onClick, menu.StartGame);
            UnityEventTools.RemovePersistentListener(play.onClick, menu.Play);
            UnityEventTools.AddPersistentListener(play.onClick, menu.Play);
        }
    }
}
