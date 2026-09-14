using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AdvancedRagdollCreator))]
public class AdvancedRagdollCreatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        AdvancedRagdollCreator creator = (AdvancedRagdollCreator)target;

        // Calculate total percentage to alert the user if they made a mistake
        float currentTotalPercentage = 0f;
        foreach (var cat in creator.categories)
        {
            currentTotalPercentage += cat.massPercentage;
        }

        // Show a warning if the sum of percentages does not equal 100
        if (Mathf.Abs(currentTotalPercentage - 100f) > 0.1f)
        {
            EditorGUILayout.HelpBox($"Warning: The sum of category mass percentages is {currentTotalPercentage}%, but it must be 100%!", MessageType.Warning);

            GUI.backgroundColor = new Color(0.3f, 1f, 0.3f); // Light green color for the action button
            if (GUILayout.Button("Auto-Balance Mass to 100%", GUILayout.Height(30)))
            {
                AutoBalanceMass(creator, currentTotalPercentage);
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            EditorGUILayout.HelpBox("Mass distribution is configured correctly (100%).", MessageType.Info);
        }

        DrawDefaultInspector();

        GUILayout.Space(15);
        if (GUILayout.Button("Generate Ragdoll", GUILayout.Height(40)))
        {
            GenerateRagdoll(creator);
        }

        if (GUILayout.Button("Remove Ragdoll Components", GUILayout.Height(25)))
        {
            CleanRagdoll(creator);
        }
    }

    private void AutoBalanceMass(AdvancedRagdollCreator creator, float currentTotal)
    {
        if (creator.categories == null || creator.categories.Count == 0) return;

        // Allows Ctrl+Z in Unity editor
        Undo.RecordObject(creator, "Auto-Balance Ragdoll Mass");

        if (currentTotal == 0)
        {
            float equalShare = 100f / creator.categories.Count;
            foreach (var cat in creator.categories)
            {
                cat.massPercentage = Mathf.Round(equalShare * 10f) / 10f;
            }
        }
        else
        {
            float factor = 100f / currentTotal;
            foreach (var cat in creator.categories)
            {
                cat.massPercentage = Mathf.Round((cat.massPercentage * factor) * 10f) / 10f;
            }
        }

        EditorUtility.SetDirty(creator);
    }

    private void GenerateRagdoll(AdvancedRagdollCreator creator)
    {
        CleanRagdoll(creator);

        foreach (var category in creator.categories)
        {
            if (category.bones == null || category.bones.Count == 0) continue;

            float categoryMass = creator.totalCharacterMass * (category.massPercentage / 100f);
            float massPerBone = categoryMass / category.bones.Count;

            foreach (var bone in category.bones)
            {
                if (bone == null) continue;

                Rigidbody rb = bone.gameObject.AddComponent<Rigidbody>();
                rb.mass = massPerBone;
                rb.angularDamping = 0.05f;
                rb.linearDamping = 0.0f;

                // ¬ключаем интерпол€цию дл€ плавного физического движени€ рэгдолла
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                CreateCollider(bone, category);
            }
        }

        // Second pass Ч creating joint connections
        foreach (var category in creator.categories)
        {
            foreach (var bone in category.bones)
            {
                if (bone == null) continue;

                Rigidbody parentRb = FindParentRigidbody(bone, creator);

                if (parentRb != null)
                {
                    ConfigurableJoint joint = bone.gameObject.AddComponent<ConfigurableJoint>();
                    joint.connectedBody = parentRb;

                    joint.anchor = Vector3.zero;
                    joint.autoConfigureConnectedAnchor = true;

                    joint.xMotion = ConfigurableJointMotion.Locked;
                    joint.yMotion = ConfigurableJointMotion.Locked;
                    joint.zMotion = ConfigurableJointMotion.Locked;

                    joint.angularXMotion = ConfigurableJointMotion.Limited;
                    joint.angularYMotion = ConfigurableJointMotion.Limited;
                    joint.angularZMotion = ConfigurableJointMotion.Limited;

                    // «ащита от раст€гивани€ суставов (особенно важно при интерпол€ции под нагрузкой)
                    joint.projectionMode = JointProjectionMode.PositionAndRotation;
                    joint.projectionDistance = 0.01f;
                    joint.projectionAngle = 1f;

                    joint.rotationDriveMode = RotationDriveMode.Slerp;

                    JointDrive slerpDrive = new JointDrive
                    {
                        positionSpring = category.spring,
                        positionDamper = category.damper,
                    };
                    joint.slerpDrive = slerpDrive;
                }
            }
        }

        Debug.Log("Ragdoll generated successfully with Interpolated Rigidbodies!");
    }

    private void CleanRagdoll(AdvancedRagdollCreator creator)
    {
        foreach (var category in creator.categories)
        {
            foreach (var bone in category.bones)
            {
                if (bone == null) continue;

                var joint = bone.GetComponent<ConfigurableJoint>();
                if (joint) DestroyImmediate(joint);

                var collider = bone.GetComponent<Collider>();
                if (collider) DestroyImmediate(collider);

                var rb = bone.GetComponent<Rigidbody>();
                if (rb) DestroyImmediate(rb);
            }
        }
        Debug.Log("Ragdoll components removed.");
    }

    private Rigidbody FindParentRigidbody(Transform currentBone, AdvancedRagdollCreator creator)
    {
        Transform t = currentBone.parent;
        while (t != null && t != creator.transform)
        {
            Rigidbody rb = t.GetComponent<Rigidbody>();
            if (rb != null) return rb;
            t = t.parent;
        }
        return null;
    }

    private void CreateCollider(Transform bone, AdvancedRagdollCreator.BoneCategory category)
    {
        float length = 0.2f;
        Vector3 direction = Vector3.right;

        if (bone.childCount > 0)
        {
            Transform child = bone.GetChild(0);
            direction = bone.InverseTransformPoint(child.position);
            length = direction.magnitude;
        }

        switch (category.colliderType)
        {
            case AdvancedRagdollCreator.ColliderType.Capsule:
                CapsuleCollider capsule = bone.gameObject.AddComponent<CapsuleCollider>();
                capsule.radius = category.boneRadius;
                capsule.height = length;
                capsule.center = direction * 0.5f;

                if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y) && Mathf.Abs(direction.x) > Mathf.Abs(direction.z)) capsule.direction = 0;
                else if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x) && Mathf.Abs(direction.y) > Mathf.Abs(direction.z)) capsule.direction = 1;
                else capsule.direction = 2;
                break;

            case AdvancedRagdollCreator.ColliderType.Box:
                BoxCollider box = box = bone.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(length, category.boneRadius * 2, category.boneRadius * 2);
                box.center = direction * 0.5f;
                break;

            case AdvancedRagdollCreator.ColliderType.Sphere:
                SphereCollider sphere = bone.gameObject.AddComponent<SphereCollider>();
                sphere.radius = category.boneRadius;
                sphere.center = Vector3.zero;
                break;
        }
    }
}
