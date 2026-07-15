using System.Collections.Generic;
using UnityEngine;

public class RopeGenerator : MonoBehaviour
{
    public RopePiece RopePiece;
    public GameObject RopeHead;

    public float RopeRange;
    public float GenInterval;
    public float PieceDelay;

    [Tooltip("オンの場合、見た目用のRopePieceに付いているColliderを無効にします。")]
    public bool DisablePieceColliders = true;

    [Header("Pull Settings")]
    [Tooltip("ロープが最大長を超えた時に、接続したオブジェクトを引く強さです。")]
    public float PullStrength = 35f;

    [Tooltip("ロープが伸びる方向の速度を弱める強さです。値を上げるほど伸びにくくなります。")]
    public float PullDamping = 8f;

    [Header("Swing Settings")]
    [Tooltip("1回のスイングで回転する角度です。360で1回転します。")]
    public float SwingRotationAngle = 360f;

    [Tooltip("1回のクリックで振る力を加える時間です。")]
    public float SwingTime = 0.35f;

    [Tooltip("横軸を時間、縦軸を角速度倍率としてスイングの重さを設定します。")]
    public AnimationCurve SwingSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.25f, 0.5f),
        new Keyframe(0.65f, 1.4f),
        new Keyframe(0.9f, 0.9f),
        new Keyframe(1f, 0.35f));

    [Tooltip("指定角度へ到達した後、角速度を毎秒どれだけ減らすかを設定します。")]
    public float SwingFollowThroughDeceleration = 720f;

    [Tooltip("曲線の終端速度が小さい場合でも、フォロースルーを発生させる最低角速度です。")]
    public float SwingMinimumFollowThroughSpeed = 60f;

    [Tooltip("スイングの角速度から半径方向へ変換する遠心力の強さです。")]
    public float CentrifugalStrength = 0.15f;

    [Tooltip("外側へ広がる速度の減衰です。大きいほど急激に広がりにくくなります。")]
    public float CentrifugalDamping = 2f;

    [Tooltip("オンの場合、クリックするたびに右振りと左振りを切り替えます。")]
    public bool AlternateSwingDirection = true;

    [Tooltip("オンの場合は最初のクリックを右方向、オフの場合は左方向へ振ります。")]
    public bool FirstSwingRight = true;

    [Tooltip("現在の回転方向と速度を、新しく始めるスイングへ引き継ぎます。")]
    public bool PreserveSwingMomentum = true;

    [Tooltip("この角速度以上で回っている場合、左右設定より現在の回転方向を優先します。")]
    public float SwingMomentumDirectionThreshold = 20f;

    [Tooltip("現在の角速度から速度曲線の角速度へ滑らかに合流するまでの時間です。")]
    public float SwingVelocityBlendTime = 0.12f;

    [Tooltip("接続したオブジェクトの最大速度です。")]
    public float MaxObjectSpeed = 20f;

    [Tooltip("スイング中のオブジェクトがPlayerへ入り込まないための最小半径です。")]
    public float PlayerAvoidanceRadius = 1.25f;

    private readonly List<RopePiece> RopePieces = new List<RopePiece>();

    private Rigidbody connectRb;
    private Vector3 previousHeadPosition;
    private Vector3 currentHeadVelocity;
    private float connectedRopeLength;
    private float currentPieceInterval;
    private float swingElapsedTime;
    private float swingCurveArea;
    private float swingTravelAngle;
    private float swingAngularVelocity;
    private float swingDirection = 1f;
    private bool nextSwingRight;
    private bool isSwinging;
    private bool isSwingFollowThrough;
    private Quaternion swingBaseHeadLocalRotation;
    private Vector3 swingUpAxis;
    private Vector3 swingStartDirection;
    private float swingVerticalOffset;
    private float swingRadius;
    private float swingMaximumRadius;
    private float swingRadialVelocity;
    private float previousSwingAngle;

    private void Awake()
    {
        nextSwingRight = FirstSwingRight;

        if (RopeHead != null)
        {
            previousHeadPosition = RopeHead.transform.position;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (connectRb == null)
            {
                TryConnect();
            }
            else
            {
                Disconnect();
            }
        }

        if (connectRb != null && !isSwinging)
        {
            UpdateRopePieces();
        }
    }

    private void FixedUpdate()
    {
        if (RopeHead == null)
        {
            return;
        }

        Vector3 headPosition = RopeHead.transform.position;
        Vector3 headVelocity = (headPosition - previousHeadPosition) / Time.fixedDeltaTime;
        currentHeadVelocity = headVelocity;

        if (connectRb != null)
        {
            if (isSwinging)
            {
                UpdateSwing();
            }
            else
            {
                ApplyPullForce(headPosition, headVelocity);
                LimitObjectSpeed();
            }
        }

        previousHeadPosition = headPosition;
    }

    private void TryConnect()
    {
        if (RopeHead == null || RopePiece == null || GenInterval <= 0f || RopeRange <= 0f)
        {
            Debug.LogWarning("RopeGenerator の RopeHead、RopePiece、GenInterval を確認してください。");
            return;
        }

        ReachableArea[] areas = FindObjectsOfType<ReachableArea>();

        ReachableArea nearestArea = null;
        float nearestDistance = Mathf.Infinity;

        foreach (ReachableArea area in areas)
        {
            if (!area.isInRange)
            {
                continue;
            }

            float distance = Vector3.Distance(RopeHead.transform.position, area.transform.position);

            if (RopeRange > 0f && distance > RopeRange)
            {
                continue;
            }

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestArea = area;
            }
        }

        if (nearestArea == null)
        {
            return;
        }

        Rigidbody targetRb = nearestArea.GetComponentInParent<Rigidbody>();

        if (targetRb == null)
        {
            Debug.LogWarning("接続するオブジェクトに Rigidbody がありません。");
            return;
        }

        connectRb = targetRb;
        previousHeadPosition = RopeHead.transform.position;
        Generation();
    }

    private void Generation()
    {
        ClearRopePieces();

        Vector3 start = RopeHead.transform.position;
        Vector3 end = connectRb.position;
        connectedRopeLength = RopeRange;

        int value = Mathf.Max(1, Mathf.CeilToInt(connectedRopeLength / GenInterval));
        currentPieceInterval = connectedRopeLength / value;

        Vector3 direction = end - start;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        direction.Normalize();
        RopeHead.transform.rotation = Quaternion.LookRotation(direction, transform.up);

        Transform parent = RopeHead.transform;

        for (int i = 0; i < value; i++)
        {
            RopePiece piece = Instantiate(RopePiece, parent);
            piece.transform.localPosition = GetLocalForwardOffset(parent, currentPieceInterval);
            piece.transform.localRotation = Quaternion.identity;
            SetWorldScale(piece.transform, Vector3.one * 0.1f);
            SetPieceCollidersEnabled(piece, !DisablePieceColliders);

            RopePieces.Add(piece);
            parent = piece.transform;
        }

        UpdateRopePieces();
    }

    private void UpdateRopePieces()
    {
        if (RopePieces.Count == 0)
        {
            return;
        }

        Vector3 start = RopeHead.transform.position;
        Vector3 end = connectRb.position;
        UpdateSaggingRope(start, end);
    }

    private void ApplyPullForce(Vector3 headPosition, Vector3 headVelocity)
    {
        Vector3 toObject = connectRb.worldCenterOfMass - headPosition;
        float distance = toObject.magnitude;

        float maxLength = connectedRopeLength > 0f ? connectedRopeLength : RopeRange;

        if (distance <= maxLength || distance <= 0.0001f)
        {
            return;
        }

        Vector3 direction = toObject / distance;
        float stretch = distance - maxLength;
        Vector3 relativeVelocity = connectRb.velocity - headVelocity;
        float separatingSpeed = Mathf.Max(0f, Vector3.Dot(relativeVelocity, direction));

        float pullAcceleration = stretch * PullStrength + separatingSpeed * PullDamping;
        connectRb.AddForce(-direction * pullAcceleration, ForceMode.Acceleration);
    }

    public void StartSwing()
    {
        if (connectRb == null || RopePieces.Count == 0)
        {
            return;
        }

        swingCurveArea = CalculateSwingCurveArea(1f);
        swingTravelAngle = 0f;
        isSwingFollowThrough = false;
        swingDirection = nextSwingRight ? 1f : -1f;

        if (AlternateSwingDirection)
        {
            nextSwingRight = !nextSwingRight;
        }

        PrepareSwing();
        isSwinging = true;
    }

    private void PrepareSwing()
    {
        swingUpAxis = transform.up.normalized;

        Vector3 offset = connectRb.position - RopeHead.transform.position;
        swingVerticalOffset = Vector3.Dot(offset, swingUpAxis);

        Vector3 horizontalOffset = Vector3.ProjectOnPlane(offset, swingUpAxis);

        if (horizontalOffset.sqrMagnitude <= 0.0001f)
        {
            horizontalOffset = Vector3.ProjectOnPlane(transform.forward, swingUpAxis);

            if (horizontalOffset.sqrMagnitude <= 0.0001f)
            {
                horizontalOffset = Vector3.ProjectOnPlane(transform.right, swingUpAxis);
            }
        }

        swingStartDirection = horizontalOffset.normalized;
        swingRadius = horizontalOffset.magnitude;

        Vector3 lookDirection = offset.sqrMagnitude > 0.0001f
            ? offset.normalized
            : swingStartDirection;
        Quaternion desiredWorldRotation = Quaternion.LookRotation(lookDirection, swingUpAxis);

        swingBaseHeadLocalRotation = RopeHead.transform.parent != null
            ? Quaternion.Inverse(RopeHead.transform.parent.rotation) * desiredWorldRotation
            : desiredWorldRotation;

        float maximumRadiusSquared =
            connectedRopeLength * connectedRopeLength -
            swingVerticalOffset * swingVerticalOffset;

        swingMaximumRadius = Mathf.Sqrt(Mathf.Max(0f, maximumRadiusSquared));

        Vector3 relativeVelocity = connectRb.velocity - currentHeadVelocity;
        Vector3 positiveTangent = Vector3.Cross(swingUpAxis, swingStartDirection).normalized;
        float signedAngularVelocity = swingRadius > 0.0001f
            ? Vector3.Dot(relativeVelocity, positiveTangent) /
              swingRadius * Mathf.Rad2Deg
            : 0f;

        if (PreserveSwingMomentum &&
            Mathf.Abs(signedAngularVelocity) >= Mathf.Max(0f, SwingMomentumDirectionThreshold))
        {
            swingDirection = Mathf.Sign(signedAngularVelocity);
        }

        swingAngularVelocity = PreserveSwingMomentum
            ? Mathf.Max(0f, signedAngularVelocity * swingDirection)
            : 0f;
        swingRadialVelocity = PreserveSwingMomentum
            ? Vector3.Dot(relativeVelocity, swingStartDirection)
            : 0f;

        float duration = Mathf.Max(0.01f, SwingTime);
        float targetAngle = Mathf.Abs(SwingRotationAngle);
        float baseAngularVelocity = swingCurveArea > 0.0001f
            ? targetAngle / (duration * swingCurveArea)
            : targetAngle / duration;

        float startRate = PreserveSwingMomentum
            ? FindSwingCurveStartRate(swingAngularVelocity, baseAngularVelocity)
            : 0f;
        swingElapsedTime = startRate * duration;
        previousSwingAngle = 0f;
    }

    private void UpdateSwing()
    {
        float duration = Mathf.Max(0.01f, SwingTime);
        float targetAngle = Mathf.Abs(SwingRotationAngle);

        if (targetAngle <= 0.0001f)
        {
            FinishSwing();
            return;
        }

        if (isSwingFollowThrough)
        {
            swingAngularVelocity = Mathf.MoveTowards(
                swingAngularVelocity,
                0f,
                Mathf.Max(0.01f, SwingFollowThroughDeceleration) *
                Time.fixedDeltaTime);

            swingTravelAngle += swingAngularVelocity * Time.fixedDeltaTime;
        }
        else
        {
            swingElapsedTime = Mathf.Min(
                duration,
                swingElapsedTime + Time.fixedDeltaTime);

            float timeRate = Mathf.Clamp01(swingElapsedTime / duration);
            float speedMultiplier = EvaluateSwingSpeedCurve(timeRate);
            float desiredAngularVelocity = swingCurveArea > 0.0001f
                ? targetAngle / (duration * swingCurveArea) * speedMultiplier
                : targetAngle / duration;

            float blend = SwingVelocityBlendTime <= 0f
                ? 1f
                : 1f - Mathf.Exp(
                    -Time.fixedDeltaTime / SwingVelocityBlendTime);
            swingAngularVelocity = Mathf.Lerp(
                swingAngularVelocity,
                desiredAngularVelocity,
                blend);

            float remainingAngle = targetAngle - swingTravelAngle;
            float deltaAngle = Mathf.Min(
                remainingAngle,
                swingAngularVelocity * Time.fixedDeltaTime);
            swingTravelAngle += deltaAngle;

            if (remainingAngle <= deltaAngle + 0.0001f)
            {
                swingTravelAngle = targetAngle;
                swingAngularVelocity = Mathf.Max(
                    swingAngularVelocity,
                    Mathf.Max(0f, SwingMinimumFollowThroughSpeed));
                isSwingFollowThrough = true;
            }
        }

        float swingAngle = swingTravelAngle * swingDirection;

        Quaternion headSwingRotation = Quaternion.AngleAxis(swingAngle, Vector3.up);
        RopeHead.transform.localRotation = headSwingRotation * swingBaseHeadLocalRotation;

        UpdateSwingRadius(swingAngle);

        Quaternion orbitRotation = Quaternion.AngleAxis(swingAngle, swingUpAxis);
        Vector3 outwardDirection = orbitRotation * swingStartDirection;
        Vector3 ropeTipPosition =
            RopeHead.transform.position +
            outwardDirection * swingRadius +
            swingUpAxis * swingVerticalOffset;

        ropeTipPosition = ClampOutsidePlayer(ropeTipPosition);

        UpdateSaggingRope(RopeHead.transform.position, ropeTipPosition);

        connectRb.MovePosition(ropeTipPosition);

        if (isSwingFollowThrough && swingAngularVelocity <= 0.0001f)
        {
            FinishSwing();
        }
    }

    private float EvaluateSwingSpeedCurve(float timeRate)
    {
        if (SwingSpeedCurve == null || SwingSpeedCurve.length == 0)
        {
            return 1f;
        }

        return Mathf.Max(0f, SwingSpeedCurve.Evaluate(Mathf.Clamp01(timeRate)));
    }

    private float CalculateSwingCurveArea(float endTimeRate)
    {
        const int sampleCount = 32;
        float clampedEnd = Mathf.Clamp01(endTimeRate);

        if (clampedEnd <= 0f)
        {
            return 0f;
        }

        float step = clampedEnd / sampleCount;
        float area = 0f;
        float previousValue = EvaluateSwingSpeedCurve(0f);

        for (int i = 1; i <= sampleCount; i++)
        {
            float timeRate = step * i;
            float currentValue = EvaluateSwingSpeedCurve(timeRate);
            area += (previousValue + currentValue) * 0.5f * step;
            previousValue = currentValue;
        }

        return area;
    }

    private float FindSwingCurveStartRate(
        float inheritedAngularVelocity,
        float baseAngularVelocity)
    {
        if (inheritedAngularVelocity <= 0.0001f || baseAngularVelocity <= 0.0001f)
        {
            return 0f;
        }

        const int sampleCount = 32;
        float bestRate = 0f;
        float smallestDifference = Mathf.Infinity;

        for (int i = 0; i <= sampleCount; i++)
        {
            float rate = (float)i / sampleCount;
            float curveAngularVelocity =
                baseAngularVelocity * EvaluateSwingSpeedCurve(rate);
            float difference = Mathf.Abs(
                curveAngularVelocity - inheritedAngularVelocity);

            if (difference < smallestDifference)
            {
                smallestDifference = difference;
                bestRate = rate;
            }
        }

        return bestRate;
    }

    private void FinishSwing()
    {
        isSwinging = false;
        isSwingFollowThrough = false;
        swingAngularVelocity = 0f;

        if (connectRb != null)
        {
            connectRb.velocity = Vector3.zero;
        }
    }

    private void UpdateSwingRadius(float swingAngle)
    {
        float deltaTime = Time.fixedDeltaTime;
        float deltaAngle = Mathf.Abs(swingAngle - previousSwingAngle) * Mathf.Deg2Rad;
        float angularVelocity = deltaAngle / Mathf.Max(deltaTime, 0.0001f);

        if (swingRadius > swingMaximumRadius)
        {
            float stretch = swingRadius - swingMaximumRadius;
            swingRadialVelocity -=
                stretch * Mathf.Max(0f, PullStrength) * deltaTime;
        }
        else if (swingRadius < swingMaximumRadius)
        {
            float effectiveRadius = Mathf.Max(swingRadius, 0.1f);
            float centrifugalAcceleration =
                angularVelocity * angularVelocity *
                effectiveRadius *
                Mathf.Max(0f, CentrifugalStrength);

            swingRadialVelocity += centrifugalAcceleration * deltaTime;
        }

        float radialDamping = swingRadius > swingMaximumRadius
            ? Mathf.Max(0f, PullDamping)
            : Mathf.Max(0f, CentrifugalDamping);
        swingRadialVelocity *= Mathf.Exp(-radialDamping * deltaTime);

        float nextRadius = Mathf.Max(
            0f,
            swingRadius + swingRadialVelocity * deltaTime);

        if (swingRadius <= swingMaximumRadius && nextRadius > swingMaximumRadius)
        {
            swingRadius = swingMaximumRadius;
            swingRadialVelocity = 0f;
        }
        else
        {
            swingRadius = nextRadius;
        }

        previousSwingAngle = swingAngle;
    }

    private void LimitObjectSpeed()
    {
        if (MaxObjectSpeed <= 0f)
        {
            return;
        }

        connectRb.velocity = Vector3.ClampMagnitude(connectRb.velocity, MaxObjectSpeed);
    }

    private void SetPieceCollidersEnabled(RopePiece piece, bool isEnabled)
    {
        Collider[] colliders = piece.GetComponentsInChildren<Collider>(true);

        foreach (Collider pieceCollider in colliders)
        {
            pieceCollider.enabled = isEnabled;
        }
    }

    private void UpdateSaggingRope(Vector3 start, Vector3 end)
    {
        if (RopePieces.Count == 0)
        {
            return;
        }

        Vector3 sagDirection = GetSagDirection(start, end);
        float sagDepth = CalculateSagDepth(start, end, sagDirection);
        float followRate = PieceDelay <= 0f
            ? 1f
            : 1f - Mathf.Exp(-Time.deltaTime / PieceDelay);
        Vector3 previousPosition = start;

        for (int i = 0; i < RopePieces.Count; i++)
        {
            float t = (i + 1f) / RopePieces.Count;
            Vector3 targetPosition = GetSagPoint(start, end, sagDirection, sagDepth, t);
            RopePiece piece = RopePieces[i];
            bool isLastPiece = i == RopePieces.Count - 1;

            piece.transform.position = isLastPiece
                ? end
                : Vector3.Lerp(piece.transform.position, targetPosition, followRate);

            if (!isLastPiece)
            {
                piece.ResolveGroundPenetration();
            }

            Vector3 direction = piece.transform.position - previousPosition;

            if (direction.sqrMagnitude > 0.0001f)
            {
                piece.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            previousPosition = piece.transform.position;
        }
    }

    private Vector3 GetSagDirection(Vector3 start, Vector3 end)
    {
        Vector3 line = end - start;

        if (line.sqrMagnitude <= 0.0001f)
        {
            return Vector3.down;
        }

        Vector3 sagDirection = Vector3.ProjectOnPlane(Vector3.down, line.normalized);

        if (sagDirection.sqrMagnitude <= 0.0001f)
        {
            sagDirection = Vector3.ProjectOnPlane(transform.forward, line.normalized);
        }

        return sagDirection.normalized;
    }

    private float CalculateSagDepth(Vector3 start, Vector3 end, Vector3 sagDirection)
    {
        float directDistance = Vector3.Distance(start, end);

        if (directDistance >= connectedRopeLength - 0.0001f)
        {
            return 0f;
        }

        float low = 0f;
        float high = Mathf.Max(0.1f, connectedRopeLength * 0.5f);

        for (int i = 0; i < 10; i++)
        {
            if (CalculateCurveLength(start, end, sagDirection, high) >= connectedRopeLength)
            {
                break;
            }

            high *= 2f;
        }

        for (int i = 0; i < 16; i++)
        {
            float middle = (low + high) * 0.5f;
            float curveLength = CalculateCurveLength(start, end, sagDirection, middle);

            if (curveLength < connectedRopeLength)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return (low + high) * 0.5f;
    }

    private float CalculateCurveLength(
        Vector3 start,
        Vector3 end,
        Vector3 sagDirection,
        float sagDepth)
    {
        float length = 0f;
        Vector3 previousPosition = start;

        for (int i = 1; i <= RopePieces.Count; i++)
        {
            float t = (float)i / RopePieces.Count;
            Vector3 position = GetSagPoint(start, end, sagDirection, sagDepth, t);
            length += Vector3.Distance(previousPosition, position);
            previousPosition = position;
        }

        return length;
    }

    private Vector3 GetSagPoint(
        Vector3 start,
        Vector3 end,
        Vector3 sagDirection,
        float sagDepth,
        float t)
    {
        float sagRate = 4f * t * (1f - t);

        return Vector3.Lerp(start, end, t) + sagDirection * sagDepth * sagRate;
    }

    private Vector3 ClampOutsidePlayer(Vector3 position)
    {
        if (PlayerAvoidanceRadius <= 0f)
        {
            return position;
        }

        Vector3 center = transform.position;
        Vector3 horizontalOffset = position - center;
        horizontalOffset.y = 0f;

        if (horizontalOffset.sqrMagnitude >= PlayerAvoidanceRadius * PlayerAvoidanceRadius)
        {
            return position;
        }

        if (horizontalOffset.sqrMagnitude <= 0.0001f)
        {
            horizontalOffset = connectRb.position - center;
            horizontalOffset.y = 0f;
        }

        if (horizontalOffset.sqrMagnitude <= 0.0001f)
        {
            horizontalOffset = transform.forward;
        }

        Vector3 correctedHorizontalPosition = center + horizontalOffset.normalized * PlayerAvoidanceRadius;
        position.x = correctedHorizontalPosition.x;
        position.z = correctedHorizontalPosition.z;

        return position;
    }

    private Vector3 GetLocalForwardOffset(Transform pieceParent, float worldDistance)
    {
        float worldForwardScale = pieceParent.TransformVector(Vector3.forward).magnitude;

        if (worldForwardScale <= 0.0001f)
        {
            return Vector3.forward * worldDistance;
        }

        return Vector3.forward * (worldDistance / worldForwardScale);
    }

    private void Disconnect()
    {
        connectRb = null;
        swingElapsedTime = 0f;
        swingTravelAngle = 0f;
        swingAngularVelocity = 0f;
        isSwinging = false;
        isSwingFollowThrough = false;
        nextSwingRight = FirstSwingRight;
        ClearRopePieces();
    }

    private void ClearRopePieces()
    {
        for (int i = RopePieces.Count - 1; i >= 0; i--)
        {
            if (RopePieces[i] != null)
            {
                Destroy(RopePieces[i].gameObject);
            }
        }

        RopePieces.Clear();
    }

    private void SetWorldScale(Transform target, Vector3 worldScale)
    {
        Transform targetParent = target.parent;

        if (targetParent == null)
        {
            target.localScale = worldScale;
            return;
        }

        Vector3 parentScale = targetParent.lossyScale;
        target.localScale = new Vector3(
            parentScale.x != 0f ? worldScale.x / parentScale.x : worldScale.x,
            parentScale.y != 0f ? worldScale.y / parentScale.y : worldScale.y,
            parentScale.z != 0f ? worldScale.z / parentScale.z : worldScale.z
        );
    }
}
