using System.Collections.Generic;
using UnityEngine;

// Oyuncunun animasyonlarını karakterin sprite sheet'ine çevirir. Animator her karede orijinal
// sheet'ten bir sprite atar; LateUpdate'te o karenin KONUMUNDAKİ (+ cellOffset) hedef sprite'la
// değiştiririz. Böylece her karakter için ayrı animasyon/controller gerekmez.
// MetaApplier oyun başında ekler.
[RequireComponent(typeof(SpriteRenderer))]
public class CharacterSkin : MonoBehaviour
{
    SpriteRenderer sr;
    CharacterData character;
    readonly Dictionary<Vector2Int, Sprite> byCell = new Dictionary<Vector2Int, Sprite>();
    readonly Dictionary<Sprite, Sprite> cache = new Dictionary<Sprite, Sprite>();
    Sprite lastApplied;   // bizim atadığımız sprite: animator yeni kare atamadıysa tekrar kaydırma

    public void Setup(CharacterData c)
    {
        sr = GetComponent<SpriteRenderer>();
        character = c;
        byCell.Clear();
        cache.Clear();
        if (c == null || c.skinSprites == null) return;
        foreach (var s in c.skinSprites)
            if (s != null) byCell[Cell(s)] = s;
        Swap();
    }

    public static Vector2Int Cell(Sprite s) => new Vector2Int(Mathf.RoundToInt(s.rect.x), Mathf.RoundToInt(s.rect.y));

    // Animator Update'te sprite'ı atar; biz ondan sonra değiştiririz
    void LateUpdate() => Swap();

    void Swap()
    {
        if (sr == null || byCell.Count == 0) return;
        Sprite cur = sr.sprite;
        if (cur == null || cur == lastApplied) return;   // animator bu karede sprite yazmadı

        // Not: robot aynı sheet'te (kayma ile) durduğu için "hedef sheet'ten mi" diye doku
        // karşılaştırması yapılamaz; son atadığımız sprite'ı hatırlamak yeterli.
        if (!cache.TryGetValue(cur, out var target))
        {
            byCell.TryGetValue(character.MapCell(Cell(cur)), out target);
            cache[cur] = target;
        }
        if (target != null && target != cur)
        {
            sr.sprite = target;
            lastApplied = target;
        }
    }
}
