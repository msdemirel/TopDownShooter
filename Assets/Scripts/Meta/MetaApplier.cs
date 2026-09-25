using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Mağazada alınan kalıcı upgrade'leri oyun başında oyuncuya uygular.
// Kendini kurar: 'Player' tag'li obje olan her sahne yüklendiğinde bir tane oluşur
// (sahneye elle eklemek gerekmez). Ana menüde Player olmadığı için hiçbir şey yapmaz.
public class MetaApplier : MonoBehaviour
{
    const float ReviveInvulnerability = 2f;
    static readonly Color ReviveColor = new Color(1f, 0.85f, 0.4f, 1f);

    PlayerContext ctx;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded += (scene, mode) => TryCreate();
        TryCreate();   // ilk sahne (Play'e basılan sahne) için sceneLoaded tetiklenmez
    }

    static void TryCreate()
    {
        if (FindAnyObjectByType<MetaApplier>() != null) return;
        if (GameObject.FindGameObjectWithTag("Player") == null) return;
        new GameObject("MetaApplier").AddComponent<MetaApplier>();
    }

    // Start: diğer bileşenlerin Awake/Start'ı (can, hız, silahlar) kurulduktan sonra uygula
    IEnumerator Start()
    {
        yield return null;   // PlayerWeapons.Start başlangıç silahını taksın, HUD'lar abone olsun

        var p = GameObject.FindGameObjectWithTag("Player");
        var catalog = MetaProgress.Catalog;
        if (p == null || catalog == null) yield break;

        ctx = new PlayerContext(p);
        foreach (var u in catalog.upgrades)
            if (u != null && u.Level > 0) Apply(u.effect, u.TotalValue);

        if (ctx.health != null) ctx.health.OnRevived += HandleRevived;
    }

    void OnDestroy()
    {
        if (ctx != null && ctx.health != null) ctx.health.OnRevived -= HandleRevived;
    }

    void Apply(MetaEffect effect, float v)
    {
        switch (effect)
        {
            case MetaEffect.MaxHealth:
                if (ctx.health != null) ctx.health.SetMaxHealth(ctx.health.Max + v, true);
                break;
            case MetaEffect.Damage:
                if (ctx.weapons != null) ctx.weapons.AddDamageMultiplier(v);
                break;
            case MetaEffect.MoveSpeed:
                if (ctx.movement != null) ctx.movement.MoveSpeed *= 1f + v;
                break;
            case MetaEffect.ExpGain:
                if (ctx.stats != null) ctx.stats.ExpMultiplier += v;
                break;
            case MetaEffect.StartMoney:
                if (ctx.stats != null) ctx.stats.AddMoney(Mathf.RoundToInt(v));
                break;
            case MetaEffect.FreeRerolls:
                var um = FindAnyObjectByType<UpgradeManager>();
                if (um != null) um.AddFreeRerolls(Mathf.RoundToInt(v));
                break;
            case MetaEffect.MagnetRange:
                if (ctx.collector != null) ctx.collector.MagnetRange *= 1f + v;
                break;
            case MetaEffect.ExtraLife:
                if (ctx.health != null) ctx.health.ExtraLives += Mathf.RoundToInt(v);
                break;
        }
    }

    // Second Chance: kısa dokunulmazlık + altın patlama efekti + bildirim
    void HandleRevived(Health h)
    {
        StartCoroutine(InvulnerableFor(h, ReviveInvulnerability));

        Vector3 pos = h.transform.position;
        SkillVfx.Flash(pos, ReviveColor, 3f, 0.4f);
        SkillVfx.SparkBurst(pos, ReviveColor, 24, 7f, 0.35f, 0.5f);
        VfxSprite.Spawn(VfxSprites.Ring, pos, SkillVfx.A(ReviveColor, 0.8f), 0.45f, SkillVfx.TopOrder)
                 .Scale(0.5f, 4f, true);

        var toast = FindAnyObjectByType<UpgradeToastUI>();
        if (toast != null) toast.Show("<color=#FFCD75>SECOND CHANCE!</color>");
    }

    static IEnumerator InvulnerableFor(Health h, float seconds)
    {
        h.Invulnerable = true;
        yield return new WaitForSeconds(seconds);
        if (h != null) h.Invulnerable = false;
    }
}
