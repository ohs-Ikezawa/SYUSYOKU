using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("移動パラメーター")]

    [Tooltip("移動速度")]
    public float speed = 3.0f;

    [Tooltip("向きを変える速度")]
    public float rotateSpeed = 10.0f;

    [Tooltip("デッドゾーン")]
    public float deadzone = 0.1f;

    private Rigidbody rb;
    private Vector3 input;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        ReadInput();
    }

    private void FixedUpdate()
    {
        Move();
        Rotate();
    }

    private void ReadInput()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = new Vector3(x, 0.0f, z);

        if (move.sqrMagnitude <= deadzone * deadzone)
        {
            input = Vector3.zero;
        }
        else
        {
            input = move.normalized;
        }
    }

    private void Move()
    {
        Vector3 moveAmount =
            input * speed * Time.fixedDeltaTime;

        rb.MovePosition(
            rb.position + moveAmount
        );
    }

    private void Rotate()
    {
        if (input.sqrMagnitude <= 0.0f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(input, Vector3.up);

        Quaternion nextRotation =
            Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                rotateSpeed * Time.fixedDeltaTime
            );

        rb.MoveRotation(nextRotation);
    }
}