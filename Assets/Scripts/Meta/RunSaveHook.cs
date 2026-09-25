using UnityEngine;

// Oyun sahnesinde: kayıttan devam ise geri yükler; pencere kapanırken oyunu kaydeder.
// Kendini kurar (sahneye eklemek gerekmez).
public class RunSaveHook : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => TryCreate();
        TryCreate();
    }

    static void TryCreate()
    {
        if (FindAnyObjectByType<RunSaveHook>() != null) return;
        if (GameObject.FindGameObjectWithTag("Player") == null) return;   // sadece oyun sahnesi
        RunSave.SuppressAutoSave = false;   // yeni oyun sahnesi: pencere kapanırsa yine kaydedilsin
        var hook = new GameObject("RunSaveHook").AddComponent<RunSaveHook>();
        if (RunSave.Pending != null) hook.StartCoroutine(RunSave.RestoreRoutine());
    }

    // Pencere kapatılırsa / Alt+F4: yarım oyun kaybolmasın
    void OnApplicationQuit()
    {
        if (!RunSave.SuppressAutoSave) RunSave.SaveCurrent();
    }
}
