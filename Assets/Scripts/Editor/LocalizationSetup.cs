using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Dil desteği için font kurulumu. PixCon (pixel font) Türkçe/Fransızca/Almanca/İtalyanca/Fince
// karakterlerini içerir ama Çince içermez: Çince için dinamik bir yedek font asset'i oluşturulup
// PixCon SDF'nin yedek (fallback) listesine eklenir. PixCon'da olmayan karakter oradan çizilir.
//
// Kullanım: Menü > TopDownShooter > Dil > Çince Fontu Kur   (bir kez yeterli; tekrar çalıştırmak güvenli)
public static class LocalizationSetup
{
    const string SourceFont = "Assets/Art/UI/Fonts/DroidSansFallbackFull.ttf";
    const string FontAssetPath = "Assets/Art/UI/Fonts/DroidSansFallback SDF.asset";
    const string PixConPath = "Assets/Art/UI/PixCon SDF.asset";

    [MenuItem("TopDownShooter/Dil/Çince Fontu Kur")]
    static void Setup()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFont);
        var pixcon = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixConPath);
        if (font == null || pixcon == null)
        {
            EditorUtility.DisplayDialog("Dil", $"Font bulunamadı:\n{SourceFont}\n{PixConPath}", "Tamam");
            return;
        }

        var cjk = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (cjk == null)
        {
            // Dinamik atlas: karakterler ihtiyaç oldukça eklenir (binlerce Çince karakteri önceden basmaya gerek yok)
            cjk = TMP_FontAsset.CreateFontAsset(font, 48, 6, GlyphRenderMode.SDFAA, 1024, 1024,
                                                AtlasPopulationMode.Dynamic, true);
            cjk.name = "DroidSansFallback SDF";
            AssetDatabase.CreateAsset(cjk, FontAssetPath);
            // Atlas dokusu ve materyal asset'in içine kaydedilmeli (yoksa sahne açılınca kaybolur)
            foreach (var tex in cjk.atlasTextures) if (tex != null) { tex.name = cjk.name + " Atlas"; AssetDatabase.AddObjectToAsset(tex, cjk); }
            if (cjk.material != null) { cjk.material.name = cjk.name + " Material"; AssetDatabase.AddObjectToAsset(cjk.material, cjk); }
        }

        // PixCon'un yedek listesine ekle (bir kez)
        if (pixcon.fallbackFontAssetTable == null) pixcon.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
        if (!pixcon.fallbackFontAssetTable.Contains(cjk)) pixcon.fallbackFontAssetTable.Add(cjk);
        EditorUtility.SetDirty(pixcon);

        // TMP'nin genel yedek listesine de ekle (PixCon kullanmayan yazılar için)
        var settings = TMP_Settings.instance;
        if (settings != null && TMP_Settings.fallbackFontAssets != null && !TMP_Settings.fallbackFontAssets.Contains(cjk))
        {
            TMP_Settings.fallbackFontAssets.Add(cjk);
            EditorUtility.SetDirty(settings);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Dil] Çince yedek font hazır ve PixCon'a bağlandı.");
    }

    [MenuItem("TopDownShooter/Dil/Çeviri Tablosunu Yeniden Yükle")]
    static void Reload()
    {
        AssetDatabase.Refresh();
        Loc.Reload();
        Debug.Log($"[Dil] Tablo yeniden yüklendi. Seçili dil: {Loc.Current}");
    }
}
