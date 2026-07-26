using System.Collections.Generic;
using UnityEngine;

// Sahnedeki canlı düşmanların merkezi listesi.
// Silahlar "menzilimdeki en yakın düşman" sorusunu buna sorar.
// Physics sorgusu yerine liste taraması: çöp (allocation) üretmez, hızlıdır.
public static class EnemyRegistry
{
    static readonly List<EnemyBase> alive = new List<EnemyBase>();

    public static IReadOnlyList<EnemyBase> Alive => alive;
    public static int Count => alive.Count;

    // Play Mode'a her girişte listeyi sıfırla.
    // (Domain Reload kapalıyken static liste önceki oturumdan kalır -> hayalet hedefler.)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => alive.Clear();

    public static void Register(EnemyBase enemy)
    {
        if (enemy != null && !alive.Contains(enemy))
            alive.Add(enemy);
    }

    public static void Unregister(EnemyBase enemy) => alive.Remove(enemy);

    // Menzil içindeki CANLI düşmanlardan RASTGELE biri. Yoksa null.
    // Tek geçişte seçim (reservoir sampling): n. uygun aday 1/n olasılıkla öncekinin
    // yerine geçer; sonuçta herkes eşit şansla seçilmiş olur. Geçici liste gerekmez.
    public static EnemyBase FindRandomInRange(Vector2 from, float maxRange)
    {
        EnemyBase picked = null;
        int found = 0;
        float maxSqr = maxRange * maxRange;

        for (int i = 0; i < alive.Count; i++)
        {
            EnemyBase e = alive[i];
            if (e == null || e.IsDead) continue;
            if (((Vector2)e.transform.position - from).sqrMagnitude > maxSqr) continue;

            found++;
            if (Random.Range(0, found) == 0) picked = e;
        }
        return picked;
    }

    // Verilen noktaya en yakın, menzil içindeki CANLI düşman. Yoksa null.
    public static EnemyBase FindNearest(Vector2 from, float maxRange)
    {
        EnemyBase best = null;
        float bestSqr = maxRange * maxRange;   // karekök almamak için karesiyle karşılaştır

        for (int i = 0; i < alive.Count; i++)
        {
            EnemyBase e = alive[i];
            if (e == null || e.IsDead) continue;   // ölü/yok edilmiş olanı hedefleme

            float sqr = ((Vector2)e.transform.position - from).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = e;
            }
        }
        return best;
    }
}
