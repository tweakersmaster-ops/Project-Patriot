using UnityEngine;

public class WeaponPickup : MonoBehaviour
{
    public WeaponType weaponType;

    private GameObject pickupRoot;
    private bool isDroppedWeapon;
    private bool wasPickedUp;

    private void Start()
    {
        if (pickupRoot == null)
        {
            pickupRoot = gameObject;
        }

        Renderer renderer = GetComponent<Renderer>();
        if (!isDroppedWeapon && renderer != null)
        {
            Shader standardShader = Shader.Find("Standard");
            if (standardShader != null)
            {
                renderer.material = new Material(standardShader);
                renderer.material.color = Color.cyan;
            }
        }

        Collider collider = GetComponent<Collider>();
        Rigidbody body = GetComponent<Rigidbody>();
        if (collider != null && body == null)
        {
            collider.isTrigger = true;
        }

        if (isDroppedWeapon)
        {
            CreateDroppedWeaponPickupTrigger();
        }
    }

    public void ConfigureDroppedWeapon(WeaponType type, GameObject droppedWeapon)
    {
        weaponType = type;
        pickupRoot = droppedWeapon;
        isDroppedWeapon = true;
    }

    private void CreateDroppedWeaponPickupTrigger()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        Bounds bounds = new Bounds(transform.position, Vector3.one * 0.5f);
        bool hasBounds = false;
        foreach (Collider existingCollider in colliders)
        {
            if (existingCollider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = existingCollider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(existingCollider.bounds);
            }
        }

        GameObject triggerObject = new GameObject("PickupTrigger");
        triggerObject.transform.SetParent(transform, false);
        SphereCollider trigger = triggerObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = hasBounds
            ? Mathf.Clamp(bounds.extents.magnitude, 0.55f, 1.15f)
            : 0.65f;
        triggerObject.transform.position = hasBounds ? bounds.center : transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (wasPickedUp)
        {
            return;
        }

        WeaponController weaponController = other.GetComponentInParent<WeaponController>();
        if (weaponController == null)
        {
            return;
        }

        wasPickedUp = true;
        weaponController.PickUpWeapon(weaponType);
        Destroy(pickupRoot != null ? pickupRoot : gameObject);
    }
}
