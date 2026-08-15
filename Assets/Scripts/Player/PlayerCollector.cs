using UnityEngine;

// Pickup'tan gelen değeri doğru sisteme yönlendirir (exp/money -> stats, health -> Health).
public class PlayerCollector : MonoBehaviour
{
    // Herhangi bir pickup toplanınca tetiklenir (ses vb. dinler).
    public static event System.Action<PickupType> AnyCollected;

    // Play Mode'a her girişte statik event'i temizle (domain reload kapalıysa kalır).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => AnyCollected = null;

    [Header("References")]
    [SerializeField] PlayerStats stats;
    [SerializeField] Health health;

    [Header("Mıknatıs")]
    [Tooltip("Bu mesafedeki (PickupMagnet'li) pickup'lar oyuncuya çekilir. 0 = kapalı.")]
    [SerializeField] float magnetRange = 3f;

    // Upgrade'ler menzili buradan artırır. Alt sınır: negatife düşmesin.
    public float MagnetRange
    {
        get => magnetRange;
        set => magnetRange = Mathf.Max(0f, value);
    }

    void Awake()
    {
        // Boş bırakılırsa otomatik bulmaya çalış
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (health == null) health = GetComponent<Health>();
    }

    public void Collect(PickupType type, float amount)
    {
        switch (type)
        {
            case PickupType.Exp:
                if (stats != null) stats.AddExp(Mathf.RoundToInt(amount));
                break;

            case PickupType.Health:
                if (health != null) health.Heal(amount);
                break;

            case PickupType.Money:
                if (stats != null) stats.AddMoney(Mathf.RoundToInt(amount));
                break;
        }

        AnyCollected?.Invoke(type);   // ses vb. için
    }
}
