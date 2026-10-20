using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class GroundedEntity : MonoBehaviour
{
    public float gravity = -20f;

    private CharacterController controller;
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (GetComponent<TeamFollowPlayer>() != null)
        {
            enabled = false;
            return;
        }

        CapsuleCollider primitiveCollider = GetComponent<CapsuleCollider>();
        if (primitiveCollider != null)
        {
            primitiveCollider.enabled = false;
            Destroy(primitiveCollider);
        }
    }

    private void Update()
    {
        if (GameContext.Instance != null && GameContext.Instance.IsPaused)
        {
            return;
        }

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;
        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }
}
