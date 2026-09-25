using System.Collections.Generic;
using UnityEngine;

// Oyundaki her ses olayı. Dosya adları bunun snake_case hali (EnemyDeath -> enemy_death.wav);
// kurulum menüsü klipleri bu kuralla bağlar. Yeniler SONA eklenir (asset'ler sayı olarak saklar).
public enum SfxId
{
    Shoot, MeleeSwing, Hit, Crit, EnemyDeath, Explosion, Hurt, PlayerDeath,
    PickupCoin, PickupExp, PickupHealth, LevelUp,
    Dash, Blast, Shield, ShieldBlock, Pulse, Burst, Heal, Frost, Overdrive, Lightning,
    Merge, Synergy, Purchase, Reroll, Unlock, Revive, WaveStart, CountdownTick, BossWarning,
    GameOver, NewRecord,
    UiHover, UiClick, UiBack, UiError, UiStart,
}

public enum MusicId { None, Menu, Game, Boss }

// Tüm klipler + ses başına ayarlar. Resources/AudioLibrary.asset olarak durur; AudioManager
// kendini kurarken buradan okur (sahneye bağlamak gerekmez).
// Kurulum: Menü > TopDownShooter > Ses > Ses Kütüphanesini Kur
[CreateAssetMenu(menuName = "TopDownShooter/Audio Library", fileName = "AudioLibrary")]
public class AudioLibrary : ScriptableObject
{
    [System.Serializable]
    public class Sfx
    {
        public SfxId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.7f;
        [Tooltip("Her çalışta perde bu kadar rastgele değişir (0.06 = ±%6): tekrar eden sesler yormasın.")]
        [Range(0f, 0.3f)] public float pitchVariance = 0.05f;
        [Tooltip("Aynı ses bu süreden sık çalmaz (saniye): üst üste yığılma olmasın.")]
        public float minInterval = 0.04f;
    }

    public List<Sfx> sfx = new List<Sfx>();

    [Header("Müzik")]
    public AudioClip menuMusic;
    public AudioClip gameMusic;
    public AudioClip bossMusic;
    [Range(0f, 1f)] public float musicVolume = 0.45f;
    [Tooltip("Parçalar arası geçiş süresi (saniye).")]
    public float crossfade = 1.2f;

    Dictionary<SfxId, Sfx> map;

    public Sfx Get(SfxId id)
    {
        if (map == null || map.Count != sfx.Count)
        {
            map = new Dictionary<SfxId, Sfx>();
            foreach (var s in sfx) if (s != null) map[s.id] = s;
        }
        return map.TryGetValue(id, out var e) ? e : null;
    }

    public AudioClip GetMusic(MusicId id)
        => id == MusicId.Menu ? menuMusic : id == MusicId.Game ? gameMusic : id == MusicId.Boss ? bossMusic : null;
}
