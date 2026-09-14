using System;
using UnityEngine;

public class AnimationExample: ProceduralAnimation
{
    public IKLookAt IKLookAt;

    public Transform TargetIK;

    public override void Start()
    {
        IKLookAt.Initialize();
    }

    public override void OnFixedUpdate()
    {
        IKLookAt.ApplyIK(TargetIK.position);
    }

}
