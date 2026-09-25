using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Reaktör Savunması: haritanın 4 köşesinde birer reaktör durur. Zaman zaman rastgele biri baskına uğrar:
//   1) UYARI    (prepTime sn): reaktör kırmızı yanıp söner, üstünde "!", ekran kenarında ok + mesafe,
//                             sağ üstte geri sayım. Oyuncu reaktöre koşar.
//   2) BASKIN   (assaultDuration sn): reaktörün etrafında düşmanlar doğar ve OYUNCUYA DEĞİL REAKTÖRE saldırır.
//   3a) Reaktör dayandı -> etrafına ödül saçılır (altın, EXP, can).
//   3b) Reaktör yıkıldı -> oyun bitmez ama oyuncu altınının bir kısmını kaybeder; reaktör bir süre çevrimdışı.
// Baskın bitince saldırganlar oyuncuya döner. Normal dalgalar bu sırada da devam eder.
//
// Düşmanlar reaktöre saldırabilsin diye reaktörün bir Health'i (Team.Player) ve trigger collider'ı var:
// mevcut saldırı kodları (yakın dövüş, mermi, patlayan) hedefteki Health'e hasar verir.
//
// Görseller: Resources/Reactor (Tech Dungeon "Props and Items": yeşil enerji tankı animasyonu 0-3,8,
// boş tank 0,10, can ışıkları 8,7 / 7,7; HUD çerçevesi skill_bar_frame). Efektler VfxSprite ile kodda.
// Kendini kurar: WaveManager olan sahnede otomatik oluşur (sahneye eklemek gerekmez).
public class ReactorDefense : MonoBehaviour
{
    [Header("Zamanlama")]
    [Tooltip("İlk baskın bu dalga başladıktan firstDelay sn sonra gelir.")]
    [SerializeField] int startWave = 2;
    [SerializeField] float firstDelay = 20f;
    [Tooltip("Bir baskın bittikten sonra sonrakine kadar geçen süre (min, max sn).")]
    [SerializeField] Vector2 attackInterval = new Vector2(60f, 90f);
    [Tooltip("Uyarı ile baskın arası: oyuncunun reaktöre yetişme süresi.")]
    [SerializeField] float prepTime = 15f;
    [SerializeField] float assaultDuration = 30f;
    [Tooltip("Yıkılan reaktör bu kadar süre baskın hedefi olmaz (onarılıyor).")]
    [SerializeField] float offlineTime = 120f;

    [Header("Baskın")]
    [SerializeField] int baseRaiders = 6;
    [SerializeField] float raidersPerWave = 1f;
    [SerializeField] int maxRaiders = 25;
    [Tooltip("Saldırganlar reaktörden bu uzaklıkta doğar (min, max).")]
    [SerializeField] Vector2 spawnDistance = new Vector2(5f, 7f);
    [SerializeField] float reactorHealth = 300f;
    [Tooltip("Her dalgada reaktör canına eklenen oran (0.15 = %15).")]
    [SerializeField] float healthPerWave = 0.15f;

    [Header("Sonuç")]
    [Tooltip("Reaktör yıkılınca kaybedilen altın oranı (0.5 = yarısı).")]
    [Range(0f, 1f)] [SerializeField] float moneyLossFraction = 0.5f;
    [SerializeField] int rewardCoins = 8;
    [SerializeField] int rewardExp = 10;
    [Tooltip("Ödüle her dalga için eklenen adet.")]
    [SerializeField] float rewardPerWave = 1f;
    [SerializeField] float rewardScatter = 2.5f;

    [Header("Yerleşim")]
    [Tooltip("Reaktörler bu dikdörtgenin 4 köşesinde (dünya). Arena zemini: DecorBuilder'daki hücreler.")]
    [SerializeField] Rect corners = new Rect(-29f, -16f, 51f, 29f);

    [Header("Görünüm")]
    [SerializeField] Color idleColor = new Color(0.62f, 0.94f, 0.43f);     // tank sıvısının yeşili
    [SerializeField] Color threatColor = new Color(0.9f, 0.27f, 0.35f);    // #E54658
    [SerializeField] Color securedColor = new Color(0.45f, 0.94f, 0.97f);  // #73EFF7
    [SerializeField] Color offlineColor = new Color(0.36f, 0.4f, 0.48f);
    [SerializeField] Color warnColor = new Color(1f, 0.8f, 0.46f);         // #FFCD75
    [SerializeField] float tankFps = 6f;
    [Tooltip("Ekran kenarında reaktörü gösteren okun boyu (UI birimi).")]
    [SerializeField] float arrowSize = 72f;

    enum Phase { Idle, Warning, Assault }

    class Reactor
    {
        public Vector3 pos;
        public Transform tankRoot;
        public SpriteRenderer tank, glow, pad, ring, hazard, shadow;
        public SpriteRenderer[] lights;
        public SpriteRenderer barFrame, barBg, barLag, barFill;
        public TextMeshPro alert;      // uyarıdaki "!"
        public MinimapUI.Marker marker;
        public Health hp;              // sadece baskın sırasında var
        public float lagHp = 1f;       // hasar sonrası beyaz "gecikmeli" bar
        public float hitPunch;         // vurulunca kısa sıçrama
        public float offlineUntil;
        public float securedFlash;     // savunulunca kısa süre parlar
        public float fxTimer;          // kıvılcım / duman aralığı
        public bool Offline => Time.time < offlineUntil;
    }

    readonly List<Reactor> reactors = new List<Reactor>();
    readonly List<EnemyBase> raiders = new List<EnemyBase>();
    readonly Dictionary<PickupType, GameObject> pickupPrefabs = new Dictionary<PickupType, GameObject>();

    Phase phase;
    Reactor current;
    float timer;          // fazın kalan süresi (Idle'da: sonraki baskına kalan)
    bool scheduled;       // ilk baskın zamanlandı mı
    int toSpawn;
    float spawnTimer, spawnInterval;

    WaveManager waves;
    Transform player;
    PlayerStats stats;
    Camera cam;
    TMP_FontAsset font;

    RectTransform hud, barFill;
    Image barFillImg, hudIcon;
    TMP_Text titleText, bodyText, arrowText;
    Image arrow;
    RectTransform arrowRoot;

    // Sprite'lar
    Sprite[] tankFrames;
    Sprite tankEmpty, lightOn, lightOff, hudFrame;
    static Sprite arrowSprite, hazardSprite;

    const int GroundOrder = -11;   // FloorGrid panellerinin (-12) üstü, dekorların (-10) altı
    const int TankOrder = -7;      // karakterlerin (0) altı
    const int BarOrder = 40;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded += (s, m) => TryCreate();
        TryCreate();
    }

    static void TryCreate()
    {
        if (FindAnyObjectByType<ReactorDefense>() != null) return;
        if (FindAnyObjectByType<WaveManager>() == null) return;   // sadece oyun sahnesi
        new GameObject("ReactorDefense").AddComponent<ReactorDefense>();
    }

    void Start()
    {
        waves = FindAnyObjectByType<WaveManager>();
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { player = p.transform; stats = p.GetComponent<PlayerStats>(); }
        cam = Camera.main;
        var ui = FindAnyObjectByType<TextMeshProUGUI>();   // HUD'un pixel fontu
        if (ui != null) font = ui.font;

        LoadSprites();
        CollectPickupPrefabs();
        foreach (var c in new[] { new Vector2(corners.xMin, corners.yMin), new Vector2(corners.xMin, corners.yMax),
                                  new Vector2(corners.xMax, corners.yMin), new Vector2(corners.xMax, corners.yMax) })
            reactors.Add(MakeReactor(c));
        BuildHud();
    }

    void OnDestroy()
    {
        foreach (var r in reactors) if (r.marker != null) MinimapUI.RemoveMarker(r.marker);
    }

    void LoadSprites()
    {
        tankFrames = new Sprite[4];
        for (int i = 0; i < 4; i++) tankFrames[i] = Resources.Load<Sprite>($"Reactor/reactor_{i}");
        tankEmpty = Resources.Load<Sprite>("Reactor/reactor_empty");
        lightOn = Resources.Load<Sprite>("Reactor/light_on");
        lightOff = Resources.Load<Sprite>("Reactor/light_off");
        hudFrame = Resources.Load<Sprite>("Reactor/hud_frame");
        if (tankFrames[0] == null) Debug.LogWarning("[ReactorDefense] Resources/Reactor sprite'ları bulunamadı.");
    }

    // Ödül pickup'ları: düşmanların LootDropper'larındaki prefab'lardan türüne göre birer tane
    void CollectPickupPrefabs()
    {
        if (waves == null) return;
        foreach (var type in waves.EnemyTypes)
        {
            if (type.prefab == null || !type.prefab.TryGetComponent<LootDropper>(out var loot)) continue;
            foreach (var prefab in loot.PickupPrefabs)
                if (prefab.TryGetComponent<Pickup>(out var pk) && !pickupPrefabs.ContainsKey(pk.Type))
                    pickupPrefabs[pk.Type] = prefab;
        }
    }

    // ================================================================ akış
    void Update()
    {
        if (player == null || waves == null) return;

        if (!scheduled && waves.CurrentWave >= startWave) { scheduled = true; timer = firstDelay; }
        if (scheduled) timer -= Time.deltaTime;

        switch (phase)
        {
            case Phase.Idle:
                if (scheduled && timer <= 0f) BeginWarning();
                break;
            case Phase.Warning:
                if (timer <= 0f) BeginAssault();
                break;
            case Phase.Assault:
                UpdateAssault();
                break;
        }

        AnimateReactors();
        UpdateHud();
    }

    void BeginWarning()
    {
        var candidates = reactors.FindAll(r => !r.Offline);
        if (candidates.Count == 0) { timer = 10f; return; }   // hepsi onarımda: biraz sonra tekrar dene

        current = candidates[Random.Range(0, candidates.Count)];
        phase = Phase.Warning;
        timer = prepTime;
        AudioManager.Play(SfxId.BossWarning);
        Announce(Loc.T("REACTOR UNDER THREAT"), threatColor);
        TutorialHints.Request("reactor",
            Loc.T("Enemies are raiding a <color=#73EFF7>REACTOR</color>! Defend it for a reward. If it falls, you lose <color=#FFCD75>coins</color>."));
    }

    void BeginAssault()
    {
        phase = Phase.Assault;
        timer = assaultDuration;

        // Reaktörün canı: düşmanların saldırı kodu hedefteki Health'e vurur
        var go = new GameObject("ReactorCore");
        go.transform.SetParent(transform, false);
        go.transform.position = current.pos;
        go.layer = player.gameObject.layer;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.7f;
        var hp = go.AddComponent<Health>();
        hp.SetTeam(Team.Player);
        hp.SetDestroyOnDeath(false);
        hp.SetMaxHealth(reactorHealth * (1f + healthPerWave * Mathf.Max(0, waves.CurrentWave - 1)));
        hp.OnDeath += OnReactorDestroyed;
        hp.OnHealthChanged += OnReactorHit;
        current.hp = hp;
        current.lagHp = 1f;

        toSpawn = Mathf.Min(maxRaiders, baseRaiders + Mathf.RoundToInt(raidersPerWave * waves.CurrentWave));
        // Saldırganlar sürenin ilk %70'inde gelir: sonuncular da reaktöre ulaşacak zaman bulsun
        spawnInterval = assaultDuration * 0.7f / Mathf.Max(1, toSpawn);
        spawnTimer = 0f;

        AudioManager.Play(SfxId.WaveStart);
        CameraShake.Shake(0.1f, 0.2f);
        VfxSprite.Spawn(VfxSprites.Ring, current.pos, WithAlpha(threatColor, 0.9f), 0.5f, GroundOrder + 2)
                 .Scale(0.5f, 6f, true).Fade(0f, 0.3f);
    }

    void OnReactorHit(Health h)
    {
        if (current == null || h.IsDead) return;
        current.hitPunch = 1f;
        // Cam kırığı gibi küçük kıvılcım
        if (Random.value < 0.5f)
            VfxSprite.Spawn(VfxSprites.Spark, current.pos + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.3f, 1f)),
                            WithAlpha(idleColor, 0.9f), 0.3f, TankOrder + 2)
                     .Scale(0.35f, 0f).Move(Random.insideUnitCircle * 2f, 4f);
    }

    void UpdateAssault()
    {
        spawnTimer -= Time.deltaTime;
        while (toSpawn > 0 && spawnTimer <= 0f)
        {
            spawnTimer += spawnInterval;
            toSpawn--;
            SpawnRaider();
        }

        if (current.hp != null && !current.hp.IsDead && timer <= 0f) Secured();
    }

    void SpawnRaider()
    {
        // Köşedeki reaktörün arena tarafında doğsun (arena merkezine doğru ±70°)
        Vector2 toCenter = (corners.center - (Vector2)current.pos).normalized;
        float angle = Mathf.Atan2(toCenter.y, toCenter.x) + Random.Range(-70f, 70f) * Mathf.Deg2Rad;
        Vector3 pos = current.pos + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(spawnDistance.x, spawnDistance.y);

        var e = waves.SpawnExtra(pos);
        if (e == null) return;
        e.SetTarget(current.hp.transform);
        raiders.Add(e);

        // Işınlanma portalı: içe çöken halka + zemin parlaması + kıvılcımlar
        VfxSprite.Spawn(VfxSprites.ThinRing, pos, WithAlpha(threatColor, 0.9f), 0.35f, GroundOrder + 1).Scale(2.2f, 0.3f, true);
        VfxSprite.Spawn(VfxSprites.Disc, pos, WithAlpha(threatColor, 0.35f), 0.5f, GroundOrder).Scale(1.4f, 0.2f).Fade(0f, 0.4f);
        for (int i = 0; i < 4; i++)
            VfxSprite.Spawn(VfxSprites.Spark, pos, WithAlpha(threatColor, 0.9f), 0.35f, TankOrder + 2)
                     .Scale(0.3f, 0f).Move(Random.insideUnitCircle.normalized * 3f, 5f);
    }

    void Secured()
    {
        float hpLeft = current.hp.Normalized;
        var r = current;
        EndAssault();
        r.securedFlash = 1.5f;

        // Ödül: can ne kadar korunduysa o kadar çok (yarısı garanti)
        float k = 0.5f + 0.5f * hpLeft;
        int bonus = Mathf.RoundToInt(rewardPerWave * waves.CurrentWave);
        Drop(r, PickupType.Money, Mathf.RoundToInt((rewardCoins + bonus) * k));
        Drop(r, PickupType.Exp, Mathf.RoundToInt((rewardExp + bonus * 2) * k));
        Drop(r, PickupType.Health, 1);

        // Kutlama: iki halka + yükselen enerji
        VfxSprite.Spawn(VfxSprites.Ring, r.pos, WithAlpha(securedColor, 0.9f), 0.6f, GroundOrder + 2)
                 .Scale(0.5f, rewardScatter * 3f, true).Fade(0f, 0.3f);
        VfxSprite.Spawn(VfxSprites.ThinRing, r.pos, WithAlpha(idleColor, 0.8f), 0.8f, GroundOrder + 2)
                 .Scale(0.5f, rewardScatter * 2f, true).Fade(0f, 0.4f);
        for (int i = 0; i < 10; i++)
            VfxSprite.Spawn(VfxSprites.Plus, r.pos + new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 0.6f)),
                            WithAlpha(i % 2 == 0 ? securedColor : idleColor, 0.9f), 0.9f, TankOrder + 2)
                     .Scale(0.3f, 0.1f).Move(new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(1.5f, 2.5f)), 1.5f).Fade(0f, 0.5f);

        AudioManager.Play(SfxId.Unlock);
        Announce(Loc.T("REACTOR SECURED!"), securedColor);
        Next();
    }

    void OnReactorDestroyed(Health h)
    {
        if (phase != Phase.Assault || current == null) return;
        var r = current;
        EndAssault();
        r.offlineUntil = Time.time + offlineTime;

        int lost = stats != null ? Mathf.FloorToInt(stats.Money * moneyLossFraction) : 0;
        if (lost > 0) stats.TrySpendMoney(lost);

        // Patlama: flaş, şok halkası, etrafa saçılan cam/metal parçaları ve sıvı
        Vector3 c = r.pos + Vector3.up * 0.5f;
        SkillVfx.PulseEmit(r.pos, 3f, 0.4f, threatColor);
        VfxSprite.Spawn(RuntimeSprite.Glow, c, WithAlpha(Color.white, 0.9f), 0.25f, TankOrder + 3).Scale(1f, 4f, true);
        for (int i = 0; i < 14; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            bool glass = i % 2 == 0;
            VfxSprite.Spawn(VfxSprites.Shard, c, glass ? WithAlpha(idleColor, 0.9f) : offlineColor, 0.7f, TankOrder + 2)
                     .Scale(glass ? 0.4f : 0.3f, 0.1f).Rotate(Random.Range(0f, 360f)).Spin(Random.Range(-600f, 600f))
                     .Move(dir * Random.Range(3f, 6f), 4f);
        }
        VfxSprite.Spawn(VfxSprites.Disc, r.pos, WithAlpha(idleColor, 0.45f), 4f, GroundOrder)   // yere dökülen sıvı
                 .Scale(0.5f, 2.4f, true, over: 0.1f).Fade(0f, 0.5f);

        AudioManager.Play(SfxId.Explosion);
        CameraShake.Shake(0.25f, 0.4f);
        GameFeel.HitStop(0.06f);
        Announce(lost > 0 ? $"{Loc.T("REACTOR LOST!")}  {Loc.F("-{0} COINS", lost)}" : Loc.T("REACTOR LOST!"), threatColor);
        Next();
    }

    // Baskını kapat: reaktör canını kaldır, saldırganlar oyuncuya dönsün
    void EndAssault()
    {
        foreach (var e in raiders) if (e != null && !e.IsDead) e.TargetPlayer();
        raiders.Clear();
        toSpawn = 0;
        if (current.hp != null)
        {
            current.hp.OnDeath -= OnReactorDestroyed;
            current.hp.OnHealthChanged -= OnReactorHit;
            Destroy(current.hp.gameObject);
            current.hp = null;
        }
    }

    void Next()
    {
        phase = Phase.Idle;
        timer = Random.Range(attackInterval.x, attackInterval.y);
        current = null;
    }

    // Ödül reaktörden fışkırır: pickup merkezde doğar, kısa bir yay çizerek yerine iner
    void Drop(Reactor r, PickupType type, int count)
    {
        if (count <= 0 || !pickupPrefabs.TryGetValue(type, out var prefab)) return;
        for (int i = 0; i < count; i++)
        {
            Vector3 from = r.pos + Vector3.up * 0.6f;
            Vector3 to = r.pos + (Vector3)(Random.insideUnitCircle.normalized * Random.Range(1.2f, rewardScatter));
            var go = Instantiate(prefab, from, Quaternion.identity);
            go.AddComponent<PopOut>().Init(from, to, 0.35f + Random.Range(0f, 0.2f));
        }
    }

    class PopOut : MonoBehaviour
    {
        Vector3 from, to;
        float duration, age;
        MonoBehaviour magnet;

        public void Init(Vector3 a, Vector3 b, float d)
        {
            from = a; to = b; duration = d;
            magnet = GetComponent<PickupMagnet>();
            if (magnet != null) magnet.enabled = false;   // uçarken mıknatıs çekmesin
        }

        void Update()
        {
            age += Time.deltaTime;
            float k = Mathf.Clamp01(age / duration);
            Vector3 p = Vector3.Lerp(from, to, 1f - (1f - k) * (1f - k));
            p.y += Mathf.Sin(k * Mathf.PI) * 0.8f;   // yay
            transform.position = p;
            if (k >= 1f)
            {
                if (magnet != null) magnet.enabled = true;
                Destroy(this);
            }
        }
    }

    // ================================================================ dünya görselleri
    Reactor MakeReactor(Vector2 pos)
    {
        var r = new Reactor { pos = pos };
        var root = new GameObject("Reactor").transform;
        root.SetParent(transform, false);
        root.position = pos;

        // Zemin: yumuşak ışık havuzu + platform halkası + (tehlikede dönen) uyarı halkası
        r.pad = MakeSprite(root, VfxSprites.Disc, WithAlpha(idleColor, 0.12f), 3.4f, GroundOrder);
        r.ring = MakeSprite(root, VfxSprites.ThinRing, WithAlpha(idleColor, 0.4f), 3.4f, GroundOrder + 1);
        r.hazard = MakeSprite(root, HazardSprite, WithAlpha(threatColor, 0f), 4.2f, GroundOrder + 1);

        // Tank: gölge + arkasında enerji parıltısı + animasyonlu sprite (tabanı reaktör noktasında)
        r.shadow = MakeSprite(root, VfxSprites.Disc, new Color(0f, 0f, 0f, 0.4f), 1f, TankOrder - 2);
        r.shadow.transform.localScale = new Vector3(0.9f, 0.32f, 1f);
        r.shadow.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        r.glow = MakeSprite(root, RuntimeSprite.Glow, WithAlpha(idleColor, 0.5f), 2.2f, TankOrder - 1);
        r.glow.transform.localPosition = new Vector3(0f, 0.6f, 0f);

        r.tankRoot = new GameObject("Tank").transform;
        r.tankRoot.SetParent(root, false);
        r.tank = MakeSprite(r.tankRoot, tankFrames[0], Color.white, 1f, TankOrder, lit: true);
        r.tank.transform.localPosition = new Vector3(0f, 0.72f, 0f);   // sprite'ın tabanı 9 px yukarıda

        // Can ışıkları: tankın çevresinde 4 lamba (her biri canın %25'i)
        r.lights = new SpriteRenderer[4];
        for (int i = 0; i < 4; i++)
        {
            float a = (45f + 90f * i) * Mathf.Deg2Rad;
            r.lights[i] = MakeSprite(root, lightOn, Color.white, 1f, TankOrder - 1, lit: true);
            r.lights[i].transform.localPosition = new Vector3(Mathf.Cos(a) * 1.25f, Mathf.Sin(a) * 0.8f + 0.1f, 0f);
        }

        // Can barı (sadece baskında): çerçeve, zemin, gecikmeli beyaz bar, dolgu
        r.barFrame = MakeBar(root, new Color(0.08f, 0.09f, 0.14f, 0.95f), 1.9f, 0.26f, BarOrder);
        r.barBg = MakeBar(root, new Color(0.25f, 0.08f, 0.12f, 1f), 1.8f, 0.16f, BarOrder + 1);
        r.barLag = MakeBar(root, new Color(1f, 1f, 1f, 0.85f), 1.8f, 0.16f, BarOrder + 2);
        r.barFill = MakeBar(root, idleColor, 1.8f, 0.16f, BarOrder + 3);

        // Uyarıda tankın üstünde zıplayan "!"
        var alertGo = new GameObject("Alert");
        alertGo.transform.SetParent(root, false);
        alertGo.transform.localPosition = new Vector3(0f, 2.1f, 0f);
        r.alert = alertGo.AddComponent<TextMeshPro>();
        if (font != null) r.alert.font = font;
        r.alert.text = "!";
        r.alert.fontSize = 9f;
        r.alert.alignment = TextAlignmentOptions.Center;
        r.alert.color = threatColor;
        r.alert.outlineWidth = 0.25f;
        r.alert.outlineColor = new Color32(20, 10, 20, 255);
        r.alert.sortingOrder = BarOrder + 5;
        alertGo.SetActive(false);

        r.marker = MinimapUI.AddMarker(pos, tankFrames[0], Color.white, 22f);
        return r;
    }

    SpriteRenderer MakeBar(Transform parent, Color c, float w, float h, int order)
    {
        var sr = MakeSprite(parent, RuntimeSprite.White, c, 1f, order);
        sr.transform.localScale = new Vector3(w, h, 1f);
        sr.transform.localPosition = new Vector3(0f, 1.75f, 0f);
        sr.enabled = false;
        return sr;
    }

    // lit: true -> sahnenin varsayılan (ışıklı) sprite materyali, dekorlarla aynı görünür; false -> ışıksız (parıltılar)
    SpriteRenderer MakeSprite(Transform parent, Sprite sprite, Color color, float size, int order, bool lit = false)
    {
        var go = new GameObject("Sprite");
        go.transform.SetParent(parent, false);
        go.transform.localScale = new Vector3(size, size, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        if (!lit) sr.material = VfxSprite.VfxMaterial;   // ışıktan etkilenmesin
        return sr;
    }

    void AnimateReactors()
    {
        float t = Time.time;
        float slow = 0.5f + 0.5f * Mathf.Sin(t * 2.5f);
        float fast = 0.5f + 0.5f * Mathf.Sin(t * 10f);
        int frame = (int)(t * tankFps) % 4;

        foreach (var r in reactors)
        {
            bool threatened = r == current && phase != Phase.Idle;
            bool offline = r.Offline;
            Color c = offline ? offlineColor : threatened ? threatColor : idleColor;
            float pulse = threatened ? fast : slow;

            if (r.securedFlash > 0f)
            {
                r.securedFlash -= Time.deltaTime;
                c = Color.Lerp(c, securedColor, Mathf.Clamp01(r.securedFlash));
            }

            // Tank: kabarcık animasyonu; çevrimdışıyken boş tank; tehlikede kırmızıya çalar
            r.tank.sprite = offline ? tankEmpty : tankFrames[frame];
            r.tank.color = threatened ? Color.Lerp(Color.white, new Color(1f, 0.55f, 0.6f), fast) : Color.white;
            r.hitPunch = Mathf.MoveTowards(r.hitPunch, 0f, Time.deltaTime * 8f);
            float sq = 1f + 0.12f * r.hitPunch;
            r.tankRoot.localScale = new Vector3(sq, 2f - sq, 1f);   // vurulunca ezilip toparlanır

            r.glow.color = WithAlpha(c, offline ? 0f : 0.35f + 0.25f * pulse);
            r.glow.transform.localScale = Vector3.one * (2f + 0.3f * pulse);
            r.pad.color = WithAlpha(c, offline ? 0.05f : 0.1f + 0.08f * pulse);
            r.ring.color = WithAlpha(c, offline ? 0.12f : threatened ? 0.5f + 0.4f * pulse : 0.35f);

            // Dönen uyarı halkası sadece tehlikede
            float hz = r.hazard.color.a;
            hz = Mathf.MoveTowards(hz, threatened ? 0.8f : 0f, Time.deltaTime * 3f);
            r.hazard.color = WithAlpha(threatColor, hz);
            r.hazard.transform.Rotate(0f, 0f, (phase == Phase.Assault && threatened ? 70f : 35f) * Time.deltaTime);

            // Can ışıkları: baskında kalan can, çevrimdışıyken hepsi sönük
            float hpK = r.hp != null ? r.hp.Normalized : 1f;
            for (int i = 0; i < r.lights.Length; i++)
            {
                bool on = !offline && hpK > i * 0.25f + 0.001f;
                r.lights[i].sprite = on ? lightOn : lightOff;
                r.lights[i].color = on || offline ? Color.white : WithAlpha(Color.white, 0.6f + 0.4f * fast);
            }

            // "!" uyarısı: zıplar
            bool alert = threatened && phase == Phase.Warning;
            if (r.alert.gameObject.activeSelf != alert) r.alert.gameObject.SetActive(alert);
            if (alert) r.alert.transform.localPosition = new Vector3(0f, 2.1f + 0.15f * Mathf.Abs(Mathf.Sin(t * 6f)), 0f);

            UpdateBar(r);
            AmbientFx(r, c, threatened, offline);

            // Minimap: normalde yeşil tank, tehlikede kırmızı ve büyüyüp küçülür, çevrimdışıyken sönük
            r.marker.sprite = offline ? tankEmpty : tankFrames[0];
            r.marker.color = offline ? new Color(1f, 1f, 1f, 0.45f)
                           : threatened ? Color.Lerp(Color.white, threatColor, fast) : Color.white;
            r.marker.pulse = threatened ? 8f : 0f;
            r.marker.size = threatened ? 30f : 22f;
        }
    }

    void UpdateBar(Reactor r)
    {
        bool on = r.hp != null;
        r.barFrame.enabled = r.barBg.enabled = r.barLag.enabled = r.barFill.enabled = on;
        if (!on) return;

        float k = r.hp.Normalized;
        r.lagHp = Mathf.MoveTowards(r.lagHp, k, Time.deltaTime * 0.6f);   // beyaz bar yavaşça yetişir
        if (r.lagHp < k) r.lagHp = k;
        SetBarWidth(r.barLag, r.lagHp);
        SetBarWidth(r.barFill, k);
        // Yeşil -> sarı -> kırmızı
        r.barFill.color = k > 0.5f ? Color.Lerp(warnColor, idleColor, (k - 0.5f) * 2f) : Color.Lerp(threatColor, warnColor, k * 2f);
    }

    static void SetBarWidth(SpriteRenderer sr, float k)
    {
        const float w = 1.8f;
        sr.transform.localScale = new Vector3(w * k, 0.16f, 1f);
        sr.transform.localPosition = new Vector3(-w * 0.5f * (1f - k), 1.75f, 0f);   // soldan dolu
    }

    // Arada bir: normalde yükselen enerji kabarcığı, tehlikede kırmızı kıvılcım, çevrimdışıyken duman
    void AmbientFx(Reactor r, Color c, bool threatened, bool offline)
    {
        r.fxTimer -= Time.deltaTime;
        if (r.fxTimer > 0f) return;
        r.fxTimer = offline ? 0.35f : threatened ? 0.15f : 0.5f;
        if (!IsVisible(r.pos)) return;   // ekranda değilse boşuna efekt üretme

        Vector3 top = r.pos + new Vector3(Random.Range(-0.25f, 0.25f), 1.3f);
        if (offline)
            VfxSprite.Spawn(VfxSprites.Disc, top, new Color(0.35f, 0.35f, 0.4f, 0.5f), 1.6f, TankOrder + 1)
                     .Scale(0.2f, 0.9f).Move(new Vector3(Random.Range(-0.2f, 0.2f), 0.8f)).Fade(0.1f, 0.4f);
        else
            VfxSprite.Spawn(threatened ? VfxSprites.Spark : VfxSprites.Disc, top, WithAlpha(c, 0.8f), 0.9f, TankOrder + 1)
                     .Scale(threatened ? 0.3f : 0.12f, 0f).Move(new Vector3(Random.Range(-0.3f, 0.3f), threatened ? 2f : 1f)).Fade(0f, 0.5f);
    }

    bool IsVisible(Vector3 p)
    {
        if (cam == null) return true;
        Vector3 v = cam.WorldToViewportPoint(p);
        return v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
    }

    // ================================================================ HUD
    void BuildHud()
    {
        // HUD canvas'ının ölçekleme ayarlarını kopyalayan ayrı bir overlay canvas.
        // sortingOrder -1: Game Over / upgrade panelleri bunun üstünde kalır.
        var go = new GameObject("ReactorHUD", typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1;
        var scaler = go.GetComponent<CanvasScaler>();
        var waveHud = FindAnyObjectByType<WaveHUD>();
        var hudScaler = waveHud != null ? waveHud.GetComponentInParent<CanvasScaler>() : null;
        if (hudScaler != null)
        {
            scaler.uiScaleMode = hudScaler.uiScaleMode;
            scaler.referenceResolution = hudScaler.referenceResolution;
            scaler.matchWidthOrHeight = hudScaler.matchWidthOrHeight;
            scaler.scaleFactor = hudScaler.scaleFactor;
        }
        else
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Ekran dışındaki reaktörü gösteren ok + mesafe
        arrowRoot = new GameObject("ArrowRoot", typeof(RectTransform)).GetComponent<RectTransform>();
        arrowRoot.SetParent(go.transform, false);
        var a = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
        a.transform.SetParent(arrowRoot, false);
        ((RectTransform)a.transform).sizeDelta = new Vector2(arrowSize, arrowSize);
        arrow = a.GetComponent<Image>();
        arrow.sprite = ArrowSprite;
        arrow.raycastTarget = false;
        arrowText = MakeText((RectTransform)arrowRoot, 26f, Vector2.zero, 32f, TextAlignmentOptions.Center);
        arrowText.rectTransform.anchorMin = arrowText.rectTransform.anchorMax = arrowText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        arrowText.rectTransform.sizeDelta = new Vector2(110f, 32f);
        arrowText.outlineWidth = 0.25f;
        arrowText.outlineColor = new Color32(10, 10, 20, 255);
        arrowRoot.gameObject.SetActive(false);

        // Sağ üst görev kutusu (HUD'daki skill bar çerçevesiyle aynı stil)
        var box = new GameObject("Objective", typeof(RectTransform), typeof(Image));
        hud = (RectTransform)box.transform;
        hud.SetParent(go.transform, false);
        hud.anchorMin = hud.anchorMax = hud.pivot = new Vector2(1f, 1f);
        hud.anchoredPosition = new Vector2(-20f, -20f);
        hud.sizeDelta = new Vector2(400f, 108f);
        var bg = box.GetComponent<Image>();
        bg.raycastTarget = false;
        if (hudFrame != null) { bg.sprite = hudFrame; bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 0.5f; }
        else bg.color = new Color(0.05f, 0.07f, 0.1f, 0.85f);

        // Solda küçük reaktör ikonu
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var irt = (RectTransform)icon.transform;
        irt.SetParent(hud, false);
        irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(12f, 2f);
        irt.sizeDelta = new Vector2(48f, 96f);
        hudIcon = icon.GetComponent<Image>();
        hudIcon.sprite = tankFrames[0];
        hudIcon.preserveAspect = true;
        hudIcon.raycastTarget = false;

        titleText = MakeText(hud, 20f, new Vector2(34f, -16f), 26f, TextAlignmentOptions.Left);
        bodyText = MakeText(hud, 28f, new Vector2(34f, -42f), 34f, TextAlignmentOptions.Left);

        var barBg = new GameObject("Bar", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        barBg.SetParent(hud, false);
        barBg.anchorMin = new Vector2(0f, 0f); barBg.anchorMax = new Vector2(1f, 0f);
        barBg.offsetMin = new Vector2(72f, 16f); barBg.offsetMax = new Vector2(-18f, 28f);
        var barBgImg = barBg.GetComponent<Image>();
        barBgImg.color = new Color(0f, 0f, 0f, 0.5f);
        barBgImg.raycastTarget = false;

        barFill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        barFill.SetParent(barBg, false);
        barFill.anchorMin = Vector2.zero; barFill.anchorMax = new Vector2(0f, 1f);
        barFill.offsetMin = new Vector2(2f, 2f); barFill.offsetMax = new Vector2(-2f, -2f);
        barFillImg = barFill.GetComponent<Image>();
        barFillImg.raycastTarget = false;

        hud.gameObject.SetActive(false);
    }

    // top.x: soldan içeri kayma (ikon için yer), top.y: üstten
    TMP_Text MakeText(RectTransform parent, float size, Vector2 top, float height, TextAlignmentOptions align)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(top.x + 38f, top.y - height);
        rt.offsetMax = new Vector2(-18f, top.y);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = size * 0.6f;   // uzun çeviriler sığsın
        t.raycastTarget = false;
        return t;
    }

    void UpdateHud()
    {
        bool on = phase != Phase.Idle && current != null;
        if (hud.gameObject.activeSelf != on)
        {
            hud.gameObject.SetActive(on);
            if (on) hudPop = 1f;
        }
        if (!on) { arrowRoot.gameObject.SetActive(false); return; }

        // Dalga görevi kutusu sağ üstteyse onun altına in
        float targetY = WaveQuests.PanelVisible ? -20f - WaveQuests.PanelHeight - 12f : -20f;
        hud.anchoredPosition = new Vector2(-20f, Mathf.MoveTowards(hud.anchoredPosition.y, targetY, Time.unscaledDeltaTime * 800f));

        // Açılışta küçük "pop", baskında hafif titreşim
        hudPop = Mathf.MoveTowards(hudPop, 0f, Time.unscaledDeltaTime * 4f);
        hud.localScale = Vector3.one * (1f + 0.15f * hudPop * hudPop);
        float fast = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);

        titleText.color = Color.Lerp(threatColor, Color.white, phase == Phase.Warning ? fast * 0.5f : 0f);
        bodyText.color = Color.white;
        hudIcon.sprite = tankFrames[(int)(Time.time * tankFps) % 4];
        hudIcon.color = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.6f), fast);

        if (phase == Phase.Warning)
        {
            titleText.text = Loc.T("REACTOR UNDER THREAT");
            bodyText.text = Loc.F("ATTACK IN {0}s", Mathf.CeilToInt(timer));
            SetBar(timer / prepTime, warnColor);   // kalan hazırlık süresi azalır
        }
        else
        {
            float k = current.hp != null ? current.hp.Normalized : 0f;
            titleText.text = Loc.T("DEFEND THE REACTOR");
            bodyText.text = Loc.F("HOLD OUT {0}s", Mathf.CeilToInt(Mathf.Max(0f, timer)));
            SetBar(k, k > 0.5f ? Color.Lerp(warnColor, idleColor, (k - 0.5f) * 2f) : Color.Lerp(threatColor, warnColor, k * 2f));
        }

        PlaceArrow(current.pos);
    }

    float hudPop;

    void SetBar(float k, Color c)
    {
        barFill.anchorMax = new Vector2(Mathf.Clamp01(k), 1f);
        barFillImg.color = c;
    }

    // Reaktör ekran dışındaysa ekran kenarında, reaktöre bakan ok + mesafe
    void PlaceArrow(Vector3 world)
    {
        if (cam == null) return;
        Vector3 sp = cam.WorldToScreenPoint(world);
        float margin = arrowSize * 0.5f + 40f;   // ok ve mesafe yazısı ekrandan taşmasın
        bool onScreen = sp.x > margin && sp.x < Screen.width - margin && sp.y > margin && sp.y < Screen.height - margin;
        arrowRoot.gameObject.SetActive(!onScreen);
        if (onScreen) return;

        Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 dir = (Vector2)sp - center;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
        float k = Mathf.Min((center.x - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.x)),
                            (center.y - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
        arrowRoot.position = center + dir * k;   // overlay canvas: dünya konumu = ekran pikseli

        var rt = arrow.rectTransform;
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        float s = 1f + 0.15f * Mathf.Sin(Time.time * 6f);
        rt.localScale = new Vector3(s, s, 1f);
        arrow.color = threatColor;

        // Mesafe yazısı okun içeri tarafında
        arrowText.rectTransform.anchoredPosition = -dir.normalized * (arrowSize * 0.5f + 34f);
        arrowText.text = $"{Mathf.RoundToInt(Vector2.Distance(player.position, world))}m";
        arrowText.color = threatColor;
    }

    // Oyuncunun üstünde kısa duyuru (dünya yazısı): büyüyerek belirir, süzülüp solar
    void Announce(string text, Color color)
    {
        var go = new GameObject("Announce");
        go.transform.position = player.position + Vector3.up * 1.6f;
        var t = go.AddComponent<TextMeshPro>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = 5f;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.outlineWidth = 0.2f;
        t.outlineColor = new Color32(10, 10, 20, 255);
        t.sortingOrder = 110;
        go.AddComponent<AnnounceLabel>();
    }

    class AnnounceLabel : MonoBehaviour
    {
        const float Life = 2.2f;
        TMP_Text t;
        Color baseColor;
        float age;

        void Start() { t = GetComponent<TMP_Text>(); baseColor = t.color; }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= Life) { Destroy(gameObject); return; }
            transform.position += Vector3.up * (0.4f * Time.deltaTime);
            float k = age / Life;
            float pop = Mathf.Clamp01(age / 0.15f);
            transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, pop);
            t.color = WithAlpha(baseColor, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
        }
    }

    // ================================================================ yardımcılar
    static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    // Kesikli uyarı halkası (tehlike şeridi gibi 12 parça). Dönerek "alarm" hissi verir.
    static Sprite HazardSprite
    {
        get
        {
            if (hazardSprite == null)
            {
                const int size = 128;
                var tex = new Texture2D(size, size) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f, dy = y + 0.5f - size * 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / (size * 0.5f);
                    float ang = Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 0.5f;   // 0..1
                    bool band = r > 0.86f && r < 0.97f;
                    bool dash = (ang * 12f) % 1f < 0.55f;
                    tex.SetPixel(x, y, band && dash ? Color.white : Color.clear);
                }
                tex.Apply();
                hazardSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            return hazardSprite;
        }
    }

    // Sağa bakan dolu üçgen ok
    static Sprite ArrowSprite
    {
        get
        {
            if (arrowSprite == null)
            {
                const int size = 32;
                var tex = new Texture2D(size, size) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float half = (size - x) * 0.5f;   // x büyüdükçe daralır (uç sağda)
                    bool inside = x >= 4 && Mathf.Abs(y + 0.5f - size * 0.5f) <= half * 0.9f;
                    tex.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
                tex.Apply();
                arrowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            return arrowSprite;
        }
    }
}
