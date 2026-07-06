using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("移動パラメーター")]
    [Tooltip("移動速度")]
    public float speed = 3.0f;
    [Tooltip("デッドゾーン")]
    public float deadzone = 0.1f;

    Rigidbody rb;
    Vector3 input;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        ReadInput();
    }

    private void FixedUpdate()
    {
        Move();
    }

    private void ReadInput()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = new Vector3(x, 0, z);

        if(move.sqrMagnitude <= deadzone)
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
        Vector3 moveAmount = input * speed * Time.fixedDeltaTime;

        rb.MovePosition(rb.position + moveAmount);
    }
}
