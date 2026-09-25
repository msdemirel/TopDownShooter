using UnityEditor;
using UnityEngine;

// Test için: ilk kez oynayan ipuçlarını (TutorialHints) sıfırlar.
public static class TutorialMenu
{
    [MenuItem("TopDownShooter/Tutorial/İpuçlarını Sıfırla")]
    static void ResetHints()
    {
        TutorialHints.ResetAll();
        Debug.Log("[Tutorial] İpuçları sıfırlandı: bir sonraki oyunda baştan gösterilecek.");
    }
}
