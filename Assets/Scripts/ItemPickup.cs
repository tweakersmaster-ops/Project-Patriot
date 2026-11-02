using UnityEngine;

public enum PickupItemType
{
    PaddockKey,
    Weapon
}

// Подбор предмета при касании: ключ от питомника или оружие.
public class ItemPickup : MonoBehaviour
{
    [Header("Что подбираем")]
    public PickupItemType itemType = PickupItemType.PaddockKey;
    public WeaponType weaponType = WeaponType.PPSH;

    [Header("Визуал")]
    public float spinSpeed = 90f;
    public float bobHeight = 0.12f;
    public float bobSpeed = 2f;

    private bool pickedUp;
    private Vector3 basePosition;

    private void Start()
    {
        EnsureTrigger();
        basePosition = transform.position;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        if (bobHeight > 0.001f)
        {
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = basePosition + Vector3.up * bob;
        }
    }

    private void EnsureTrigger()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.65f;
            sphere.isTrigger = true;
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (pickedUp)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        if (itemType == PickupItemType.PaddockKey)
        {
            PlayerInventory.hasPaddockKey = true;
            pickedUp = true;
            Destroy(gameObject);
            return;
        }

        WeaponController weapons = player.GetComponent<WeaponController>();
        if (weapons == null)
        {
            weapons = other.GetComponentInParent<WeaponController>();
        }

        if (weapons == null)
        {
            return;
        }

        weapons.PickUpWeapon(weaponType);
        pickedUp = true;
        Destroy(gameObject);
    }
}
