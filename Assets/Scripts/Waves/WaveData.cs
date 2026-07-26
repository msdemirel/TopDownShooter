using UnityEngine;

// Bir dalgayı tanımlar. Her dalga birden fazla "spawn girişi" içerebilir
// (ör. type1 lv2 x5 + type2 lv1 x3). Girişler sırayla ya da karışık işlenir.
[CreateAssetMenu(menuName = "TopDownShooter/Wave", fileName = "NewWave")]
public class WaveData : ScriptableObject
{
    [System.Serializable]
    public class SpawnEntry
    {
        public EnemyTypeData type;
        public int level = 1;
        public int count = 5;
        public float spawnInterval = 0.5f;  // bu girişteki düşmanlar arası süre
        public float startDelay = 0f;        // bu giriş başlamadan önce beklenecek süre
    }

    // Not: Dalganın süresi ayrı bir alan DEĞİL — aşağıdaki girişlerin startDelay/spawnInterval
    // değerlerinden hesaplanır. Dalga, tüm düşmanlar doğunca biter. Ekrandaki geri sayım da
    // bu hesaplanan süreyi kullanır (WaveManager.WaveTimeLeft).

    [Tooltip("Açıksa: tüm girişlerdeki düşmanlar tek torbada karıştırılıp rastgele sırayla doğar " +
             "(önce hepsi element 0, sonra element 1 olmaz). Kapalıysa girişler sırayla işlenir.")]
    public bool randomOrder = false;

    public SpawnEntry[] entries = new SpawnEntry[1];
}
