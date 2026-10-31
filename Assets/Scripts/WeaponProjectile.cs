using System.Collections.Generic;
using UnityEngine;

public sealed class WeaponProjectile : MonoBehaviour
{
    private static readonly PhysicMaterial GrenadeBounceMaterial = new PhysicMaterial("Grenade Bounce")
    {
        bounciness = 0.45f,
        dynamicFriction = 0.3f,
        staticFriction = 0.3f,
        bounceCombine = PhysicMaterialCombine.Maximum,
        frictionCombine = PhysicMaterialCombine.Minimum
    };

    private Rigidbody body;
    private WeaponDefinition definition;
    private Health owner;
    private float detonateAt;
    private float destroyAt;
    private bool detonated;

    public static WeaponProjectile Launch(
        Vector3 position,
        Vector3 direction,
        Health projectileOwner,
        WeaponDefinition weapon)
    {
        if (weapon == null || weapon.behavior == WeaponBehavior.Hitscan)
        {
            Debug.LogError("Cannot launch a projectile without an explosive weapon definition.");
            return null;
        }

        GameObject projectileObject = new GameObject(weapon.displayName + " Projectile");
        projectileObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        SphereCollider collider = projectileObject.AddComponent<SphereCollider>();
        bool isGrenade = weapon.behavior == WeaponBehavior.Grenade;
        collider.radius = isGrenade ? 0.12f : 0.09f;
        if (isGrenade)
        {
            collider.sharedMaterial = GrenadeBounceMaterial;
        }

        Rigidbody projectileBody = projectileObject.AddComponent<Rigidbody>();
        projectileBody.mass = weapon.behavior == WeaponBehavior.Grenade ? 0.35f : 0.5f;
        projectileBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        projectileBody.interpolation = RigidbodyInterpolation.Interpolate;
        WeaponProjectile projectile = projectileObject.AddComponent<WeaponProjectile>();
        projectile.Initialize(projectileBody, weapon, projectileOwner, direction);
        return projectile;
    }

    private void Initialize(Rigidbody projectileBody, WeaponDefinition weapon, Health projectileOwner, Vector3 direction)
    {
        body = projectileBody;
        definition = weapon;
        owner = projectileOwner;

        GameObject visual = GameObject.CreatePrimitive(
            weapon.behavior == WeaponBehavior.Grenade ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
        visual.name = "ProjectileVisual";
        visual.transform.SetParent(transform, false);
        visual.transform.localScale = weapon.behavior == WeaponBehavior.Grenade
            ? new Vector3(0.18f, 0.18f, 0.18f)
            : new Vector3(0.09f, 0.36f, 0.09f);
        if (weapon.behavior != WeaponBehavior.Grenade)
        {
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        Collider visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null)
        {
            Destroy(visualCollider);
        }

        Renderer renderer = visual.GetComponent<Renderer>();
        renderer.material.color = weapon.faction == WeaponFaction.German
            ? new Color(0.32f, 0.3f, 0.24f)
            : new Color(0.34f, 0.38f, 0.25f);

        Collider[] ownerColliders = owner != null
            ? owner.GetComponentsInChildren<Collider>()
            : new Collider[0];
        Collider projectileCollider = GetComponent<Collider>();
        foreach (Collider ownerCollider in ownerColliders)
        {
            if (ownerCollider != null)
            {
                Physics.IgnoreCollision(projectileCollider, ownerCollider);
            }
        }

        body.velocity = direction.normalized * weapon.projectileSpeed;
        if (weapon.behavior == WeaponBehavior.Grenade)
        {
            body.angularVelocity = Random.insideUnitSphere * 8f;
            detonateAt = Time.time + 2.8f;
        }
        else
        {
            body.useGravity = false;
            detonateAt = float.PositiveInfinity;
        }

        float travelTime = weapon.range / Mathf.Max(weapon.projectileSpeed, 0.01f);
        destroyAt = Time.time + (weapon.behavior == WeaponBehavior.Grenade
            ? 8f
            : Mathf.Min(travelTime, 6f));
    }

    private void Update()
    {
        if (Time.time >= detonateAt || Time.time >= destroyAt)
        {
            Explode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (owner != null && collision.collider.GetComponentInParent<Health>() == owner)
        {
            return;
        }

        if (definition.behavior != WeaponBehavior.Grenade)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (detonated)
        {
            return;
        }

        detonated = true;
        Collider[] hits = Physics.OverlapSphere(transform.position, definition.blastRadius);
        HashSet<Health> damagedEntities = new HashSet<Health>();
        foreach (Collider hit in hits)
        {
            Health target = hit.GetComponentInParent<Health>();
            if (target == null || target == owner || !damagedEntities.Add(target))
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, target.transform.position);
            float damageScale = 1f - Mathf.Clamp01(distance / definition.blastRadius);
            if (damageScale > 0f)
            {
                target.TakeDamage(definition.damage * damageScale);
            }
        }

        Destroy(gameObject);
    }
}
