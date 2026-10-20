using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class EnemyRagdoll : MonoBehaviour
{
    public float deathImpulse = 2.5f;
    public float jointSpring = 12f;
    public float jointDamper = 2f;
    public WeaponType droppedWeaponType = WeaponType.PPSH;

    private readonly List<Collider> ragdollColliders = new List<Collider>();
    private readonly List<Rigidbody> ragdollBodies = new List<Rigidbody>();
    private readonly Dictionary<Transform, Rigidbody> bodiesByBone = new Dictionary<Transform, Rigidbody>();

    private Health health;
    private CharacterController characterController;
    private GroundedEntity groundedEntity;
    private EnemyAI enemyAI;
    private TeamFollowPlayer teamFollowPlayer;
    private Animator animator;
    private Rigidbody hipsBody;
    private bool isRagdoll;

    public bool HasHumanoidHitboxes => ragdollColliders.Count > 0;

    private void Awake()
    {
        health = GetComponent<Health>();
        health.destroyOnDeath = false;
        health.OnDied += ActivateRagdoll;

        characterController = GetComponent<CharacterController>();
        groundedEntity = GetComponent<GroundedEntity>();
        enemyAI = GetComponent<EnemyAI>();
        teamFollowPlayer = GetComponent<TeamFollowPlayer>();
    }

    private void Start()
    {
        Transform characterVisual = transform.Find("CharacterVisual");
        Initialize(characterVisual);
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
            return;
        }

        animator = characterVisual.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
        {
            return;
        }

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
        ConfigureSegment(lower, end, upper, radius * 0.85f, 1.5f, false);
        ConfigureSegment(end, null, lower, radius * 0.7f, 0.7f, true);
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
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
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
            lowTwist.limit = -28f;
            joint.lowTwistLimit = lowTwist;
            SoftJointLimit highTwist = joint.highTwistLimit;
            highTwist.limit = 28f;
            joint.highTwistLimit = highTwist;
            SoftJointLimit swing1 = joint.swing1Limit;
            swing1.limit = 38f;
            joint.swing1Limit = swing1;
            SoftJointLimit swing2 = joint.swing2Limit;
            swing2.limit = 38f;
            joint.swing2Limit = swing2;
            SoftJointLimitSpring spring = joint.swingLimitSpring;
            spring.spring = jointSpring;
            spring.damper = jointDamper;
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
        if (groundedEntity != null)
        {
            groundedEntity.enabled = false;
        }
        if (animator != null)
        {
            animator.enabled = false;
        }
        if (teamFollowPlayer != null)
        {
            teamFollowPlayer.enabled = false;
        }
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        DropWeapon();

        if (ragdollBodies.Count == 0)
        {
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
            body.isKinematic = false;
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
}
