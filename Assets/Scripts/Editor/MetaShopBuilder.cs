using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static UIBuilderKit;

// Ana menüye kalıcı upgrade mağazasını ekler:
//   - Ana panelde PLAY'in altına "UPGRADES" butonu + Core bakiyesi (Options/Quit aşağı kayar)
//   - "ShopPanel": başlık, Core bakiyesi, UPGRADES / UNLOCKS sekmeleri, içerik alanları, BACK
//   - MetaShopUI (kartları çalışırken katalogdan kurar) ve MainMenuUI referansları
//
// Kullanım: MainMenu sahnesi açıkken Menü > TopDownShooter > UI > Mağazayı Kur
// Ana menünün geri kalanına dokunmaz. Tekrar çalıştırmak güvenli (mağaza yeniden kurulur).
// "Ana Menüyü Kur" da sonunda bunu çağırır.
public static class MetaShopBuilder
{
    const string V2 = "Assets/Art/Generated_v2/";
    const float ButtonStep = 105f;   // ana paneldeki butonlar arası dikey adım

    [MenuItem("TopDownShooter/UI/Mağazayı Kur")]
    static void BuildMenu()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("Mağaza", "Önce Play Mode'dan çık.", "Tamam"); return; }

        var menu = Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
        if (menu == null)
        {
            EditorUtility.DisplayDialog("Mağaza", "Açık sahnede MainMenuUI yok. Önce MainMenu sahnesini aç.", "Tamam");
            return;
        }
        var mso = new SerializedObject(menu);
        var main = mso.FindProperty("mainPanel").objectReferenceValue as GameObject;
        var canvas = menu.GetComponentInParent<Canvas>();
        if (main == null || canvas == null)
        {
            EditorUtility.DisplayDialog("Mağaza", "Ana menü kurulu değil. Önce 'Ana Menüyü Kur'.", "Tamam");
            return;
        }

        Build(menu, (RectTransform)canvas.transform, (RectTransform)main.transform, mso);
        mso.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        Debug.Log("[MetaShopBuilder] Mağaza kuruldu. Sahneyi kaydetmeyi unutma (Ctrl+S).");
    }

    public static void Build(MainMenuUI menu, RectTransform root, RectTransform main, SerializedObject mso)
    {
        // ---- Eskileri temizle ----
        if (mso.FindProperty("shopPanel").objectReferenceValue is GameObject oldShop)
            Undo.DestroyObjectImmediate(oldShop);
        var oldBtn = main.Find("UpgradesButton");
        bool firstTime = oldBtn == null;
        if (oldBtn != null) Undo.DestroyObjectImmediate(oldBtn.gameObject);
        var oldBadge = main.Find("CoreBalance");
        if (oldBadge != null) Undo.DestroyObjectImmediate(oldBadge.gameObject);

        // ---- Ana panel: UPGRADES butonu PLAY'in altına, Options/Quit bir adım aşağı ----
        var play = main.Find("PlayButton") as RectTransform;
        float x = play != null ? play.anchoredPosition.x : -430f;
        float y = play != null ? play.anchoredPosition.y - 115f : -165f;
        if (firstTime)
            foreach (var n in new[] { "OptionsButton", "QuitButton" })
                if (main.Find(n) is RectTransform b)
                {
                    Undo.RecordObject(b, "Mağaza");
                    b.anchoredPosition -= new Vector2(0f, ButtonStep);
                }

        var upgrades = MakeButton("UpgradesButton", main, "UPGRADES", V2 + "HUD/icon_core.png",
                                  new Vector2(x, y), new Vector2(420f, 84f), 32f, Cyan, menu.OpenShop);
        upgrades.transform.SetSiblingIndex(play != null ? play.GetSiblingIndex() + 1 : 0);

        // Core bakiyesi: butonun sağında küçük rozet
        var badge = Empty("CoreBalance", main, new Vector2(x + 290f, y), new Vector2(150f, 50f));
        Icon("Icon", badge, V2 + "HUD/icon_core.png", new Vector2(-50f, 0f), 36f);
        var balance = Text("Value", badge, "0", 30f, Gold, TextAlignmentOptions.Left,
                           new Vector2(30f, 0f), new Vector2(110f, 44f));

        // ---- Mağaza paneli ----
        var shop = Panel("ShopPanel", root);
        shop.SetSiblingIndex(main.GetSiblingIndex() + 1);

        ShadowText("Title", shop, "UPGRADES", 72f, Gold, new Vector2(0f, 430f));

        var coreBox = Frame("CoreBox", shop, UIDir + "frame_slot.png", new Vector2(700f, 430f), new Vector2(300f, 84f));
        Icon("Icon", coreBox, V2 + "HUD/icon_core.png", new Vector2(-100f, 0f), 52f);
        var coreText = Text("Value", coreBox, "0", 40f, White, TextAlignmentOptions.Left,
                            new Vector2(40f, 0f), new Vector2(170f, 60f));

        // Sekmeler: ikon ile yazı birbirine değmesin diye geniş kutu + biraz küçük yazı
        var tabUp = MakeButton("TabUpgrades", shop, "UPGRADES", V2 + "HUD/icon_core.png",
                               new Vector2(-175f, 340f), new Vector2(330f, 66f), 24f, Cyan, null);
        var tabUn = MakeButton("TabUnlocks", shop, "UNLOCKS", GameOverIcons + "ui_trophy.png",
                               new Vector2(175f, 340f), new Vector2(330f, 66f), 24f, White, null);

        var upContent = Empty("UpgradesContent", shop, new Vector2(0f, -20f), new Vector2(1700f, 600f));
        var unContent = Empty("UnlocksContent", shop, new Vector2(0f, -20f), new Vector2(1700f, 600f));

        var back = MakeButton("BackButton", shop, "BACK", MenuIcons + "ui_back.png",
                              new Vector2(0f, -430f), new Vector2(320f, 80f), 30f, White, menu.ShowMain);

        var ui = shop.gameObject.AddComponent<MetaShopUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("upgradesContent").objectReferenceValue = upContent;
        so.FindProperty("unlocksContent").objectReferenceValue = unContent;
        so.FindProperty("coreText").objectReferenceValue = coreText;
        so.FindProperty("upgradesTab").objectReferenceValue = tabUp.GetComponent<Button>();
        so.FindProperty("unlocksTab").objectReferenceValue = tabUn.GetComponent<Button>();
        so.FindProperty("cardSprite").objectReferenceValue = LoadSprite(UIDir + "frame_slot.png");
        so.FindProperty("buttonSprite").objectReferenceValue = LoadSprite(V2 + "HUD/button_frame.png");
        so.FindProperty("coreIcon").objectReferenceValue = LoadSprite(V2 + "HUD/icon_core.png");
        so.FindProperty("checkIcon").objectReferenceValue = LoadSprite(MenuIcons + "ui_check.png");
        so.FindProperty("font").objectReferenceValue = UIBuilderKit.Font;
        so.ApplyModifiedPropertiesWithoutUndo();

        shop.gameObject.SetActive(false);
        Undo.RegisterCreatedObjectUndo(shop.gameObject, "Mağaza");
        Undo.RegisterCreatedObjectUndo(upgrades, "Mağaza");
        Undo.RegisterCreatedObjectUndo(badge.gameObject, "Mağaza");

        mso.FindProperty("shopPanel").objectReferenceValue = shop.gameObject;
        mso.FindProperty("shopFirstSelected").objectReferenceValue = back;
        mso.FindProperty("coreBalanceText").objectReferenceValue = balance;
    }
}
