using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health))]
public class EnemyRagdoll : MonoBehaviour
{
    public float deathImpulse = 2.5f;
    public float jointSpring = 12f;
    public float jointDamper = 2f;
    public WeaponType droppedWeaponType = WeaponType.PPSH;

    [Header("Смерть без физики (низкая графика)")]
    [Tooltip("Сколько секунд труп остаётся на сцене после отыгранной анимации смерти, прежде чем удалиться")]
    public float corpseLifetime = 12f;

    private const string DeathStateName = "die";
    private static readonly int DieTriggerId = Animator.StringToHash("Die");
    private static readonly int HitTriggerId = Animator.StringToHash("Hit");

    private readonly List<Collider> ragdollColliders = new List<Collider>();
    private readonly List<Rigidbody> ragdollBodies = new List<Rigidbody>();
    private readonly Dictionary<Transform, Rigidbody> bodiesByBone = new Dictionary<Transform, Rigidbody>();

    private Health health;
    private CharacterController characterController;
    private CapsuleCollider capsuleCollider;
    private NavMeshAgent navigationAgent;
    private GroundedEntity groundedEntity;
    private EnemyAI enemyAI;
    private NPCFollow npcFollow;
    private TeamFollowPlayer teamFollowPlayer;
    private CharacterAnimatorDriver animatorDriver;
    private Animator animator;
    private Rigidbody hipsBody;
    private bool isRagdoll;
    private bool physicsPrepared;

    public bool HasHumanoidHitboxes => ragdollColliders.Count > 0;

    private void Awake()
    {
        health = GetComponent<Health>();
        health.destroyOnDeath = false;
        health.OnDied += ActivateRagdoll;

        characterController = GetComponent<CharacterController>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        navigationAgent = GetComponent<NavMeshAgent>();
        groundedEntity = GetComponent<GroundedEntity>();
        enemyAI = GetComponent<EnemyAI>();
        npcFollow = GetComponent<NPCFollow>();
        teamFollowPlayer = GetComponent<TeamFollowPlayer>();
    }

    private void Start()
    {
        Initialize(transform);
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= ActivateRagdoll;
        }
    }

    public void Initialize(Transform characterVisual)
    {
        if (characterVisual == null)
        {
            characterVisual = transform;
        }

        animator = null;
        foreach (Animator candidate in characterVisual.GetComponentsInChildren<Animator>(true))
        {
            if (candidate.avatar != null && candidate.avatar.isHuman)
            {
                animator = candidate;
                break;
            }
        }

        if (animator == null)
        {
            Debug.LogWarning("EnemyRagdoll could not find a humanoid Animator in the character hierarchy.", this);
            return;
        }

        // Низкая графика: физику не строим вообще — коллайдеры, тела и суставы
        // на костях не нужны, смерть отыграет Animator (состояние "die").
        if (!RagdollSettings.RagdollEnabled)
        {
            return;
        }

        physicsPrepared = true;

        Transform hips = GetBone(HumanBodyBones.Hips);
        Transform spine = GetBone(HumanBodyBones.Spine);
        Transform chest = GetBone(HumanBodyBones.UpperChest);
        if (chest == null)
        {
            chest = GetBone(HumanBodyBones.Chest);
        }
        Transform head = GetBone(HumanBodyBones.Head);

        if (hips == null || spine == null || chest == null || head == null)
        {
            Debug.LogWarning("EnemyRagdoll could not find the required humanoid bones.", this);
            return;
        }

        ConfigureSegment(hips, spine, null, 0.16f, 5f, false);
        ConfigureSegment(spine, chest, hips, 0.13f, 4f, false);
        ConfigureSegment(chest, head, spine, 0.16f, 4f, false);
        ConfigureSegment(head, null, chest, 0.12f, 2f, true);

        ConfigureLimb(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, chest, 0.075f);
        ConfigureLimb(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, chest, 0.075f);
        ConfigureLimb(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, hips, 0.11f);
        ConfigureLimb(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, hips, 0.11f);

        bodiesByBone.TryGetValue(hips, out hipsBody);
    }

    private void ConfigureLimb(HumanBodyBones upperId, HumanBodyBones lowerId, HumanBodyBones endId, Transform torsoParent, float radius)
    {
        Transform upper = GetBone(upperId);
        Transform lower = GetBone(lowerId);
        Transform end = GetBone(endId);
        if (upper == null || lower == null || end == null)
        {
            return;
        }

        ConfigureSegment(upper, lower, torsoParent, radius, 2f, false);
        ConfigureSegment(lower, end, upper, radius * 0.85f, 2f, false);
        // Кисти и ступни отдельными физическими телами НЕ делаем: у них
        // почти нулевая инерция, и после включения рэгдола они начинают
        // бешено крутиться. Пусть остаются визуальными костями на
        // предплечье/голени и падают вместе с ними.
    }

    private Transform GetBone(HumanBodyBones bone)
    {
        return animator != null ? animator.GetBoneTransform(bone) : null;
    }

    private void ConfigureSegment(Transform bone, Transform endpoint, Transform parentBone, float radius, float mass, bool sphere)
    {
        if (bone == null || bodiesByBone.ContainsKey(bone))
        {
            return;
        }

        float lossyScale = Mathf.Max(Mathf.Abs(bone.lossyScale.x), Mathf.Abs(bone.lossyScale.y), Mathf.Abs(bone.lossyScale.z));
        float localRadius = radius / Mathf.Max(lossyScale, 0.0001f);
        Collider boneCollider;
        if (sphere || endpoint == null)
        {
            SphereCollider sphereCollider = bone.gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = localRadius;
            boneCollider = sphereCollider;
        }
        else
        {
            Vector3 localEnd = bone.InverseTransformPoint(endpoint.position);
            float length = localEnd.magnitude;
            if (length <= 0.001f)
            {
                return;
            }

            CapsuleCollider capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = localRadius;
            capsule.height = length + localRadius * 2f;
            capsule.center = localEnd * 0.5f;
            Vector3 absoluteEnd = new Vector3(Mathf.Abs(localEnd.x), Mathf.Abs(localEnd.y), Mathf.Abs(localEnd.z));
            capsule.direction = absoluteEnd.x > absoluteEnd.y && absoluteEnd.x > absoluteEnd.z ? 0 : absoluteEnd.y > absoluteEnd.z ? 1 : 2;
            boneCollider = capsule;
        }

        boneCollider.isTrigger = true;
        boneCollider.enabled = true;
        ragdollColliders.Add(boneCollider);

        Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
        body.mass = mass;
        body.useGravity = true;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        // Гасим вращение костей: иначе кисти и ступни с малой инерцией
        // начинают бешено крутиться вокруг своей оси после включения физики.
        body.drag = 0.4f;
        body.angularDrag = 8f;
        body.maxAngularVelocity = 5f;
        body.sleepThreshold = 0.05f;
        ragdollBodies.Add(body);
        bodiesByBone.Add(bone, body);

        if (parentBone != null && bodiesByBone.TryGetValue(parentBone, out Rigidbody parentBody))
        {
            CharacterJoint joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parentBody;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = parentBone.InverseTransformPoint(bone.position);
            joint.enableProjection = true;
            joint.projectionDistance = 0.12f;
            joint.projectionAngle = 18f;
            SoftJointLimit lowTwist = joint.lowTwistLimit;
            lowTwist.limit = -20f;
            joint.lowTwistLimit = lowTwist;
            SoftJointLimit highTwist = joint.highTwistLimit;
            highTwist.limit = 20f;
            joint.highTwistLimit = highTwist;
            SoftJointLimit swing1 = joint.swing1Limit;
            swing1.limit = 30f;
            joint.swing1Limit = swing1;
            SoftJointLimit swing2 = joint.swing2Limit;
            swing2.limit = 30f;
            joint.swing2Limit = swing2;
            SoftJointLimitSpring spring = joint.swingLimitSpring;
            spring.spring = jointSpring * 4f;
            spring.damper = jointDamper * 4f;
            joint.swingLimitSpring = spring;
        }
    }

    private void ActivateRagdoll(Health deadHealth)
    {
        if (isRagdoll)
        {
            return;
        }

        isRagdoll = true;
        if (enemyAI != null)
        {
            enemyAI.StopNavigation();
            enemyAI.enabled = false;
        }
        if (npcFollow != null)
        {
            npcFollow.enabled = false;
        }
        if (navigationAgent != null)
        {
            if (navigationAgent.isOnNavMesh)
            {
                navigationAgent.isStopped = true;
                navigationAgent.ResetPath();
            }
            navigationAgent.enabled = false;
        }

        // Союзникам агент добавляется в рантайме, после Awake рэгдола.
        NavMeshAgent[] runtimeAgents = GetComponentsInChildren<NavMeshAgent>(true);
        for (int i = 0; i < runtimeAgents.Length; i++)
        {
            if (runtimeAgents[i] == navigationAgent)
            {
                continue;
            }

            if (runtimeAgents[i].enabled && runtimeAgents[i].isOnNavMesh)
            {
                runtimeAgents[i].isStopped = true;
                runtimeAgents[i].ResetPath();
            }
            runtimeAgents[i].enabled = false;
        }
        if (groundedEntity != null)
        {
            groundedEntity.enabled = false;
        }
        // Отключаем ВСЕ драйверы и аниматоры в иерархии: у некоторых
        // персонажей компоненты продублированы на дочерней модели, и
        // оставшийся включённым Animator продолжает крутить кости.
        // При выключенной физике (низкая графика) аниматор, наоборот, нужен
        // живым — он отыгрывает смерть.
        if (RagdollSettings.RagdollEnabled)
        {
            CharacterAnimatorDriver[] drivers = GetComponentsInChildren<CharacterAnimatorDriver>(true);
            for (int i = 0; i < drivers.Length; i++)
            {
                drivers[i].enabled = false;
            }

            Animator[] animators = GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].enabled = false;
            }
        }
        if (teamFollowPlayer != null)
        {
            teamFollowPlayer.enabled = false;
        }

        // Союзники получают TeamFollowPlayer/AllyCombatAI уже после Awake,
        // поэтому ищем их в иерархии в момент смерти.
        TeamFollowPlayer[] followers = GetComponentsInChildren<TeamFollowPlayer>(true);
        for (int i = 0; i < followers.Length; i++)
        {
            followers[i].enabled = false;
        }

        AllyCombatAI[] allyCombat = GetComponentsInChildren<AllyCombatAI>(true);
        for (int i = 0; i < allyCombat.Length; i++)
        {
            allyCombat[i].enabled = false;
        }

        CivilianFlee[] fleeingCivilians = GetComponentsInChildren<CivilianFlee>(true);
        for (int i = 0; i < fleeingCivilians.Length; i++)
        {
            fleeingCivilians[i].enabled = false;
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }
        if (capsuleCollider != null)
        {
            capsuleCollider.enabled = false;
        }

        DropWeapon();

        // Низкая графика: физику не включаем, смерть отыгрывает Animator.
        // Второе условие — физика не была построена на старте (низкая графика
        // при спавне), а настройки успели поднять уже после смерти.
        if (!RagdollSettings.RagdollEnabled || !physicsPrepared)
        {
            PlayDeathAnimation();
            return;
        }

        if (ragdollBodies.Count == 0)
        {
            Debug.LogWarning("EnemyRagdoll has no configured humanoid bodies; using the root-body fallback.", this);
            ActivateFallbackRagdoll();
            return;
        }

        foreach (Collider ragdollCollider in ragdollColliders)
        {
            ragdollCollider.isTrigger = false;
            ragdollCollider.enabled = true;
        }
        foreach (Rigidbody body in ragdollBodies)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = false;
            body.WakeUp();
        }

        if (hipsBody != null)
        {
            Vector3 impulseDirection = (transform.forward * -0.6f + Vector3.up * 0.8f).normalized;
            hipsBody.AddForce(impulseDirection * deathImpulse, ForceMode.Impulse);
            hipsBody.AddTorque(Random.onUnitSphere * deathImpulse * 0.15f, ForceMode.Impulse);
        }
    }

    private void DropWeapon()
    {
        Transform weapon = null;
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "WeaponSlot" || child.name == "EnemyRifle")
            {
                weapon = child;
                break;
            }
        }

        if (weapon == null)
        {
            return;
        }

        weapon.SetParent(transform.parent, true);
        WeaponPickup pickup = weapon.GetComponent<WeaponPickup>();
        if (pickup == null)
        {
            pickup = weapon.gameObject.AddComponent<WeaponPickup>();
        }
        pickup.ConfigureDroppedWeapon(droppedWeaponType, weapon.gameObject);

        Renderer[] renderers = weapon.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            BoxCollider weaponCollider = weapon.GetComponent<BoxCollider>();
            if (weaponCollider == null)
            {
                weaponCollider = weapon.gameObject.AddComponent<BoxCollider>();
            }
            weaponCollider.center = weapon.InverseTransformPoint(bounds.center);
            weaponCollider.size = bounds.size;
            weaponCollider.isTrigger = false;
        }

        Rigidbody weaponBody = weapon.GetComponent<Rigidbody>();
        if (weaponBody == null)
        {
            weaponBody = weapon.gameObject.AddComponent<Rigidbody>();
        }
        weaponBody.mass = 2f;
        weaponBody.interpolation = RigidbodyInterpolation.Interpolate;
        weaponBody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        weaponBody.AddForce((Vector3.up * 0.8f + transform.right * Random.Range(-0.5f, 0.5f)) * deathImpulse, ForceMode.Impulse);
        weaponBody.AddTorque(Random.onUnitSphere * deathImpulse * 0.2f, ForceMode.Impulse);
    }

    private void ActivateFallbackRagdoll()
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            capsule = gameObject.AddComponent<CapsuleCollider>();
        }

        capsule.enabled = true;
        capsule.isTrigger = false;
        capsule.center = new Vector3(0f, 0.9f, 0f);
        capsule.height = 1.8f;
        capsule.radius = 0.28f;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.mass = 70f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        body.AddForce((Vector3.up - transform.forward * 0.5f) * deathImpulse, ForceMode.Impulse);
        body.AddTorque(Random.onUnitSphere * deathImpulse * 0.25f, ForceMode.Impulse);
    }

    // --- Смерть через аниматор (физика рэгдола отключена, низкая графика) ---

    private void PlayDeathAnimation()
    {
        if (animator == null)
        {
            animator = FindHumanoidAnimator(transform);
        }

        if (animator == null)
        {
            // Аниматора нет — держать труп некому, просто убираем его.
            Destroy(gameObject, corpseLifetime);
            return;
        }

        // У части NPC в иерархии два Animator'а на одном скелете.
        // Второй продолжает крутить idle и «перебивает» анимацию смерти —
        // на низкой графике оставляем только один.
        Animator[] allAnimators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < allAnimators.Length; i++)
        {
            if (allAnimators[i] != animator)
            {
                allAnimators[i].enabled = false;
            }
        }

        // Драйвер анимаций больше не должен ничего писать в аниматор:
        // событие Hit от того же удара оставляет «висящий» триггер, который
        // на следующем кадре перебивает состояние смерти (AnyState -> hit),
        // и труп возвращается в стойку.
        CharacterAnimatorDriver[] drivers = GetComponentsInChildren<CharacterAnimatorDriver>(true);
        for (int i = 0; i < drivers.Length; i++)
        {
            drivers[i].enabled = false;
        }

        animator.enabled = true;
        animator.speed = 1f;
        // Триггер Die ставит и CharacterAnimatorDriver по событию OnDied,
        // но порядок подписки не гарантирован — ставим сами.
        animator.ResetTrigger(HitTriggerId);
        animator.ResetTrigger(DieTriggerId);
        animator.SetTrigger(DieTriggerId);

        // Страховка: если в контроллере нет перехода по триггеру Die,
        // включаем состояние "die" напрямую, чтобы труп не стоял столбом.
        StartCoroutine(ForceDeathState());
        StartCoroutine(FreezeDeathAfterClip());
    }

    private System.Collections.IEnumerator ForceDeathState()
    {
        // Даём триггеру шанс отработать пару кадров.
        yield return null;
        yield return null;
        if (animator != null && !IsDeathStatePlaying())
        {
            // Снимаем возможный отложенный Hit, иначе он перебьёт смерть.
            animator.ResetTrigger(HitTriggerId);
            animator.Play(DeathStateName, 0, 0f);
            yield return null;
            if (!IsDeathStatePlaying())
            {
                animator.Play("Base Layer." + DeathStateName, 0, 0f);
            }
        }
    }

    private System.Collections.IEnumerator FreezeDeathAfterClip()
    {
        // Ждём перехода в состояние смерти (переход в контроллере — 0.25 с).
        float waited = 0f;
        while (waited < 1.5f && !IsDeathStatePlaying())
        {
            waited += Time.deltaTime;
            yield return null;
        }

        // Длину клипа берём у текущего состояния — AnimatorClipInfo в момент
        // перехода ещё пуст, из-за чего раньше труп замирал на первом кадре.
        float clipLength = 0f;
        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            clipLength = stateInfo.length;
        }

        if (clipLength <= 0.01f)
        {
            clipLength = GetDeathClipLength();
        }

        if (clipLength <= 0.01f)
        {
            clipLength = 1.5f;
        }

        // Клип должен реально доиграть до конца, только потом морозим.
        // Ждём по normalizedTime (устойчиво к перезапуску состояния),
        // страховочный таймаут — на случай зацикленного клипа.
        float elapsed = 0f;
        float timeout = clipLength * 1.5f + 0.5f;
        while (animator != null && elapsed < timeout)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if ((info.IsName(DeathStateName) || info.IsName("Base Layer." + DeathStateName)) &&
                info.normalizedTime >= 1f)
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // У миксamo-клипов падение «в пол» держится на root motion, а он у
        // NPC выключен (движение делает NavMeshAgent). Без этого труп лежит
        // горизонтально, но висит в воздухе на высоте бёдер — опускаем его
        // так, чтобы самая нижняя точка позы легла на поверхность.
        SettleOnGround();

        if (animator != null)
        {
            animator.speed = 0f;
        }

        yield return new WaitForSeconds(corpseLifetime);
        Destroy(gameObject);
    }

    // Опускает/поднимает корень трупа так, чтобы его нижняя точка касалась земли.
    private void SettleOnGround()
    {
        if (animator == null)
        {
            return;
        }

        // Нижнюю точку трупа берём по костям скелета, а не по Bounds рендереров:
        // у лежащего миксamo-трупа Bounds остаются «стоячими» (~2 м в высоту,
        // т.к. бинды считаются от корневой кости без поворота) и врут по вертикали.
        float lowestY = float.PositiveInfinity;
        Vector3 center = Vector3.zero;
        int sampled = 0;

        HumanBodyBones[] boneIds = new HumanBodyBones[]
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Head,
            HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg
        };

        for (int i = 0; i < boneIds.Length; i++)
        {
            Transform bone = animator.GetBoneTransform(boneIds[i]);
            if (bone == null)
            {
                continue;
            }

            if (bone.position.y < lowestY)
            {
                lowestY = bone.position.y;
            }

            center += bone.position;
            sampled++;
        }

        if (sampled == 0 || float.IsPositiveInfinity(lowestY))
        {
            return;
        }

        center /= sampled;

        float groundY = float.NegativeInfinity;
        Vector3 origin = center + Vector3.up * 2f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null || collider.transform == transform || collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hits[i].point.y > groundY)
            {
                groundY = hits[i].point.y;
            }
        }

        if (float.IsNegativeInfinity(groundY))
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                return;
            }

            groundY = terrain.SampleHeight(center) + terrain.transform.position.y;
        }

        // Кость — центр тела, до поверхности остаётся примерно 0.08 м толщины.
        float delta = groundY - lowestY + 0.08f;
        if (Mathf.Abs(delta) > 0.01f)
        {
            transform.position += Vector3.up * delta;
        }
    }

    private bool IsDeathStatePlaying()
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        return info.IsName(DeathStateName) || info.IsName("Base Layer." + DeathStateName);
    }

    private float GetDeathClipLength()
    {
        if (animator == null)
        {
            return 0f;
        }

        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
        if (clips == null || clips.Length == 0 || clips[0].clip == null)
        {
            return 0f;
        }

        return clips[0].clip.length;
    }

    private static Animator FindHumanoidAnimator(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Animator[] candidates = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i].avatar != null && candidates[i].avatar.isHuman)
            {
                return candidates[i];
            }
        }

        return null;
    }
}
