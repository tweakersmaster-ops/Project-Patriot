using UnityEngine;

// Выдаёт врагу с EnemyAI огнестрельное оружие в руки при спавне
// (ППШ или Kar98k). Оружие висит в правой кисти и видно всегда,
// даже когда враг патрулирует.
public class EnemyWeaponEquip : MonoBehaviour
{
    [Header("Оружие")]
    public bool randomWeapon = true;
    public WeaponType weapon = WeaponType.Kar98k;

    private WeaponController weaponController;
    private Transform weaponHand;
    private Transform weaponAnchor;

    private void Start()
    {
        weaponController = GetComponent<WeaponController>();
        if (weaponController == null)
        {
            weaponController = gameObject.AddComponent<WeaponController>();
        }

        WeaponType type = randomWeapon
            ? (Random.value < 0.5f ? WeaponType.PPSH : WeaponType.Kar98k)
            : weapon;

        weaponController.ConfigureAIWeapon(type);
        weaponController.ResetLoadout();
        EnsureWeaponView();
        weaponController.SetAIWeaponDrawn(true);
    }

    private void LateUpdate()
    {
        if (weaponAnchor != null && weaponHand != null)
        {
            // Ствол чуть вверх: прижим затыльник приклада к груди выглядел
            // так, будто винтовка прорастает сквозь туловище.
            weaponAnchor.position = weaponHand.position;
            weaponAnchor.rotation = transform.rotation * Quaternion.Euler(-18f, 0f, 0f);
        }
    }

    private void EnsureWeaponView()
    {
        Animator animator = FindHumanoidAnimator();
        if (animator != null)
        {
            weaponHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        Transform existing = transform.Find("EnemyWeaponAnchor");
        if (existing != null)
        {
            weaponAnchor = existing;
        }
        else
        {
            GameObject anchor = new GameObject("EnemyWeaponAnchor");
            weaponAnchor = anchor.transform;
            weaponAnchor.SetParent(transform, false);
            weaponAnchor.localPosition = new Vector3(0f, 1.15f, 0.15f);
        }

        if (weaponHand != null)
        {
            weaponAnchor.SetParent(weaponHand, false);
            weaponAnchor.localPosition = new Vector3(0.02f, -0.02f, 0.04f);
            weaponAnchor.localRotation = Quaternion.identity;
        }

        if (weaponAnchor.GetComponent<WeaponView>() == null)
        {
            weaponAnchor.gameObject.AddComponent<WeaponView>();
        }
    }

    private Animator FindHumanoidAnimator()
    {
        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i].avatar != null && animators[i].avatar.isHuman)
            {
                return animators[i];
            }
        }

        return null;
    }
}
