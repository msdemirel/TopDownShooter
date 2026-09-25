using UnityEngine;

// Ortam dekoru için döngüsel sprite animasyonu (terminal ekranları, tank kabarcıkları...).
// Her obje rastgele bir karede başlar: yan yana duran aynı objeler senkron yanıp sönmesin.
// Sadece görsel: collider yok, oyunu etkilemez. Kurulum: TopDownShooter > Ortam > Dekorları Yerleştir
[RequireComponent(typeof(SpriteRenderer))]
public class DecorAnimator : MonoBehaviour
{
    [SerializeField] Sprite[] frames;
    [SerializeField] float fps = 6f;

    SpriteRenderer sr;
    float phase;

    public void Setup(Sprite[] f, float framesPerSecond)
    {
        frames = f;
        fps = framesPerSecond;
    }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        phase = Random.value * 10f;
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        int i = (int)((Time.time + phase) * fps) % frames.Length;
        if (sr.sprite != frames[i]) sr.sprite = frames[i];
    }
}
