using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private Rigidbody2D rb;
    private Animator animator;
    private Vector2 moveInput;
    private Vector2 lastDirection = Vector2.down;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");

        bool isMoving = moveInput.sqrMagnitude > 0.01f;

        if (isMoving)
        {
            Vector2 normalized = moveInput.normalized;
            lastDirection = normalized;

            animator.SetFloat(MoveXHash, normalized.x);
            animator.SetFloat(MoveYHash, normalized.y);
        }
        else
        {
            animator.SetFloat(MoveXHash, lastDirection.x);
            animator.SetFloat(MoveYHash, lastDirection.y);
        }

        animator.SetBool(IsMovingHash, isMoving);

        if (Input.GetMouseButtonDown(0))
        {
            animator.SetTrigger(AttackHash);
        }
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = moveInput.normalized * moveSpeed;
    }
}