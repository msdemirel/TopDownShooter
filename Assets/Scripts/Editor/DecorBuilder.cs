using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Arenayı paketteki objelerle (Tech Dungeon "props and items") süsler. SADECE GÖRSEL:
// hiçbir objede collider yok, hepsi karakterlerin altında çizilir -> oynanışı etkilemez.
//
//   Duvar dipleri : terminaller, geniş ekranlar, tanklar, makineler (üst duvar), sandık/varil/kutu
//                   kümeleri (alt ve yan duvarlar), köşelerde yığınlar
//   Orta alan     : küçük "adacıklar" (depo yığını, çalışma istasyonu, tank çifti, bozuk makine,
//                   laboratuvar köşesi) + zemine yapışık dekor (kablo, ızgara, döküntü, kapsül, sıvı);
//                   oyuncunun başlangıç noktası çevresi boş
//
// Kullanım: MainGame sahnesi açıkken Menü > TopDownShooter > Ortam > Dekorları Yerleştir
// Hepsi sahnede "Decor" objesinin altında: istediğini elle sil/taşı. Tekrar çalıştırınca eski
// Decor silinip yeniden kurulur. Yerleşim sabit tohumla (Seed) üretilir: aynı tohum = aynı düzen.
public static class DecorBuilder
{
    const string Sheet = "Assets/Art/Tech Dungeon Roguelite - Asset Pack (v7)/Props and Items/props and items x1.png";
    const int Seed = 7;

    // Arena zemini (hücre koordinatı; hücre (x,y)'nin merkezi dünyada (x+0.5, y+0.5))
    const int MinX = -34, MaxX = 27, MinY = -21, MaxY = 18;
    static readonly Vector2 PlayerStart = new Vector2(-3.8f, 0f);
    const float ClearRadius = 5f;   // başlangıç noktası çevresi boş kalsın

    const int PropOrder = -10;      // Default katmanında düşmanların (0) ve oyuncunun (13) altında
    const int DecalOrder = 2;       // Floor&Background katmanında zemin tile'larının (0) üstünde

    static Dictionary<int, Sprite> sprites;
    static readonly HashSet<Vector2Int> used = new HashSet<Vector2Int>();
    static System.Random rng;
    static Transform root;
    static int count;

    // ---- Obje tanımları (sprite indeksleri) ----
    class Prop
    {
        public int[][] frames;     // [hücre][kare]: çok hücreli objelerde her hücre ayrı animasyon
        public Vector2Int[] cells; // hücrelerin sol-alt hücreye göre konumu
        public float fps = 6f;
        public Prop(float fps, Vector2Int[] cells, params int[][] frames) { this.fps = fps; this.cells = cells; this.frames = frames; }
        public int Width => cells.Max(c => c.x) + 1;
    }

    static readonly Vector2Int[] One = { new Vector2Int(0, 0) };
    static readonly Vector2Int[] Wide = { new Vector2Int(0, 0), new Vector2Int(1, 0) };
    static readonly Vector2Int[] Tall = { new Vector2Int(0, 0), new Vector2Int(0, 1) };   // alt, üst

    static Prop Anim(float fps, params int[] f) => new Prop(fps, One, f);
    static Prop Static(int i) => new Prop(0f, One, new[] { i });

    // Üst duvar: ekranlı / yüksek objeler
    static readonly Prop[] TopWall =
    {
        Anim(5f, 15, 16, 17, 18),                                                          // terminal (yeşil ekran)
        Anim(5f, 21, 22, 23, 24),                                                          // terminal (mavi ekran)
        Anim(4f, 2, 3, 4, 5),                                                              // duvar monitörü
        new Prop(3f, Wide, new[] { 29, 31, 33, 35 }, new[] { 30, 32, 34, 36 }),            // geniş ekran
        new Prop(4f, Tall, new[] { 53, 54, 55, 56 }, new[] { 41, 42, 43, 44 }),            // tank (kabarcıklı)
        new Prop(4f, Tall, new[] { 75, 76, 77, 78 }, new[] { 65, 66, 67, 68 }),            // tank (dolu)
        Static(19), Static(20), Static(25), Static(27),                                    // makineler
    };
    // Yan duvarlar: tanklar + depo
    static readonly Prop[] SideWall =
    {
        new Prop(4f, Tall, new[] { 53, 54, 55, 56 }, new[] { 41, 42, 43, 44 }),
        new Prop(4f, Tall, new[] { 75, 76, 77, 78 }, new[] { 65, 66, 67, 68 }),
        new Prop(0f, Tall, new[] { 80 }, new[] { 79 }),                                    // boş tank
        Static(49), Static(50), Static(51), Static(52),                                    // sandıklar
        Static(61), Static(62), Static(63), Static(64),                                    // kutular
    };
    // Küçük depo objeleri (alt duvar ve köşe kümeleri)
    static readonly int[] Clutter = { 0, 1, 49, 50, 51, 52, 61, 62, 63, 64, 6, 7 };
    // Zemine yapışık dekor (orta alan) — ağırlıklı: sıvı az, kablo/ızgara/döküntü/kapsül çok
    static readonly int[] Decals = { 8, 10, 11, 13, 13, 14, 14, 39, 39, 40, 40, 73, 73, 74, 74, 6, 7, 6, 7, 61, 62 };

    // Orta alan adacıkları: (hücre, obje) listesi. Obje: Prop ya da tek sprite indeksi.
    class Island { public (Vector2Int at, Prop prop)[] parts; public Island(params (Vector2Int, Prop)[] p) { parts = p; } }
    static Vector2Int C(int x, int y) => new Vector2Int(x, y);
    static readonly Prop TankA = new Prop(4f, Tall, new[] { 53, 54, 55, 56 }, new[] { 41, 42, 43, 44 });
    static readonly Prop TankB = new Prop(4f, Tall, new[] { 75, 76, 77, 78 }, new[] { 65, 66, 67, 68 });
    static readonly Prop TerminalA = Anim(5f, 15, 16, 17, 18);
    static readonly Prop TerminalB = Anim(5f, 21, 22, 23, 24);
    static readonly Island[] Islands =
    {
        // depo yığını
        new Island((C(0, 0), Static(49)), (C(1, 0), Static(51)), (C(0, 1), Static(50)), (C(2, 0), Static(0))),
        new Island((C(0, 0), Static(52)), (C(1, 0), Static(1)), (C(1, 1), Static(62)), (C(-1, 0), Static(63))),
        // çalışma istasyonu
        new Island((C(0, 0), TerminalA), (C(1, 0), TerminalB), (C(2, 0), Static(64)), (C(0, -1), Static(73))),
        new Island((C(0, 0), Static(19)), (C(1, 0), TerminalA), (C(-1, 0), Static(61)), (C(1, -1), Static(74))),
        // tank çifti
        new Island((C(0, 0), TankA), (C(1, 0), TankA), (C(2, 0), Static(62))),
        new Island((C(0, 0), TankB), (C(1, 0), TankA), (C(-1, 0), Static(63))),
        // bozuk makine + döküntü
        new Island((C(0, 0), Static(20)), (C(1, 0), Static(13)), (C(-1, 0), Static(14)), (C(0, -1), Static(40))),
        new Island((C(0, 0), Static(25)), (C(1, 0), Static(27)), (C(0, -1), Static(39)), (C(2, -1), Static(14))),
        // laboratuvar köşesi: tank + sızıntı + kapsüller
        new Island((C(0, 0), TankB), (C(1, 0), Static(8)), (C(1, 1), Static(6)), (C(2, 0), Static(7))),
        new Island((C(0, 0), new Prop(0f, Tall, new[] { 80 }, new[] { 79 })), (C(1, 0), Static(10)), (C(-1, 0), Static(6))),
    };

    [MenuItem("TopDownShooter/Ortam/Dekorları Yerleştir")]
    static void Build()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("Dekor", "Önce Play Mode'dan çık.", "Tamam"); return; }
        if (GameObject.FindGameObjectWithTag("Player") == null)
        {
            EditorUtility.DisplayDialog("Dekor", "Player bulunamadı. Önce MainGame sahnesini aç.", "Tamam");
            return;
        }

        sprites = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>()
            .Select(s => (s, idx: int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))))
            .ToDictionary(p => p.idx, p => p.s);
        if (sprites.Count == 0) { Debug.LogError($"[DecorBuilder] {Sheet} bulunamadı."); return; }

        var old = GameObject.Find("Decor");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var go = new GameObject("Decor");
        Undo.RegisterCreatedObjectUndo(go, "Dekorları Yerleştir");
        root = go.transform;
        used.Clear();
        rng = new System.Random(Seed);
        count = 0;

        var props = Group("WallProps");
        var clutter = Group("Clutter");
        var decals = Group("FloorDecals");

        // ---- Üst duvar boyunca: ekranlar, terminaller, tanklar
        for (int x = MinX + 1; x < MaxX - 1;)
        {
            var p = Pick(TopWall);
            // yüksek obje üst sıraya sığsın: alt hücresi MaxY-1'de
            int y = p.cells.Any(c => c.y > 0) ? MaxY - 1 : MaxY;
            if (x + p.Width - 1 >= MaxX) break;
            Place(p, new Vector2Int(x, y), props);
            x += p.Width + rng.Next(1, 4);
        }

        // ---- Yan duvarlar: seyrek tank / sandık
        foreach (int x in new[] { MinX, MaxX })
            for (int y = MinY + 3; y < MaxY - 3;)
            {
                var p = Pick(SideWall);
                Place(p, new Vector2Int(x, y), props);
                y += 2 + rng.Next(3, 7);
            }

        // ---- Alt duvar: depo kümeleri
        for (int x = MinX + 2; x < MaxX - 2;)
        {
            int n = rng.Next(1, 4);
            for (int i = 0; i < n; i++) PlaceStatic(Clutter[rng.Next(Clutter.Length)], new Vector2Int(x + i, MinY), clutter);
            x += n + rng.Next(4, 9);
        }

        // ---- Köşeler: yığınlar
        foreach (var corner in new[] { new Vector2Int(MinX, MinY), new Vector2Int(MaxX, MinY),
                                       new Vector2Int(MinX, MaxY), new Vector2Int(MaxX, MaxY) })
        {
            int dx = corner.x == MinX ? 1 : -1, dy = corner.y == MinY ? 1 : -1;
            for (int i = 0; i < 5; i++)
                PlaceStatic(Clutter[rng.Next(Clutter.Length)],
                            corner + new Vector2Int(dx * rng.Next(0, 3), dy * rng.Next(0, 3)), clutter);
        }

        // ---- Orta alan: adacıklar (her şablon bir kez, dağınık; aralarında geniş boşluk)
        var islands = Group("Islands");
        var islandCenters = new List<Vector2Int>();
        foreach (var isl in Islands.OrderBy(_ => rng.Next()))
        {
            for (int tries = 0; tries < 200; tries++)
            {
                var c = new Vector2Int(rng.Next(MinX + 5, MaxX - 4), rng.Next(MinY + 4, MaxY - 5));
                if (Vector2.Distance(c + new Vector2(0.5f, 0.5f), PlayerStart) < ClearRadius + 3f) continue;
                if (islandCenters.Any(o => Vector2Int.Distance(o, c) < 9f)) continue;
                if (!CanPlace(isl, c)) continue;
                foreach (var (at, prop) in isl.parts) Place(prop, c + at, islands);
                islandCenters.Add(c);
                break;
            }
        }

        // ---- Orta alan: zemine yapışık dekor, seyrek ve dağınık
        var decalCells = new List<Vector2Int>();
        for (int tries = 0; tries < 600 && decalCells.Count < 55; tries++)
        {
            var c = new Vector2Int(rng.Next(MinX + 2, MaxX - 1), rng.Next(MinY + 2, MaxY - 1));
            Vector2 world = c + new Vector2(0.5f, 0.5f);
            if (Vector2.Distance(world, PlayerStart) < ClearRadius) continue;
            if (decalCells.Any(o => Vector2Int.Distance(o, c) < 3.2f)) continue;
            if (islandCenters.Any(o => Vector2Int.Distance(o, c) < 3f)) continue;   // adacığın dibine yığılmasın
            if (used.Contains(c)) continue;
            decalCells.Add(c);
            var r = Renderer(decals, "Decal", sprites[Decals[rng.Next(Decals.Length)]], c, "Floor&Background", DecalOrder);
            r.flipX = rng.Next(2) == 0;
            r.color = new Color(1f, 1f, 1f, 0.9f);
        }

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log($"[DecorBuilder] {count} dekor objesi yerleştirildi (collider yok). Sahneyi kaydetmeyi unutma (Ctrl+S).");
    }

    static Prop Pick(Prop[] list) => list[rng.Next(list.Length)];

    // Adacığın tüm hücreleri boş ve arena içinde mi?
    static bool CanPlace(Island isl, Vector2Int at)
        => isl.parts.All(p => p.prop.cells.All(c => Inside(at + p.at + c) && !used.Contains(at + p.at + c)));

    static Transform Group(string name)
    {
        var g = new GameObject(name).transform;
        g.SetParent(root, false);
        return g;
    }

    // Çok hücreli (animasyonlu) obje. Hücrelerden biri doluysa hiç koyma.
    static void Place(Prop p, Vector2Int at, Transform parent)
    {
        if (p.cells.Any(c => used.Contains(at + c) || !Inside(at + c))) return;
        for (int i = 0; i < p.cells.Length; i++)
        {
            var frames = p.frames[i].Where(sprites.ContainsKey).Select(k => sprites[k]).ToArray();
            if (frames.Length == 0) continue;
            var r = Renderer(parent, "Prop", frames[0], at + p.cells[i], "Default", PropOrder);
            if (frames.Length > 1 && p.fps > 0f)
                r.gameObject.AddComponent<DecorAnimator>().Setup(frames, p.fps);
        }
    }

    static void PlaceStatic(int index, Vector2Int at, Transform parent)
    {
        if (!sprites.ContainsKey(index) || used.Contains(at) || !Inside(at)) return;
        if (Vector2.Distance(at + new Vector2(0.5f, 0.5f), PlayerStart) < ClearRadius) return;
        var r = Renderer(parent, "Clutter", sprites[index], at, "Default", PropOrder);
        r.flipX = rng.Next(2) == 0;
    }

    static bool Inside(Vector2Int c) => c.x >= MinX && c.x <= MaxX && c.y >= MinY && c.y <= MaxY;

    // Sadece SpriteRenderer: collider YOK
    static SpriteRenderer Renderer(Transform parent, string name, Sprite s, Vector2Int cell, string layer, int order)
    {
        used.Add(cell);
        count++;
        var go = new GameObject($"{name}_{s.name.Substring(s.name.LastIndexOf('_') + 1)}");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = s;
        r.sortingLayerName = layer;
        r.sortingOrder = order;
        return r;
    }
}
