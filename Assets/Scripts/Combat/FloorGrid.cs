using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Hacklenebilir zemin: düşman bir zemin hücresinin üstünde ölünce o hücre "yanar" (ışıklı havalandırma paneli).
// Haritanın HERHANGİ bir yerinde yanık hücreler blockSize x blockSize'lık (2x2) bir kare oluşturunca
// o blok AŞIRI YÜKLENİR ve kısa süre rastgele bir bonus verir (etki alanı = blok + zonePadding kenar):
//   Şok      : alandaki düşmanlara aralıklı şimşek (canlarının bir yüzdesi kadar hasar)
//   Yavaşlat : alandaki düşmanlar yavaşlar
//   Mıknatıs : haritadaki tüm pickup'lar oyuncuya çekilir
// Bonus bitince bloğun hücreleri söner; dalga değişince bloğa katılmamış yanık hücreler de söner.
// Bir bloğun 3 hücresi yanmışsa eksik hücre köşeli çerçeveyle işaretlenir ("bir öldürme daha").
//
// Görseller: Resources/Grid (Tech Dungeon tileset: ışıklı havalandırma 36,1 -> taban + boyanabilir ışık,
// tehlike şeridi 5,12 -> boyanabilir; bonus ikonları). Hücreler Grid ile hizalı (hücre (x,y) = dünyada
// [x, x+1) x [y, y+1)). Collider yok. Kendini kurar: WaveManager olan sahnede otomatik oluşur.
public class FloorGrid : MonoBehaviour
{
    enum Bonus { Zap, Stasis, Magnet }

    [Header("Izgara")]
    [Tooltip("Aşırı yükleme için yan yana yanması gereken kare (2 = 2x2).")]
    [SerializeField] int blockSize = 2;
    [Tooltip("Bonusun etki alanı bloğun her yanından bu kadar hücre taşar (2x2 blok + 1 = 4x4 alan).")]
    [SerializeField] int zonePadding = 1;

    [Header("Bonus")]
    [SerializeField] float bonusDuration = 8f;
    [Tooltip("Şok: kaç saniyede bir çakar.")]
    [SerializeField] float zapInterval = 0.5f;
    [Tooltip("Şok: her çakışta en fazla kaç düşman vurulur.")]
    [SerializeField] int zapTargets = 3;
    [Tooltip("Şok hasarı = düşmanın max canının bu oranı (boss'ta bossZapFactor ile çarpılır).")]
    [SerializeField] float zapMaxHealthFraction = 0.25f;
    [SerializeField] float bossZapFactor = 0.1f;
    [Tooltip("Yavaşlatma yüzdesi (0.5 = %50 yavaş).")]
    [SerializeField] float stasisSlow = 0.5f;

    [Header("Görünüm")]
    [SerializeField] Color chargedColor = new Color(0.45f, 0.94f, 0.97f);   // #73EFF7
    [SerializeField] Color zapColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] Color stasisColor = new Color(0.45f, 0.65f, 1f);
    [SerializeField] Color magnetColor = new Color(0.45f, 1f, 0.5f);
    const int BaseOrder = -13;    // Default katmanında dekorların (-10) ve karakterlerin altında
    const int GlowOrder = -12;
    const int ZoneOrder = -11;
    const int IconOrder = 45;     // bonus ikonu karakterlerin üstünde

    // İpuçları okur: oyuncu en az bir hücre yaktı mı
    public static bool AnyCharged { get; private set; }

    // Bir blok aşırı yüklenince: etki alanı (dünya). Dalga görevleri dinler.
    public static event System.Action<Rect> ZoneOverloaded;

    static FloorGrid instance;

    // Nokta şu an aktif bir bonus alanının içinde mi (dalga görevi: "ızgarada öldür")
    public static bool IsInActiveZone(Vector2 p)
    {
        if (instance == null) return false;
        foreach (var z in instance.zones) if (z.area.Contains(p)) return true;
        return false;
    }

    class Cell
    {
        public SpriteRenderer baseSr, glowSr;
        public float age;
    }

    // Aşırı yüklenmiş blok: hücreleri artık ona ait (bonus bitene kadar yeniden yakılamaz)
    class Zone
    {
        public readonly List<Vector2Int> cells = new List<Vector2Int>();
        public readonly List<Cell> visuals = new List<Cell>();
        public Rect area;   // etki alanı (dünya)
        public Bonus bonus;
        public float timeLeft, tick, fxTick;
        public SpriteRenderer floor, icon, timerBg, timerFill;
        public readonly List<SpriteRenderer> border = new List<SpriteRenderer>();
    }

    readonly Dictionary<Vector2Int, Cell> charged = new Dictionary<Vector2Int, Cell>();
    readonly HashSet<Vector2Int> locked = new HashSet<Vector2Int>();   // aktif bloklara ait hücreler
    readonly Dictionary<Vector2Int, SpriteRenderer> hints = new Dictionary<Vector2Int, SpriteRenderer>();
    readonly List<Zone> zones = new List<Zone>();
    WaveManager waves;
    int lastWave;
    TMP_FontAsset font;

    Sprite cellBase, cellGlow, stripe, zapIcon, stasisIcon, magnetIcon;
    static Sprite fallbackPanel, hintSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { AnyCharged = false; ZoneOverloaded = null; instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded += (s, m) => TryCreate();
        TryCreate();
    }

    static void TryCreate()
    {
        if (FindAnyObjectByType<FloorGrid>() != null) return;
        if (FindAnyObjectByType<WaveManager>() == null) return;   // sadece oyun sahnesi
        new GameObject("FloorGrid").AddComponent<FloorGrid>();
    }

    void Awake()
    {
        instance = this;
        waves = FindAnyObjectByType<WaveManager>();
        var ui = FindAnyObjectByType<TextMeshProUGUI>();   // HUD'un pixel fontu (etiket için)
        if (ui != null) font = ui.font;
        AnyCharged = false;

        cellBase = Resources.Load<Sprite>("Grid/cell_base");
        cellGlow = Resources.Load<Sprite>("Grid/cell_glow");
        stripe = Resources.Load<Sprite>("Grid/stripe");
        zapIcon = LoadFirst("Grid/zap");
        stasisIcon = LoadFirst("Grid/stasis");
        magnetIcon = LoadFirst("Grid/magnet");
    }

    static Sprite LoadFirst(string path)
    {
        var all = Resources.LoadAll<Sprite>(path);   // ikonlar çoklu sprite modunda olabilir
        return all != null && all.Length > 0 ? all[0] : null;
    }

    void OnEnable() => EnemyBase.AnyKilled += OnKill;
    void OnDisable() => EnemyBase.AnyKilled -= OnKill;
    void OnDestroy() { if (instance == this) instance = null; }

    // ================================================================ hücre yakma
    void OnKill(EnemyBase e)
    {
        if (e == null) return;
        Vector3 pos = e.transform.position;
        var cell = new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
        if (charged.ContainsKey(cell) || locked.Contains(cell)) return;   // zaten yanık / bonus sürüyor

        charged[cell] = MakeCell(cell, chargedColor);
        // Yanma anı: beyaz parlama + birkaç kıvılcım
        Vector3 c = CellCenter(cell);
        VfxSprite.Spawn(RuntimeSprite.White, c, WithAlpha(Color.white, 0.7f), 0.25f, GlowOrder + 1).Scale(0.95f, 1.2f, true).Fade(0f, 0f);
        for (int i = 0; i < 3; i++)
            VfxSprite.Spawn(VfxSprites.Spark, c, WithAlpha(chargedColor, 0.9f), 0.3f, GlowOrder + 2)
                     .Scale(0.25f, 0f).Move(Random.insideUnitCircle.normalized * 2f, 5f);
        AnyCharged = true;

        if (!TryOverload(cell)) RefreshHints();
    }

    // Yeni yanan hücreyi içerebilecek tüm blockSize x blockSize kareleri dene; ilk dolu olanı yükle
    bool TryOverload(Vector2Int cell)
    {
        int n = blockSize;
        for (int ox = 0; ox < n; ox++)
        for (int oy = 0; oy < n; oy++)
        {
            var origin = new Vector2Int(cell.x - ox, cell.y - oy);
            if (IsFull(origin)) { Overload(origin); return true; }
        }
        return false;
    }

    bool IsFull(Vector2Int origin)
    {
        for (int x = 0; x < blockSize; x++)
        for (int y = 0; y < blockSize; y++)
            if (!charged.ContainsKey(new Vector2Int(origin.x + x, origin.y + y))) return false;
        return true;
    }

    void Overload(Vector2Int origin)
    {
        var z = new Zone
        {
            bonus = (Bonus)Random.Range(0, 3),
            timeLeft = bonusDuration,
            area = new Rect(origin.x - zonePadding, origin.y - zonePadding,
                            blockSize + zonePadding * 2, blockSize + zonePadding * 2),
        };
        Color c = BonusColor(z.bonus);
        Vector3 center = z.area.center;

        // Hücreler bloğa geçer: bonus rengine boyanır, bitene kadar kilitli
        var points = new List<Vector3>();
        for (int x = 0; x < blockSize; x++)
        for (int y = 0; y < blockSize; y++)
        {
            var cell = new Vector2Int(origin.x + x, origin.y + y);
            var v = charged[cell];
            charged.Remove(cell);
            locked.Add(cell);
            z.cells.Add(cell);
            z.visuals.Add(v);
            v.glowSr.color = c;
            v.age = 0f;
            points.Add(CellCenter(cell));
        }
        // Hücreler arası enerji: şimşekler blok merkezinde birleşir
        foreach (var p in points) LightningArc.Spawn(new[] { p, center }, c, 0.07f, 0.35f, GlowOrder + 3);

        // Alan: zemin ışığı + tehlike şeridi çerçeve + ikon ve süre çubuğu
        z.floor = MakeSprite(RuntimeSprite.White, center, WithAlpha(c, 0.1f), ZoneOrder);
        z.floor.transform.localScale = new Vector3(z.area.width, z.area.height, 1f);
        BuildBorder(z, c);

        Vector3 iconPos = new Vector3(center.x, z.area.yMax + 0.45f);
        z.icon = MakeSprite(BonusIcon(z.bonus), iconPos, Color.white, IconOrder);
        z.icon.transform.localScale = Vector3.one * 0.7f;
        z.timerBg = MakeSprite(RuntimeSprite.White, iconPos + Vector3.down * 0.5f, new Color(0f, 0f, 0f, 0.6f), IconOrder);
        z.timerBg.transform.localScale = new Vector3(1.2f, 0.1f, 1f);
        z.timerFill = MakeSprite(RuntimeSprite.White, iconPos + Vector3.down * 0.5f, c, IconOrder + 1);

        VfxSprite.Spawn(VfxSprites.Ring, center, WithAlpha(c, 0.8f), 0.5f, ZoneOrder + 3)
                 .Scale(0.5f, z.area.width * 1.6f, true).Fade(0f, 0.3f);
        VfxSprite.Spawn(RuntimeSprite.Glow, center, WithAlpha(c, 0.6f), 0.4f, ZoneOrder + 2).Scale(1f, z.area.width * 1.5f, true);

        ShowLabel(iconPos + Vector3.up * 0.5f, BonusName(z.bonus), c);
        AudioManager.Play(SfxId.Synergy);
        CameraShake.Shake(0.08f, 0.15f);
        zones.Add(z);
        RefreshHints();
        ZoneOverloaded?.Invoke(z.area);
    }

    // Tehlike şeridi: alanın 4 kenarına döşenir (Tiled çizim, boyanabilir beyaz şerit)
    void BuildBorder(Zone z, Color c)
    {
        Rect a = z.area;
        float t = stripe != null ? stripe.bounds.size.y : 0.19f;
        void Edge(Vector3 pos, float length, float angle)
        {
            var sr = MakeSprite(stripe != null ? stripe : RuntimeSprite.White, pos, c, ZoneOrder + 1);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            if (stripe != null) { sr.drawMode = SpriteDrawMode.Tiled; sr.size = new Vector2(length, t); }
            else sr.transform.localScale = new Vector3(length, t, 1f);
            z.border.Add(sr);
        }
        Edge(new Vector3(a.center.x, a.yMax - t * 0.5f), a.width, 0f);
        Edge(new Vector3(a.center.x, a.yMin + t * 0.5f), a.width, 180f);
        Edge(new Vector3(a.xMin + t * 0.5f, a.center.y), a.height - t * 2f, 90f);
        Edge(new Vector3(a.xMax - t * 0.5f, a.center.y), a.height - t * 2f, 270f);
    }

    // ================================================================ her kare
    void Update()
    {
        if (waves != null && waves.CurrentWave != lastWave)
        {
            lastWave = waves.CurrentWave;
            ClearCharged();
        }

        float dt = Time.deltaTime, t = Time.time;

        // Yanık hücreler: doğarken büyüyerek oturur, ışık çizgileri dalga gibi titrer
        foreach (var kv in charged)
        {
            var v = kv.Value;
            v.age += dt;
            float pop = Mathf.Clamp01(v.age / 0.2f);
            float s = Mathf.Lerp(1.25f, 1f, 1f - (1f - pop) * (1f - pop));
            v.baseSr.transform.localScale = v.glowSr.transform.localScale = Vector3.one * s;
            float wave = 0.65f + 0.35f * Mathf.Sin(t * 3f - (kv.Key.x + kv.Key.y) * 0.8f);   // köşegen tarama
            v.glowSr.color = WithAlpha(chargedColor, wave);
        }

        // Eksik hücre işaretleri nabız atar
        float hintA = 0.35f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 7f));
        foreach (var h in hints.Values)
        {
            h.color = WithAlpha(chargedColor, hintA);
            h.transform.localScale = Vector3.one * (0.95f + 0.08f * Mathf.Sin(t * 7f));
        }

        for (int i = zones.Count - 1; i >= 0; i--)
        {
            var z = zones[i];
            z.timeLeft -= dt;
            z.tick -= dt;
            AnimateZone(z, t);

            if (z.tick <= 0f) { z.tick = TickInterval(z.bonus); Apply(z); }

            if (z.timeLeft <= 0f)
            {
                EndZone(z);
                zones.RemoveAt(i);
            }
        }
    }

    void AnimateZone(Zone z, float t)
    {
        Color c = BonusColor(z.bonus);
        bool ending = z.timeLeft < 2f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * (ending ? 16f : 5f));   // son 2 sn hızlı yanıp söner

        foreach (var b in z.border) b.color = WithAlpha(c, 0.55f + 0.4f * pulse);
        z.floor.color = WithAlpha(c, 0.06f + 0.06f * pulse);
        foreach (var v in z.visuals) v.glowSr.color = WithAlpha(Color.Lerp(c, Color.white, 0.3f * pulse), 1f);

        Vector3 top = new Vector3(z.area.center.x, z.area.yMax + 0.45f + 0.08f * Mathf.Sin(t * 3f));
        z.icon.transform.position = top;
        z.icon.color = WithAlpha(Color.white, ending ? 0.5f + 0.5f * pulse : 1f);
        z.timerBg.transform.position = top + Vector3.down * 0.5f;
        float k = Mathf.Clamp01(z.timeLeft / bonusDuration);
        z.timerFill.transform.localScale = new Vector3(1.2f * k, 0.1f, 1f);
        z.timerFill.transform.position = top + new Vector3(-0.6f * (1f - k), -0.5f);

        // Bonusa özgü ortam efekti
        z.fxTick -= Time.deltaTime;
        if (z.fxTick > 0f) return;
        Rect a = z.area;
        Vector3 Rand() => new Vector3(Random.Range(a.xMin + 0.3f, a.xMax - 0.3f), Random.Range(a.yMin + 0.3f, a.yMax - 0.3f));
        switch (z.bonus)
        {
            case Bonus.Zap:   // alan içinde küçük elektrik çıtırtıları
                z.fxTick = 0.2f;
                var p = Rand();
                LightningArc.Spawn(new[] { p, p + (Vector3)(Random.insideUnitCircle * 0.9f) }, WithAlpha(c, 0.7f), 0.04f, 0.12f, ZoneOrder + 3);
                break;
            case Bonus.Stasis:   // süzülen kar taneleri
                z.fxTick = 0.15f;
                VfxSprite.Spawn(VfxSprites.Snowflake, Rand() + Vector3.up * 0.6f, WithAlpha(c, 0.8f), 1.2f, ZoneOrder + 3)
                         .Scale(0.25f, 0.12f).Move(new Vector3(Random.Range(-0.2f, 0.2f), -0.6f)).Spin(Random.Range(-90f, 90f)).Fade(0.2f, 0.6f);
                break;
            case Bonus.Magnet:   // kenarlardan merkeze akan parçacıklar + içe çöken halka
                z.fxTick = 0.12f;
                Vector3 from = Rand();
                Vector3 toC = (Vector3)a.center - from;
                VfxSprite.Spawn(VfxSprites.Spark, from, WithAlpha(c, 0.9f), 0.5f, ZoneOrder + 3)
                         .Scale(0.25f, 0.05f).Move(toC * 2f);
                if (Random.value < 0.15f)
                    VfxSprite.Spawn(VfxSprites.ThinRing, a.center, WithAlpha(c, 0.6f), 0.6f, ZoneOrder + 2).Scale(a.width * 1.1f, 0.3f, true);
                break;
        }
    }

    float TickInterval(Bonus b) => b == Bonus.Zap ? zapInterval : 0.25f;

    void Apply(Zone z)
    {
        switch (z.bonus)
        {
            case Bonus.Zap: Zap(z); break;
            case Bonus.Stasis:
                foreach (var e in EnemiesIn(z)) e.ApplySlow(stasisSlow, 0.5f);
                break;
            case Bonus.Magnet:
                foreach (var m in FindObjectsByType<PickupMagnet>()) m.Attract();
                break;
        }
    }

    readonly List<EnemyBase> found = new List<EnemyBase>();

    List<EnemyBase> EnemiesIn(Zone z)
    {
        found.Clear();
        var alive = EnemyRegistry.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            var e = alive[i];
            if (e != null && z.area.Contains(e.transform.position)) found.Add(e);
        }
        return found;
    }

    void Zap(Zone z)
    {
        var targets = EnemiesIn(z);
        if (targets.Count == 0) return;
        Color c = BonusColor(z.bonus);
        int n = Mathf.Min(zapTargets, targets.Count);
        for (int i = 0; i < n; i++)
        {
            var e = targets[Random.Range(0, targets.Count)];
            targets.Remove(e);
            if (!e.TryGetComponent<Health>(out var h) || h.IsDead) continue;

            // Şimşek bloğun bir hücresinden düşmana sıçrar
            Vector3 from = CellCenter(z.cells[Random.Range(0, z.cells.Count)]);
            LightningArc.Spawn(new[] { from, e.transform.position }, c, 0.08f, 0.2f, 50);
            VfxSprite.Spawn(VfxSprites.Spark, e.transform.position, c, 0.25f, 51).Scale(0.6f, 0f);
            h.TakeDamage(h.Max * zapMaxHealthFraction * (e.IsBoss ? bossZapFactor : 1f));
        }
        AudioManager.Play(SfxId.Lightning, 0.5f);
    }

    // ================================================================ sıfırlama
    void EndZone(Zone z)
    {
        foreach (var cell in z.cells) locked.Remove(cell);
        foreach (var v in z.visuals) FadeOut(v);
        Destroy(z.floor.gameObject);
        foreach (var b in z.border) Destroy(b.gameObject);
        Destroy(z.icon.gameObject);
        Destroy(z.timerBg.gameObject);
        Destroy(z.timerFill.gameObject);
        VfxSprite.Spawn(VfxSprites.ThinRing, z.area.center, WithAlpha(BonusColor(z.bonus), 0.6f), 0.4f, ZoneOrder + 2)
                 .Scale(z.area.width * 1.2f, 0.4f, true).Fade(0f, 0.2f);
        RefreshHints();
    }

    // Dalga değişince: bloğa katılmamış yanık hücreler söner (her dalga yeni bir mini hedef)
    void ClearCharged()
    {
        foreach (var v in charged.Values) FadeOut(v);
        charged.Clear();
        RefreshHints();
    }

    void FadeOut(Cell v)
    {
        if (v.glowSr != null)
        {
            VfxSprite.Spawn(cellGlow != null ? cellGlow : Fallback, v.glowSr.transform.position, v.glowSr.color, 0.35f, GlowOrder)
                     .Scale(1f, 0.3f).Fade(0f, 0f);
            Destroy(v.glowSr.gameObject);
        }
        if (v.baseSr != null) Destroy(v.baseSr.gameObject);
    }

    // Bir blokta 3 hücre yanıksa 4.'yü işaretle (kilitli olmayan, yanık olmayan)
    readonly HashSet<Vector2Int> wanted = new HashSet<Vector2Int>();

    void RefreshHints()
    {
        wanted.Clear();
        foreach (var cell in charged.Keys)
            for (int ox = 0; ox < blockSize; ox++)
            for (int oy = 0; oy < blockSize; oy++)
            {
                var origin = new Vector2Int(cell.x - ox, cell.y - oy);
                int have = 0; Vector2Int missing = default; bool blocked = false;
                for (int x = 0; x < blockSize; x++)
                for (int y = 0; y < blockSize; y++)
                {
                    var q = new Vector2Int(origin.x + x, origin.y + y);
                    if (charged.ContainsKey(q)) have++;
                    else if (locked.Contains(q)) blocked = true;
                    else missing = q;
                }
                if (!blocked && have == blockSize * blockSize - 1) wanted.Add(missing);
            }

        var remove = new List<Vector2Int>();
        foreach (var k in hints.Keys) if (!wanted.Contains(k)) remove.Add(k);
        foreach (var k in remove) { Destroy(hints[k].gameObject); hints.Remove(k); }
        foreach (var k in wanted)
            if (!hints.ContainsKey(k)) hints[k] = MakeSprite(HintSprite, CellCenter(k), WithAlpha(chargedColor, 0f), GlowOrder);
    }

    // ================================================================ görsel yardımcılar
    Cell MakeCell(Vector2Int cell, Color glow)
    {
        Vector3 c = CellCenter(cell);
        var v = new Cell
        {
            baseSr = MakeSprite(cellBase != null ? cellBase : Fallback, c, cellBase != null ? Color.white : new Color(0.15f, 0.17f, 0.25f, 0.9f), BaseOrder, lit: cellBase != null),
            glowSr = MakeSprite(cellGlow != null ? cellGlow : Fallback, c, glow, GlowOrder),
        };
        return v;
    }

    // lit: sahnenin ışıklı sprite materyali (zemin karolarıyla aynı görünsün); değilse ışıksız (parlayan kısımlar)
    SpriteRenderer MakeSprite(Sprite sprite, Vector3 pos, Color color, int order, bool lit = false)
    {
        var go = new GameObject("GridSprite");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        if (!lit) sr.material = VfxSprite.VfxMaterial;
        return sr;
    }

    void ShowLabel(Vector3 pos, string text, Color color)
    {
        var go = new GameObject("GridLabel");
        go.transform.position = pos;
        var t = go.AddComponent<TextMeshPro>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = 4f;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.outlineWidth = 0.2f;
        t.outlineColor = new Color32(10, 10, 20, 255);
        t.sortingOrder = 100;
        go.AddComponent<FloatingLabel>();
    }

    string BonusName(Bonus b) => b switch
    {
        Bonus.Zap => Loc.T("ZAP GRID"),
        Bonus.Stasis => Loc.T("STASIS GRID"),
        _ => Loc.T("MAGNET GRID"),
    };

    Color BonusColor(Bonus b) => b switch
    {
        Bonus.Zap => zapColor,
        Bonus.Stasis => stasisColor,
        _ => magnetColor,
    };

    Sprite BonusIcon(Bonus b) => (b switch
    {
        Bonus.Zap => zapIcon,
        Bonus.Stasis => stasisIcon,
        _ => magnetIcon,
    }) ?? Fallback;

    static Vector3 CellCenter(Vector2Int c) => new Vector3(c.x + 0.5f, c.y + 0.5f, 0f);
    static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    // Resources/Grid bulunamazsa: düz beyaz kenarlı panel
    static Sprite Fallback
    {
        get
        {
            if (fallbackPanel == null)
            {
                const int size = 16;
                var tex = new Texture2D(size, size) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, edge ? 1f : 0.35f));
                }
                tex.Apply();
                fallbackPanel = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            return fallbackPanel;
        }
    }

    // Eksik hücre işareti: 4 köşe çentiği (32 px = 1 birim)
    static Sprite HintSprite
    {
        get
        {
            if (hintSprite == null)
            {
                const int size = 32, len = 9, w = 2, inset = 2;
                var tex = new Texture2D(size, size) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Min(x - inset, size - 1 - inset - x), dy = Mathf.Min(y - inset, size - 1 - inset - y);
                    bool corner = dx >= 0 && dy >= 0 && ((dx < w && dy < len) || (dy < w && dx < len));
                    tex.SetPixel(x, y, corner ? Color.white : Color.clear);
                }
                tex.Apply();
                hintSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            return hintSprite;
        }
    }

    // Bonus adı: büyüyerek belirir, yukarı süzülür ve solar
    class FloatingLabel : MonoBehaviour
    {
        const float Life = 1.6f;
        TMP_Text t;
        Color baseColor;
        float age;

        void Start() { t = GetComponent<TMP_Text>(); baseColor = t.color; }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= Life) { Destroy(gameObject); return; }
            transform.position += Vector3.up * (0.6f * Time.deltaTime);
            transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(age / 0.15f));
            float k = age / Life;
            t.color = WithAlpha(baseColor, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
        }
    }
}
