using UnityEngine;

public class ReachableArea : MonoBehaviour
{
    public bool isInRange { get; private set; }

    [Header("判定したい対象のタグ")]
    public string targetTag = "Player";

    [Header("デバッグ表示")]
    public bool showGizmos = true;
    public Color gizmoColorF = new Color(0f, 1f, 0f, 0.25f);
    public Color gizmoColorT = new Color(1f, 0f, 0f, 0.25f);

    private SphereCollider sphereCollider;

    private void Awake()
    {
        sphereCollider = GetComponent<SphereCollider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            isInRange = true;

            Transform RopeTransform = other.transform.Find("RopeHoldPoint");

            if(RopeTransform == null)
            {
                RopeTransform = other.transform;
            }

            if(Input.GetKeyDown(KeyCode.Return))
            {
                
            }

        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            isInRange = false;
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        SphereCollider col = GetComponent<SphereCollider>();
        if (col == null) return;

        Gizmos.color = isInRange ? gizmoColorT : gizmoColorF;

        Vector3 worldCenter = transform.TransformPoint(col.center);

        float radius = col.radius * Mathf.Max(
            transform.lossyScale.x,
            transform.lossyScale.y,
            transform.lossyScale.z
        );

        Gizmos.DrawWireSphere(worldCenter, radius);
    }
}