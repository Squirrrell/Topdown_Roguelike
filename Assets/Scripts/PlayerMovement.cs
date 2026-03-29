using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 6f;
    public bool spawnAtScreenCenter = true;
    public string playerSortingLayerName = "Default";
    public int playerSortingOrder = 20;

    [Header("Dash")]
    public float dashSpeed = 18f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 3f;

    [Header("Shooting")]
    public float shootInterval = 0.7f;
    public float bulletSpeed = 11f;
    public float bulletLifetime = 3f;
    public int bulletDamage = 1;
    public int bulletPierce = 0;
    public int bulletRicochet = 0;
    public int bulletsPerShot = 1;
    public float multiShotSpreadAngle = 12f;
    public string bulletSortingLayerName = "Default";
    public int bulletSortingOrder = 14;

    [Header("Optimization")]
    public int maxPooledPlayerBullets = 160;

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private PlayerHealth playerHealth;
    private Vector2 movement;
    private Vector2 lastMoveDirection = Vector2.right;
    private float nextShootTime = 0f;
    private static Sprite bulletSprite;
    private bool isDashing = false;
    private Vector2 dashDirection = Vector2.right;
    private float dashEndTime = 0f;
    private float nextDashTime = 0f;
    private readonly Queue<PlayerBullet> bulletPool = new Queue<PlayerBullet>(64);

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        playerHealth = GetComponent<PlayerHealth>();

        if (sr != null)
        {
            if (!string.IsNullOrWhiteSpace(playerSortingLayerName))
                sr.sortingLayerName = playerSortingLayerName;

            sr.sortingOrder = playerSortingOrder;
        }

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

        if (movement.sqrMagnitude > 0.001f)
            lastMoveDirection = movement;

        HandleDashInput();

        if (isDashing && Time.time >= dashEndTime)
            EndDash();

        TryShootAtCursor();
    }

    void FixedUpdate()
    {
        if (rb == null)
            return;

        if (isDashing)
        {
            rb.linearVelocity = dashDirection * dashSpeed;
            return;
        }

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

    void TryShootAtCursor()
    {
        if (Time.time < nextShootTime)
            return;

        if (Mouse.current == null)
            return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 targetWorld3 = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, Mathf.Abs(mainCamera.transform.position.z)));
        Vector2 targetWorld = new Vector2(targetWorld3.x, targetWorld3.y);
        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 direction = targetWorld - origin;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        SpawnBullets(origin, direction.normalized);
        nextShootTime = Time.time + shootInterval;
    }

    void SpawnBullets(Vector2 origin, Vector2 baseDirection)
    {
        int shotCount = Mathf.Max(1, bulletsPerShot);
        if (shotCount == 1)
        {
            SpawnBullet(origin, baseDirection);
            return;
        }

        float clampedSpread = Mathf.Clamp(multiShotSpreadAngle, 0f, 90f);
        float centerOffset = (shotCount - 1) * 0.5f;
        for (int i = 0; i < shotCount; i++)
        {
            float offsetIndex = i - centerOffset;
            float angle = offsetIndex * clampedSpread;
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * baseDirection;
            SpawnBullet(origin, direction.normalized);
        }
    }

    void SpawnBullet(Vector2 origin, Vector2 direction)
    {
        PlayerBullet bullet = GetPlayerBulletFromPool();
        GameObject bulletObject = bullet.gameObject;
        bulletObject.hideFlags = HideFlags.None;
        bulletObject.SetActive(true);
        bulletObject.transform.position = origin;
        bullet.Initialize(direction, bulletSpeed, bulletDamage, bulletLifetime, bulletPierce, bulletRicochet, gameObject, ReturnPlayerBulletToPool);
    }

    public void AddBulletDamage(int amount)
    {
        if (amount <= 0)
            return;

        bulletDamage += amount;
    }

    public void AddBulletPierce(int amount)
    {
        if (amount <= 0)
            return;

        bulletPierce += amount;
    }

    public void AddBulletRicochet(int amount)
    {
        if (amount <= 0)
            return;

        bulletRicochet += amount;
    }

    public void AddBulletsPerShot(int amount)
    {
        if (amount <= 0)
            return;

        bulletsPerShot += amount;
    }

    PlayerBullet GetPlayerBulletFromPool()
    {
        while (bulletPool.Count > 0)
        {
            PlayerBullet pooled = bulletPool.Dequeue();
            if (pooled != null)
                return pooled;
        }

        return CreatePlayerBullet();
    }

    PlayerBullet CreatePlayerBullet()
    {
        GameObject bulletObject = new GameObject("Player Bullet", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D), typeof(PlayerBullet));

        SpriteRenderer bulletRenderer = bulletObject.GetComponent<SpriteRenderer>();
        bulletRenderer.sprite = GetBulletSprite();
        bulletRenderer.color = new Color(0.22f, 1f, 0.18f, 1f);
        if (!string.IsNullOrWhiteSpace(bulletSortingLayerName))
            bulletRenderer.sortingLayerName = bulletSortingLayerName;

        bulletRenderer.sortingOrder = bulletSortingOrder;

        CircleCollider2D bulletCollider = bulletObject.GetComponent<CircleCollider2D>();
        bulletCollider.isTrigger = true;
        bulletCollider.radius = 0.14f;

        Rigidbody2D bulletRb = bulletObject.GetComponent<Rigidbody2D>();
        bulletRb.bodyType = RigidbodyType2D.Kinematic;
        bulletRb.gravityScale = 0f;

        return bulletObject.GetComponent<PlayerBullet>();
    }

    void ReturnPlayerBulletToPool(PlayerBullet bullet)
    {
        if (bullet == null)
            return;

        int maxPool = Mathf.Max(0, maxPooledPlayerBullets);
        if (bulletPool.Count >= maxPool)
        {
            Destroy(bullet.gameObject);
            return;
        }

        bullet.gameObject.hideFlags = HideFlags.HideInHierarchy;
        bullet.gameObject.SetActive(false);
        bulletPool.Enqueue(bullet);
    }

    Sprite GetBulletSprite()
    {
        if (bulletSprite != null)
            return bulletSprite;

        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;

        float radius = (size - 1) * 0.5f;
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, dist <= radius ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        bulletSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
        return bulletSprite;
    }

    void HandleDashInput()
    {
        if (Keyboard.current == null)
            return;

        bool pressedDash = Keyboard.current.leftShiftKey.wasPressedThisFrame || Keyboard.current.rightShiftKey.wasPressedThisFrame;
        if (!pressedDash || isDashing || Time.time < nextDashTime)
            return;

        dashDirection = lastMoveDirection.sqrMagnitude > 0.001f ? lastMoveDirection.normalized : Vector2.right;
        isDashing = true;
        dashEndTime = Time.time + dashDuration;
        nextDashTime = Time.time + dashCooldown;

        if (playerHealth != null)
            playerHealth.SetExternalInvincibility(true);
    }

    void EndDash()
    {
        isDashing = false;

        if (playerHealth != null)
            playerHealth.SetExternalInvincibility(false);
    }
}