using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CharacterController))]
public class EnemyAI : MonoBehaviour
{
    public Transform target;
    public float moveSpeed = 2.2f;
    public float attackRange = 12f;
    public float detectionRange = 15f;
    public float fireRate = 1.8f;
    public float damage = 5f;
    public float projectileSpeed = 20f;
    public float bulletSpread = 0.05f;
    public float range = 60f;
    public float gravity = -20f;

    private CharacterController controller;
    private NavMeshAgent navigationAgent;
    private float nextShotTime;
    private float verticalVelocity;
    private Vector3 lastDestination;
    private bool hasDestination;
    private bool hasNavigationPath;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }

        CapsuleCollider primitiveCollider = GetComponent<CapsuleCollider>();
        if (primitiveCollider != null)
        {
            primitiveCollider.enabled = false;
            Destroy(primitiveCollider);
        }

        ConfigureNavigationAgent();
        CharacterAnimatorDriver.AttachIfPresent(gameObject);
        nextShotTime = Time.time + UnityEngine.Random.Range(0f, fireRate);
    }

    public void RefreshNavigation()
    {
        ConfigureNavigationAgent();
    }

    private void ConfigureNavigationAgent()
    {
        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }

        navigationAgent = GetComponent<NavMeshAgent>();
        if (navigationAgent == null)
        {
            navigationAgent = gameObject.AddComponent<NavMeshAgent>();
        }

        navigationAgent.speed = moveSpeed;
        navigationAgent.stoppingDistance = attackRange;
        navigationAgent.updatePosition = false;
        navigationAgent.updateRotation = false;
        hasNavigationPath = false;
        hasDestination = false;
        if (NavMesh.SamplePosition(GetNavigationPosition(), out NavMeshHit navMeshHit, 2f, NavMesh.AllAreas) &&
            navigationAgent.Warp(navMeshHit.position))
        {
            hasNavigationPath = true;
            navigationAgent.nextPosition = navMeshHit.position;
        }
        else
        {
            Debug.LogWarning("Enemy could not join a NavMesh and will use direct movement: " + name, this);
        }
    }

    private void Update()
    {
        if (controller == null || !controller.enabled)
        {
            return;
        }

        Vector3 horizontalMovement = Vector3.zero;
        float targetDistance = float.PositiveInfinity;
        if (target != null)
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            targetDistance = toTarget.magnitude;
            if (targetDistance <= detectionRange)
            {
                if (CombatSettings.AimingEnabled && toTarget.sqrMagnitude > 0.01f)
                {
                    Quaternion lookRotation = Quaternion.LookRotation(toTarget.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 4f);
                }

                if (targetDistance > attackRange)
                {
                    if (hasNavigationPath && navigationAgent.isOnNavMesh)
                    {
                        navigationAgent.isStopped = false;
                        if (!hasDestination || (lastDestination - target.position).sqrMagnitude > 0.25f || !navigationAgent.hasPath)
                        {
                            hasDestination = navigationAgent.SetDestination(target.position);
                            lastDestination = target.position;
                        }

                        horizontalMovement = navigationAgent.desiredVelocity;
                    }
                    else if (toTarget.sqrMagnitude > 0.01f)
                    {
                        horizontalMovement = toTarget.normalized * moveSpeed;
                    }
                }
                else
                {
                    if (hasNavigationPath && navigationAgent.isOnNavMesh)
                    {
                        navigationAgent.isStopped = true;
                        hasDestination = false;
                    }

                    if (Time.time >= nextShotTime)
                    {
                        FireAtTarget();
                        nextShotTime = Time.time + fireRate;
                    }
                }
            }
            else if (hasNavigationPath && navigationAgent.isOnNavMesh)
            {
                navigationAgent.isStopped = true;
                hasDestination = false;
            }
        }
        else if (hasNavigationPath && navigationAgent.isOnNavMesh)
        {
            navigationAgent.isStopped = true;
        }

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;
        horizontalMovement.y = verticalVelocity;
        controller.Move(horizontalMovement * Time.deltaTime);

        if (hasNavigationPath && navigationAgent.enabled && navigationAgent.isOnNavMesh)
        {
            navigationAgent.nextPosition = GetNavigationPosition();
        }
    }

    public void StopNavigation()
    {
        if (navigationAgent != null)
        {
            navigationAgent.enabled = false;
        }

        hasNavigationPath = false;
    }

    private Vector3 GetNavigationPosition()
    {
        if (controller == null)
        {
            return transform.position;
        }

        return transform.TransformPoint(controller.center - Vector3.up * (controller.height * 0.5f));
    }

    private void FireAtTarget()
    {
        if (target == null)
        {
            return;
        }

        Vector3 origin = transform.position + Vector3.up * 0.8f;
        Vector3 aimDirection = CombatSettings.AimingEnabled
            ? (target.position + Vector3.up * 0.8f - origin).normalized
            : transform.forward;
        aimDirection += UnityEngine.Random.insideUnitSphere *
                        (CombatSettings.AimingEnabled ? bulletSpread : bulletSpread * 4f);
        aimDirection.Normalize();

        Ray ray = new Ray(origin, aimDirection);
        RaycastHit[] hits = Physics.RaycastAll(ray, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));
        Health shooterHealth = GetComponent<Health>();
        Health targetHealth = target.GetComponentInParent<Health>();
        foreach (RaycastHit hit in hits)
        {
            Health hitHealth = hit.collider.GetComponentInParent<Health>();
            CharacterController hitController = hit.collider as CharacterController;
            EnemyRagdoll hitRagdoll = hitController != null ? hitController.GetComponent<EnemyRagdoll>() : null;
            if ((hitRagdoll != null && hitRagdoll.HasHumanoidHitboxes) ||
                (shooterHealth != null && hitHealth == shooterHealth))
            {
                continue;
            }

            if (targetHealth != null && hitHealth == targetHealth)
            {
                targetHealth.TakeDamage(damage);
            }

            break;
        }

        Debug.DrawRay(origin, aimDirection * range, Color.red, 0.2f);
    }
}
