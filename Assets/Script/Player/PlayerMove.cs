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

    [Header("Weightによる移動速度補正")]

    [Tooltip("接続中のObjectを取得するロープ処理です。同じGameObjectにある場合は自動取得します。")]
    [SerializeField] private RopeGenerator ropeGenerator;

    [Tooltip("移動速度補正の基準にするMediumObjectです。未指定の場合はシーンから自動取得します。")]
    [SerializeField] private MediumObject mediumWeightReference;

    [Tooltip("MediumObjectを取得できない場合に使用する基準Weightです。")]
    [SerializeField, Min(0.01f)] private float fallbackMediumWeight = 5f;

    [Tooltip("基準Weightを1超えるごとに、基本移動速度から引く値です。")]
    [SerializeField, Min(0f)] private float speedDebuffPerWeight = 1f;

    private Rigidbody rb;
    private Vector3 input;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (ropeGenerator == null)
        {
            ropeGenerator = GetComponent<RopeGenerator>();
        }

        if (mediumWeightReference == null)
        {
            mediumWeightReference = FindObjectOfType<MediumObject>();
        }
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
        float currentMoveSpeed = GetWeightAdjustedSpeed();
        Vector3 moveAmount =
            input * currentMoveSpeed * Time.fixedDeltaTime;

        rb.MovePosition(
            rb.position + moveAmount
        );
    }

    private float GetWeightAdjustedSpeed()
    {
        if (ropeGenerator == null || ropeGenerator.ConnectedObject == null)
        {
            return speed;
        }

        float mediumWeight = mediumWeightReference != null &&
                             mediumWeightReference.Weight > 0f
            ? mediumWeightReference.Weight
            : fallbackMediumWeight;
        float connectedWeight = ropeGenerator.ConnectedObject.Weight;

        if (connectedWeight <= mediumWeight || connectedWeight <= 0f)
        {
            return speed;
        }

        float weightDifference = connectedWeight - mediumWeight;
        float speedDebuff = weightDifference * speedDebuffPerWeight;

        return Mathf.Max(0f, speed - speedDebuff);
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
