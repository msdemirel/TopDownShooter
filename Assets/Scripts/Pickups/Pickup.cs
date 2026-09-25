using UnityEngine;

// Not: Sayılar bilerek sabitlendi. Enum değeri prefab'larda sayı olarak saklanıyor;
// araya değer eklenip kaydırılırsa mevcut pickup prefab'ları sessizce yanlış türe döner.
// 1 = eski Ammo (ammo sistemi kaldırıldı, sayı yeniden kullanılmıyor).
public enum PickupType
{
    Exp = 0,
    Health = 2,
    Money = 3,
}

// Düşman ölünce düşen toplanabilir obje. Player üstüne gelince değerini ona aktarır.
[RequireComponent(typeof(Collider2D))]
public class Pickup : MonoBehaviour
{
    [Header("Pickup")]
    [SerializeField] PickupType type = PickupType.Exp;
    [SerializeField] float amount = 1f;

    public PickupType Type => type;

    [Header("Player tag")]
    [SerializeField] string playerTag = "Player";

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (other.TryGetComponent<PlayerCollector>(out var collector))
        {
            collector.Collect(type, amount);
            Destroy(gameObject);
        }
    }
}
