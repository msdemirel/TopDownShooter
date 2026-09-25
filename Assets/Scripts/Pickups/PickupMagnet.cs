using UnityEngine;

// Pickup'ı, oyuncu yeterince yaklaşınca ona doğru çeker (mıknatıs).
// Çekim menzili OYUNCUDA durur (PlayerCollector.MagnetRange) — böylece
// upgrade'le artırılabilir; bu component sadece hareketi yapar.
// Çekilsin istediğin pickup prefab'ına ekle (exp, para...), istemediğine ekleme.
public class PickupMagnet : MonoBehaviour
{
    [Header("Çekim hareketi")]
    [Tooltip("Çekim başladığı andaki hız.")]
    [SerializeField] float startSpeed = 6f;
    [Tooltip("Çekim sürdükçe hız bu kadar artar (birim/sn²) — sona doğru hızlanarak gelir.")]
    [SerializeField] float acceleration = 25f;

    [Tooltip("Oyuncuda PlayerCollector yoksa kullanılacak yedek menzil.")]
    [SerializeField] float fallbackRange = 3f;

    Transform player;
    PlayerCollector collector;
    float speed;
    bool attracted;   // bir kez kapıldı mı? (kapılan pickup menzilden çıksa da takibi bırakmaz)

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            collector = p.GetComponent<PlayerCollector>();
        }

        speed = startSpeed;
    }

    // Menzil beklemeden çekimi başlat (FloorGrid mıknatıs bonusu)
    public void Attract() => attracted = true;

    void Update()
    {
        if (player == null) return;

        if (!attracted)
        {
            float range = collector != null ? collector.MagnetRange : fallbackRange;
            if (range <= 0f) return;   // mıknatıs kapalı

            float sqr = ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude;
            if (sqr > range * range) return;

            attracted = true;   // kapıldı: oyuncu uzaklaşsa bile artık peşini bırakmaz
        }

        speed += acceleration * Time.deltaTime;
        transform.position = Vector2.MoveTowards(transform.position, player.position,
                                                 speed * Time.deltaTime);
    }
}
