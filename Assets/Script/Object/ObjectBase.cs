using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectBase : MonoBehaviour
{
    [Header("オブジェクトの設定")]
    [Tooltip("オブジェクトの大きさ")] 　public Vector3 Size;
    [Tooltip("オブジェクトの重さ:オブジェクトを引きずるときに使います")] public float Weight;
    [Tooltip("オブジェクトの固さ:オブジェクトが衝突したときに使います")] public float Hardness;

    [Header("スイング関連の設定")]
    [Tooltip("スイング速度の設定")]
    public AnimationCurve SwingCurve = new AnimationCurve();

    [Tooltip("スイング最大速度の設定")]
    public float MaxSpeed;
    [Tooltip("跳ね返り可能回数")] 　　　public float BounceCnt;
   

    private void Awake()
    {
        transform.localScale = Size;
    }

}
