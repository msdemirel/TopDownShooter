// Hasar alabilen her şeyin uyguladığı arayüz.
public interface IDamageable
{
    Team Team { get; }
    void TakeDamage(float amount);
}
