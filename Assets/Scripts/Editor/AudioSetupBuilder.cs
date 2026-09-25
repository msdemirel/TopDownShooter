using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Ses kütüphanesini kurar: Resources/AudioLibrary.asset (AudioManager kendini buradan kurar).
// Klipler Assets/Audio/Generated_v2'den ADLA bağlanır (SfxId.EnemyDeath -> SFX/enemy_death.wav).
//
// Kullanım: Menü > TopDownShooter > Ses > Ses Kütüphanesini Kur
// Tekrar çalıştırmak güvenli: klipler yenilenir, ses seviyesi/perde/aralık ayarların SADECE
// ilk kurulumda yazılır (Inspector'da yaptığın ince ayarlar korunur).
// Sesleri yeniden üretmek için: python3 Tools/AudioGen/gen_audio.py
public static class AudioSetupBuilder
{
    const string LibraryPath = "Assets/Resources/AudioLibrary.asset";
    const string Dir = "Assets/Audio/Generated_v2/";

    // id -> (ses, perde değişimi, min aralık). Sık çalanlar kısık ve değişken, olaylar belirgin.
    static (float vol, float pitch, float interval) Defaults(SfxId id) => id switch
    {
        SfxId.Shoot => (0.35f, 0.08f, 0.05f),
        SfxId.MeleeSwing => (0.4f, 0.1f, 0.06f),
        SfxId.Hit => (0.35f, 0.1f, 0.03f),
        SfxId.Crit => (0.5f, 0.05f, 0.04f),
        SfxId.EnemyDeath => (0.45f, 0.1f, 0.03f),
        SfxId.Explosion => (0.7f, 0.06f, 0.08f),
        SfxId.Hurt => (0.7f, 0.05f, 0.1f),
        SfxId.PlayerDeath => (0.9f, 0f, 0.5f),
        SfxId.PickupCoin => (0.4f, 0.04f, 0.03f),
        SfxId.PickupExp => (0.35f, 0f, 0.03f),       // perdeyi exp kombosu yönetir
        SfxId.PickupHealth => (0.6f, 0.03f, 0.05f),
        SfxId.LevelUp => (0.7f, 0f, 0.1f),
        SfxId.Blast => (0.85f, 0.03f, 0.05f),
        SfxId.Frost or SfxId.Lightning => (0.6f, 0.03f, 0.05f),
        SfxId.Dash or SfxId.Shield or SfxId.Pulse or SfxId.Burst or SfxId.Heal or SfxId.Overdrive => (0.7f, 0.03f, 0.05f),
        SfxId.ShieldBlock => (0.5f, 0.08f, 0.08f),
        SfxId.Purchase or SfxId.Reroll or SfxId.WaveStart => (0.6f, 0f, 0.05f),
        SfxId.CountdownTick => (0.45f, 0f, 0.2f),
        SfxId.Revive or SfxId.BossWarning => (0.9f, 0f, 0.5f),
        SfxId.UiHover => (0.25f, 0.03f, 0.03f),
        SfxId.UiClick or SfxId.UiBack or SfxId.UiError => (0.45f, 0.03f, 0.05f),
        _ => (0.8f, 0f, 0.1f),   // Merge, Synergy, Unlock, GameOver, NewRecord, UiStart
    };

    [MenuItem("TopDownShooter/Ses/Ses Kütüphanesini Kur")]
    static void Setup()
    {
        Directory.CreateDirectory("Assets/Resources");
        var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<AudioLibrary>();
            AssetDatabase.CreateAsset(lib, LibraryPath);
        }

        int missing = 0;
        foreach (SfxId id in System.Enum.GetValues(typeof(SfxId)))
        {
            string file = $"{Dir}SFX/{Snake(id.ToString())}.wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(file);
            if (clip == null) { missing++; Debug.LogWarning($"[AudioSetup] {file} yok."); }

            var entry = lib.sfx.Find(e => e.id == id);
            if (entry == null)
            {
                var (vol, pitch, interval) = Defaults(id);
                entry = new AudioLibrary.Sfx { id = id, volume = vol, pitchVariance = pitch, minInterval = interval };
                lib.sfx.Add(entry);
            }
            entry.clip = clip;
        }
        lib.sfx.Sort((a, b) => a.id.CompareTo(b.id));

        lib.menuMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(Dir + "Music/menu.wav");
        lib.gameMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(Dir + "Music/game.wav");
        lib.bossMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(Dir + "Music/boss.wav");

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Selection.activeObject = lib;
        Debug.Log(missing == 0
            ? $"[AudioSetup] Hazır: {lib.sfx.Count} ses + 3 müzik. Play'e bas: ses her sahnede kendini kurar."
            : $"[AudioSetup] {missing} ses dosyası eksik: python3 Tools/AudioGen/gen_audio.py çalıştırıp Ctrl+R yap.");
    }

    // "EnemyDeath" -> "enemy_death"
    static string Snake(string s) => Regex.Replace(s, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
}
