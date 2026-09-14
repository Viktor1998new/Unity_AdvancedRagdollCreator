using System;
using System.Collections.Generic;
using UnityEngine;

public class AdvancedRagdollCreator : MonoBehaviour
{
    public enum ColliderType { Capsule, Box, Sphere }

    [Serializable]
    public class BoneCategory
    {
        public string categoryName;
        public float spring = 15000f; // Base Active Ragdoll power
        public float damper = 1000f;
        public float maxForce = float.MaxValue;
        public ColliderType colliderType = ColliderType.Capsule;
        public float boneRadius = 0.05f;

        [Header("Manual Dimensions (Fix for Feet/Head)")]
        public bool manualColliderDimensions = false;
        public Vector3 customColliderSize = new Vector3(0.2f, 0.1f, 0.1f);

        [Header("Collider Offset Customization")]
        public Vector3 colliderCenterOffset = Vector3.zero;
        public Vector3 colliderRotationOffset = Vector3.zero;

        [Space]
        [Range(0f, 100f)]
        public float massPercentage = 20f;
        public List<Transform> bones = new List<Transform>();
    }

    [Header("Physical Body Settings")]
    public float totalCharacterMass = 80f;

    [Header("Fall Mode Control")]
    [SerializeField] private bool isFalling = false;
    public List<BoneCategory> categories = new List<BoneCategory>();

    [Header("Gizmos Settings")]
    [Tooltip("Длина стрелочек осей в окне сцены")]
    public float axisLength = 0.15f;
    [Tooltip("Показывать текстовые названия костей рядом с осями?")]
    public bool showBoneNames = true;

    [ProceduralAnimationSelector]
    [SerializeReference]
    public List<ProceduralAnimation> animationsList = new List<ProceduralAnimation>();

    public bool IsFalling
    {
        get => isFalling;
        set
        {
            if (isFalling != value)
            {
                isFalling = value;
                UpdateRagdollState();
            }
        }
    }

    private PlayerController _PC;

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            UpdateRagdollState();
        }
    }

    private void Start()
    {
        foreach (var anim in animationsList)
        {
            anim.Init(this);
        }

        UpdateRagdollState();
    }

    public void UpdateRagdollState()
    {
        foreach (var category in categories)
        {
            foreach (var bone in category.bones)
            {
                if (bone == null) continue;
                ConfigurableJoint joint = bone.GetComponent<ConfigurableJoint>();
                if (joint == null) continue;

                float targetSpring = isFalling ? 0f : category.spring;
                float targetDamper = isFalling ? 1f : category.damper;

                JointDrive drive = new JointDrive
                {
                    positionSpring = targetSpring,
                    positionDamper = targetDamper,
                    maximumForce = category.maxForce
                };

                joint.angularXDrive = drive;
                joint.angularYZDrive = drive;
                joint.slerpDrive = drive;
            }
        }
    }

    private void FixedUpdate()
    {
        foreach (var anim in animationsList)
        {
            if (anim != null && anim.Enable)
            {
                anim.OnFixedUpdate();
            }
        }
    }

    private void OnDrawGizmos()
    {
        foreach (var anim in animationsList)
        {
            if (anim != null && anim.Enable)
            {
                anim.OnDrawGizmos();
            }
        }

        if (categories == null) return;

        foreach (var category in categories)
        {
            if (category.bones == null) continue;

            foreach (var bone in category.bones)
            {
                if (bone == null) continue;

                ConfigurableJoint joint = bone.GetComponent<ConfigurableJoint>();
                Vector3 bonePos = bone.position;

                    Gizmos.color = new Color(0.5f, 0.1f, 0.1f, 0.4f);
                    Gizmos.DrawRay(bonePos, bone.right * axisLength);

                    Gizmos.color = new Color(0.1f, 0.5f, 0.1f, 0.4f);
                    Gizmos.DrawRay(bonePos, bone.up * axisLength);

                    Gizmos.color = new Color(0.1f, 0.1f, 0.5f, 0.4f);
                    Gizmos.DrawRay(bonePos, bone.forward * axisLength);
                
            }
        }
    }

}