using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

// Tutorial videolarını (TutorialPanel) oyunun içinden çeker: Play Mode'dayken
//   Menü > TopDownShooter > Tutorial > Klip Kaydet > <sayfa>
// seçilince Game görünümü Seconds saniye boyunca kaydedilir ve Resources/Tutorial/<sayfa>.webm
// olarak yazılır (eski klibin üstüne). Kayıt sırasında göstermek istediğin şeyi oyna
// (ör. "grid" için düşmanları aynı yerde öldürüp bir 2x2 bloğu aşırı yükle).
// VP8 WebM: Linux editörü de oynatabilsin diye (H.264 MP4 Linux'ta içe aktarılamıyor).
public static class TutorialClipRecorder
{
    const float Seconds = 8f;
    const int Width = 1280, Height = 800;   // video alanıyla aynı oran (16:10)
    const string Folder = "Assets/Resources/Tutorial";
    const string Root = "TopDownShooter/Tutorial/Klip Kaydet/";

    static RecorderController controller;
    static double stopAt;
    static string recordingId;

    [MenuItem(Root + "1 Hareket (move)")] static void Move() => Record("move");
    [MenuItem(Root + "2 Seviye Atlama (levelup)")] static void LevelUp() => Record("levelup");
    [MenuItem(Root + "3 Silahlar (weapons)")] static void Weapons() => Record("weapons");
    [MenuItem(Root + "4 Yetenekler (skills)")] static void Skills() => Record("skills");
    [MenuItem(Root + "5 Dalgalar (waves)")] static void Waves() => Record("waves");
    [MenuItem(Root + "6 Dalga Gorevleri (quests)")] static void Quests() => Record("quests");
    [MenuItem(Root + "7 Zemin Izgarasi (grid)")] static void Grid() => Record("grid");
    [MenuItem(Root + "8 Reaktor (reactor)")] static void Reactor() => Record("reactor");
    [MenuItem(Root + "9 Core (core)")] static void Core() => Record("core");

    [MenuItem(Root + "Kaydı Şimdi Durdur")]
    static void StopNow() => Stop();

    // Kayıt sırasında başka bir klip başlatılamasın; Play Mode dışında menüler sönük
    [MenuItem(Root + "1 Hareket (move)", true)] [MenuItem(Root + "2 Seviye Atlama (levelup)", true)]
    [MenuItem(Root + "3 Silahlar (weapons)", true)] [MenuItem(Root + "4 Yetenekler (skills)", true)]
    [MenuItem(Root + "5 Dalgalar (waves)", true)] [MenuItem(Root + "6 Dalga Gorevleri (quests)", true)]
    [MenuItem(Root + "7 Zemin Izgarasi (grid)", true)] [MenuItem(Root + "8 Reaktor (reactor)", true)]
    [MenuItem(Root + "9 Core (core)", true)]
    static bool CanRecord() => Application.isPlaying && controller == null;

    [MenuItem(Root + "Kaydı Şimdi Durdur", true)]
    static bool CanStop() => controller != null;

    static void Record(string id)
    {
        Directory.CreateDirectory(Folder);

        var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = "Tutorial " + id;
        movie.Enabled = true;
        movie.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.WEBM,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
        };
        movie.AudioInputSettings.PreserveAudio = false;
        movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = Width, OutputHeight = Height };
        movie.OutputFile = Path.Combine(Application.dataPath, "Resources/Tutorial", id);   // uzantıyı Recorder ekler

        settings.AddRecorderSettings(movie);
        settings.SetRecordModeToManual();
        settings.FrameRate = 30f;
        settings.CapFrameRate = true;

        controller = new RecorderController(settings);
        controller.PrepareRecording();
        if (!controller.StartRecording())
        {
            Debug.LogError("[Tutorial] Kayıt başlatılamadı.");
            controller = null;
            return;
        }

        recordingId = id;
        stopAt = EditorApplication.timeSinceStartup + Seconds;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Debug.Log($"[Tutorial] '{id}' kaydediliyor ({Seconds:0} sn)... Göstermek istediğin şeyi şimdi oyna.");
    }

    static void Tick()
    {
        if (controller != null && EditorApplication.timeSinceStartup >= stopAt) Stop();
    }

    static void OnPlayModeChanged(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.ExitingPlayMode) Stop();
    }

    static void Stop()
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        if (controller == null) return;

        if (controller.IsRecording()) controller.StopRecording();
        controller = null;
        AssetDatabase.Refresh();
        Debug.Log($"[Tutorial] '{recordingId}' kaydedildi: {Folder}/{recordingId}.webm");
    }
}
