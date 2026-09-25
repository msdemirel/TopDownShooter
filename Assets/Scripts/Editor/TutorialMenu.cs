using UnityEditor;
using UnityEngine;

// Test için: ilk kez oynayan ipuçlarını (TutorialHints) ve "Nasıl Oynanır" ekranını (TutorialPanel) sıfırlar.
public static class TutorialMenu
{
    [MenuItem("TopDownShooter/Tutorial/İpuçlarını Sıfırla")]
    static void ResetHints()
    {
        TutorialHints.ResetAll();
        TutorialPanel.ResetSeen();
        Debug.Log("[Tutorial] İpuçları ve Nasıl Oynanır ekranı sıfırlandı: bir sonraki oyunda baştan gösterilecek.");
    }

    [MenuItem("TopDownShooter/Tutorial/Nasıl Oynanır Ekranını Aç")]
    static void Open() => TutorialPanel.Show(false);

    [MenuItem("TopDownShooter/Tutorial/Nasıl Oynanır Ekranını Aç", true)]
    static bool CanOpen() => Application.isPlaying && !TutorialPanel.IsOpen;
}
