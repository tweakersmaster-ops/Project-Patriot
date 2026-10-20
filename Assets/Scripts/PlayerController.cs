using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 8.5f;
    public float crouchSpeed = 2.5f;
    [SerializeField, Min(0f)] private float acceleration = 16f;
    [SerializeField, Min(0f)] private float deceleration = 20f;
    public float mouseSensitivity = 2f;
    public float gravity = -20f;
    public float jumpHeight = 1.5f;
    public float standingHeight = 2f;
    public float crouchingHeight = 1.2f;

    [Header("Stamina")]
    [SerializeField, Min(0.1f)] private float maxStamina = 6f;
    [SerializeField, Min(0f)] private float staminaDrainPerSecond = 1f;
    [SerializeField, Min(0f)] private float staminaRecoveryPerSecond = 0.8f;
    [SerializeField, Min(0f)] private float staminaRecoveryDelay = 1.25f;
    [SerializeField, Range(0f, 1f)] private float sprintRecoveryThreshold = 0.3f;

    private CharacterController controller;
    private Camera playerCamera;
    private float verticalRotation = 0f;
    private Vector3 velocity;
    private bool isCrouching;
    private float currentHorizontalSpeed;
    private float currentStamina;
    private float staminaRecoveryTimer;
    private bool isExhausted;
    private readonly Collider[] overlapBuffer = new Collider[16];

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float StaminaNormalized => maxStamina > 0f ? Mathf.Clamp01(currentStamina / maxStamina) : 0f;
    public float CurrentHorizontalSpeed => currentHorizontalSpeed;
    public bool IsSprinting { get; private set; }
    public bool IsCrouching => isCrouching;

    private void Start()
    {
        currentStamina = maxStamina;
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }

        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
        {
            GameObject cameraObject = new GameObject("Player Camera");
            cameraObject.transform.SetParent(transform);
            cameraObject.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            playerCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        if (controller != null)
        {
            controller.height = standingHeight;
            controller.center = new Vector3(0f, standingHeight * 0.5f, 0f);
        }

        transform.localScale = Vector3.one;
        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = new Vector3(0f, standingHeight * 0.75f, 0f);
        }

        CharacterAnimatorDriver.AttachIfPresent(gameObject);
    }

    public void ResetForNewGame()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }

        controller.enabled = true;
        controller.height = standingHeight;
        controller.center = new Vector3(0f, standingHeight * 0.5f, 0f);
        transform.localScale = Vector3.one;
        velocity = Vector3.zero;
        verticalRotation = 0f;
        isCrouching = false;
        currentHorizontalSpeed = 0f;
        currentStamina = maxStamina;
        staminaRecoveryTimer = 0f;
        isExhausted = false;
        IsSprinting = false;

        playerCamera = GetComponentInChildren<Camera>(true);
        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = new Vector3(0f, standingHeight * 0.75f, 0f);
            playerCamera.transform.localRotation = Quaternion.identity;
        }
    }

    private void Update()
    {
        if (GameContext.Instance != null && GameContext.Instance.IsPaused)
        {
            IsSprinting = false;
            return;
        }

        HandleMouseLook();
        HandleMovement();
    }

    private void HandleMouseLook()
    {
        if (playerCamera == null)
        {
            return;
        }

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -80f, 80f);
        playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    private void HandleMovement()
    {
        if (controller == null)
        {
            return;
        }

        bool crouchPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        float targetHeight = crouchPressed ? crouchingHeight : standingHeight;

        if (Mathf.Abs(controller.height - targetHeight) > 0.01f &&
            (targetHeight < controller.height || HasRoomToStand(targetHeight)))
        {
            controller.height = targetHeight;
            controller.center = new Vector3(0f, targetHeight * 0.5f, 0f);
        }

        isCrouching = controller.height < standingHeight - 0.01f;
        float targetCameraHeight = isCrouching ? crouchingHeight * 0.8f : standingHeight * 0.75f;
        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, new Vector3(0f, targetCameraHeight, 0f), Time.deltaTime * 10f);
        }

        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        bool hasMovementInput = input.sqrMagnitude > 0.01f;
        bool sprintRequested = !isCrouching &&
                               hasMovementInput &&
                               Input.GetKey(KeyCode.LeftShift) &&
                               !isExhausted;
        UpdateStamina(sprintRequested);
        IsSprinting = sprintRequested && currentStamina > 0f;

        float targetSpeed;
        if (isCrouching)
        {
            targetSpeed = crouchSpeed;
        }
        else if (IsSprinting)
        {
            float fatigue = Mathf.InverseLerp(0f, 0.4f, StaminaNormalized);
            targetSpeed = Mathf.Lerp(moveSpeed, sprintSpeed, fatigue);
        }
        else
        {
            targetSpeed = moveSpeed;
        }

        float speedChange = hasMovementInput ? acceleration : deceleration;
        currentHorizontalSpeed = Mathf.MoveTowards(
            currentHorizontalSpeed,
            hasMovementInput ? targetSpeed : 0f,
            speedChange * Time.deltaTime);

        Vector3 moveDirection = (transform.forward * input.z + transform.right * input.x).normalized;
        Vector3 movement = moveDirection * currentHorizontalSpeed;

        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        if (Input.GetButtonDown("Jump") && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;

        movement.y = velocity.y;
        controller.Move(movement * Time.deltaTime);
    }

    private void UpdateStamina(bool sprintRequested)
    {
        if (sprintRequested && currentStamina > 0f)
        {
            currentStamina = Mathf.Max(0f, currentStamina - staminaDrainPerSecond * Time.deltaTime);
            staminaRecoveryTimer = staminaRecoveryDelay;
            if (currentStamina <= 0f)
            {
                isExhausted = true;
            }
            return;
        }

        staminaRecoveryTimer = Mathf.Max(0f, staminaRecoveryTimer - Time.deltaTime);
        if (staminaRecoveryTimer <= 0f)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRecoveryPerSecond * Time.deltaTime);
        }

        if (isExhausted && StaminaNormalized >= sprintRecoveryThreshold)
        {
            isExhausted = false;
        }
    }

    private bool HasRoomToStand(float targetHeight)
    {
        Vector3 scale = transform.lossyScale;
        float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = Mathf.Max(targetHeight * Mathf.Abs(scale.y), radius * 2f);
        Vector3 center = transform.TransformPoint(new Vector3(
            controller.center.x,
            targetHeight * 0.5f,
            controller.center.z));
        float segmentOffset = height * 0.5f - radius;
        Vector3 point1 = center + transform.up * segmentOffset;
        Vector3 point2 = center - transform.up * segmentOffset;
        int overlapCount = Physics.OverlapCapsuleNonAlloc(
            point1,
            point2,
            radius,
            overlapBuffer,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlap = overlapBuffer[i];
            if (overlap != null && overlap != controller && !overlap.transform.IsChildOf(transform))
            {
                return false;
            }
        }

        return true;
    }
}
