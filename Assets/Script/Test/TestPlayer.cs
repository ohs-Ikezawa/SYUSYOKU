using UnityEngine;

[RequireComponent(typeof(RopeGenerator))]
public class TestPlayer : MonoBehaviour
{
    [Tooltip("Playerが使用するロープ処理です。同じGameObjectにある場合は自動取得します。")]
    [SerializeField] private RopeGenerator ropeGenerator;

    private void Awake()
    {
        if (ropeGenerator == null)
        {
            ropeGenerator = GetComponent<RopeGenerator>();
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) && ropeGenerator != null)
        {
            ropeGenerator.StartSwing();
        }
    }
}
