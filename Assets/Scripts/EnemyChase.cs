using UnityEngine;

public class EnemyChase : MonoBehaviour
{
    public enum EnemyType
    {
        BasicTank,
        FastStriker,
        SlowShooter
    }

    [Header("Type")]
    public EnemyType enemyType = EnemyType.BasicTank;
    public bool applyTypeDefaultsOnStart = true;
    public string enemySortingLayerName = "Default";
    public int enemySortingOrder = 5;

    [Header("Movement & Contact")]
    public float speed = 3f;
    public float stopDistance = 0.35f;
    public int contactDamage = 1;
    public float damageInterval = 0.6f;

    [Header("Health")]
    public int maxHealth = 5;

    [Header("Shooter")]
    public float shootInterval = 5f;
    public int bulletsPerBurst = 12;
    public float bulletSpeed = 4.5f;
    public int bulletDamage = 1;
    public float bulletLifetime = 4f;
    public string bulletSortingLayerName = "Default";
    public int bulletSortingOrder = 12;

    private static Sprite circleSprite;
    private static Sprite squareSprite;

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Transform player;
    private PlayerHealth playerHealth;
    private Collider2D[] enemyColliders;
    private float nextDamageTime = 0f;
    private float nextShootTime = 0f;
    private int currentHealth;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        enemyColliders = GetComponents<Collider2D>();

        if (applyTypeDefaultsOnStart)
            ApplyTypeDefaults();

        currentHealth = maxHealth;
        ApplyTypeVisuals();
    }

    void Start()
    {
        TryFindPlayer();

        // Kinematic movement avoids unstable physics impulses while chasing.
        if (rb != null)
            rb.bodyType = RigidbodyType2D.Kinematic;

        IgnorePlayerCollisions();

        if (enemyType == EnemyType.SlowShooter)
            nextShootTime = Time.time + shootInterval;
    }

    void FixedUpdate()
    {
        if (player == null)
        {
            TryFindPlayer();
            return;
        }

        Vector2 enemyPos = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 playerPos = player.position;
        Vector2 toPlayer = playerPos - enemyPos;

        if (toPlayer.sqrMagnitude > stopDistance * stopDistance)
        {
            Vector2 next = enemyPos + toPlayer.normalized * speed * Time.fixedDeltaTime;

            if (rb != null)
                rb.MovePosition(next);
            else
                transform.position = next;
        }

        if (toPlayer.sqrMagnitude <= (stopDistance + 0.2f) * (stopDistance + 0.2f))
            TryDealDamage();
    }

    void Update()
    {
        if (enemyType != EnemyType.SlowShooter)
            return;

        if (Time.time < nextShootTime)
            return;

        ShootRadialBurst();
        nextShootTime = Time.time + shootInterval;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player"))
            TryDealDamage();
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            TryDealDamage();
    }

    void TryFindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
            return;

        player = playerObject.transform;
        playerHealth = playerObject.GetComponent<PlayerHealth>();
        IgnorePlayerCollisions();
    }

    void IgnorePlayerCollisions()
    {
        if (player == null || enemyColliders == null)
            return;

        Collider2D[] playerColliders = player.GetComponents<Collider2D>();
        if (playerColliders == null)
            return;

        for (int i = 0; i < enemyColliders.Length; i++)
        {
            for (int j = 0; j < playerColliders.Length; j++)
            {
                if (enemyColliders[i] != null && playerColliders[j] != null)
                    Physics2D.IgnoreCollision(enemyColliders[i], playerColliders[j], true);
            }
        }
    }

    void TryDealDamage()
    {
        if (Time.time < nextDamageTime)
            return;

        if (playerHealth == null)
        {
            TryFindPlayer();
            if (playerHealth == null)
                return;
        }

        playerHealth.TakeDamage(contactDamage);
        nextDamageTime = Time.time + damageInterval;
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0)
            return;

        currentHealth -= damage;

        if (currentHealth <= 0)
            Die();
    }

    void Die()
    {
        Destroy(gameObject);
    }

    void ApplyTypeDefaults()
    {
        switch (enemyType)
        {
            case EnemyType.BasicTank:
                maxHealth = 5;
                contactDamage = 1;
                speed = 5f;
                break;

            case EnemyType.FastStriker:
                maxHealth = 3;
                contactDamage = 2;
                speed = 8.5f;
                break;

            case EnemyType.SlowShooter:
                maxHealth = 2;
                contactDamage = 1;
                speed = 2f;
                shootInterval = 5f;
                bulletsPerBurst = 12;
                break;
        }
    }

    void ApplyTypeVisuals()
    {
        if (sr == null)
            return;

        if (!string.IsNullOrWhiteSpace(enemySortingLayerName))
            sr.sortingLayerName = enemySortingLayerName;

        sr.sortingOrder = enemySortingOrder;

        switch (enemyType)
        {
            case EnemyType.BasicTank:
                sr.sprite = GetCircleSprite();
                sr.color = new Color(0.75f, 0.75f, 0.75f, 1f);
                break;

            case EnemyType.FastStriker:
                sr.sprite = GetCircleSprite();
                sr.color = new Color(1f, 0.55f, 0.05f, 1f);
                break;

            case EnemyType.SlowShooter:
                sr.sprite = GetSquareSprite();
                sr.color = new Color(0.9f, 0.1f, 0.1f, 1f);
                break;
        }
    }

    void ShootRadialBurst()
    {
        if (bulletsPerBurst <= 0)
            return;

        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        float step = 360f / bulletsPerBurst;

        for (int i = 0; i < bulletsPerBurst; i++)
        {
            float angleDegrees = i * step;
            float angleRadians = angleDegrees * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians));

            GameObject bulletObject = new GameObject("Enemy Bullet", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D), typeof(EnemyBullet));
            bulletObject.transform.position = origin;

            SpriteRenderer bulletSr = bulletObject.GetComponent<SpriteRenderer>();
            bulletSr.sprite = GetCircleSprite();
            bulletSr.color = new Color(1f, 0f, 0f, 1f);
            if (!string.IsNullOrWhiteSpace(bulletSortingLayerName))
                bulletSr.sortingLayerName = bulletSortingLayerName;

            bulletSr.sortingOrder = bulletSortingOrder;

            CircleCollider2D bulletCollider = bulletObject.GetComponent<CircleCollider2D>();
            bulletCollider.isTrigger = true;
            bulletCollider.radius = 0.18f;

            Rigidbody2D bulletRb = bulletObject.GetComponent<Rigidbody2D>();
            bulletRb.bodyType = RigidbodyType2D.Kinematic;
            bulletRb.gravityScale = 0f;

            EnemyBullet bullet = bulletObject.GetComponent<EnemyBullet>();
            bullet.Initialize(direction.normalized, bulletSpeed, bulletDamage, bulletLifetime, gameObject);
        }
    }

    Sprite GetCircleSprite()
    {
        if (circleSprite != null)
            return circleSprite;

        const int size = 64;
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
        circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
        return circleSprite;
    }

    Sprite GetSquareSprite()
    {
        if (squareSprite != null)
            return squareSprite;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, Color.white);
            }
        }

        texture.Apply();
        squareSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
        return squareSprite;
    }
}