using UnityEngine;
using UnityEngine.AI;

// Полноценный IK персонажа:
//  - левая рука доводится до цевья оружия (правая держит оружие через привязку к кисти);
//  - ступни подстраиваются под рельеф: когда персонаж стоит и ступни висят над землёй,
//    тело приземляется вниз; при ходьбе поджимается только опорная ступня, переносимая
//    (в воздухе) не трогается.
// Работает только на гуманоидных аниматорах и не мешает анимации смерти.
[RequireComponent(typeof(Animator))]
public class CharacterIK : MonoBehaviour
{
    [Header("Руки")]
    [Range(0f, 1f)] public float handIkWeight = 1f;
    [Range(0f, 1f)] public float elbowHintWeight = 0.45f;
    // Доля длины оружия от центра, где левая рука держит цевьё.
    [Range(0f, 1f)] public float foregripPosition = 0.55f;

    [Header("Ноги")]
    [Range(0f, 1f)] public float footIkWeight = 0.75f;
    [Range(0f, 1f)] public float footRotationWeight = 0.5f;
    [Tooltip("Высота, с которой начинается луч под ступнёй")]
    public float footRayStart = 0.6f;
    [Tooltip("Длина луча от точки старта вниз")]
    public float footRayLength = 1.6f;
    [Tooltip("На сколько ступня (кость) должна быть выше поверхности при касании")]
    public float ankleOffset = 0.06f;
    [Tooltip("Если ступня выше поверхности больше этого — нога в полёте, IK пропускается")]
    public float plantRange = 0.15f;
    [Tooltip("Максимальный подъём ступни относительно анимации (защита от «ступни вверх»)")]
    public float maxLift = 0.12f;
    [Tooltip("Максимальное опускание ступни/тела относительно анимации")]
    public float maxDrop = 0.4f;
    [Tooltip("Порог горизонтальной скорости: ниже — персонаж считается стоящим, и его можно приземлять")]
    public float standSpeedThreshold = 0.15f;
    public LayerMask groundMask = ~0;

    private Animator animator;
    private Health health;

    private Transform leftHand;
    private Transform leftLowerArm;
    private Transform leftFoot;
    private Transform rightFoot;

    // Корень персонажа (объект с коллайдером/агентами): его собственные коллайдеры
    // не должны считаться «землёй» при рейкасте ступней.
    private Transform characterRoot;
    private NavMeshAgent agent;
    private CharacterController controller;
    private bool ready;

    // Буфер под рейкасты ступней: без аллокаций каждый кадр.
    private static readonly RaycastHit[] FootHits = new RaycastHit[8];

    private void Awake()
    {
        animator = GetComponent<Animator>();
        health = GetComponent<Health>();

        // У части персонажей в иерархии два Animator'а — IK должен считаться
        // один раз, на корневом.
        Transform parent = transform.parent;
        while (parent != null)
        {
            if (parent.GetComponent<CharacterIK>() != null)
            {
                enabled = false;
                return;
            }

            parent = parent.parent;
        }

        if (animator == null || !animator.isHuman)
        {
            enabled = false;
            return;
        }

        leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

        // Находим корень персонажа: самый верхний предок с маркером персонажа.
        characterRoot = transform;
        Transform cursor = transform;
        while (cursor.parent != null)
        {
            cursor = cursor.parent;
            if (cursor.GetComponent<CharacterController>() != null ||
                cursor.GetComponent<CapsuleCollider>() != null ||
                cursor.GetComponent<EnemyRagdoll>() != null ||
                cursor.GetComponent<Health>() != null ||
                cursor.GetComponent<NavMeshAgent>() != null)
            {
                characterRoot = cursor;
            }
        }

        agent = characterRoot.GetComponent<NavMeshAgent>();
        controller = characterRoot.GetComponent<CharacterController>();

        ready = leftHand != null && leftFoot != null && rightFoot != null;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!ready)
        {
            return;
        }

        // Во время смерти позой управляет только EnemyRagdoll.
        if (health != null && health.CurrentHealth <= 0f)
        {
            ResetIK();
            return;
        }

        ApplyHandIK();
        ApplyFootIK();
    }

    private void ApplyHandIK()
    {
        Transform weapon = FindWeaponModel();
        if (weapon == null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
            return;
        }

        Renderer[] renderers = weapon.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        // Оружие у ботов смотрит вперёд персонажа (WeaponView у AI-режима
        // обнуляет локальный поворот и наследует поворот корня).
        Vector3 forward = transform.forward;
        float halfLength = Mathf.Abs(Vector3.Dot(bounds.extents, forward));
        Vector3 target = bounds.center + forward * (halfLength * foregripPosition);
        target += Vector3.up * 0.02f;

        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, handIkWeight);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, target);

        // Кисть не докручиваем: у миксamo-скелета своя ориентация кости,
        // доворот обычно ломает запястье. Двигаем только позицию.
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);

        // Локоть уводим наружу и вниз — рука не должна выворачиваться через грудь.
        if (leftLowerArm != null)
        {
            Vector3 hint = leftLowerArm.position - transform.right * 0.25f - transform.up * 0.2f;
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, elbowHintWeight * handIkWeight);
            animator.SetIKHintPosition(AvatarIKHint.LeftElbow, hint);
        }
    }

    private void ApplyFootIK()
    {
        if (footIkWeight <= 0f)
        {
            ClearFootWeights();
            return;
        }

        bool leftGround = RayFootGround(leftFoot.position, out RaycastHit leftHit);
        bool rightGround = RayFootGround(rightFoot.position, out RaycastHit rightHit);

        if (!leftGround && !rightGround)
        {
            ClearFootWeights();
            return;
        }

        // Персонаж стоит (не идёт), а ступни висят над землёй — опускаем всё тело,
        // чтобы нижняя ступня легла на поверхность. При ходьбе этого не делаем,
        // чтобы не дёргать таз на каждом шаге (там опорная нога и так у земли).
        if (GetHorizontalSpeed() < standSpeedThreshold)
        {
            float leftToGround = leftGround ? leftFoot.position.y - leftHit.point.y : float.PositiveInfinity;
            float rightToGround = rightGround ? rightFoot.position.y - rightHit.point.y : float.PositiveInfinity;
            float lowToGround = Mathf.Min(leftToGround, rightToGround);

            if (!float.IsPositiveInfinity(lowToGround) && lowToGround > ankleOffset + 0.02f)
            {
                float drop = Mathf.Clamp(lowToGround - ankleOffset, 0f, maxDrop);
                if (drop > 0.001f)
                {
                    Vector3 bp = animator.bodyPosition;
                    bp.y -= drop;
                    animator.bodyPosition = bp;
                }
            }
        }

        ApplyFoot(leftFoot, AvatarIKGoal.LeftFoot, leftGround, leftHit);
        ApplyFoot(rightFoot, AvatarIKGoal.RightFoot, rightGround, rightHit);
    }

    private void ApplyFoot(Transform foot, AvatarIKGoal goal, bool grounded, RaycastHit hit)
    {
        if (foot == null || !grounded)
        {
            animator.SetIKPositionWeight(goal, 0f);
            animator.SetIKRotationWeight(goal, 0f);
            return;
        }

        // Нога в полёте (фаза переноса) — не прижимаем её к земле.
        float footToGround = foot.position.y - hit.point.y;
        if (footToGround > plantRange)
        {
            animator.SetIKPositionWeight(goal, 0f);
            animator.SetIKRotationWeight(goal, 0f);
            return;
        }

        float targetY = hit.point.y + ankleOffset;
        float delta = targetY - foot.position.y;
        delta = Mathf.Clamp(delta, -maxDrop, maxLift);

        Vector3 target = foot.position;
        target.y = foot.position.y + delta;

        animator.SetIKPositionWeight(goal, footIkWeight);
        animator.SetIKPosition(goal, target);
        animator.SetIKRotationWeight(goal, footRotationWeight);
        animator.SetIKRotation(goal, Quaternion.FromToRotation(Vector3.up, hit.normal) * foot.rotation);
    }

    private bool RayFootGround(Vector3 footPos, out RaycastHit best)
    {
        best = new RaycastHit();

        Vector3 origin = footPos + Vector3.up * footRayStart;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, FootHits, footRayStart + footRayLength, groundMask, QueryTriggerInteraction.Ignore);

        bool found = false;
        for (int i = 0; i < count; i++)
        {
            Collider collider = FootHits[i].collider;
            if (collider == null || IsOwnCollider(collider))
            {
                continue;
            }

            if (!found || FootHits[i].point.y > best.point.y)
            {
                best = FootHits[i];
                found = true;
            }
        }

        return found;
    }

    private float GetHorizontalSpeed()
    {
        if (agent != null)
        {
            if (!agent.isOnNavMesh)
            {
                return 0f;
            }

            Vector3 v = agent.velocity;
            v.y = 0f;
            return v.magnitude;
        }

        if (controller != null)
        {
            Vector3 v = controller.velocity;
            v.y = 0f;
            return v.magnitude;
        }

        return 0f;
    }

    private bool IsOwnCollider(Collider collider)
    {
        if (collider == null)
        {
            return true;
        }

        Transform t = collider.transform;
        return t == characterRoot || t.IsChildOf(characterRoot);
    }

    private Transform FindWeaponModel()
    {
        WeaponView view = GetComponentInChildren<WeaponView>(true);
        if (view != null)
        {
            return view.transform;
        }

        // На случай, если WeaponView ещё не создан, ищем якорь по имени.
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == "WeaponViewAnchor")
            {
                return children[i];
            }
        }

        return null;
    }

    private void ClearFootWeights()
    {
        animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
        animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
        animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
    }

    private void ResetIK()
    {
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
        animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
        ClearFootWeights();
    }
}
