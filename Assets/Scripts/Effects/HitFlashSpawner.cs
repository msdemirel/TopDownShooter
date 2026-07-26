using UnityEngine;

// Health.AnyDamaged'i dinler: hasar alan objenin sprite'ını kısa süre renklendirir.
// Sahneye BİR tane koymak yeterli — HitFlash component'ini hedefe kendisi ekler.
//
// Not: Sprite rengi ÇARPILARAK uygulanır (tint). Bu yüzden "beyaz flaş" mümkün değil
// (beyaz = değişiklik yok); kırmızımsı tint hem koyu hem açık sprite'larda iyi okunur.
public class HitFlashSpawner : MonoBehaviour
{
    [Header("Kimler için")]
    [SerializeField] bool flashEnemies = true;
    [SerializeField] bool flashPlayer = true;

    [Header("Görünüm")]
    [SerializeField] Color enemyFlashColor = new Color(1f, 0.35f, 0.35f);
    [SerializeField] Color playerFlashColor = new Color(1f, 0.25f, 0.25f);
    [Tooltip("Flaşın süresi (saniye). Kısa tut — 0.05-0.12 arası iyi hissettirir.")]
    [SerializeField] float duration = 0.08f;

    void OnEnable() { Health.AnyDamaged += HandleDamaged; }
    void OnDisable() { Health.AnyDamaged -= HandleDamaged; }

    void HandleDamaged(Health target, float amount, bool isCrit)
    {
        if (target == null) return;

        bool isPlayer = target.Team == Team.Player;
        if (isPlayer && !flashPlayer) return;
        if (!isPlayer && !flashEnemies) return;

        // İlk vuruşta component'i tak, sonrakilerde aynısını kullan
        if (!target.TryGetComponent<HitFlash>(out var flash))
            flash = target.gameObject.AddComponent<HitFlash>();

        flash.Flash(isPlayer ? playerFlashColor : enemyFlashColor, duration);
    }
}
