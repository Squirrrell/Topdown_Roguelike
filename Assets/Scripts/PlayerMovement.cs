using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 6f;
    public bool spawnAtScreenCenter = true;

    private Rigidbody2D rb;
    private Vector2 movement;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        if (spawnAtScreenCenter)
            MoveToScreenCenter();
    }

    public void SnapToScreenCenter()
    {
        MoveToScreenCenter();
    }

    void Update()
    {
        movement = Vector2.zero;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
        {
            movement.x = -1;
        }

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
        {
            movement.x = 1;
        }

        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
        {
            movement.y = -1;
        }

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
        {
            movement.y = 1;
        }

        movement = movement.normalized;
    }

    void FixedUpdate()
    {
        if (rb == null)
            return;

        rb.linearVelocity = movement * speed;
    }

    void MoveToScreenCenter()
    {
        Vector3 targetPosition;
        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            // In a top-down 2D setup, camera XY is the visible center.
            targetPosition = new Vector3(mainCamera.transform.position.x, mainCamera.transform.position.y, transform.position.z);
        }
        else
        {
            targetPosition = new Vector3(0f, 0f, transform.position.z);
        }

        if (rb != null)
            rb.position = targetPosition;
        else
            transform.position = targetPosition;
    }
}