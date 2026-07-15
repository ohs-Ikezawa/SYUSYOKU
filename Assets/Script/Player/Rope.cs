using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("ロープの設定")]
    [Tooltip("ロープ1個分の見た目として生成するプレハブです。")] public GameObject RopePiece;
    [Tooltip("ロープの親になる持ち手です。子に RopeAnchor がある場合は、その位置をロープの根元として使います。")] public GameObject RopePosition;
    [Tooltip("ロープの欠片同士の間隔です。小さいほど滑らかになりますが、生成数と計算量が増えます。")] public float RopeInterval = 0.2f;
    [Tooltip("ロープ全体の固定長です。ロープはこの長さを超えないように補正されます。")] public float RopeMaxDistance = 3.0f;
    [Tooltip("スイング中にロープ先端の回転速度をオブジェクトへ伝える強さです。大きいほど振り回す力が強くなります。")] public float RopeSwingForce = 4.0f;
    [Tooltip("スイング中に持ち手の動きをロープ全体へ伝える強さです。大きいほど根元の動きがロープ全体へ強く伝わります。")] public float RopeWholeSwingForce = 1.4f;
    [Tooltip("スイングしていない時に、プレイヤー移動で生まれた力をロープへ伝える強さです。オブジェクトへ直接ではなく、ロープの欠片へ順番に伝わります。")] public float RopeMoveTransmitPower = 0.35f;
    [Tooltip("ロープの欠片へ伝わる力が、先端へ行くほどどれくらい弱くなるかです。小さいほど先端に届く力が弱くなります。")] public float RopeForceDecay = 0.85f;
    [Tooltip("オブジェクトの速度を毎フレーム弱める割合です。1に近いほど止まりにくく、0に近いほどすぐ減速します。")] public float RopeObjectDamping = 0.94f;
    [Tooltip("ロープに繋がったオブジェクトの最大速度です。暴れすぎる場合はこの値を下げます。")] public float RopeObjectMaxSpeed = 35.0f;

    [Header("スイングの設定")]
    [Tooltip("次のクリックで右方向に振るかを表します。Always Right Hand Swing がオフの時はクリックごとに反転します。")] public bool SwingRight = false;
    [Tooltip("オンにすると左右交互ではなく、クリックするたび常に右手方向へ180度ずつ回します。")] public bool AlwaysRightHandSwing = false;
    [Tooltip("現在スイング処理中かを表示します。基本的には確認用で、プレイ中に自動で切り替わります。")] public bool IsSwing = false;
    [Tooltip("RopeAnchor が RopePosition の周りを回る横方向の半径です。大きいほど大振りになります。")] public float RopeSwingRadius = 0.9f;
    [Tooltip("RopeAnchor が弧を描く時の前後方向の半径です。大きいほど円に近く、小さいほど横振りに近くなります。")] public float RopeSwingArcDepth = 0.9f;
    [Tooltip("クリック後、180度振り終わるまでの時間です。小さいほど素早く強いスイングになります。")] public float RopeSwingTime = 0.2f;

    [Header("鞭のしなり設定")]
    [Tooltip("スイングの弧運動をロープへ波として伝える強さです。大きいほど鞭のようにしなります。")] public float RopeWhipSpeed = 10f;
    [Tooltip("ロープ先端へスイングが伝わる遅れです。大きいほど先端が遅れてついてきます。")] public float RopeWhipDelay = 0.3f;

    private readonly List<GameObject> RopePieces = new List<GameObject>();
    private readonly List<Vector3> RopePositions = new List<Vector3>();
    private readonly List<Vector3> PrevRopePositions = new List<Vector3>();

    [Tooltip("ロープの長さを固定する補正回数です。大きいほど伸びにくくなりますが、計算量が増えます。")] public int RopeConstraintIteration = 20;

    [Header("地面判定")]
    [Tooltip("ロープの欠片が埋まらないように判定する地面レイヤーです。")] public LayerMask GroundLayer;
    [Tooltip("地面判定用のレイを上から下へ飛ばす高さです。地面を検出しにくい場合は上げます。")] public float GroundCheckHeight = 3.0f;
    [Tooltip("地面に当たった時、ロープを地面から少し浮かせる高さです。")] public float RopeGroundOffset = 0.05f;

    private Transform parent;
    private Transform ropeAnchor;

    private Rigidbody connectedRb;

    private float swingStartAngle;
    private float swingTargetAngle;
    private float currentSwingAngle;
    private float currentSwingDirection = -1f;
    private float SwingTimer;

    private Vector3 previousRopeAnchorPosition;

    void Start()
    {
        SetupRopeAnchor();

        if (RopePosition != null)
        {
            currentSwingAngle = 0f;
            ApplyRopeSwingPosition();
            previousRopeAnchorPosition = GetRopeAnchorPosition();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (connectedRb == null)
            {
                //接続を試みる
                TryConnected();
            }
            else
            {
                //接続の解除
                DisConnected();
            }
        }

        if (Input.GetMouseButtonDown(0) && connectedRb != null)
        {
            StartRopeSwing();
        }

        if (connectedRb != null)
        {
            UpdateRopeSwing();
            UpdateRopePieces();
        }
    }

    void FixedUpdate()
    {
        if (connectedRb != null)
        {
            UpdateRopeConstraint();
        }
    }

    private void TryConnected()
    {
        ReachableArea[] Areas = FindObjectsOfType<ReachableArea>();

        ReachableArea NearestArea = null;
        float NearestDistance = Mathf.Infinity;

        foreach (ReachableArea area in Areas)
        {
            if (!area.isInRange) continue;

            float Distance = Vector3.Distance(transform.position, area.transform.position);

            if (Distance < NearestDistance)
            {
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
        connectedRb = null;
        ClearRopePieces();

        RopePositions.Clear();
        PrevRopePositions.Clear();
    }

    private void ConnectObject(Rigidbody rb)
    {
        connectedRb = rb;
        SetupRopeAnchor();

        CreateRopePoints();
        CreateRopePieces();
    }

    private void SetupRopeAnchor()
    {
        if (RopePosition == null)
        {
            ropeAnchor = transform;
            return;
        }

        Transform foundAnchor = RopePosition.transform.Find("RopeAnchor");
        ropeAnchor = foundAnchor != null ? foundAnchor : RopePosition.transform;
    }

    private void CreateRopePoints()
    {
        RopePositions.Clear();
        PrevRopePositions.Clear();

        FixRopeInterval();
        FixRopeLength();

        Vector3 start = GetRopeAnchorPosition();
        Vector3 end = connectedRb.position;

        Vector3 direction = end - start;

        if (direction.sqrMagnitude <= 0.000001f)
        {
            direction = GetRopeForward();
        }
        else
        {
            direction.Normalize();
        }

        int count = Mathf.CeilToInt(RopeMaxDistance / RopeInterval) + 1;

        for (int i = 0; i < count; i++)
        {
            Vector3 position = start + direction * RopeInterval * i;

            RopePositions.Add(position);
            PrevRopePositions.Add(position);
        }
    }

    private void CreateRopePieces()
    {
        if (RopePiece == null || connectedRb == null) return;

        ClearRopePieces();
        FixRopeInterval();

        int count = Mathf.CeilToInt(RopeMaxDistance / RopeInterval);
        parent = RopePosition != null ? RopePosition.transform : transform;

        for (int num = 0; num < count; num++)
        {
            GameObject piece = Instantiate(RopePiece, parent);

            piece.transform.localPosition = Vector3.forward * RopeInterval;
            piece.transform.localRotation = Quaternion.identity;
            piece.transform.localScale = Vector3.one;

            RopePieces.Add(piece);
            parent = piece.transform;
        }

        UpdateRopePieces();
    }

    private void ClearRopePieces()
    {
        foreach (GameObject piece in RopePieces)
        {
            Destroy(piece);
        }

        RopePieces.Clear();
        parent = RopePosition != null ? RopePosition.transform : transform;
    }

    private void UpdateRopePieces()
    {
        if (RopePiece == null || connectedRb == null) return;

        FixRopeInterval();
        FixRopeLength();

        int neededCount = Mathf.CeilToInt(RopeMaxDistance / RopeInterval);

        while (RopePieces.Count < neededCount)
        {
            GameObject piece = CreateRopePiece(parent);

            piece.transform.localPosition = Vector3.forward * RopeInterval;
            piece.transform.localRotation = Quaternion.identity;
            piece.transform.localScale = Vector3.one;

            RopePieces.Add(piece);
            parent = piece.transform;
        }

        while (RopePieces.Count > neededCount)
        {
            GameObject lastPiece = RopePieces[RopePieces.Count - 1];
            RopePieces.RemoveAt(RopePieces.Count - 1);
            Destroy(lastPiece);
        }

        if (RopePieces.Count > 0)
        {
            parent = RopePieces[RopePieces.Count - 1].transform;
        }
        else
        {
            parent = RopePosition != null ? RopePosition.transform : transform;
        }

        for (int i = 0; i < RopePieces.Count; i++)
        {
            if (i + 1 >= RopePositions.Count) break;

            Vector3 current = RopePositions[i];
            Vector3 next = RopePositions[i + 1];

            RopePieces[i].transform.position = next;

            Vector3 direction = next - current;

            if (direction.sqrMagnitude > 0.000001f)
            {
                RopePieces[i].transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }
    }

    private GameObject CreateRopePiece(Transform parentTransform)
    {
        if (parentTransform != null)
        {
            return Instantiate(RopePiece, parentTransform);
        }

        return Instantiate(RopePiece);
    }

    private void UpdateRopeConstraint()
    {
        if (RopePositions.Count == 0) return;
        if (connectedRb == null) return;

        FixRopeInterval();

        Vector3 anchorPosition = GetRopeAnchorPosition();
        Vector3 anchorVelocity = (anchorPosition - previousRopeAnchorPosition) / Time.fixedDeltaTime;
        Vector3 objectStartPosition = connectedRb.position;
        Vector3 objectPosition = objectStartPosition;
        int ropeTipIndex = RopePositions.Count - 1;
        float swingPowerRate = IsSwing ? 1f : 0f;
        float anchorObjectDistance = Vector3.Distance(anchorPosition, connectedRb.position);
        bool isRopeTight = IsSwing || anchorObjectDistance >= RopeMaxDistance;

        //ロープ点を慣性で動かす
        for (int i = 1; i < RopePositions.Count; i++)
        {
            Vector3 current = RopePositions[i];
            Vector3 velocity = RopePositions[i] - PrevRopePositions[i];

            if (!IsSwing)
            {
                velocity *= Mathf.Clamp01(RopeObjectDamping);
            }

            PrevRopePositions[i] = current;

            RopePositions[i] = current + velocity + Physics.gravity * Time.fixedDeltaTime * Time.fixedDeltaTime;

            if (swingPowerRate > 0.01f)
            {
                float weight = (float)i / (RopePositions.Count - 1);
                RopePositions[i] += anchorVelocity * RopeWholeSwingForce * weight * swingPowerRate * Time.fixedDeltaTime;
                RopePositions[i] += GetDelayedSwingArcVelocity(i, RopePositions.Count) * weight * swingPowerRate * Time.fixedDeltaTime;
            }
            else
            {
                RopePositions[i] += GetMoveTransmitVelocity(anchorVelocity, i) * Time.fixedDeltaTime;
            }

            RopePositions[i] = ClampToGround(RopePositions[i]);
        }

        //根元は必ず持ち手に固定
        RopePositions[0] = anchorPosition;
        ClampRopeToMaxLength(anchorPosition);

        //隣同士の距離を RopeInterval に戻す
        for (int iteration = 0; iteration < RopeConstraintIteration; iteration++)
        {
            RopePositions[0] = anchorPosition;

            for (int i = 0; i < RopePositions.Count - 1; i++)
            {
                Vector3 current = RopePositions[i];
                Vector3 next = RopePositions[i + 1];

                Vector3 diff = next - current;
                float distance = diff.magnitude;

                if (distance <= 0.000001f) continue;

                Vector3 direction = diff / distance;
                float error = distance - RopeInterval;

                if (i == 0)
                {
                    RopePositions[i + 1] -= direction * error;
                    RopePositions[i + 1] = ClampToGround(RopePositions[i + 1]);
                }
                else
                {
                    RopePositions[i] += direction * error * 0.5f;
                    RopePositions[i + 1] -= direction * error * 0.5f;

                    RopePositions[i] = ClampToGround(RopePositions[i]);
                    RopePositions[i + 1] = ClampToGround(RopePositions[i + 1]);
                }
            }

            ClampRopeToMaxLength(anchorPosition);

            RopePositions[ropeTipIndex] = ClampToGround(RopePositions[ropeTipIndex]);
        }

        if (!IsSwing)
        {
            DampenRopeVelocity();
        }

        Vector3 ropeTipPosition = RopePositions[ropeTipIndex];
        Vector3 prevRopeTipPosition = PrevRopePositions[ropeTipIndex];
        Vector3 ropeTipVelocity = (ropeTipPosition - prevRopeTipPosition) / Time.fixedDeltaTime;
        Vector3 dampingVelocity = connectedRb.velocity * Mathf.Clamp01(RopeObjectDamping);
        Vector3 nextVelocity = dampingVelocity;

        if (isRopeTight)
        {
            objectPosition = ropeTipPosition;
            Vector3 correctionVelocity = (objectPosition - objectStartPosition) / Time.fixedDeltaTime;
            nextVelocity += correctionVelocity;
        }

        if (IsSwing)
        {
            nextVelocity += GetSwingTangentVelocity(ropeTipVelocity, ropeTipPosition) * RopeSwingForce;
        }

        if (nextVelocity.sqrMagnitude > RopeObjectMaxSpeed * RopeObjectMaxSpeed)
        {
            nextVelocity = nextVelocity.normalized * RopeObjectMaxSpeed;
        }

        if (isRopeTight)
        {
            connectedRb.MovePosition(objectPosition);
        }

        connectedRb.velocity = nextVelocity;
        previousRopeAnchorPosition = anchorPosition;
    }

    private Vector3 GetRopeAnchorPosition()
    {
        if (ropeAnchor != null)
        {
            return ropeAnchor.position;
        }

        if (RopePosition != null)
        {
            return RopePosition.transform.position;
        }

        return transform.position;
    }

    private Vector3 GetRopeForward()
    {
        if (RopePosition != null)
        {
            return RopePosition.transform.forward;
        }

        return transform.forward;
    }

    private void ClampRopeToMaxLength(Vector3 anchorPosition)
    {
        FixRopeLength();

        for (int i = 1; i < RopePositions.Count; i++)
        {
            Vector3 fromAnchor = RopePositions[i] - anchorPosition;
            float maxDistance = RopeInterval * i;

            if (fromAnchor.sqrMagnitude <= maxDistance * maxDistance) continue;

            RopePositions[i] = anchorPosition + fromAnchor.normalized * maxDistance;
            RopePositions[i] = ClampToGround(RopePositions[i]);
        }
    }

    private Vector3 GetMoveTransmitVelocity(Vector3 anchorVelocity, int ropeIndex)
    {
        float decay = Mathf.Pow(Mathf.Clamp01(RopeForceDecay), ropeIndex - 1);

        return anchorVelocity * RopeMoveTransmitPower * decay;
    }

    private void DampenRopeVelocity()
    {
        float damping = Mathf.Clamp01(RopeObjectDamping);

        for (int i = 1; i < RopePositions.Count; i++)
        {
            Vector3 velocity = RopePositions[i] - PrevRopePositions[i];
            PrevRopePositions[i] = RopePositions[i] - velocity * damping;
        }
    }

    private Vector3 GetSwingTangentVelocity(Vector3 velocity, Vector3 position)
    {
        Vector3 center = RopePosition != null ? RopePosition.transform.position : transform.position;
        Vector3 radius = position - center;
        radius.y = 0f;

        if (radius.sqrMagnitude <= 0.000001f)
        {
            return velocity;
        }

        Vector3 tangent = Vector3.Cross(radius.normalized, Vector3.up) * currentSwingDirection;

        float speed = Vector3.Dot(velocity, tangent);

        if (speed < 0f)
        {
            speed = 0f;
        }

        return tangent * speed;
    }

    private Vector3 GetDelayedSwingArcVelocity(int ropeIndex, int ropeCount)
    {
        if (!IsSwing || ropeCount <= 1 || RopeSwingTime <= 0f)
        {
            return Vector3.zero;
        }

        float ropeRate = (float)ropeIndex / (ropeCount - 1);
        float delay = RopeWhipDelay * ropeRate;
        float currentTimeRate = Mathf.Clamp01(SwingTimer / RopeSwingTime - delay);
        float previousTimeRate = Mathf.Clamp01((SwingTimer - Time.fixedDeltaTime) / RopeSwingTime - delay);

        if (currentTimeRate <= 0f)
        {
            return Vector3.zero;
        }

        float currentAngle = Mathf.Lerp(
            swingStartAngle,
            swingTargetAngle,
            Mathf.SmoothStep(0f, 1f, currentTimeRate));

        float previousAngle = Mathf.Lerp(
            swingStartAngle,
            swingTargetAngle,
            Mathf.SmoothStep(0f, 1f, previousTimeRate));

        Vector3 currentPoint = GetSwingArcPoint(currentAngle, ropeIndex);
        Vector3 previousPoint = GetSwingArcPoint(previousAngle, ropeIndex);

        return (currentPoint - previousPoint) / Time.fixedDeltaTime * (RopeWhipSpeed * 0.1f);
    }

    private Vector3 GetSwingArcPoint(float angle, int ropeIndex)
    {
        Vector3 center = RopePosition != null ? RopePosition.transform.position : transform.position;
        float angleRad = angle * Mathf.Deg2Rad;
        float radius = RopeSwingRadius + RopeInterval * ropeIndex;
        Vector3 side = transform.right * Mathf.Cos(angleRad) * radius;
        Vector3 forward = transform.forward * Mathf.Sin(angleRad) * radius;

        return center + side + forward;
    }

    private void FixRopeInterval()
    {
        if (RopeInterval <= 0f)
        {
            RopeInterval = 0.2f;
        }
    }

    private void FixRopeLength()
    {
        if (RopeMaxDistance <= 0f)
        {
            RopeMaxDistance = 3.0f;
        }
    }

    private void ApplyRopeSwingPosition()
    {
        if (RopePosition == null) return;

        Vector3 localOffset = GetRopeAnchorLocalSwingPosition();

        if (ropeAnchor != null && ropeAnchor != RopePosition.transform)
        {
            ropeAnchor.localPosition = localOffset;
            return;
        }

        RopePosition.transform.localPosition = localOffset;
    }

    private Vector3 GetRopeAnchorLocalSwingPosition()
    {
        float angleRad = currentSwingAngle * Mathf.Deg2Rad;
        float side = Mathf.Cos(angleRad) * RopeSwingRadius;
        float forward = Mathf.Sin(angleRad) * RopeSwingArcDepth;

        return new Vector3(side, 0f, forward);
    }

    void StartRopeSwing()
    {
        IsSwing = true;
        SwingTimer = 0;
        currentSwingDirection = AlwaysRightHandSwing ? 1f : (SwingRight ? 1f : -1f);

        swingStartAngle = currentSwingAngle;
        swingTargetAngle = currentSwingAngle + currentSwingDirection * 180f;

        if (!AlwaysRightHandSwing)
        {
            SwingRight = !SwingRight;
        }
    }

    private void UpdateRopeSwing()
    {
        if (!IsSwing) return;
        if (RopePosition == null) return;

        SwingTimer += Time.deltaTime;

        float t = SwingTimer / RopeSwingTime;
        t = Mathf.Clamp01(t);

        float easedT = Mathf.SmoothStep(0f, 1f, t);
        currentSwingAngle = Mathf.Lerp(swingStartAngle, swingTargetAngle, easedT);

        ApplyRopeSwingPosition();

        if (t >= 1f)
        {
            IsSwing = false;
        }
    }

    private Vector3 ClampToGround(Vector3 position)
    {
        Vector3 rayStart = position + Vector3.up * GroundCheckHeight;

        if (Physics.Raycast(
            rayStart,
            Vector3.down,
            out RaycastHit hit,
            GroundCheckHeight * 2f,
            GroundLayer))
        {
            float groundY = hit.point.y + RopeGroundOffset;

            if (position.y < groundY)
            {
                position.y = groundY;
            }
        }

        return position;
    }
}
