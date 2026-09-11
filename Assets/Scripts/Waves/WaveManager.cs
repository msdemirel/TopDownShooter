using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Dalgaları sırayla yönetir.
//
// Model: Her dalga, listesindeki TÜM düşmanlar doğana kadar sürer — süre bir kesme değildir.
// Dalganın spawn planı bitmeye yaklaşınca ekranda geri sayım gösterilir (5,4,3,2,1) ki
// oyuncu yeni dalganın geleceğini anlasın. Plan bitince bir sonraki dalga başlar.
// Sağ kalan düşmanlar silinmez; sahada birikirler.
//
// Not: Oyun donduğunda (upgrade paneli, Time.timeScale = 0) hem spawn hem geri sayım durur;
// ikisi de Time.deltaTime ile ilerliyor.
public class WaveManager : MonoBehaviour
{
    // Spawn planındaki tek adım: "şu kadar bekle, sonra bu düşmanı doğur".
    struct SpawnStep
    {
        public EnemyTypeData type;
        public int level;
        public float delayBefore;
    }

    // Sonsuz modda üretilebilecek bir düşman türü.
    [System.Serializable]
    public class EndlessEnemyEntry
    {
        public EnemyTypeData type;

        [Tooltip("Bu tür en erken bu dalga numarasında gelmeye başlar (1 = baştan itibaren).")]
        public int minWave = 1;

        [Tooltip("Level 1 halinin zorluk maliyeti. Pahalı düşman daha AZ sayıda gelir. " +
                 "Maliyet düşmanın leveliyle çarpılır (Level 3 = 3 kat maliyet).")]
        public float cost = 1f;
    }

    [Header("Waves")]
    [SerializeField] WaveData[] waves;
    [Tooltip("Sonsuz havuz BOŞSA ve liste bitmişse: son dalga sonsuza kadar tekrar eder. " +
             "Sonsuz havuz doluysa bu ayarın etkisi yoktur.")]
    [SerializeField] bool loopLastWave = true;

    [Header("Sonsuz mod (elle yazılmış dalgalar bitince)")]
    [Tooltip("Doluysa: waves listesi bitince dalgalar buradan otomatik üretilir ve her dalga " +
             "öncekinden zorlaşır. Boşsa eski davranış geçerlidir (loopLastWave).")]
    [SerializeField] EndlessEnemyEntry[] endlessEnemies;

    [Tooltip("Üretilen İLK dalganın zorluk bütçesi. Kabaca: maliyeti 1 olan level 1 düşmandan kaç tane?")]
    [SerializeField] float baseBudget = 10f;

    [Tooltip("Her yeni dalgada bütçeye eklenen miktar — zorlaşma hızı bu.")]
    [SerializeField] float budgetPerWave = 3f;

    [Tooltip("Kaç üretilen dalgada bir düşman leveli +1 artsın (EnemyTypeData.levels dizisinden). " +
             "Bir türün max leveline gelinince artık sadece sayı artar.")]
    [SerializeField] int wavesPerEnemyLevel = 4;

    [Tooltip("Üretilen dalganın spawn planı kaç saniyeye yayılsın.")]
    [SerializeField] float targetWaveDuration = 25f;

    [Tooltip("Güvenlik sınırı: tek dalgada en fazla kaç düşman üretilsin (performans).")]
    [SerializeField] int maxEnemiesPerWave = 60;

    [Header("Akış")]
    [Tooltip("Oyun başladıktan sonra ilk dalgaya kadar beklenen süre.")]
    [SerializeField] float startDelay = 3f;
    [Tooltip("Dalganın spawn planının son kaç saniyesinde geri sayım görünsün (5 -> 5,4,3,2,1).")]
    [SerializeField] float countdownSeconds = 5f;

    [Header("Spawn alanı (harita sınırları)")]
    [Tooltip("Haritayı kaplayan bir veya birden fazla Collider2D (Tilemap Collider 2D ya da BoxCollider2D). " +
             "En az biri atanırsa düşmanlar bu alanların içinde rastgele doğar ve aşağıdaki iki yöntem " +
             "kullanılmaz. Birden fazla alanda büyük olan, alanıyla orantılı olarak daha sık seçilir.")]
    [SerializeField] Collider2D[] spawnAreas;

    // Eski sürümdeki tek alanlı kurulum. Sahnede/prefabta atanmış referans kaybolmasın diye duruyor:
    // OnValidate onu spawnAreas'a taşır, taşınmamış olsa bile çalışma anında yine kullanılır.
    [SerializeField, HideInInspector] Collider2D spawnArea;
    [Tooltip("Oyuncuya bu mesafeden yakın doğmasın. 0 = kapalı.")]
    [SerializeField] float minDistanceFromPlayer = 4f;

    [Header("Spawn konumu (spawn alanı yoksa)")]
    [Tooltip("Doluysa bunlardan rastgele seçilir. Boşsa merkez etrafında halka üzerinde spawn olur.")]
    [SerializeField] Transform[] spawnPoints;
    [SerializeField] Transform spawnCenter;  // boşsa bu objenin pozisyonu
    [SerializeField] float spawnRadius = 8f;
    [Tooltip("Oyuncudan uzak bir nokta bulmak için en fazla deneme sayısı.")]
    [SerializeField] int maxSpawnTries = 20;

    Transform player;

    // ---- HUD bunları her karede okur ----
    public int CurrentWave { get; private set; }        // 1-tabanlı; 0 = henüz başlamadı
    public bool WaveActive { get; private set; }        // dalganın spawn'ı sürüyor mu
    public float WaveTimeLeft { get; private set; }     // spawn planının kalan süresi
    public float WaveDuration { get; private set; }     // planın toplam süresi
    public float CountdownSeconds => countdownSeconds;

    // Her dalgada yeniden kullanılan tamponlar (her seferinde yeni liste ayırmamak için)
    readonly List<SpawnStep> plan = new List<SpawnStep>();
    readonly List<WaveData.SpawnEntry> bag = new List<WaveData.SpawnEntry>();
    readonly List<EndlessEnemyEntry> endlessCandidates = new List<EndlessEnemyEntry>();

    // Geçerli spawn alanları ve seçim ağırlıkları (Start'ta bir kez toplanır).
    readonly List<Collider2D> areas = new List<Collider2D>();
    readonly List<float> areaWeights = new List<float>();
    float totalAreaWeight;

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        CollectSpawnAreas();

        StartCoroutine(RunWaves());
    }

    IEnumerator RunWaves()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        int authored = waves != null ? waves.Length : 0;

        if (authored == 0 && !HasEndlessPool)
        {
            Debug.LogWarning("[WaveManager] Hem waves listesi hem sonsuz havuz boş.", this);
            yield break;
        }

        int index = 0;
        while (true)
        {
            // Sıra: önce elle yazılmış dalgalar, sonra sonsuz üretim.
            // Sonsuz havuz boşsa eski davranış: son dalga tekrarlanır (loopLastWave).
            if (index < authored)
            {
                CurrentWave = index + 1;
                BuildPlan(waves[index]);
            }
            else if (HasEndlessPool)
            {
                CurrentWave = index + 1;
                BuildEndlessPlan(index - authored);
            }
            else if (loopLastWave)
            {
                CurrentWave = index + 1;
                BuildPlan(waves[authored - 1]);
            }
            else break;   // liste bitti, tekrar da istenmiyor

            yield return RunPlan();
            index++;
        }
    }

    // Hazırlanmış planı baştan sona yürütür; dalga TÜM düşmanlar doğunca biter.
    IEnumerator RunPlan()
    {
        // Güvenlik: plan boşsa (hatalı veri) sıfır süreli dalgalar art arda
        // gelip oyunu kilitlemesin diye kısa bir bekleme koyup dalgayı atla.
        if (plan.Count == 0)
        {
            Debug.LogWarning("[WaveManager] Dalga planı boş — dalga atlanıyor.", this);
            yield return new WaitForSeconds(1f);
            yield break;
        }

        WaveDuration = TotalPlanTime();
        WaveTimeLeft = WaveDuration;

        WaveActive = true;
        AudioManager.PlayWave();

        // Beklemeyi ve geri sayımı AYNI döngüde yürütüyoruz: böylece ekrandaki
        // sayaç ile gerçek spawn zamanı birbirinden kayamaz.
        for (int i = 0; i < plan.Count; i++)
        {
            float wait = plan[i].delayBefore;
            while (wait > 0f)
            {
                float dt = Time.deltaTime;
                wait -= dt;
                WaveTimeLeft = Mathf.Max(0f, WaveTimeLeft - dt);
                yield return null;
            }

            SpawnEnemy(plan[i].type, plan[i].level);
        }

        WaveTimeLeft = 0f;
        WaveActive = false;
    }

    // ---- Spawn planı ----
    // Dalganın tüm düşmanlarını, aralarındaki beklemelerle birlikte tek bir listeye açar.
    // Planı önden kurmak, dalganın toplam süresini kesin olarak bilmemizi sağlar (geri sayım için).
    void BuildPlan(WaveData wave)
    {
        plan.Clear();
        if (wave.entries == null) return;

        if (wave.randomOrder) BuildRandomPlan(wave);
        else BuildOrderedPlan(wave);
    }

    // Girişler sırayla: önce element 0'ın tamamı, sonra element 1...
    void BuildOrderedPlan(WaveData wave)
    {
        foreach (var entry in wave.entries)
        {
            if (entry == null || entry.type == null || entry.count <= 0) continue;

            for (int n = 0; n < entry.count; n++)
            {
                plan.Add(new SpawnStep
                {
                    type = entry.type,
                    level = entry.level,
                    // İlk düşmandan önce girişin startDelay'i; sonrakiler arasında spawnInterval
                    delayBefore = n == 0
                        ? Mathf.Max(0f, entry.startDelay)
                        : Mathf.Max(0f, entry.spawnInterval)
                });
            }
        }
    }

    // Tüm girişlerdeki düşmanlar tek torbada karıştırılır: tipler karışık gelir.
    // Not: karışık modda girişlerin kendi 'startDelay' değeri kullanılmaz.
    void BuildRandomPlan(WaveData wave)
    {
        bag.Clear();
        foreach (var entry in wave.entries)
        {
            if (entry == null || entry.type == null || entry.count <= 0) continue;
            for (int n = 0; n < entry.count; n++)
                bag.Add(entry);   // her düşman için bir kayıt
        }

        Shuffle(bag);

        for (int i = 0; i < bag.Count; i++)
        {
            plan.Add(new SpawnStep
            {
                type = bag[i].type,
                level = bag[i].level,
                // İlk düşman hemen doğar; sonrakiler bir öncekinin aralığı kadar sonra
                delayBefore = i == 0 ? 0f : Mathf.Max(0f, bag[i - 1].spawnInterval)
            });
        }
    }

    // ---- Sonsuz mod: dalga üretimi ----
    // Mantık: her üretilen dalganın bir zorluk BÜTÇESİ vardır ve bütçe her dalgada artar.
    // Havuzdan rastgele düşman seçilir; her düşman, maliyeti kadar bütçeden düşer.
    // Düşman leveli de dalgalar ilerledikçe artar (EnemyTypeData.levels'tan). Bir tür
    // max leveline ulaştıysa artık güçlenemez; bütçenin fazlası "daha kalabalık" olur.
    //
    // endlessIndex: 0-tabanlı, sadece ÜRETİLEN dalgaları sayar (elle yazılanlar hariç).
    void BuildEndlessPlan(int endlessIndex)
    {
        plan.Clear();

        float budget = baseBudget + budgetPerWave * endlessIndex;
        int targetLevel = 1 + endlessIndex / Mathf.Max(1, wavesPerEnemyLevel);

        // Bu dalgada gelebilecek türleri topla (minWave kilidi açılmış olanlar)
        endlessCandidates.Clear();
        foreach (var e in endlessEnemies)
        {
            if (e == null || e.type == null) continue;
            if (CurrentWave >= e.minWave) endlessCandidates.Add(e);
        }

        // Hiçbirinin kilidi açılmamışsa (minWave'ler çok yüksek girilmiş) hepsini kullan:
        // boş dalga üretmekten iyidir.
        if (endlessCandidates.Count == 0)
        {
            foreach (var e in endlessEnemies)
                if (e != null && e.type != null) endlessCandidates.Add(e);
        }
        if (endlessCandidates.Count == 0) return;   // havuz tamamen boş (RunPlan atlar)

        // Bütçe yettiği sürece rastgele düşman ekle
        while (plan.Count < maxEnemiesPerWave)
        {
            var e = endlessCandidates[UnityEngine.Random.Range(0, endlessCandidates.Count)];
            int lv = LevelFor(e, targetLevel);
            float cost = CostOf(e, lv);

            if (cost > budget)
            {
                // Bu tür pahalı geldi; daha ucuz bir tür hâlâ sığıyorsa denemeye devam
                if (!AnyAffordable(budget, targetLevel)) break;
                continue;
            }

            budget -= cost;
            plan.Add(new SpawnStep { type = e.type, level = lv });
        }

        // Bütçe ilk düşmana bile yetmediyse en ucuz türden bir tane koy (boş dalga olmasın)
        if (plan.Count == 0) AddCheapest(targetLevel);

        // Düşmanları dalganın hedef süresine eşit aralıklarla yay
        float interval = Mathf.Max(0.05f, targetWaveDuration) / plan.Count;
        for (int i = 0; i < plan.Count; i++)
        {
            SpawnStep s = plan[i];
            s.delayBefore = interval;
            plan[i] = s;
        }
    }

    // Hedef leveli, türün sahip olduğu level sayısına klample.
    int LevelFor(EndlessEnemyEntry e, int targetLevel)
    {
        int maxLv = (e.type.levels != null && e.type.levels.Length > 0) ? e.type.levels.Length : 1;
        return Mathf.Clamp(targetLevel, 1, maxLv);
    }

    // Maliyet levelle çarpılır: güçlenen düşman bütçeden daha çok yer.
    float CostOf(EndlessEnemyEntry e, int level)
    {
        return Mathf.Max(0.1f, e.cost) * level;
    }

    bool AnyAffordable(float budget, int targetLevel)
    {
        foreach (var e in endlessCandidates)
            if (CostOf(e, LevelFor(e, targetLevel)) <= budget) return true;
        return false;
    }

    void AddCheapest(int targetLevel)
    {
        EndlessEnemyEntry best = null;
        int bestLv = 1;
        float bestCost = float.MaxValue;

        foreach (var e in endlessCandidates)
        {
            int lv = LevelFor(e, targetLevel);
            float cost = CostOf(e, lv);
            if (cost < bestCost)
            {
                best = e;
                bestLv = lv;
                bestCost = cost;
            }
        }

        if (best != null)
            plan.Add(new SpawnStep { type = best.type, level = bestLv });
    }

    // Havuzda geçerli (type atanmış) en az bir giriş var mı?
    bool HasEndlessPool
    {
        get
        {
            if (endlessEnemies == null) return false;
            foreach (var e in endlessEnemies)
                if (e != null && e.type != null) return true;
            return false;
        }
    }

    float TotalPlanTime()
    {
        float total = 0f;
        for (int i = 0; i < plan.Count; i++) total += plan[i].delayBefore;
        return total;
    }

    // Fisher-Yates karıştırma
    void Shuffle(List<WaveData.SpawnEntry> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    // ---- Düşman doğurma ----
    void SpawnEnemy(EnemyTypeData type, int level)
    {
        if (type.prefab == null)
        {
            Debug.LogWarning($"[WaveManager] '{type.typeName}' prefab atanmamış.", this);
            return;
        }

        Vector3 pos = GetSpawnPosition();
        GameObject go = Instantiate(type.prefab, pos, Quaternion.identity);

        if (go.TryGetComponent<EnemyBase>(out var enemy))
            enemy.Init(type.GetLevel(level));
        else
            Debug.LogWarning($"[WaveManager] '{type.typeName}' prefab'ında EnemyBase yok.", go);
    }

    Vector3 GetSpawnPosition()
    {
        // Oyuncudan yeterince uzak bir konum bulana kadar dene
        Vector3 pos = RandomCandidate();
        for (int i = 0; i < maxSpawnTries && !IsValid(pos); i++)
            pos = RandomCandidate();

        return pos;  // uygun bulunamazsa yine de son adayı döndür
    }

    Vector3 RandomCandidate()
    {
        // 1) Harita alanı/alanları atanmışsa onların içinde rastgele bir nokta
        if (areas.Count > 0)
            return RandomPointInAreas();

        // 2) Spawn noktaları
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            var t = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            if (t != null) return t.position;
        }

        // 3) Merkez etrafında halka
        Vector3 center = spawnCenter != null ? spawnCenter.position : transform.position;
        Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
        if (dir == Vector2.zero) dir = Vector2.up;
        return center + (Vector3)(dir * spawnRadius);
    }

    // Alanlardan biri seçilir, o alanın sınırları içinde rastgele bir nokta döner.
    Vector3 RandomPointInAreas()
    {
        Collider2D area = PickArea();
        Bounds b = area.bounds;

        // Kutu olmayan şekillerde (tilemap, polygon vb.) nokta gerçekten şeklin içinde mi diye bak.
        for (int i = 0; i < 30; i++)
        {
            Vector2 p = new Vector2(
                UnityEngine.Random.Range(b.min.x, b.max.x),
                UnityEngine.Random.Range(b.min.y, b.max.y));

            if (area.OverlapPoint(p)) return p;
        }
        return b.center;  // bulunamazsa merkez
    }

    // Alan büyüklüğüyle orantılı seçim: küçük bir tilemap, büyüğüyle aynı sayıda düşman almasın.
    Collider2D PickArea()
    {
        if (areas.Count == 1 || totalAreaWeight <= 0f) return areas[0];

        float r = UnityEngine.Random.Range(0f, totalAreaWeight);
        for (int i = 0; i < areas.Count; i++)
        {
            r -= areaWeights[i];
            if (r <= 0f) return areas[i];
        }
        return areas[areas.Count - 1];   // kayan nokta hatasına karşı
    }

    // Atanmış alanları bir kez toplar ve ağırlıklarını (bounds alanı) hesaplar.
    void CollectSpawnAreas()
    {
        areas.Clear();
        areaWeights.Clear();
        totalAreaWeight = 0f;

        AddSpawnArea(spawnArea);   // eski tek alanlı kurulum
        if (spawnAreas != null)
            foreach (var a in spawnAreas) AddSpawnArea(a);
    }

    void AddSpawnArea(Collider2D area)
    {
        if (area == null || areas.Contains(area)) return;

        Vector3 size = area.bounds.size;
        float weight = Mathf.Max(0.0001f, size.x * size.y);

        areas.Add(area);
        areaWeights.Add(weight);
        totalAreaWeight += weight;
    }

#if UNITY_EDITOR
    // Eskiden tek bir 'spawnArea' vardı; sahnedeki atama kaybolmasın diye diziye taşınır.
    void OnValidate()
    {
        if (spawnArea == null) return;

        if (spawnAreas == null) spawnAreas = new Collider2D[0];
        foreach (var a in spawnAreas)
        {
            if (a != spawnArea) continue;
            spawnArea = null;   // zaten taşınmış
            return;
        }

        var migrated = new List<Collider2D>(spawnAreas) { spawnArea };
        spawnAreas = migrated.ToArray();
        spawnArea = null;
    }
#endif

    // Konum uygun mu: oyuncuya çok yakın değil. (Haritada duvar yok, engel kontrolü gerekmiyor.)
    bool IsValid(Vector3 pos)
    {
        if (minDistanceFromPlayer <= 0f || player == null) return true;
        return Vector2.Distance(pos, player.position) >= minDistanceFromPlayer;
    }
}
