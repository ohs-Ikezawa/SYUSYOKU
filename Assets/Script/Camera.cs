using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("追従対象")]
    public Transform target;

    [Header("カメラ追従")]
    public float cameraDistance = 8.0f;
    public float cameraHeight = 1.5f;
    public float cameraFollowSpeed = 12.0f;

    [Header("上下視点")]
    public float mouseSensitivity = 2.0f;
    public float minPitch = -45.0f;
    public float maxPitch = 70.0f;

    private float pitch;

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        UpdatePitch();
        FollowBehindTarget();
    }

    void UpdatePitch()
    {
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    void FollowBehindTarget()
    {
        Vector3 pivot = target.position + Vector3.up * cameraHeight;
        Vector3 backDirection = -target.forward;
        Vector3 cameraOffset = Quaternion.AngleAxis(pitch, target.right) * backDirection * cameraDistance;
        Vector3 targetPosition = pivot + cameraOffset;

        float followRate = 1.0f - Mathf.Exp(-cameraFollowSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, followRate);
        transform.rotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
    }
}
