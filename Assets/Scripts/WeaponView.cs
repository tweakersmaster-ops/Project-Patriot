using System.Collections.Generic;
using UnityEngine;

public class WeaponView : MonoBehaviour
{
    private static readonly Dictionary<Color, Material> PartMaterials = new Dictionary<Color, Material>();
    private static Material muzzleFlashSharedMaterial;

    private GameObject weaponRoot;
    private ParticleSystem muzzleFlash;
    private Light muzzleLight;
    private float flashTimer;
    private float recoilAmount;
    private float bobTimer;
    private float sprintBlend;
    private bool isAiming;
    private WeaponController weaponController;
    private PlayerController playerController;

    private static readonly Vector3 WeaponRestPosition = new Vector3(0.52f, -0.34f, 0.88f);
    private static readonly Vector3 MosinAimPosition = new Vector3(0.06f, -0.17f, 0.58f);
    private const float WeaponModelScale = 1.18f;

    private void Start()
    {
        weaponController = GetComponentInParent<WeaponController>(true);
        if (weaponController == null)
        {
            weaponController = FindObjectOfType<WeaponController>();
        }

        playerController = weaponController != null
            ? weaponController.GetComponent<PlayerController>()
            : GetComponentInParent<PlayerController>(true);

        if (weaponController == null)
        {
            enabled = false;
            return;
        }

        if (weaponController != null)
        {
            weaponController.OnWeaponSwitched += UpdateWeaponModel;
            weaponController.OnWeaponFired += OnWeaponFired;
            weaponController.OnAimChanged += HandleAimChanged;
            weaponController.OnWeaponDrawChanged += HandleWeaponDrawChanged;
        }

        CreateWeaponRoot();
        isAiming = weaponController.IsAiming;
        weaponRoot.SetActive(!weaponController.IsAIControlled || weaponController.IsWeaponDrawn);
        UpdateWeaponModel(weaponController != null ? weaponController.CurrentWeapon : null);
    }

    private void Update()
    {
        if (weaponRoot != null)
        {
            float moveX = KeyBindings.Horizontal;
            float moveY = KeyBindings.Vertical;
            float movementAmount = playerController != null
                ? Mathf.Clamp01(playerController.CurrentHorizontalSpeed / Mathf.Max(playerController.sprintSpeed, 0.01f))
                : Mathf.Clamp01(new Vector2(moveX, moveY).magnitude);
            float targetSprintBlend = playerController != null && playerController.IsSprinting ? 1f : 0f;
            sprintBlend = Mathf.MoveTowards(sprintBlend, targetSprintBlend, Time.deltaTime * 3f);
            bobTimer += Time.deltaTime * Mathf.Lerp(2f, 9f, movementAmount) * Mathf.Lerp(1f, 1.3f, sprintBlend);
            recoilAmount = Mathf.MoveTowards(recoilAmount, 0f, Time.deltaTime * 4.5f);

            float bobSide = Mathf.Sin(bobTimer) * Mathf.Lerp(0.012f, 0.02f, sprintBlend) * movementAmount;
            float bobUp = Mathf.Abs(Mathf.Cos(bobTimer)) * Mathf.Lerp(0.018f, 0.032f, sprintBlend) * movementAmount;
            Vector3 basePosition = isAiming && weaponController.CurrentWeapon != null &&
                                   weaponController.CurrentWeapon.canAim
                ? MosinAimPosition
                : WeaponRestPosition;
            Vector3 targetPosition = basePosition + new Vector3(bobSide * (isAiming ? 0.2f : 1f), bobUp * (isAiming ? 0.2f : 1f), 0f);
            float mouseX = weaponController.IsAIControlled ? 0f : Input.GetAxis("Mouse X");
            float mouseY = weaponController.IsAIControlled ? 0f : Input.GetAxis("Mouse Y");
            Quaternion targetRotation = Quaternion.Euler(
                -recoilAmount * 7f - mouseY * 0.7f,
                mouseX * 0.8f + sprintBlend * 1.5f,
                -mouseX * 1.5f - bobSide * 80f - sprintBlend * 1.5f);

            weaponRoot.transform.localPosition = Vector3.Lerp(weaponRoot.transform.localPosition, targetPosition, Time.deltaTime * 12f);
            weaponRoot.transform.localRotation = Quaternion.Slerp(weaponRoot.transform.localRotation, targetRotation, Time.deltaTime * 12f);
        }

        if (muzzleLight != null)
        {
            muzzleLight.enabled = flashTimer > 0f;
            flashTimer -= Time.deltaTime;
        }
    }

    private void HandleAimChanged(bool aiming)
    {
        isAiming = aiming;
    }

    private void HandleWeaponDrawChanged(bool drawn)
    {
        if (weaponRoot != null)
        {
            weaponRoot.SetActive(drawn);
        }
    }

    private void OnDestroy()
    {
        if (weaponController != null)
        {
            weaponController.OnWeaponSwitched -= UpdateWeaponModel;
            weaponController.OnWeaponFired -= OnWeaponFired;
            weaponController.OnAimChanged -= HandleAimChanged;
            weaponController.OnWeaponDrawChanged -= HandleWeaponDrawChanged;
        }
    }

    private void CreateWeaponRoot()
    {
        if (weaponRoot != null)
        {
            Destroy(weaponRoot);
        }

        weaponRoot = new GameObject("WeaponModel");
        weaponRoot.transform.SetParent(transform, false);
        weaponRoot.transform.localPosition = WeaponRestPosition;
        weaponRoot.transform.localScale = Vector3.one * WeaponModelScale;

        GameObject muzzle = new GameObject("MuzzleFlash");
        muzzle.transform.SetParent(weaponRoot.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0.02f, 1.05f);

        muzzleFlash = muzzle.AddComponent<ParticleSystem>();
        muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = muzzleFlash.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.28f, 0.025f, 1f),
            new Color(1f, 0.85f, 0.25f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = muzzleFlash.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = muzzleFlash.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.015f;

        ParticleSystemRenderer flashRenderer = muzzleFlash.GetComponent<ParticleSystemRenderer>();
        flashRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        Shader flashShader = Shader.Find("Particles/Standard Unlit");
        if (flashShader == null)
        {
            flashShader = Shader.Find("Sprites/Default");
        }
        if (flashShader != null)
        {
            if (muzzleFlashSharedMaterial == null)
            {
                muzzleFlashSharedMaterial = new Material(flashShader)
                {
                    color = new Color(1f, 0.55f, 0.1f, 1f)
                };
            }

            flashRenderer.sharedMaterial = muzzleFlashSharedMaterial;
        }

        muzzleLight = muzzle.AddComponent<Light>();
        muzzleLight.type = LightType.Point;
        muzzleLight.range = 2f;
        muzzleLight.intensity = 2.5f;
        muzzleLight.color = new Color(1f, 0.55f, 0.15f);
        muzzleLight.enabled = false;
    }

    private void UpdateWeaponModel(WeaponDefinition weapon)
    {
        if (weaponController == null)
        {
            return;
        }

        if (weaponRoot == null)
        {
            CreateWeaponRoot();
        }

        foreach (Transform child in weaponRoot.transform)
        {
            if (muzzleFlash == null || child.gameObject != muzzleFlash.gameObject)
            {
                Destroy(child.gameObject);
            }
        }

        if (weapon == null)
        {
            return;
        }

        float muzzleOffset = 1.1f;
        switch (weapon.category)
        {
            case WeaponCategory.Pistol:
            case WeaponCategory.Revolver:
                muzzleOffset = 0.62f;
                break;
            case WeaponCategory.SubmachineGun:
                muzzleOffset = 0.78f;
                break;
            case WeaponCategory.Launcher:
                muzzleOffset = 0.7f;
                break;
            case WeaponCategory.Grenade:
                muzzleOffset = 0.2f;
                break;
        }

        muzzleFlash.transform.localPosition = new Vector3(0f, 0.02f, muzzleOffset);

        Color metal = weapon.faction == WeaponFaction.German
            ? new Color(0.19f, 0.2f, 0.19f)
            : new Color(0.22f, 0.23f, 0.2f);
        Color wood = weapon.faction == WeaponFaction.German
            ? new Color(0.36f, 0.25f, 0.15f)
            : new Color(0.42f, 0.29f, 0.16f);
        Color accent = weapon.faction == WeaponFaction.German
            ? new Color(0.42f, 0.39f, 0.31f)
            : new Color(0.38f, 0.41f, 0.31f);

        if (weapon.category == WeaponCategory.Melee)
        {
            muzzleFlash.gameObject.SetActive(false);
            CreateMeleeModel(weapon.type);
            return;
        }

        muzzleFlash.gameObject.SetActive(true);

        if (weapon.category == WeaponCategory.Grenade)
        {
            CreatePart(PrimitiveType.Sphere, new Vector3(0f, 0f, 0.05f),
                weapon.type == WeaponType.Stielhandgranate24
                    ? new Vector3(0.16f, 0.18f, 0.16f)
                    : new Vector3(0.22f, 0.28f, 0.22f), accent);
            if (weapon.type == WeaponType.Stielhandgranate24)
            {
                CreatePart(PrimitiveType.Cylinder, new Vector3(0f, -0.28f, 0.05f),
                    new Vector3(0.055f, 0.24f, 0.055f), wood);
            }
            else if (weapon.type == WeaponType.RGD33)
            {
                CreatePart(PrimitiveType.Cylinder, new Vector3(0f, -0.28f, 0.05f),
                    new Vector3(0.1f, 0.14f, 0.1f), metal);
            }

            return;
        }

        float receiverLength;
        float barrelLength;
        Vector3 receiverScale;
        switch (weapon.category)
        {
            case WeaponCategory.Pistol:
            case WeaponCategory.Revolver:
                receiverLength = 0.3f;
                barrelLength = 0.35f;
                receiverScale = new Vector3(0.17f, 0.14f, 0.33f);
                break;
            case WeaponCategory.SubmachineGun:
                receiverLength = 0.48f;
                barrelLength = 0.42f;
                receiverScale = new Vector3(0.18f, 0.18f, 0.48f);
                break;
            case WeaponCategory.MachineGun:
                receiverLength = 0.55f;
                barrelLength = 0.75f;
                receiverScale = new Vector3(0.23f, 0.21f, 0.58f);
                break;
            case WeaponCategory.Launcher:
                receiverLength = 0.88f;
                barrelLength = 0.82f;
                receiverScale = new Vector3(0.17f, 0.17f, 0.9f);
                break;
            default:
                receiverLength = 0.62f;
                barrelLength = 0.82f;
                receiverScale = new Vector3(0.19f, 0.18f, 0.65f);
                break;
        }

        CreatePart(PrimitiveType.Cube, new Vector3(0f, 0f, 0.04f), receiverScale, metal);

        if (weapon.category == WeaponCategory.Launcher)
        {
            CreatePart(PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 0.13f),
                new Vector3(0.12f, 0.52f, 0.12f), accent, Quaternion.Euler(90f, 0f, 0f));
            CreatePart(PrimitiveType.Cube, new Vector3(0f, -0.2f, -0.2f),
                new Vector3(0.18f, 0.28f, 0.18f), wood);
            return;
        }

        CreatePart(PrimitiveType.Cylinder, new Vector3(0f, 0.025f, 0.28f + barrelLength * 0.5f),
            new Vector3(weapon.category == WeaponCategory.MachineGun ? 0.055f : 0.04f, barrelLength * 0.5f, 0.04f),
            accent, Quaternion.Euler(90f, 0f, 0f));
        CreatePart(PrimitiveType.Cube, new Vector3(0f, -0.24f, 0.04f),
            new Vector3(0.13f, 0.34f, 0.14f), wood, Quaternion.Euler(12f, 0f, 0f));

        if (weapon.category == WeaponCategory.Pistol || weapon.category == WeaponCategory.Revolver)
        {
            CreatePart(PrimitiveType.Cube, new Vector3(0f, -0.2f, 0.02f),
                new Vector3(0.13f, 0.27f, 0.15f), wood, Quaternion.Euler(-12f, 0f, 0f));
            if (weapon.category == WeaponCategory.Revolver)
            {
                CreatePart(PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0.17f),
                    new Vector3(0.13f, 0.09f, 0.13f), accent);
            }

            if (weapon.type == WeaponType.MauserC96)
            {
                CreatePart(PrimitiveType.Cube, new Vector3(0f, -0.06f, -0.31f),
                    new Vector3(0.23f, 0.21f, 0.4f), wood);
            }
        }
        else
        {
            CreatePart(PrimitiveType.Cube, new Vector3(0f, -0.015f, -0.52f),
                new Vector3(0.15f, 0.17f, receiverLength + 0.28f), wood);
            bool drumMagazine = weapon.type == WeaponType.PPSH ||
                                weapon.type == WeaponType.PPD40 ||
                                weapon.type == WeaponType.DP27;
            if (drumMagazine)
            {
                CreatePart(PrimitiveType.Cylinder, new Vector3(0f, -0.19f, 0.1f),
                    new Vector3(0.17f, 0.07f, 0.17f), metal);
            }
            else
            {
                CreatePart(PrimitiveType.Cube, new Vector3(0f, -0.2f, 0.15f),
                    new Vector3(0.13f, 0.27f, 0.2f), metal);
            }

            if (weapon.category == WeaponCategory.MachineGun)
            {
                CreatePart(PrimitiveType.Cube, new Vector3(-0.2f, -0.18f, 0.48f),
                    new Vector3(0.035f, 0.3f, 0.035f), accent, Quaternion.Euler(8f, 0f, -12f));
                CreatePart(PrimitiveType.Cube, new Vector3(0.2f, -0.18f, 0.48f),
                    new Vector3(0.035f, 0.3f, 0.035f), accent, Quaternion.Euler(8f, 0f, 12f));
            }

            if (weapon.type == WeaponType.MosinSniper)
            {
                CreatePart(PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0.03f),
                    new Vector3(0.045f, 0.25f, 0.045f), accent);
            }
        }
    }

    private void CreateMeleeModel(WeaponType type)
    {
        Color sleeve = new Color(0.24f, 0.28f, 0.22f);
        Color skin = new Color(0.72f, 0.55f, 0.4f);
        if (type == WeaponType.Hands)
        {
            CreatePart(PrimitiveType.Capsule, new Vector3(-0.19f, -0.2f, 0.24f),
                new Vector3(0.12f, 0.38f, 0.12f), sleeve, Quaternion.Euler(90f, 0f, -12f));
            CreatePart(PrimitiveType.Capsule, new Vector3(0.19f, -0.2f, 0.24f),
                new Vector3(0.12f, 0.38f, 0.12f), sleeve, Quaternion.Euler(90f, 0f, 12f));
            CreatePart(PrimitiveType.Sphere, new Vector3(-0.1f, -0.08f, 0.48f),
                new Vector3(0.15f, 0.12f, 0.16f), skin);
            CreatePart(PrimitiveType.Sphere, new Vector3(0.1f, -0.08f, 0.48f),
                new Vector3(0.15f, 0.12f, 0.16f), skin);
            return;
        }

        CreatePart(PrimitiveType.Capsule, new Vector3(0.2f, -0.2f, 0.22f),
            new Vector3(0.12f, 0.38f, 0.12f), sleeve, Quaternion.Euler(90f, 0f, 12f));
        CreatePart(PrimitiveType.Sphere, new Vector3(0.22f, -0.08f, 0.47f),
            new Vector3(0.15f, 0.12f, 0.16f), skin);
        CreatePart(PrimitiveType.Cube, new Vector3(0.22f, -0.02f, 0.57f),
            new Vector3(0.075f, 0.08f, 0.2f), new Color(0.25f, 0.16f, 0.1f),
            Quaternion.Euler(-18f, 0f, 0f));
        CreatePart(PrimitiveType.Cube, new Vector3(0.22f, 0.04f, 0.79f),
            new Vector3(0.045f, 0.035f, 0.28f), new Color(0.72f, 0.74f, 0.7f),
            Quaternion.Euler(-18f, 0f, 0f));
    }

    private void CreatePart(
        PrimitiveType shape,
        Vector3 position,
        Vector3 scale,
        Color color,
        Quaternion? rotation = null)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.transform.SetParent(weaponRoot.transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        if (rotation.HasValue)
        {
            part.transform.localRotation = rotation.Value;
        }

        Renderer partRenderer = part.GetComponent<Renderer>();
        partRenderer.sharedMaterial = GetPartMaterial(color);
        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
        {
            Destroy(partCollider);
        }
    }

    private static Material GetPartMaterial(Color color)
    {
        if (PartMaterials.TryGetValue(color, out Material material))
        {
            return material;
        }

        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError("Could not find the Standard shader for weapon view materials.");
            return null;
        }

        material = new Material(shader)
        {
            color = color
        };
        PartMaterials.Add(color, material);
        return material;
    }

    private void OnWeaponFired(WeaponDefinition weapon)
    {
        if (weapon == null || weapon.category == WeaponCategory.Grenade ||
            weapon.category == WeaponCategory.Melee ||
            muzzleLight == null || muzzleFlash == null)
        {
            return;
        }

        muzzleLight.color = Color.Lerp(new Color(1f, 0.35f, 0.05f), weapon.muzzleColor, 0.2f);
        muzzleLight.intensity = 3f;
        flashTimer = 0.055f;
        muzzleFlash.Emit(Random.Range(5, 9));

        recoilAmount = 1f;
    }
}
