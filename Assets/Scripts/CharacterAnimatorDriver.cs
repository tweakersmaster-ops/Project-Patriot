using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class CharacterAnimatorDriver : MonoBehaviour
{
    private static readonly int VelocityXId = Animator.StringToHash("VelocityX");
    private static readonly int VelocityYId = Animator.StringToHash("VelocityY");
    private static readonly int MoveAmountId = Animator.StringToHash("MoveAmount");
    private static readonly int IsCrouchingId = Animator.StringToHash("IsCrouching");
    private static readonly int IsAimingId = Animator.StringToHash("IsAiming");
    private static readonly int IsGroundedId = Animator.StringToHash("IsGrounded");
    private static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
    private static readonly int FireId = Animator.StringToHash("Fire");
    private static readonly int ReloadId = Animator.StringToHash("Reload");
    private static readonly int ThrowGrenadeId = Animator.StringToHash("ThrowGrenade");
    private static readonly int HitId = Animator.StringToHash("Hit");
    private static readonly int DieId = Animator.StringToHash("Die");

    private Animator characterAnimator;
    private CharacterController characterController;
    private NavMeshAgent navigationAgent;
    private PlayerController playerController;
    private EnemyAI enemyAI;
    private TeamFollowPlayer teamFollowPlayer;
    private WeaponController weaponController;
    private Health health;
    private int aimLayerIndex = -1;
    private int actionLayerIndex = -1;
    private int emptyActionStateHash;
    private bool actionLayerActive;

    public static void AttachIfPresent(GameObject characterRoot)
    {
        if (characterRoot == null || characterRoot.GetComponent<CharacterAnimatorDriver>() != null)
        {
            return;
        }

        foreach (Animator animator in characterRoot.GetComponentsInChildren<Animator>(true))
        {
            if (animator.runtimeAnimatorController != null && animator.avatar != null)
            {
                characterRoot.AddComponent<CharacterAnimatorDriver>();
                return;
            }
        }
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        navigationAgent = GetComponent<NavMeshAgent>();
        playerController = GetComponent<PlayerController>();
        enemyAI = GetComponent<EnemyAI>();
        teamFollowPlayer = GetComponent<TeamFollowPlayer>();
        weaponController = GetComponent<WeaponController>();
        health = GetComponent<Health>();

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        foreach (Animator candidate in animators)
        {
            if (candidate.runtimeAnimatorController != null && candidate.avatar != null)
            {
                characterAnimator = candidate;
                break;
            }
        }

        if (characterAnimator == null)
        {
            Debug.LogWarning("CharacterAnimatorDriver found no Animator with both a controller and Avatar.", this);
            enabled = false;
            return;
        }

        characterAnimator.enabled = true;
        characterAnimator.applyRootMotion = false; // Принудительно отключаем Root Motion
        
        aimLayerIndex = characterAnimator.GetLayerIndex("Upper Body Aim");
        actionLayerIndex = characterAnimator.GetLayerIndex("Upper Body Actions");
        emptyActionStateHash = Animator.StringToHash("Upper Body Actions.AGC_Action_Empty");
        
        if (aimLayerIndex >= 0)
        {
            characterAnimator.SetLayerWeight(aimLayerIndex, 0f);
        }

        if (actionLayerIndex >= 0)
        {
            characterAnimator.SetLayerWeight(actionLayerIndex, 0f);
        }
    }

    private void OnEnable()
    {
        if (weaponController != null)
        {
            weaponController.OnWeaponFired += HandleWeaponFired;
            weaponController.OnReloadStarted += HandleReloadStarted;
        }

        if (health != null)
        {
            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (weaponController != null)
        {
            weaponController.OnWeaponFired -= HandleWeaponFired;
            weaponController.OnReloadStarted -= HandleReloadStarted;
        }

        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }
    }

    private void LateUpdate()
    {
        if (characterAnimator == null)
        {
            return;
        }

        Vector3 worldVelocity = GetWorldVelocity();
        Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);
        localVelocity.y = 0f;
        
        float horizontalSpeed = localVelocity.magnitude;
        float referenceSpeed = GetReferenceSpeed();
        Vector2 blendVelocity = Vector2.ClampMagnitude(
            new Vector2(localVelocity.x, localVelocity.z) * (2f / referenceSpeed),
            2f);

        characterAnimator.SetFloat(VelocityXId, blendVelocity.x, 0.1f, Time.deltaTime);
        characterAnimator.SetFloat(VelocityYId, blendVelocity.y, 0.1f, Time.deltaTime);
        characterAnimator.SetFloat(MoveAmountId, Mathf.Clamp01(horizontalSpeed / referenceSpeed), 0.1f, Time.deltaTime);
        
        characterAnimator.SetBool(IsCrouchingId, playerController != null && playerController.IsCrouching);
        characterAnimator.SetBool(IsAimingId, CombatSettings.AimingEnabled && weaponController != null && weaponController.IsAiming);
        characterAnimator.SetBool(IsGroundedId, characterController == null || characterController.isGrounded);
        characterAnimator.SetFloat(VerticalSpeedId, characterController != null ? characterController.velocity.y : 0f);

        if (aimLayerIndex >= 0)
        {
            float targetWeight = CombatSettings.AimingEnabled && weaponController != null && weaponController.IsAiming ? 1f : 0f;
            characterAnimator.SetLayerWeight(
                aimLayerIndex,
                Mathf.MoveTowards(characterAnimator.GetLayerWeight(aimLayerIndex), targetWeight, Time.deltaTime * 8f));
        }

        if (actionLayerActive && actionLayerIndex >= 0 &&
            !characterAnimator.IsInTransition(actionLayerIndex) &&
            characterAnimator.GetCurrentAnimatorStateInfo(actionLayerIndex).fullPathHash == emptyActionStateHash)
        {
            characterAnimator.SetLayerWeight(actionLayerIndex, 0f);
            actionLayerActive = false;
        }
    }

    private Vector3 GetWorldVelocity()
    {
        // Если есть NavMeshAgent, берем скорость с него (упрощено для стабильности)
        if (navigationAgent != null && navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh)
        {
            return navigationAgent.velocity;
        }

        // Если есть CharacterController, берем с него
        if (characterController != null && characterController.enabled)
        {
            return characterController.velocity;
        }

        return Vector3.zero;
    }

    private float GetReferenceSpeed()
    {
        if (playerController != null)
        {
            return Mathf.Max(playerController.moveSpeed, 0.01f);
        }

        if (enemyAI != null)
        {
            return Mathf.Max(enemyAI.moveSpeed, 0.01f);
        }

        if (teamFollowPlayer != null)
        {
            return Mathf.Max(teamFollowPlayer.followSpeed, 0.01f);
        }

        if (navigationAgent != null)
        {
            return Mathf.Max(navigationAgent.speed, 0.01f);
        }

        return 5f;
    }

    private void HandleWeaponFired(WeaponDefinition weapon)
    {
        if (weapon == null)
        {
            return;
        }

        if (weapon.behavior == WeaponBehavior.Grenade)
        {
            TriggerAction(ThrowGrenadeId);
        }
        else
        {
            TriggerAction(FireId);
        }
    }

    private void HandleReloadStarted()
    {
        TriggerAction(ReloadId);
    }

    private void HandleDamaged(Health damagedHealth, float amount)
    {
        if (characterAnimator != null)
        {
            characterAnimator.SetTrigger(HitId);
        }
    }

    private void HandleDied(Health deadHealth)
    {
        if (characterAnimator != null)
        {
            characterAnimator.SetTrigger(DieId);
        }
    }

    private void TriggerAction(int triggerId)
    {
        if (characterAnimator == null || actionLayerIndex < 0)
        {
            return;
        }

        characterAnimator.ResetTrigger(FireId);
        characterAnimator.ResetTrigger(ReloadId);
        characterAnimator.ResetTrigger(ThrowGrenadeId);
        characterAnimator.SetTrigger(triggerId);
        characterAnimator.SetLayerWeight(actionLayerIndex, 1f);
        actionLayerActive = true;
    }
}