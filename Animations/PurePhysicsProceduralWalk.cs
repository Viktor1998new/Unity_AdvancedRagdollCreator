using System;
using UnityEngine;

[Serializable]
public class PurePhysicsProceduralWalk : ProceduralAnimation
{
    [SerializeField] private Transform hips;

    [Header("Body")]
    [SerializeField] private float bodyRotateSpeed = 10f;
    public float bodyMoveForce = 500f;

    [Header("Legs")]
    public SimpleLeg leftLeg;
    public SimpleLeg rightLeg;

    [Serializable]
    public class SimpleLeg
    {
        [Header("IK Systems")]
        public TwoBoneIKLookAt legIK;
        public IKFoot footIK;

        [Header("Placement Settings")]
        [Tooltip("Базовое локальное смещение ноги относительно центра таза (например: X: -0.2 для левой ноги, Y: -0.9, Z: 0)")]
        public Vector3 homeLocalOffset;

        [HideInInspector] public Vector3 targetPosition;
        [HideInInspector] public Vector3 poleTargetPosition;
        [HideInInspector] public float previousPhase = -1f;
        [HideInInspector] public bool isSwinging;
    }

    [Header("Step")]
    [Tooltip("Base distance covered by one foot step.")]
    public float stepLength = 0.45f;

    [Tooltip("Maximum step distance at high speed.")]
    public float maxStepLength = 0.65f;

    [Tooltip("How much the step grows with actual movement speed.")]
    public float strideLengthPerSpeed = 0.25f;

    [Tooltip("Cadence multiplier. 4-8 is usually a good range.")]
    public float stepSpeed = 6f;

    [Tooltip("Maximum vertical foot lift.")]
    public float stepHeight = 0.25f;

    [Tooltip("How far forward the foot is placed relative to the ideal pointer.")]
    [Range(0f, 1f)]
    public float landingBias = 0.35f;

    [Header("Step Height Modifier")]
    [Tooltip("Фактическая максимальная высота подъема стопы в метрах (умножается на stepHeightCurve)")]
    public float maxStepLift = 0.15f;

    [Header("Ground")]
    public LayerMask groundLayer;
    public float groundRayHeight = 2f;
    public float groundRayDistance = 5f;

    [Header("Animation Curves")]
    [Tooltip("0 = floor, 1 = maximum lift. X = swing phase 0..1.")]
    public AnimationCurve stepHeightCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3f),
        new Keyframe(0.12f, 0.12f),
        new Keyframe(0.50f, 1f, 0f, 0f),
        new Keyframe(0.82f, 0.28f),
        new Keyframe(1f, 0f, -3f, 0f));

    [Tooltip("Controls the subtle forward/back trajectory during the swing.")]
    public AnimationCurve stepForwardCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 1.2f, 1.2f),
        new Keyframe(0.45f, 0.58f),
        new Keyframe(0.82f, 0.92f),
        new Keyframe(1f, 1f, 0.5f, 0f));

    [Tooltip("Heel rises after the foot leaves the floor.")]
    public AnimationCurve heelLiftCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.18f, 0.75f),
        new Keyframe(0.48f, 1f),
        new Keyframe(0.72f, 0.35f),
        new Keyframe(1f, 0f));

    [Tooltip("Foot pitch during swing. Negative = toe points down, positive = toe points up.")]
    public AnimationCurve footPitchCurve = new AnimationCurve(
        new Keyframe(0f, 0.20f),
        new Keyframe(0.16f, -0.45f),
        new Keyframe(0.55f, -0.15f),
        new Keyframe(0.82f, 0.25f),
        new Keyframe(1f, 0.10f));

    [Tooltip("Controls the heel-to-toe roll after contact. 0..1.")]
    public AnimationCurve footRollCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.25f, 0f),
        new Keyframe(0.58f, 0.15f),
        new Keyframe(0.82f, 0.65f),
        new Keyframe(1f, 1f));

    [Tooltip("Vertical body movement. 0 = disabled.")]
    public AnimationCurve bodyBobCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.25f, 1f),
        new Keyframe(0.5f, 0f),
        new Keyframe(0.75f, 1f),
        new Keyframe(1f, 0f));

    [Header("Foot Contact")]
    [Tooltip("How much of the foot's rotation happens during heel strike.")]
    [Range(0f, 1f)]
    public float heelStrikeAmount = 0.8f;

    [Tooltip("Maximum foot pitch in degrees.")]
    public float footPitchAngle = 22f;

    [Tooltip("Extra rotation around the foot forward axis.")]
    public float footRollAngle = 8f;

    [Tooltip("How strongly the foot follows the procedural target.")]
    public float footRotationSpeed = 18f;

    [Header("Weight Shift")]
    public float bodyBobForce = 0f;
    public float lateralSwayForce = 0f;

    [Header("Anatomy")]
    public float thighLength = 0.45f;
    public float calfLength = 0.45f;

    private float gaitPhase;
    private Quaternion hipsStartRotation;

    private ConfigurableJoint hipsJoint;
    private Rigidbody hipsRigidbody;
    private PlayerController _PC;

    public override void Start()
    {
        _PC = Owner.GetComponent<PlayerController>();

        Debug.Log(_PC);

        hipsJoint = hips != null ? hips.GetComponent<ConfigurableJoint>() : null;
        hipsRigidbody = hips != null ? hips.GetComponent<Rigidbody>() : null;
        
        if (leftLeg.legIK != null) leftLeg.legIK.Initialize();
        if (leftLeg.footIK != null) leftLeg.footIK.Initialize();

        if (rightLeg.legIK != null) rightLeg.legIK.Initialize();
        if (rightLeg.footIK != null) rightLeg.footIK.Initialize();

        if (hipsJoint != null) hipsStartRotation = hipsJoint.transform.localRotation;
        
        Vector3 forwardDir = hipsRigidbody.transform.forward;
        Vector3 rightDir = hipsRigidbody.transform.right;

        Vector3 leftIdealPos = hipsRigidbody.position +
                               rightDir * leftLeg.homeLocalOffset.x +
                               forwardDir * leftLeg.homeLocalOffset.z +
                               Vector3.up * leftLeg.homeLocalOffset.y;

        Vector3 rightIdealPos = hipsRigidbody.position +
                                rightDir * rightLeg.homeLocalOffset.x +
                                forwardDir * rightLeg.homeLocalOffset.z +
                                Vector3.up * rightLeg.homeLocalOffset.y;

        Vector3 leftFloor = GetGroundPoint(leftIdealPos);
        Vector3 rightFloor = GetGroundPoint(rightIdealPos);

        InitializeLeg(leftLeg, leftFloor);
        InitializeLeg(rightLeg, rightFloor);

        gaitPhase = 0f;
    }

    private void InitializeLeg(SimpleLeg leg, Vector3 floor)
    {
        leg.targetPosition = floor;

        Vector3 forwardDir = hipsRigidbody != null ? hipsRigidbody.transform.forward : Vector3.forward;
        leg.poleTargetPosition = floor + forwardDir * 1.5f + Vector3.up * 0.2f;

        leg.isSwinging = false;
        leg.previousPhase = -1f;
    }

    public override void OnFixedUpdate()
    {
        if (hipsRigidbody == null || _PC == null) return;

        Vector3 targetEuler = _PC.syncLookRotation.eulerAngles;
        Quaternion planarTargetRotation = Quaternion.Euler(0f, targetEuler.y, 0f);

        Quaternion targetRotation = Quaternion.RotateTowards(
            hipsRigidbody.rotation,
            planarTargetRotation,
            bodyRotateSpeed * Time.fixedDeltaTime * 100f);

        if (hipsJoint != null)
            hipsJoint.targetRotation = Quaternion.Inverse(planarTargetRotation) * hipsStartRotation;

        Vector3 forwardDirection = targetRotation * Vector3.forward;
        Vector3 rightDirection = targetRotation * Vector3.right;
        
        Vector3 moveDirection =
            rightDirection * _PC.moveInput.x +
            forwardDirection * _PC.moveInput.y;

        if (moveDirection.magnitude > 1f)
            moveDirection.Normalize();

        if (moveDirection.magnitude > 0.05f)
            hipsRigidbody.AddForce(moveDirection * bodyMoveForce, ForceMode.Force);

        Vector3 planarVelocity = hipsRigidbody.linearVelocity;
        planarVelocity.y = 0f;

        float planarSpeed = planarVelocity.magnitude;
        float effectiveStepLength = Mathf.Clamp(
            stepLength + planarSpeed * strideLengthPerSpeed,
            stepLength,
            maxStepLength);

        float cadence = effectiveStepLength > 0.01f
            ? (planarSpeed / effectiveStepLength) * stepSpeed
            : 0f;

        gaitPhase = Mathf.Repeat(
            gaitPhase + cadence * Time.fixedDeltaTime,
            1f);

        float stanceOffsetLength = 0f;

        Vector3 forwardOffset = forwardDirection * stanceOffsetLength;

        Vector3 leftLegStanceModifier = forwardOffset;
        Vector3 rightLegStanceModifier = -forwardOffset;

        UpdateLeg(leftLeg, moveDirection, forwardDirection, rightDirection,
            effectiveStepLength, gaitPhase, leftLegStanceModifier);

        UpdateLeg(rightLeg, moveDirection, forwardDirection, rightDirection,
            effectiveStepLength, Mathf.Repeat(gaitPhase + 0.5f, 1f), rightLegStanceModifier);

        if (bodyBobForce > 0f || lateralSwayForce > 0f)
        {
            float bob = bodyBobCurve.Evaluate(gaitPhase);
            float sway = Mathf.Sin(gaitPhase * Mathf.PI * 2f);

            Vector3 lifeForce =
                Vector3.up * (bob - 0.5f) * bodyBobForce +
                rightDirection * sway * lateralSwayForce;

            hipsRigidbody.AddForce(lifeForce, ForceMode.Force);
        }

        ApplyPerfectJointIK(leftLeg);
        ApplyPerfectJointIK(rightLeg);

        if (leftLeg.footIK != null) leftLeg.footIK.ApplyIK(leftLeg);
        if (rightLeg.footIK != null) rightLeg.footIK.ApplyIK(rightLeg);
    }

    private void ApplyPerfectJointIK(SimpleLeg leg)
    {
        if (leg.legIK == null || leg.legIK.upperJoint == null || leg.legIK.midJoint == null)
            return;

        Vector3 hipsAngularVelocity = hipsRigidbody.angularVelocity;
        Vector3 predictedTargetPos = leg.targetPosition;

        if (hipsAngularVelocity.magnitude > 0.01f)
        {
            Quaternion rotationPrediction = Quaternion.Euler(hipsAngularVelocity * Mathf.Rad2Deg * Time.fixedDeltaTime);
            Vector3 offset = leg.targetPosition - hipsRigidbody.position;
            predictedTargetPos = hipsRigidbody.position + Quaternion.Inverse(rotationPrediction) * offset;
        }

        leg.legIK.ApplyIK(predictedTargetPos, leg.poleTargetPosition);
    }

    private void UpdateLeg(
    SimpleLeg leg,
    Vector3 moveDirection,
    Vector3 forwardDir,
    Vector3 rightDir,
    float stepLength,
    float currentPhase,
    Vector3 stanceModifier)
    {
        if (leg.legIK == null || leg.legIK.upperJoint == null) return;

        leg.previousPhase = currentPhase;
        leg.isSwinging = currentPhase > 0.5f;

        Vector3 stableHipsPosition = new Vector3(
            hipsRigidbody.position.x,
            Owner.transform.position.y,
            hipsRigidbody.position.z
        );

        Vector3 homeWorldPos = stableHipsPosition +
                               rightDir * leg.homeLocalOffset.x +
                               forwardDir * leg.homeLocalOffset.z +
                               Vector3.up * leg.homeLocalOffset.y;

        homeWorldPos += stanceModifier;

        if (leg.isSwinging)
        {
            float swingNormalizedTime = (currentPhase - 0.5f) / 0.5f;
            swingNormalizedTime = Mathf.Clamp01(swingNormalizedTime);

            Vector3 stepStartBehind = homeWorldPos - moveDirection * (stepLength * 0.5f);
            Vector3 stepTargetAhead = homeWorldPos + moveDirection * (stepLength * 0.5f);

            float forwardT = stepForwardCurve.Evaluate(swingNormalizedTime);
            Vector3 flatPosition = Vector3.Lerp(stepStartBehind, stepTargetAhead, forwardT);

            Vector3 groundPoint = GetGroundPoint(flatPosition);

            float curveLift = stepHeightCurve.Evaluate(swingNormalizedTime);
            float arcHeight = curveLift * maxStepLift;

            leg.targetPosition = groundPoint + Vector3.up * arcHeight;
        }
        else
        {
            float stanceNormalizedTime = Mathf.InverseLerp(0.0f, 0.5f, currentPhase);

            Vector3 stanceStart = homeWorldPos + moveDirection * (stepLength * 0.5f);
            Vector3 stanceEnd = homeWorldPos - moveDirection * (stepLength * 0.5f);

            Vector3 groundTarget = Vector3.Lerp(stanceStart, stanceEnd, stanceNormalizedTime);
            leg.targetPosition = GetGroundPoint(groundTarget);
        }

        Vector3 stableHipPos = new Vector3(
            leg.legIK.upperJoint.transform.position.x,
            stableHipsPosition.y + leg.homeLocalOffset.y,
            leg.legIK.upperJoint.transform.position.z
        );

        float kneeForwardOffset = 1.5f;
        leg.poleTargetPosition = stableHipPos + forwardDir * kneeForwardOffset + Vector3.up * 0.1f;
    }

    private Vector3 GetGroundPoint(Vector3 startPos)
    {
        if (Physics.Raycast(
            startPos + Vector3.up * groundRayHeight,
            Vector3.down,
            out RaycastHit hit,
            groundRayDistance,
            groundLayer))
        {
            return hit.point;
        }

        return startPos;
    }

    public override void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        DrawLegGizmos(leftLeg, Color.blue, Color.cyan);
        DrawLegGizmos(rightLeg, Color.red, Color.magenta);
    }

    private void DrawLegGizmos(SimpleLeg leg, Color mainColor, Color targetColor)
    {
        if (leg == null || leg.legIK == null || leg.legIK.upperJoint == null || leg.legIK.midJoint == null || hipsRigidbody == null)
            return;

        Gizmos.color = targetColor;
        Gizmos.DrawSphere(leg.targetPosition, 0.06f);

        Gizmos.color = mainColor;
        Vector3 hipPos = leg.legIK.upperJoint.transform.position;
        Vector3 kneePos = leg.legIK.midJoint.transform.position;
        Gizmos.DrawLine(hipPos, kneePos);
        Gizmos.DrawLine(kneePos, leg.targetPosition);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(leg.poleTargetPosition, 0.045f);
        Gizmos.DrawLine(kneePos, leg.poleTargetPosition);

        if (leg.isSwinging)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(leg.targetPosition, 0.08f);
        }

        Vector3 forwardDir = hipsRigidbody.transform.forward;
        Vector3 rightDir = hipsRigidbody.transform.right;

        Vector3 moveInput = _PC != null ? _PC.moveInput : Vector3.zero;
        Vector3 currentMoveDir = rightDir * moveInput.x + forwardDir * moveInput.y;
        if (currentMoveDir.magnitude > 1f) currentMoveDir.Normalize();
        if (currentMoveDir.magnitude < 0.05f) currentMoveDir = forwardDir;

        Vector3 homeWorldPos = hipsRigidbody.position +
                               rightDir * leg.homeLocalOffset.x +
                               forwardDir * leg.homeLocalOffset.z +
                               Vector3.up * leg.homeLocalOffset.y;

        Vector3 planarVelocity = hipsRigidbody.linearVelocity;
        planarVelocity.y = 0f;
        float currentStepLength = Mathf.Clamp(stepLength + planarVelocity.magnitude * strideLengthPerSpeed, stepLength, maxStepLength);

        Vector3 stepStartBehind = homeWorldPos - currentMoveDir * (currentStepLength * 0.5f);
        Vector3 stepTargetAhead = homeWorldPos + currentMoveDir * (currentStepLength * 0.5f);

        int resolution = 15;
        Vector3 previousPathPoint = Vector3.zero;

        for (int i = 0; i <= resolution; i++)
        {
            float t = (float)i / resolution;

            float forwardT = stepForwardCurve.Evaluate(t);
            Vector3 flatPosition = Vector3.Lerp(stepStartBehind, stepTargetAhead, forwardT);

            // Симулируем высоту подъема на основе вашей stepHeightCurve
            float curveLift = stepHeightCurve.Evaluate(t);
            float arcHeight = curveLift * maxStepLift;

            // Находим итоговую точку в воздухе
            Vector3 airPoint = flatPosition + Vector3.up * arcHeight;
            Vector3 currentPathPoint = GetGroundPoint(airPoint); // Прижимаем к рельефу земли

            // Отрисовываем траекторию
            if (i > 0)
            {
                // Рисуем дугу в воздухе (Фаза маха — Swing) зеленовато-голубым цветом
                Gizmos.color = Color.Lerp(targetColor, Color.green, 0.4f);
                Gizmos.DrawLine(previousPathPoint, currentPathPoint);
            }

            // Рисуем маленькие точки-узелки вдоль траектории шага
            Gizmos.DrawSphere(currentPathPoint, 0.015f);

            previousPathPoint = currentPathPoint;
        }

        // Рисуем обратную прямую линию на земле (Фаза опоры — Stance) белым цветом
        Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
        Gizmos.DrawLine(GetGroundPoint(stepTargetAhead), GetGroundPoint(stepStartBehind));
    }

}
