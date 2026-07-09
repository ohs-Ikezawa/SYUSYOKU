using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class Player : MonoBehaviour
{
    [Header("ロープの設定")]
    [Tooltip("ロープのプレハブ")] public GameObject RopePiece;
    [Tooltip("ロープを持つ位置")] public GameObject RopePosition;
    [Tooltip("ロープの設置間隔")] public float RopeInterval;
    [Tooltip("ロープのたるみ")]   public float RopeSagging;

    [Header("地面判定")]
    [Tooltip("地面のレイヤー")] 　public LayerMask GroundLayer;
    [Tooltip("レイを出す高さ")]   public float GroundCheckHeight = 5.0f;
    [Tooltip("地面めり込み防止")] public float RopeGroundOffset = 0.05f;

    private List<GameObject> RopePieces = new List<GameObject>();

    SpringJoint currentJoint;
    public float RopeMaxDistance;
    public float RopeMinDistance;
    public float RopeSpring;
    public float RopeDamper;

    private Rigidbody playerRb;
    private Rigidbody connectedRb;

    // Start is called before the first frame update
    void Start()
    {
        playerRb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Return))
        {
            if(connectedRb == null)
            {
                //オブジェクトと接続を試みる
                TryConnected();
            }
            else
            {
                //オブジェクトを切り離す
                DisConnected();
            }
        }

        //ロープの更新
        if (connectedRb != null)
        {
            UpdateRopePieces();
        }
    }

    private void TryConnected()
    {
        ReachableArea[] Areas = FindObjectsOfType<ReachableArea>();

        ReachableArea NearestArea = null;
        float NearestDistance = Mathf.Infinity;

        //接続可能範囲を検索
        foreach(ReachableArea area in Areas)
        {
            //接続可能範囲外なら飛ばす
            if (!area.isInRange) continue;

            float Distance = Vector3.Distance(transform.position,area.transform.position);

            if(Distance < NearestDistance)
            {
                //接続可能
                NearestDistance = Distance;
                NearestArea = area;
            }
        }

        if (NearestArea == null) return;

        Rigidbody targetRb = NearestArea.GetComponentInParent<Rigidbody>();

        if (targetRb == null) return;

        ConnectObject(targetRb);
    }

    private void DisConnected()
    {
        if (currentJoint != null)
        {
            Destroy(currentJoint);
        }

        currentJoint = null;
        connectedRb = null;

        ClearRopePieces();
    }

    private void ConnectObject(Rigidbody rb)
    {
        connectedRb = rb;

        currentJoint = connectedRb.gameObject.AddComponent<SpringJoint>();
        currentJoint.connectedBody = playerRb;

        currentJoint.autoConfigureConnectedAnchor = false;
        currentJoint.anchor = Vector3.zero;
        currentJoint.connectedAnchor = Vector3.zero;

        currentJoint.maxDistance = RopeMaxDistance;
        currentJoint.minDistance = RopeMinDistance;
        currentJoint.spring = RopeSpring;
        currentJoint.damper = RopeDamper;

        CreateRopePieces();
    }

    private void CreateRopePieces()
    {
        if (RopePiece == null || connectedRb == null) return;

        //ロープの欠片が残ってた場合の保険
        ClearRopePieces();

        Vector3 start = GetRopePosition();
        Vector3 end   = connectedRb.transform.position;

        float distance = Vector3.Distance(start, end);

        //0割対策
        if (RopeInterval <= 0)
            RopeInterval = 0.2f;

        int count = Mathf.CeilToInt(distance / RopeInterval);

        //距離を間隔で割って個数を定めロープの見た目を生成
        for(int num = 0;num < count; num++)
        {
            GameObject piece = CreateRopePiece();
            RopePieces.Add(piece);
        }

        UpdateRopePieces();
    }

    private void ClearRopePieces()
    {
        foreach(GameObject piece in RopePieces)
        {
            Destroy(piece);
        }

        RopePieces.Clear();
    }

    private void UpdateRopePieces()
    {
        if (RopePiece == null || connectedRb == null) return;

        Vector3 start = GetRopePosition();
        Vector3 end = connectedRb.position;

        float distance = Vector3.Distance(start, end);

        //0割対策
        if (RopeInterval <= 0)
            RopeInterval = 0.2f;

        //ロープの最大値
        float ropeLength = Mathf.Max(distance, RopeMaxDistance);

        //ロープの欠片の数を決定
        int neededCount = Mathf.CeilToInt(ropeLength / RopeInterval);

        while (RopePieces.Count < neededCount)
        {
            GameObject piece = CreateRopePiece();
            RopePieces.Add(piece);
        }

        while (RopePieces.Count > neededCount)
        {
            GameObject lastPiece = RopePieces[RopePieces.Count - 1];
            RopePieces.RemoveAt(RopePieces.Count - 1);
            Destroy(lastPiece);
        }

        //ロープのあまり具合(近いほど大きくなる)
        float stack = ropeLength - distance;

        //どれくらいたるんでるか
        float segRate = Mathf.Clamp01(stack / ropeLength);
        float segAmount = segRate * RopeSagging;

        for (int i = 0; i < RopePieces.Count; i++)
        {
            float t = (i + 1f) / (RopePieces.Count + 1f);

            Vector3 position = GetSaggingRopePoint(start, end, t, segAmount);
            //ロープのめり込み補正
            position = ClampToGround(position);
            RopePieces[i].transform.position = position;

            float nextT = Mathf.Clamp01(t + 0.01f);
            float prevT = Mathf.Clamp01(t - 0.01f);

            Vector3 nextPos = GetSaggingRopePoint(start,end,nextT, segAmount);
            Vector3 prevPos = GetSaggingRopePoint(start,end,prevT, segAmount);

            Vector3 direction = nextPos - prevPos;

            if (direction.sqrMagnitude > 0.000001f)
            {
                RopePieces[i].transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }

    private GameObject CreateRopePiece()
    {
        if (RopePosition != null)
        {
            return Instantiate(RopePiece, RopePosition.transform);
        }

        return Instantiate(RopePiece);
    }

    private Vector3 GetRopePosition()
    {
        //RopePositionが設定されていない場合用の自動設定関数
        if (RopePosition != null)
        {
            return RopePosition.transform.position;
        }

        return transform.position;
    }

    private Vector3 GetSaggingRopePoint(Vector3 start,Vector3 end,float t,float Seg)
    {
        Vector3 LinePos = Vector3.Lerp( start , end , t );

        float curve = Mathf.Sin(t * Mathf.PI);

        return LinePos + Vector3.down * curve * Seg;
    }

    private Vector3 ClampToGround(Vector3 position)
    {
        //地面めり込みを防止する関数
        Vector3 rayStart = position + Vector3.up * GroundCheckHeight;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, GroundCheckHeight * 2f, GroundLayer))
        {
            //ロープが地面レイヤーの物体より下にあるなら上に補正する
            if (position.y < hit.point.y + RopeGroundOffset)
            {
                position.y = hit.point.y + RopeGroundOffset;
            }
        }

        return position;
    }
}

