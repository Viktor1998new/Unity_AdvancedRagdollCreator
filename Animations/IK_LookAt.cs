using UnityEngine;

public class IK_LookAt : ProceduralAnimation
{
    public TwoBoneIKLookAt iKLook;

    public Transform TargetIK;
    public Transform kneeIK;

    [Header("Gizmos Settings")]
    public bool drawGizmos = true;
    public Color boneColor = Color.green;
    public Color targetColor = Color.cyan;
    public Color poleColor = Color.yellow;

    public override void Start()
    {
        iKLook.Initialize();
    }

    public override void OnFixedUpdate()
    {
        if (TargetIK != null && kneeIK != null)
        {
            iKLook.ApplyIK(TargetIK.position, kneeIK.position);
        }
    }

    // Автоматический метод Unity для отрисовки отладочной графики в окне Scene
    public override void OnDrawGizmos()
    {
        if (!drawGizmos || iKLook == null || iKLook.upperJoint == null || iKLook.midJoint == null)
            return;

        // 1. Рисуем финальную цель IK (TargetIK) в виде сферы и кольца
        if (TargetIK != null)
        {
            Gizmos.color = targetColor;
            Gizmos.DrawSphere(TargetIK.position, 0.06f);
            Gizmos.DrawWireSphere(TargetIK.position, 0.08f);
        }

        // 2. Рисуем цель направления сустава (KneeIK / Pole Target)
        if (kneeIK != null)
        {
            Gizmos.color = poleColor;
            Gizmos.DrawWireSphere(kneeIK.position, 0.05f);

            // Соединяем линией средний сустав (колено/локоть) с его подсказкой направления
            Vector3 midJointPos = iKLook.midJoint.transform.position;
            Gizmos.DrawLine(midJointPos, kneeIK.position);
        }

        // 3. Рисуем текущие физические кости конечности
        Gizmos.color = boneColor;
        Vector3 upperJointPos = iKLook.upperJoint.transform.position;
        Vector3 midJointPosCurrent = iKLook.midJoint.transform.position;

        // Кость 1: От плеча/бедра до локтя/колена
        Gizmos.DrawLine(upperJointPos, midJointPosCurrent);

        // Кость 2: От локтя/колена до текущего физического конца конечности
        // Если у среднего сустава есть дочерний объект (стопа/кисть), тянем линию к нему, иначе к TargetIK
        Vector3 endPointPos = iKLook.midJoint.transform.childCount > 0
            ? iKLook.midJoint.transform.GetChild(0).position
            : (TargetIK != null ? TargetIK.position : midJointPosCurrent);

        Gizmos.DrawLine(midJointPosCurrent, endPointPos);

        // 4. Рисуем пунктирную/тонкую линию траектории от начала конечности до цели
        if (TargetIK != null)
        {
            Gizmos.color = new Color(targetColor.r, targetColor.g, targetColor.b, 0.4f);
            Gizmos.DrawLine(upperJointPos, TargetIK.position);
        }
    }
}
