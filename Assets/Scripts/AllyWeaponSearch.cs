using UnityEngine;
using UnityEngine.AI;

// Союзник без огнестрельного оружия ищет выпавшее оружие рядом,
// подходит к нему и подбирает. Пока ищет — сам ведёт агента,
// поэтому TeamFollowPlayer на это время выключается.
public class AllyWeaponSearch : MonoBehaviour
{
    [Header("Поиск")]
    public float searchRadius = 30f;
    public float searchInterval = 1.5f;
    public float pickupDistance = 1.6f;
    public float moveSpeed = 4.5f;

    private WeaponController weapons;
    private TeamFollowPlayer follow;
    private NavMeshAgent agent;
    private WeaponPickup targetPickup;
    private float nextSearchTime;
    private bool hasFollow;
    private bool followSuspended;

    public bool IsSearching => targetPickup != null;

    private void Start()
    {
        weapons = GetComponent<WeaponController>();
        follow = GetComponent<TeamFollowPlayer>();
        hasFollow = follow != null;
        agent = GetComponent<NavMeshAgent>();
        nextSearchTime = Time.time + Random.Range(0f, 1f);
    }

    private void Update()
    {
        if (GameContext.Instance != null && GameContext.Instance.IsPaused)
        {
            return;
        }

        if (weapons == null)
        {
            weapons = GetComponent<WeaponController>();
            if (weapons == null)
            {
                return;
            }
        }

        if (HasFirearm())
        {
            StopSearching();
            return;
        }

        if (targetPickup == null || !targetPickup.IsAvailable)
        {
            targetPickup = null;
            if (Time.time >= nextSearchTime)
            {
                targetPickup = FindPickup();
                nextSearchTime = Time.time + searchInterval;
                if (targetPickup != null)
                {
                    StartSearching();
                }
            }
        }

        if (targetPickup == null)
        {
            return;
        }

        Vector3 offset = targetPickup.transform.position - transform.position;
        offset.y = 0f;
        if (offset.magnitude <= pickupDistance)
        {
            if (targetPickup.TryTake(weapons))
            {
                targetPickup = null;
                StopSearching();
            }

            return;
        }

        MoveTowards(targetPickup.transform.position);
    }

    private bool HasFirearm()
    {
        WeaponDefinition current = weapons.CurrentWeapon;
        if (current == null)
        {
            return false;
        }

        return current.type != WeaponType.Hands &&
               current.type != WeaponType.Knife &&
               current.behavior != WeaponBehavior.Melee;
    }

    private WeaponPickup FindPickup()
    {
        // Выпавшее оружие лежит в триггерных коллайдерах, поэтому
        // запрашиваем и триггеры.
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            searchRadius,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);

        WeaponPickup nearest = null;
        float nearestDistanceSquared = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            WeaponPickup pickup = hits[i].GetComponentInParent<WeaponPickup>();
            if (pickup == null || !pickup.IsAvailable)
            {
                continue;
            }

            float distanceSquared = (pickup.transform.position - transform.position).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearest = pickup;
            }
        }

        return nearest;
    }

    private void MoveTowards(Vector3 worldPosition)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            NavMeshCharacterUtility.Resume(agent);
            agent.speed = moveSpeed;
            Vector3 destination;
            if (NavMeshCharacterUtility.TryGetNavPoint(worldPosition, 2.5f, out destination))
            {
                agent.SetDestination(destination);
            }
            else
            {
                agent.SetDestination(worldPosition);
            }

            Vector3 velocity = agent.desiredVelocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 0.04f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(velocity.normalized),
                    Time.deltaTime * 8f);
            }

            return;
        }

        Vector3 offset = worldPosition - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 step = offset.normalized * moveSpeed * Time.deltaTime;
        transform.position += step;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(offset.normalized),
            Time.deltaTime * 8f);
    }

    private void StartSearching()
    {
        if (hasFollow && follow != null && follow.enabled)
        {
            follow.enabled = false;
            followSuspended = true;
        }
    }

    private void StopSearching()
    {
        targetPickup = null;
        if (followSuspended && follow != null)
        {
            follow.enabled = true;
            followSuspended = false;
        }
    }

    private void OnDisable()
    {
        targetPickup = null;
        if (followSuspended && follow != null)
        {
            follow.enabled = true;
            followSuspended = false;
        }
    }
}
