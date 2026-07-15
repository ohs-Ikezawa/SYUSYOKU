using UnityEngine;

public class RopePiece : MonoBehaviour
{
    [Header("Ground Collision")]
    [Tooltip("ロープの欠片がめり込まない地面レイヤーです。")]
    public LayerMask GroundLayer = 1 << 7;

    [Tooltip("地面を探すために、欠片の上から判定を開始する高さです。")]
    public float GroundCheckHeight = 2f;

    [Tooltip("地面との判定に使用する欠片の半径です。")]
    public float GroundRadius = 0.05f;

    [Tooltip("地面から少し浮かせる距離です。")]
    public float GroundOffset = 0.01f;

    public void ResolveGroundPenetration()
    {
        if (GroundLayer.value == 0 || GroundCheckHeight <= 0f)
        {
            return;
        }

        Vector3 position = transform.position;
        Vector3 rayStart = position + Vector3.up * GroundCheckHeight;
        float rayDistance = GroundCheckHeight + Mathf.Max(0f, GroundRadius);

        if (!Physics.Raycast(
                rayStart,
                Vector3.down,
                out RaycastHit hit,
                rayDistance,
                GroundLayer,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        float minimumY = hit.point.y + Mathf.Max(0f, GroundRadius) + GroundOffset;

        if (position.y < minimumY)
        {
            position.y = minimumY;
            transform.position = position;
        }
    }
}
