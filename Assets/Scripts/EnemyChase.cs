using UnityEngine;
using System.Collections.Generic;

public class EnemyChase : MonoBehaviour
{
    public static int ActiveEnemyCount { get; private set; }

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
    public int maxPooledEnemyBullets = 320;

    [Header("Fast Striker Dash")]
    public float strikerSpeedFactorOfPlayer = 0.5f;
    public float strikerDashSpeed = 16f;
    public float strikerDashDuration = 0.2f;
    public float strikerDashDistanceMultiplier = 3f;
    public float strikerDashTelegraphTime = 0.45f;
    public float strikerDashCooldownMin = 1.4f;
    public float strikerDashCooldownMax = 2.4f;
    public float strikerDashLineLength = 2f;
    public float strikerDashLineSpacing = 0.35f;
    public float strikerDashLineWidth = 0.06f;
    public Color strikerDashLineColor = new Color(1f, 0.75f, 0.1f, 0.95f);
    public string strikerDashLineSortingLayerName = "Default";
    public int strikerDashLineSortingOrder = 30;

    private static Sprite circleSprite;
    private static Sprite squareSprite;

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Transform player;
    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private Collider2D[] enemyColliders;
    private float nextDamageTime = 0f;
    private float nextShootTime = 0f;
    private int currentHealth;
    private bool isDeadEnemy = false;
    private bool isCountedAsActiveEnemy = false;
    private float nextPlayerSearchTime = 0f;

    private bool strikerIsTelegraphing = false;
    private bool strikerIsDashing = false;
    private float strikerTelegraphEndTime = 0f;
    private float strikerNextDashTime = 0f;
    private Vector2 strikerDashDirection = Vector2.right;
    private float strikerDashRemainingDistance = 0f;
    private LineRenderer strikerTelegraphLineA;
    private LineRenderer strikerTelegraphLineB;
    private static Material lineMaterial;
    private EnemyWaveSpawner waveSpawner;
    private readonly Queue<EnemyBullet> bulletPool = new Queue<EnemyBullet>(64);

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
        waveSpawner = FindFirstObjectByType<EnemyWaveSpawner>();
        TryFindPlayer();

        // Dynamic bodies with solid colliders allow enemies to collide with each other.
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        if (enemyColliders != null)
        {
            for (int i = 0; i < enemyColliders.Length; i++)
            {
                if (enemyColliders[i] != null)
                    enemyColliders[i].isTrigger = false;
            }
        }

        IgnorePlayerCollisions();

        if (enemyType == EnemyType.FastStriker)
        {
            UpdateStrikerBaseSpeedFromPlayer();
            CreateStrikerTelegraphLines();
            strikerNextDashTime = Time.time + Random.Range(strikerDashCooldownMin, strikerDashCooldownMax);
        }

        if (enemyType == EnemyType.SlowShooter)
            nextShootTime = Time.time + shootInterval;
    }

    void OnEnable()
    {
        if (!isCountedAsActiveEnemy)
        {
            ActiveEnemyCount++;
            isCountedAsActiveEnemy = true;
        }
    }

    void OnDisable()
    {
        if (isCountedAsActiveEnemy)
        {
            ActiveEnemyCount = Mathf.Max(0, ActiveEnemyCount - 1);
            isCountedAsActiveEnemy = false;
        }

        HideStrikerTelegraphLines();
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

        if (enemyType == EnemyType.FastStriker)
            HandleFastStrikerMovement(enemyPos, toPlayer);
        else if (toPlayer.sqrMagnitude > stopDistance * stopDistance)
            MoveWithSpeed(enemyPos, toPlayer.normalized, speed);

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

    void TryFindPlayer()
    {
        if (Time.time < nextPlayerSearchTime)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            nextPlayerSearchTime = Time.time + 0.25f;
            return;
        }

        player = playerObject.transform;
        playerHealth = playerObject.GetComponent<PlayerHealth>();
        playerMovement = playerObject.GetComponent<PlayerMovement>();
        IgnorePlayerCollisions();
        UpdateStrikerBaseSpeedFromPlayer();
        nextPlayerSearchTime = Time.time + 1f;
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
        if (damage <= 0 || isDeadEnemy)
            return;

        currentHealth -= damage;

        if (currentHealth <= 0)
            Die();
    }

    public void ConfigureEnemyType(EnemyType newType, bool applyDefaults)
    {
        enemyType = newType;

        if (applyDefaults)
            ApplyTypeDefaults();

        currentHealth = maxHealth;
        ApplyTypeVisuals();

        strikerIsTelegraphing = false;
        strikerIsDashing = false;
        HideStrikerTelegraphLines();

        if (enemyType == EnemyType.SlowShooter)
            nextShootTime = Time.time + shootInterval;
        else
            nextShootTime = 0f;

        if (enemyType == EnemyType.FastStriker)
        {
            UpdateStrikerBaseSpeedFromPlayer();
            CreateStrikerTelegraphLines();
            strikerNextDashTime = Time.time + Random.Range(strikerDashCooldownMin, strikerDashCooldownMax);
        }
    }

    void Die()
    {
        if (isDeadEnemy)
            return;

        isDeadEnemy = true;

        if (playerHealth == null)
            TryFindPlayer();

        if (playerHealth != null)
            playerHealth.AddExperience(1);

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
                speed = 3f;
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

            EnemyBullet bullet = GetEnemyBulletFromPool();
            GameObject bulletObject = bullet.gameObject;
            bulletObject.hideFlags = HideFlags.None;
            bulletObject.SetActive(true);
            bulletObject.transform.position = origin;
            bullet.Initialize(direction.normalized, bulletSpeed, bulletDamage, bulletLifetime, gameObject, ReturnEnemyBulletToPool);
        }
    }

    EnemyBullet GetEnemyBulletFromPool()
    {
        while (bulletPool.Count > 0)
        {
            EnemyBullet pooled = bulletPool.Dequeue();
            if (pooled != null)
                return pooled;
        }

        return CreateEnemyBullet();
    }

    EnemyBullet CreateEnemyBullet()
    {
        GameObject bulletObject = new GameObject("Enemy Bullet", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D), typeof(EnemyBullet));

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

        return bulletObject.GetComponent<EnemyBullet>();
    }

    void ReturnEnemyBulletToPool(EnemyBullet bullet)
    {
        if (bullet == null)
            return;

        int maxPool = Mathf.Max(0, maxPooledEnemyBullets);
        if (bulletPool.Count >= maxPool)
        {
            Destroy(bullet.gameObject);
            return;
        }

        bullet.gameObject.hideFlags = HideFlags.HideInHierarchy;
        bullet.gameObject.SetActive(false);
        bulletPool.Enqueue(bullet);
    }

    void MoveWithSpeed(Vector2 enemyPos, Vector2 direction, float moveSpeed)
    {
        Vector2 next = enemyPos + direction * moveSpeed * Time.fixedDeltaTime;

        if (rb != null)
            rb.MovePosition(next);
        else
            transform.position = next;
    }

    void HandleFastStrikerMovement(Vector2 enemyPos, Vector2 toPlayer)
    {
        UpdateStrikerBaseSpeedFromPlayer();

        if (strikerIsDashing)
        {
            if (strikerDashRemainingDistance <= 0f)
            {
                strikerIsDashing = false;
                strikerNextDashTime = Time.time + Random.Range(strikerDashCooldownMin, strikerDashCooldownMax);
            }
            else
            {
                float maxStep = strikerDashSpeed * Time.fixedDeltaTime;
                float stepDistance = Mathf.Min(maxStep, strikerDashRemainingDistance);
                MoveWithSpeed(enemyPos, strikerDashDirection, stepDistance / Time.fixedDeltaTime);
                strikerDashRemainingDistance -= stepDistance;
                return;
            }
        }

        if (strikerIsTelegraphing)
        {
            UpdateStrikerTelegraphLines(enemyPos);

            if (Time.time >= strikerTelegraphEndTime)
            {
                strikerIsTelegraphing = false;
                strikerIsDashing = true;
                strikerDashRemainingDistance = GetStrikerDashDistance();
                HideStrikerTelegraphLines();
            }

            return;
        }

        if (toPlayer.sqrMagnitude > stopDistance * stopDistance)
            MoveWithSpeed(enemyPos, toPlayer.normalized, speed);

        if (Time.time >= strikerNextDashTime && toPlayer.sqrMagnitude > 0.001f)
            StartStrikerTelegraph(enemyPos, toPlayer.normalized);
    }

    void StartStrikerTelegraph(Vector2 enemyPos, Vector2 direction)
    {
        strikerDashDirection = direction;
        strikerIsTelegraphing = true;
        strikerTelegraphEndTime = Time.time + strikerDashTelegraphTime;
        CreateStrikerTelegraphLines();
        UpdateStrikerTelegraphLines(enemyPos);
        SetStrikerTelegraphVisible(true);
    }

    void UpdateStrikerBaseSpeedFromPlayer()
    {
        if (enemyType != EnemyType.FastStriker)
            return;

        if (playerMovement == null)
            return;

        speed = Mathf.Max(0.1f, playerMovement.speed * strikerSpeedFactorOfPlayer);
    }

    void CreateStrikerTelegraphLines()
    {
        if (strikerTelegraphLineA != null && strikerTelegraphLineB != null)
            return;

        if (lineMaterial == null)
            lineMaterial = new Material(Shader.Find("Sprites/Default"));

        strikerTelegraphLineA = CreateTelegraphLineRenderer("Dash Telegraph A");
        strikerTelegraphLineB = CreateTelegraphLineRenderer("Dash Telegraph B");
        SetStrikerTelegraphVisible(false);
    }

    LineRenderer CreateTelegraphLineRenderer(string objectName)
    {
        GameObject lineObj = new GameObject(objectName, typeof(LineRenderer));
        lineObj.transform.SetParent(transform, false);

        LineRenderer line = lineObj.GetComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.material = lineMaterial;
        line.startWidth = strikerDashLineWidth;
        line.endWidth = strikerDashLineWidth;
        line.startColor = strikerDashLineColor;
        line.endColor = strikerDashLineColor;
        line.numCapVertices = 4;

        if (!string.IsNullOrWhiteSpace(strikerDashLineSortingLayerName))
            line.sortingLayerName = strikerDashLineSortingLayerName;

        line.sortingOrder = strikerDashLineSortingOrder;
        return line;
    }

    void UpdateStrikerTelegraphLines(Vector2 enemyPos)
    {
        if (strikerTelegraphLineA == null || strikerTelegraphLineB == null)
            return;

        Vector2 perpendicular = new Vector2(-strikerDashDirection.y, strikerDashDirection.x) * (strikerDashLineSpacing * 0.5f);
        Vector2 startA = enemyPos + perpendicular;
        Vector2 startB = enemyPos - perpendicular;
        float dashDistance = GetStrikerDashDistance();
        Bounds? fightingBounds = TryGetFightingBounds();
        float lengthA = dashDistance;
        float lengthB = dashDistance;

        if (fightingBounds.HasValue)
        {
            lengthA = GetDistanceToBoundsEdge(startA, strikerDashDirection, fightingBounds.Value, dashDistance);
            lengthB = GetDistanceToBoundsEdge(startB, strikerDashDirection, fightingBounds.Value, dashDistance);
        }

        Vector2 endA = startA + strikerDashDirection * lengthA;
        Vector2 endB = startB + strikerDashDirection * lengthB;

        float z = transform.position.z;
        strikerTelegraphLineA.SetPosition(0, new Vector3(startA.x, startA.y, z));
        strikerTelegraphLineA.SetPosition(1, new Vector3(endA.x, endA.y, z));
        strikerTelegraphLineB.SetPosition(0, new Vector3(startB.x, startB.y, z));
        strikerTelegraphLineB.SetPosition(1, new Vector3(endB.x, endB.y, z));
    }

    float GetStrikerDashDistance()
    {
        float baseDistance = Mathf.Max(0f, strikerDashSpeed * strikerDashDuration);
        return baseDistance * Mathf.Max(0f, strikerDashDistanceMultiplier);
    }

    Bounds? TryGetFightingBounds()
    {
        if (waveSpawner == null)
            waveSpawner = FindFirstObjectByType<EnemyWaveSpawner>();

        if (waveSpawner == null)
            return null;

        if (waveSpawner.TryGetPlayableBounds(out Bounds playableBounds))
            return playableBounds;

        Vector3 center = waveSpawner.areaCenter != null ? waveSpawner.areaCenter.position : waveSpawner.transform.position;
        Vector3 size = new Vector3(Mathf.Max(0.01f, waveSpawner.areaSize.x), Mathf.Max(0.01f, waveSpawner.areaSize.y), 0.01f);
        return new Bounds(new Vector3(center.x, center.y, 0f), size);
    }

    float GetDistanceToBoundsEdge(Vector2 start, Vector2 direction, Bounds bounds, float maxDistance)
    {
        float tx = float.PositiveInfinity;
        if (Mathf.Abs(direction.x) > 0.0001f)
        {
            float boundX = direction.x > 0f ? bounds.max.x : bounds.min.x;
            tx = (boundX - start.x) / direction.x;
        }

        float ty = float.PositiveInfinity;
        if (Mathf.Abs(direction.y) > 0.0001f)
        {
            float boundY = direction.y > 0f ? bounds.max.y : bounds.min.y;
            ty = (boundY - start.y) / direction.y;
        }

        float distanceToEdge = Mathf.Min(tx, ty);
        if (float.IsInfinity(distanceToEdge) || distanceToEdge <= 0f)
            return 0f;

        return Mathf.Min(distanceToEdge, maxDistance);
    }

    void HideStrikerTelegraphLines()
    {
        SetStrikerTelegraphVisible(false);
    }

    void SetStrikerTelegraphVisible(bool isVisible)
    {
        if (strikerTelegraphLineA != null)
            strikerTelegraphLineA.enabled = isVisible;

        if (strikerTelegraphLineB != null)
            strikerTelegraphLineB.enabled = isVisible;
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