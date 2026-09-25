using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// Oyunun markası: ürün adı "Ohmfall", uygulama ikonu, Unity açılış ekranı kapalı.
// Unity açılınca/derlenince ürün adı farklıysa kendiliğinden uygulanır; elle:
//   Menü > TopDownShooter > Marka > Ohmfall Ayarlarını Uygula
// Not: PlayerPrefs ve kayıt klasörü ürün adına bağlıdır (~/.config/unity3d/<şirket>/<ürün>).
// Ad değişince eski kayıtlar yeni klasöre kopyalanmazsa oyun sıfırdan başlar.
[InitializeOnLoad]
public static class OhmfallBranding
{
    const string ProductName = "Ohmfall";
    const string IconPath = "Assets/Art/Branding/ohmfall_icon.png";

    static OhmfallBranding()
    {
        // Asset veritabanı hazır olduktan sonra (ikon yüklenebilsin)
        EditorApplication.delayCall += () =>
        {
            if (PlayerSettings.productName != ProductName) Apply();
        };
    }

    [MenuItem("TopDownShooter/Marka/Ohmfall Ayarlarını Uygula")]
    static void Apply()
    {
        PlayerSettings.productName = ProductName;

        // Unity 6: "Made with Unity" açılış ekranı ücretsiz lisansta da kapatılabilir
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;

        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);   // varsayılan ikon: tüm platformlar
        else Debug.LogWarning($"[Marka] İkon bulunamadı: {IconPath}");

        AssetDatabase.SaveAssets();
        Debug.Log($"[Marka] Ürün adı '{ProductName}', ikon ayarlandı, Unity açılış ekranı kapatıldı.");
    }
}
