using System;
using System.Collections.Generic;
using UnityEngine;
using static ProceduralAnimation;
using static PurePhysicsProceduralWalk;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class ProceduralAnimationSelectorAttribute : PropertyAttribute { }

[Serializable]
public abstract class ProceduralAnimation
{
    public bool Enable;
    public AdvancedRagdollCreator Owner {  get; private set; }

    public void Init(AdvancedRagdollCreator advancedRagdoll)
    {
        Owner = advancedRagdoll;

        Start();
    }

    public abstract void Start();
    public abstract void OnFixedUpdate();
    public virtual void OnDrawGizmos() { }

    [Serializable]
    public abstract class IKBasic
    {
        [Header("Bone Axis Settings (Local)")]
        public Vector3 localForwardAxis = new Vector3(0, -1, 0);
        public Vector3 localUpAxis = new Vector3(0, 0, 1);

        public virtual void Initialize()
        {

        }
    }

    [Serializable]
    public class IKLookAt : IKBasic
    {
        public ConfigurableJoint joint;
        private Quaternion initialJointRotation;
        private bool isInitialized = false;

        public override void Initialize()
        {
            if (joint == null) return;

            initialJointRotation = joint.transform.localRotation;
            isInitialized = true;
        }

        public void ApplyIK(Vector3 Target)
        {
            if (!isInitialized || joint == null) return;

            Vector3 worldTargetPos = Target;
            Vector3 direction = worldTargetPos - joint.transform.position;

            if (direction.sqrMagnitude > 0.001f)
            {
                Vector3 worldUpReference = Vector3.forward;
                if (joint.connectedBody != null)
                {
                    worldUpReference = joint.connectedBody.transform.forward;
                }
                else if (joint.transform.parent != null)
                {
                    worldUpReference = joint.transform.parent.forward;
                }

                Quaternion lookRotation = Quaternion.LookRotation(direction, worldUpReference);

                Vector3 forward = localForwardAxis.sqrMagnitude > 0.001f ? localForwardAxis.normalized : Vector3.forward;
                Vector3 up = localUpAxis.sqrMagnitude > 0.001f ? localUpAxis.normalized : Vector3.up;

                Quaternion axisCorrection = Quaternion.Inverse(Quaternion.LookRotation(forward, up));
                lookRotation = lookRotation * axisCorrection;

                if (joint.connectedBody != null)
                {
                    Quaternion targetLocalRotation = Quaternion.Inverse(joint.connectedBody.transform.rotation) * lookRotation;
                    joint.targetRotation = Quaternion.Inverse(targetLocalRotation) * initialJointRotation;
                }
                else
                {
                    joint.targetRotation = Quaternion.Inverse(lookRotation) * initialJointRotation;
                }
            }
        }
    }

    [Serializable]
    public class TwoBoneIKLookAt : IKBasic
    {
        [Header("Joints Configuration")]
        public ConfigurableJoint upperJoint;
        
        public ConfigurableJoint midJoint;
       
        public Transform endBone;

        private Quaternion initialUpperRotation;
        private Quaternion initialMidRotation;

        private float upperBoneLength;
        private float midBoneLength;
        private bool isInitialized = false;

        public override void Initialize()
        {
            if (upperJoint == null || midJoint == null || endBone == null) return;

            initialUpperRotation = upperJoint.transform.localRotation;
            initialMidRotation = midJoint.transform.localRotation;

            upperBoneLength = Vector3.Distance(upperJoint.transform.position, midJoint.transform.position);
            midBoneLength = Vector3.Distance(midJoint.transform.position, endBone.position);

            if (upperBoneLength < 0.01f) upperBoneLength = 0.5f;
            if (midBoneLength < 0.01f) midBoneLength = 0.5f;

            isInitialized = true;
        }

        public void ApplyIK(Vector3 endTarget, Vector3 poleTarget)
        {
            if (!isInitialized || upperJoint == null || midJoint == null) return;

            Vector3 forwardAxisNorm = localForwardAxis.sqrMagnitude > 0.001f ? localForwardAxis.normalized : Vector3.forward;
            Vector3 upAxisNorm = localUpAxis.sqrMagnitude > 0.001f ? localUpAxis.normalized : Vector3.up;
            Quaternion axisCorrection = Quaternion.Inverse(Quaternion.LookRotation(forwardAxisNorm, upAxisNorm));

            Vector3 upperJointPos = upperJoint.transform.position;

            Vector3 upperToEnd = endTarget - upperJointPos;
            float distToTarget = upperToEnd.magnitude;
            float maxLength = upperBoneLength + midBoneLength;

            if (distToTarget >= maxLength - 0.005f)
            {
                distToTarget = maxLength - 0.005f;
            }

            Vector3 upperToEndDir = upperToEnd.normalized;
            Vector3 upperToPoleTarget = poleTarget - upperJointPos;

            Vector3 chainNormal = Vector3.Cross(upperToEndDir, upperToPoleTarget).normalized;

            if (float.IsNaN(chainNormal.x) || chainNormal.sqrMagnitude < 0.1f)
            {
                Vector3 sideFallback = upperJoint.connectedBody != null ? upperJoint.connectedBody.transform.right : Vector3.right;
                chainNormal = Vector3.Cross(upperToEndDir, sideFallback).normalized;

                if (chainNormal.sqrMagnitude < 0.001f)
                {
                    chainNormal = Vector3.right;
                }
            }

            Vector3 worldMidForward = Vector3.Cross(chainNormal, upperToEndDir).normalized;
            if (worldMidForward.sqrMagnitude < 0.001f) worldMidForward = Vector3.forward;

            float cosMid = (upperBoneLength * upperBoneLength + midBoneLength * midBoneLength - distToTarget * distToTarget) / (2f * upperBoneLength * midBoneLength);
            float midAngleRad = Mathf.Acos(Mathf.Clamp(cosMid, -1f, 1f));
            float midAngleDeg = 180f - (midAngleRad * Mathf.Rad2Deg);

            midAngleDeg = Mathf.Clamp(midAngleDeg, 0f, 140f);

            Vector3 midLocalRotationAxis = Vector3.Cross(forwardAxisNorm, upAxisNorm).normalized;
            Quaternion midTargetLocalRot = Quaternion.AngleAxis(midAngleDeg, midLocalRotationAxis);

            midJoint.targetRotation = Quaternion.Inverse(midTargetLocalRot) * initialMidRotation;

            float cosUpper = (upperBoneLength * upperBoneLength + distToTarget * distToTarget - midBoneLength * midBoneLength) / (2f * upperBoneLength * distToTarget);
            float upperAngleDeg = Mathf.Acos(Mathf.Clamp(cosUpper, -1f, 1f)) * Mathf.Rad2Deg;

            Quaternion upperWorldLook = Quaternion.LookRotation(upperToEndDir, worldMidForward);
            upperWorldLook = Quaternion.AngleAxis(upperAngleDeg, chainNormal) * upperWorldLook;
            upperWorldLook = upperWorldLook * axisCorrection;

            if (upperJoint.connectedBody != null)
            {
                Quaternion targetUpperSpace = Quaternion.Inverse(upperJoint.connectedBody.transform.rotation) * upperWorldLook;

                Quaternion relativeRot = targetUpperSpace * Quaternion.Inverse(initialUpperRotation);
                Vector3 eulerAngles = relativeRot.eulerAngles;

                if (eulerAngles.x > 180f) eulerAngles.x -= 360f;
                if (eulerAngles.y > 180f) eulerAngles.y -= 360f;
                if (eulerAngles.z > 180f) eulerAngles.z -= 360f;

                if (upperJoint.angularXMotion == ConfigurableJointMotion.Limited)
                {
                    eulerAngles.x = Mathf.Clamp(eulerAngles.x, upperJoint.lowAngularXLimit.limit, upperJoint.highAngularXLimit.limit);
                }
                if (upperJoint.angularYMotion == ConfigurableJointMotion.Limited)
                {
                    eulerAngles.y = Mathf.Clamp(eulerAngles.y, -upperJoint.angularYLimit.limit, upperJoint.angularYLimit.limit);
                }
                if (upperJoint.angularZMotion == ConfigurableJointMotion.Limited)
                {
                    eulerAngles.z = Mathf.Clamp(eulerAngles.z, -upperJoint.angularZLimit.limit, upperJoint.angularZLimit.limit);
                }

                Quaternion clampedRelativeRot = Quaternion.Euler(eulerAngles);
                Quaternion finalTargetLocalRotation = clampedRelativeRot * initialUpperRotation;

                if (!float.IsNaN(finalTargetLocalRotation.x) && !float.IsNaN(finalTargetLocalRotation.y))
                {
                    upperJoint.targetRotation = Quaternion.Inverse(finalTargetLocalRotation) * initialUpperRotation;
                }
            }
            else
            {
                if (!float.IsNaN(upperWorldLook.x) && !float.IsNaN(upperWorldLook.y))
                {
                    upperJoint.targetRotation = Quaternion.Inverse(upperWorldLook) * initialUpperRotation;
                }
            }
        }
    }

    [Serializable]
    public class FullBodyIK : IKBasic
    {
        [Header("Chain Settings")]
        public Transform rootBone;
        public Transform endEffector;

        [Header("Physics Joints")]
        public List<ConfigurableJoint> jointsChain = new List<ConfigurableJoint>();

        [Header("FABRIK Settings")]
        public int iterations = 10;
        public float delta = 0.001f;

        private List<Transform> virtualBones = new List<Transform>();
        private List<Quaternion> initialLocalRotations = new List<Quaternion>();
        private float[] boneLengths;
        private float completeLength;
        private bool isInitialized = false;

        public override void Initialize()
        {
            if (jointsChain.Count == 0 || rootBone == null || endEffector == null) return;

            virtualBones.Clear();
            initialLocalRotations.Clear();

            foreach (var joint in jointsChain)
            {
                virtualBones.Add(joint.transform);
                initialLocalRotations.Add(joint.transform.localRotation);
            }
            virtualBones.Add(endEffector);

            boneLengths = new float[virtualBones.Count - 1];
            completeLength = 0;

            for (int i = 0; i < boneLengths.Length; i++)
            {
                boneLengths[i] = Vector3.Distance(virtualBones[i].position, virtualBones[i + 1].position);
                completeLength += boneLengths[i];
            }

            isInitialized = true;
        }

        public void ApplyIK(Vector3 targetPosition)
        {
            if (!isInitialized) return;

            Vector3[] positions = new Vector3[virtualBones.Count];
            for (int i = 0; i < virtualBones.Count; i++)
            {
                positions[i] = virtualBones[i].position;
            }

            Vector3 rootPos = virtualBones[0].position;

            if (Vector3.Distance(positions[0], targetPosition) >= completeLength)
            {
                Vector3 direction = (targetPosition - positions[0]).normalized;
                for (int i = 0; i < positions.Length - 1; i++)
                {
                    positions[i + 1] = positions[i] + direction * boneLengths[i];
                }
            }
            else
            {
                for (int iter = 0; iter < iterations; iter++)
                {
                    positions[positions.Length - 1] = targetPosition;
                    for (int i = positions.Length - 2; i >= 0; i--)
                    {
                        positions[i] = positions[i + 1] + (positions[i] - positions[i + 1]).normalized * boneLengths[i];
                    }

                    positions[0] = rootPos;
                    for (int i = 0; i < positions.Length - 1; i++)
                    {
                        positions[i + 1] = positions[i] + (positions[i + 1] - positions[i]).normalized * boneLengths[i];
                    }

                    if (Vector3.Distance(positions[positions.Length - 1], targetPosition) < delta)
                        break;
                }
            }

            for (int i = 0; i < jointsChain.Count; i++)
            {
                ConfigurableJoint joint = jointsChain[i];

                Vector3 calculatedDir = (positions[i + 1] - positions[i]).normalized;

                Vector3 upRef = joint.connectedBody != null ? joint.connectedBody.transform.forward : rootBone.forward;

                Quaternion lookRotation = Quaternion.LookRotation(calculatedDir, upRef);

                Vector3 forwardAxisNorm = localForwardAxis.sqrMagnitude > 0.001f ? localForwardAxis.normalized : Vector3.forward;
                Vector3 upAxisNorm = localUpAxis.sqrMagnitude > 0.001f ? localUpAxis.normalized : Vector3.up;
                Quaternion axisCorrection = Quaternion.Inverse(Quaternion.LookRotation(forwardAxisNorm, upAxisNorm));

                lookRotation = lookRotation * axisCorrection;

                if (joint.connectedBody != null)
                {
                    Quaternion targetLocalRotation = Quaternion.Inverse(joint.connectedBody.transform.rotation) * lookRotation;
                    joint.targetRotation = Quaternion.Inverse(targetLocalRotation) * initialLocalRotations[i];
                }
                else
                {
                    joint.targetRotation = Quaternion.Inverse(lookRotation) * initialLocalRotations[i];
                }
            }
        }
    }

    [Serializable]
    public class IKFoot : IKBasic
    {
        [Header("Joint")]
        public ConfigurableJoint footJoint;

        [Header("Foot Settings")]
        public float footPitchAngle = 20f;
        public float footRollAngle = 10f;
        public float footRotationSpeed = 10f;

        [Header("Curves")]
        public AnimationCurve footPitchCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve footRollCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Quaternion initialFootRotation;
        private bool isInitialized = false;

        public override void Initialize()
        {
            if (footJoint == null) return;

            initialFootRotation = footJoint.transform.localRotation;
            isInitialized = true;
        }

        public void ApplyIK(SimpleLeg leg)
        {
            if (!isInitialized || footJoint == null || leg == null) return;

            float phase = Mathf.Repeat(leg.previousPhase, 1f);
            Quaternion proceduralRotation = Quaternion.identity;

            if (leg.isSwinging)
            {
                float swingT = Mathf.InverseLerp(0.55f, 1f, phase);
                float pitch = footPitchCurve.Evaluate(swingT) * footPitchAngle;
                float roll = footRollCurve.Evaluate(swingT) * footRollAngle;

                proceduralRotation =
                    Quaternion.AngleAxis(pitch, Vector3.right) *
                    Quaternion.AngleAxis(roll, Vector3.forward);
            }
            else
            {
                float rollT = footRollCurve.Evaluate(Mathf.Repeat(phase, 1f));
                float roll = rollT * footRollAngle * 0.35f;

                proceduralRotation = Quaternion.AngleAxis(roll, Vector3.forward);
            }

            Quaternion targetLocalRotation = proceduralRotation * initialFootRotation;

            Quaternion targetJointSpace = ToJointSpaceRotation(footJoint, targetLocalRotation, initialFootRotation);

            footJoint.targetRotation = Quaternion.RotateTowards(
                footJoint.targetRotation,
                targetJointSpace,
                footRotationSpeed * 100f * Time.fixedDeltaTime
            );
        }

        private Quaternion ToJointSpaceRotation(ConfigurableJoint joint, Quaternion targetLocalRotation, Quaternion startLocalRotation)
        {
            Quaternion relativeRotation = Quaternion.Inverse(startLocalRotation) * targetLocalRotation;

            Vector3 jointX = joint.axis.normalized;
            Vector3 jointY = joint.secondaryAxis.normalized;
            Vector3 jointZ = Vector3.Cross(jointX, jointY).normalized;
            jointY = Vector3.Cross(jointZ, jointX).normalized;

            Matrix4x4 lookMatrix = Matrix4x4.identity;
            lookMatrix.SetColumn(0, new Vector4(jointX.x, jointX.y, jointX.z, 0));
            lookMatrix.SetColumn(1, new Vector4(jointY.x, jointY.y, jointY.z, 0));
            lookMatrix.SetColumn(2, new Vector4(jointZ.x, jointZ.y, jointZ.z, 0));

            Quaternion toJointSpace = lookMatrix.rotation;

            return Quaternion.Inverse(Quaternion.Inverse(toJointSpace) * relativeRotation * toJointSpace);
        }
    }
}