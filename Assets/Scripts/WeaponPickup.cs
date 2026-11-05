using UnityEngine;

// Лежащее оружие. Игрок подбирает его кнопкой G, когда находится в зоне:
// автоматического подбора при касании больше нет. Союзники подбирают
// через AllyWeaponSearch (TryTake).
public class WeaponPickup : MonoBehaviour
{
    public WeaponType weaponType;
    public string prompt = "Нажмите G, чтобы подобрать";

    private GameObject pickupRoot;
    private bool isDroppedWeapon;
    private bool wasPickedUp;
    private PlayerController playerInRange;

    public bool IsAvailable => !wasPickedUp;
    public WeaponType WeaponType => weaponType;

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

    private void Update()
    {
        if (wasPickedUp)
        {
            return;
        }

        if (playerInRange == null)
        {
            return;
        }

        PickupPromptUI.Show(prompt);

        if (Input.GetKeyDown(KeyCode.G))
        {
            WeaponController weapons = playerInRange.GetComponent<WeaponController>();
            if (weapons != null)
            {
                TryTake(weapons);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (wasPickedUp)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            playerInRange = player;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null && player == playerInRange)
        {
            playerInRange = null;
            PickupPromptUI.Hide();
        }
    }

    // Забрать оружие (игрок по G или союзник через AllyWeaponSearch).
    public bool TryTake(WeaponController weaponController)
    {
        if (wasPickedUp || weaponController == null)
        {
            return false;
        }

        wasPickedUp = true;
        playerInRange = null;
        weaponController.PickUpWeapon(weaponType);
        PickupPromptUI.Hide();
        Destroy(pickupRoot != null ? pickupRoot : gameObject);
        return true;
    }

    private void OnDisable()
    {
        if (playerInRange != null)
        {
            playerInRange = null;
            PickupPromptUI.Hide();
        }
    }
}
