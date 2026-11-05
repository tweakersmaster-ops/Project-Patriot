using UnityEngine;

public enum PickupItemType
{
    PaddockKey,
    Weapon
}

// Подбор предмета: ключ от питомника берётся при касании, оружие —
// только по кнопке G, когда игрок стоит в зоне. Рядом с оружием
// показывается подсказка.
public class ItemPickup : MonoBehaviour
{
    [Header("Что подбираем")]
    public PickupItemType itemType = PickupItemType.PaddockKey;
    public WeaponType weaponType = WeaponType.PPSH;
    public string weaponPrompt = "Нажмите G, чтобы подобрать";

    [Header("Визуал")]
    public float spinSpeed = 90f;
    public float bobHeight = 0.12f;
    public float bobSpeed = 2f;

    private bool pickedUp;
    private Vector3 basePosition;
    private PlayerController playerInRange;

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

        if (pickedUp || itemType != PickupItemType.Weapon || playerInRange == null)
        {
            return;
        }

        PickupPromptUI.Show(weaponPrompt);

        if (Input.GetKeyDown(KeyCode.G))
        {
            WeaponController weapons = playerInRange.GetComponent<WeaponController>();
            if (weapons != null)
            {
                weapons.PickUpWeapon(weaponType);
                pickedUp = true;
                playerInRange = null;
                PickupPromptUI.Hide();
                Destroy(gameObject);
            }
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

        playerInRange = player;
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

    private void OnDisable()
    {
        if (playerInRange != null)
        {
            playerInRange = null;
            PickupPromptUI.Hide();
        }
    }
}
