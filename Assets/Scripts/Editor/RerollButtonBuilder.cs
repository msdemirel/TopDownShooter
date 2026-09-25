using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Upgrade panelinin altına "REROLL (R)  5 [coin]" butonunu kurar ve UpgradePanel'e bağlar.
// Buton sahnede gerçek bir obje olur: yerini/boyutunu sonra elle değiştirebilirsin.
//
// Kullanım: MainGame sahnesi açıkken Menü > TopDownShooter > UI > Reroll Butonunu Kur
// Tekrar çalıştırmak güvenli: eski buton silinip yeniden kurulur.
public static class RerollButtonBuilder
{
    const string ButtonSprite = "Assets/Art/Generated_v2/HUD/button_frame.png";
    const string CoinSprite = "Assets/Art/Generated_v2/Cards/coin_small.png";

    // Ekran (1920x1080 Canvas) birimleriyle: kartların alt kenarı merkezden ~235 aşağıda
    static readonly Vector2 CanvasOffset = new Vector2(0f, -285f);
    static readonly Vector2 CanvasSize = new Vector2(260f, 54f);

    [MenuItem("TopDownShooter/UI/Reroll Butonunu Kur")]
    static void Build()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Reroll Butonu", "Önce Play Mode'dan çık.", "Tamam");
            return;
        }
        var panel = Object.FindAnyObjectByType<UpgradePanel>(FindObjectsInactive.Include);
        if (panel == null)
        {
            EditorUtility.DisplayDialog("Reroll Butonu", "Sahnede UpgradePanel yok. Önce MainGame sahnesini aç.", "Tamam");
            return;
        }

        var so = new SerializedObject(panel);
        var rootGo = so.FindProperty("root").objectReferenceValue as GameObject;
        var parent = (rootGo != null ? rootGo : panel.gameObject).transform as RectTransform;
        var canvas = parent.GetComponentInParent<Canvas>(true).rootCanvas;

        // Eski buton
        if (so.FindProperty("rerollButton").objectReferenceValue is Button old)
            Undo.DestroyObjectImmediate(old.gameObject);

        // Panel ölçekli olabilir (0.69 gibi): buton Canvas biriminde istenen boyutta görünsün
        float k = canvas.transform.lossyScale.x / Mathf.Max(0.0001f, parent.lossyScale.x);

        var go = new GameObject("RerollButton", typeof(RectTransform), typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(go, "Reroll Butonunu Kur");
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one * k;
        rt.anchoredPosition = CanvasOffset * k;
        rt.sizeDelta = CanvasSize;

        var img = go.GetComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonSprite);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 0.5f;   // sprite pikseli 2 UI birimi (HUD ile aynı)

        TMP_FontAsset font = panel.GetComponentInChildren<TMP_Text>(true)?.font;

        var label = Text("Label", rt, font, "REROLL <size=70%><color=#94B0C2>(R)</color></size>",
                         TextAlignmentOptions.MidlineLeft, new Vector2(20f, 0f), new Vector2(150f, 40f));
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0f, 0.5f);

        var cost = Text("Cost", rt, font, "5", TextAlignmentOptions.MidlineRight, new Vector2(-46f, 0f), new Vector2(70f, 40f));
        cost.rectTransform.anchorMin = cost.rectTransform.anchorMax = cost.rectTransform.pivot = new Vector2(1f, 0.5f);

        var coinGo = new GameObject("Coin", typeof(RectTransform), typeof(Image));
        var crt = (RectTransform)coinGo.transform;
        crt.SetParent(rt, false);
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 0.5f);
        crt.anchoredPosition = new Vector2(-16f, 0f);
        crt.sizeDelta = new Vector2(24f, 24f);
        var coin = coinGo.GetComponent<Image>();
        coin.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CoinSprite);
        coin.raycastTarget = false;

        so.FindProperty("rerollButton").objectReferenceValue = go.GetComponent<Button>();
        so.FindProperty("rerollCostText").objectReferenceValue = cost;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log("[RerollButtonBuilder] Reroll butonu kuruldu. Sahneyi kaydetmeyi unutma (Ctrl+S).");
    }

    static TMP_Text Text(string name, RectTransform parent, TMP_FontAsset font, string text,
                         TextAlignmentOptions align, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = 24f;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.richText = true;
        return t;
    }
}
