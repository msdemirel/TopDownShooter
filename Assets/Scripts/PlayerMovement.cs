using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Tuning")]
    [SerializeField] float moveSpeed = 6f;

    // Upgrade'ler hızı buradan artırır. Alt sınır: hız 0'a düşüp oyuncuyu kilitlemesin.
    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0.5f, value);
    }

    [Header("Input")]
    [SerializeField] InputActionReference moveAction;

    Rigidbody2D rb;
    SpriteRenderer sr;
    Vector2 moveInput;
    Animator animator;

    // ---- Dash (PlayerSkills tetikler) ----
    // Dash sırasında normal hareket devre dışı kalır; sabit hızla ileri atılır.
    Vector2 lastMoveDir = Vector2.right;  // dururken dash atarsa son baktığı yöne gitsin
    Vector2 dashVelocity;
    float dashTimeLeft;

    // Hareket yönüne (dururken son yöne) doğru kısa süreli atılma başlatır.
    // Kullanılan yönü döndürür (dash izi efektini o yöne çevirmek için).
    public Vector2 StartDash(float speed, float duration)
    {
        Vector2 dir = moveInput.sqrMagnitude > 0.01f ? moveInput.normalized : lastMoveDir;
        dashVelocity = dir * speed;
        dashTimeLeft = duration;
        return dir;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    void OnEnable() { moveAction.action.Enable(); }
    void OnDisable() { moveAction.action.Disable(); }

    void Update()
    {
        // Oyun donukken (pause menüsü / upgrade paneli) girdi işlenmesin:
        // karakter yerinde yön değiştirmesin. (PlayerSkills de aynı kontrolü yapar.)
        if (Time.timeScale == 0f) return;

        moveInput = moveAction.action.ReadValue<Vector2>();

        if (moveInput.sqrMagnitude > 0.01f)
            lastMoveDir = moveInput.normalized;

        if (moveInput.x > 0.01f) sr.flipX = false;
        else if (moveInput.x < -0.01f) sr.flipX = true;

        animator.SetFloat("speed", moveInput.magnitude);
    }

    void FixedUpdate()
    {
        // Dash süresi boyunca kontrol dash'te; bitince normal harekete dönülür
        if (dashTimeLeft > 0f)
        {
            dashTimeLeft -= Time.fixedDeltaTime;
            rb.linearVelocity = dashVelocity;
            return;
        }

        rb.linearVelocity = moveInput * moveSpeed;
    }
}