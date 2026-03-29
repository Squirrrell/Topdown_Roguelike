using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class EnemyWaveSpawner : MonoBehaviour
{
    public enum SpawnerState
    {
        WaitingForActivation,
        WaitingForWaveDelay,
        SpawningWave,
        WaitingForWaveClear
    }

    [Header("Activation")]
    public bool startWavesWithKey = true;
    public Key startKey = Key.E;

    [Header("Debug")]
    public bool debugMode = false;
    [FormerlySerializedAs("showDebugOveorlay")]
    public bool showDebugOverlay = true;
    public bool outputDebugToConsole = true;
    public float debugLogInterval = 1f;
    public Key toggleDebugKey = Key.F3;
    public Key forceStartKey = Key.F8;
    public Key forceSpawnWaveKey = Key.F9;

    [Header("Wave Size")]
    public int minEnemiesPerWave = 2;
    public int maxEnemiesPerWave = 7;
    public float delayBeforeNextWave = 1f;

    [Header("Spawn Source")]
    public EnemyChase[] enemyPrefabs;
    public EnemyChase fallbackEnemyPrefab;
    public bool autoUseSceneEnemyAsFallback = true;
    public bool autoCreateRuntimeFallbackEnemy = true;
    public bool randomizeTypeWhenUsingFallback = true;

    [Header("Spawn Area")]
    public Transform areaCenter;
    public Vector2 areaSize = new Vector2(16f, 9f);
    public int maxSpawnValidationAttempts = 12;
    public bool useWallDefinedPlayableArea = true;
    public string leftWallName = "LeftWall";
    public string rightWallName = "RightWall";
    public string topWallName = "TopWall";
    public string bottomWallName = "BottomWall";
    public bool constrainSpawnsToCameraView = true;
    public float spawnEdgePadding = 0.3f;

    [Header("Summoning")]
    public float summonDelay = 0.45f;
    public float spawnSpacingWithinWave = 0.08f;
    public float summonCircleRadius = 0.55f;
    public float summonCircleThickness = 0.16f;
    public string summonCircleSortingLayerName = "Default";
    public int summonCircleSortingOrder = 10;

    private int currentWave = 0;
    private bool waitingForNextWave = false;
    private bool wavesActivated = false;
    private bool hasLoggedMissingSpawnSourceWarning = false;
    private EnemyChase runtimeFallbackEnemyPrefab;
    private SpawnerState state = SpawnerState.WaitingForActivation;
    private float nextDebugLogTime = 0f;
    private GameObject cachedLeftWall;
    private GameObject cachedRightWall;
    private GameObject cachedTopWall;
    private GameObject cachedBottomWall;

    private static Sprite summonCircleSprite;

    void Start()
    {
        // Intentionally no spawn on start; waves begin after pressing E.
        SetState(SpawnerState.WaitingForActivation);
    }

    void Update()
    {
        HandleDebugInput();
        TryLogDebugSnapshot();

        if (!wavesActivated)
        {
            if (startWavesWithKey && IsStartKeyPressed())
                ActivateWaves();

            return;
        }

        if (waitingForNextWave)
            return;

        if (EnemyChase.ActiveEnemyCount == 0)
        {
            waitingForNextWave = true;
            SetState(SpawnerState.WaitingForWaveDelay);
            Invoke(nameof(SpawnNextWave), delayBeforeNextWave);
        }
    }

    public void ActivateWaves()
    {
        if (wavesActivated)
            return;

        if (!HasAnySpawnSourceConfigured())
        {
            StopWavesDueToMissingSpawnSource();
            return;
        }

        hasLoggedMissingSpawnSourceWarning = false;

        wavesActivated = true;
        waitingForNextWave = true;
        SetState(SpawnerState.WaitingForWaveDelay);
        Invoke(nameof(SpawnNextWave), delayBeforeNextWave);
        Debug.Log("Enemy waves activated.");
    }

    bool IsStartKeyPressed()
    {
        if (Keyboard.current != null)
        {
            var keyControl = Keyboard.current[startKey];
            if (keyControl != null && keyControl.wasPressedThisFrame)
                return true;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        if (startKey == Key.E && Input.GetKeyDown(KeyCode.E))
            return true;
#endif

        return false;
    }

    void SpawnNextWave()
    {
        if (!wavesActivated)
            return;

        currentWave++;
        SetState(SpawnerState.SpawningWave);

        int count = Random.Range(minEnemiesPerWave, maxEnemiesPerWave + 1);
        StartCoroutine(SpawnWaveRoutine(count));
    }

    IEnumerator SpawnWaveRoutine(int count)
    {
        EnemyChase[] prefabsToSpawn = new EnemyChase[count];
        Vector3[] spawnPositions = new Vector3[count];
        GameObject[] summonCircles = new GameObject[count];

        for (int i = 0; i < count; i++)
        {
            EnemyChase prefabToSpawn = PickEnemyPrefab();
            if (prefabToSpawn == null)
            {
                for (int j = 0; j < i; j++)
                {
                    if (summonCircles[j] != null)
                        Destroy(summonCircles[j]);
                }

                StopWavesDueToMissingSpawnSource();
                yield break;
            }

            Vector3 spawnPosition = GetRandomSpawnPosition();
            prefabsToSpawn[i] = prefabToSpawn;
            spawnPositions[i] = spawnPosition;
            summonCircles[i] = CreateSummoningCircle(spawnPosition);
        }

        if (summonDelay > 0f)
            yield return new WaitForSeconds(summonDelay);

        for (int i = 0; i < count; i++)
        {
            if (summonCircles[i] != null)
                Destroy(summonCircles[i]);

            Vector3 validatedSpawnPosition = GetValidatedSpawnPosition(spawnPositions[i]);
            EnemyChase enemy = Instantiate(prefabsToSpawn[i], validatedSpawnPosition, Quaternion.identity);
            if (!enemy.gameObject.activeSelf)
                enemy.gameObject.SetActive(true);

            if (randomizeTypeWhenUsingFallback && fallbackEnemyPrefab != null && prefabsToSpawn[i] == fallbackEnemyPrefab)
            {
                enemy.ConfigureEnemyType((EnemyChase.EnemyType)Random.Range(0, 3), true);
            }
        }

        waitingForNextWave = false;
        SetState(SpawnerState.WaitingForWaveClear);
        Debug.Log("Spawned wave " + currentWave + " with " + count + " enemies");
    }

    void HandleDebugInput()
    {
        if (Keyboard.current == null)
            return;

        var toggleKeyControl = Keyboard.current[toggleDebugKey];
        if (toggleKeyControl != null && toggleKeyControl.wasPressedThisFrame)
        {
            debugMode = !debugMode;
            Debug.Log("EnemyWaveSpawner debugMode: " + debugMode);
        }

        if (!debugMode)
            return;

        var forceStartKeyControl = Keyboard.current[forceStartKey];
        if (forceStartKeyControl != null && forceStartKeyControl.wasPressedThisFrame)
            ActivateWaves();

        var forceWaveKeyControl = Keyboard.current[forceSpawnWaveKey];
        if (forceWaveKeyControl != null && forceWaveKeyControl.wasPressedThisFrame)
        {
            if (!wavesActivated)
                ActivateWaves();
            else if (!waitingForNextWave)
                SpawnNextWave();
        }
    }

    void SetState(SpawnerState newState)
    {
        if (state == newState)
            return;

        state = newState;
        if (debugMode)
            Debug.Log("EnemyWaveSpawner state -> " + state);
    }

    void TryLogDebugSnapshot()
    {
        if (!debugMode || !outputDebugToConsole)
            return;

        if (Time.time < nextDebugLogTime)
            return;

        nextDebugLogTime = Time.time + Mathf.Max(0.1f, debugLogInterval);

        Debug.Log(
            "EnemyWaveSpawner debug | state=" + state +
            " | wavesActivated=" + wavesActivated +
            " | waitingForNextWave=" + waitingForNextWave +
            " | currentWave=" + currentWave +
            " | liveEnemies=" + EnemyChase.ActiveEnemyCount);
    }

    void OnGUI()
    {
        if (!debugMode || !showDebugOverlay)
            return;

        Rect panel = new Rect(12f, 12f, 340f, 145f);
        GUI.Box(panel, "Spawner Debug");

        GUILayout.BeginArea(new Rect(22f, 38f, 320f, 120f));
        GUILayout.Label("State: " + state);
        GUILayout.Label("Waves Activated: " + wavesActivated);
        GUILayout.Label("Waiting For Next Wave: " + waitingForNextWave);
        GUILayout.Label("Current Wave: " + currentWave);
        GUILayout.Label("Live Enemies: " + EnemyChase.ActiveEnemyCount);
        GUILayout.EndArea();
    }

    EnemyChase PickEnemyPrefab()
    {
        if (enemyPrefabs != null && enemyPrefabs.Length > 0)
        {
            int validCount = 0;
            for (int i = 0; i < enemyPrefabs.Length; i++)
            {
                if (enemyPrefabs[i] != null)
                    validCount++;
            }

            if (validCount > 0)
            {
                int targetValidIndex = Random.Range(0, validCount);
                for (int i = 0; i < enemyPrefabs.Length; i++)
                {
                    if (enemyPrefabs[i] == null)
                        continue;

                    if (targetValidIndex == 0)
                        return enemyPrefabs[i];

                    targetValidIndex--;
                }
            }
        }

        return fallbackEnemyPrefab;
    }

    bool HasAnySpawnSourceConfigured()
    {
        TryAutoAssignFallbackFromScene();
        TryCreateRuntimeFallbackEnemy();

        if (fallbackEnemyPrefab != null)
            return true;

        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            return false;

        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            if (enemyPrefabs[i] != null)
                return true;
        }

        return false;
    }

    void TryAutoAssignFallbackFromScene()
    {
        if (!autoUseSceneEnemyAsFallback || fallbackEnemyPrefab != null)
            return;

        EnemyChase liveEnemy = FindFirstObjectByType<EnemyChase>();
        if (liveEnemy == null)
            return;

        fallbackEnemyPrefab = liveEnemy;
        Debug.LogWarning("EnemyWaveSpawner: No spawn prefab assigned; using an existing EnemyChase from the scene as fallback template.", this);
    }

    void TryCreateRuntimeFallbackEnemy()
    {
        if (!autoCreateRuntimeFallbackEnemy || fallbackEnemyPrefab != null)
            return;

        if (runtimeFallbackEnemyPrefab != null)
        {
            fallbackEnemyPrefab = runtimeFallbackEnemyPrefab;
            return;
        }

        GameObject runtimeTemplate = new GameObject(
            "Runtime Enemy Fallback Template",
            typeof(SpriteRenderer),
            typeof(Rigidbody2D),
            typeof(CircleCollider2D),
            typeof(EnemyChase));

        runtimeTemplate.hideFlags = HideFlags.HideInHierarchy;
        runtimeTemplate.SetActive(false);

        Rigidbody2D rb = runtimeTemplate.GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        CircleCollider2D circleCollider = runtimeTemplate.GetComponent<CircleCollider2D>();
        circleCollider.isTrigger = false;
        circleCollider.radius = 0.28f;

        runtimeFallbackEnemyPrefab = runtimeTemplate.GetComponent<EnemyChase>();
        fallbackEnemyPrefab = runtimeFallbackEnemyPrefab;

        Debug.LogWarning("EnemyWaveSpawner: No spawn prefab assigned; generated a runtime fallback EnemyChase template.", this);
    }

    void StopWavesDueToMissingSpawnSource()
    {
        if (!hasLoggedMissingSpawnSourceWarning)
        {
            Debug.LogWarning("EnemyWaveSpawner: Assign at least one valid EnemyChase prefab in enemyPrefabs or fallbackEnemyPrefab in the Inspector.", this);
            hasLoggedMissingSpawnSourceWarning = true;
        }

        wavesActivated = false;
        waitingForNextWave = false;
        CancelInvoke(nameof(SpawnNextWave));
        SetState(SpawnerState.WaitingForActivation);
    }

    void OnDestroy()
    {
        if (runtimeFallbackEnemyPrefab != null)
            Destroy(runtimeFallbackEnemyPrefab.gameObject);
    }

    Vector3 GetRandomSpawnPosition()
    {
        Vector3 center = areaCenter != null ? areaCenter.position : transform.position;
        Bounds spawnBounds = GetAllowedSpawnBounds();
        int attempts = Mathf.Max(1, maxSpawnValidationAttempts);

        for (int i = 0; i < attempts; i++)
        {
            float x = Random.Range(spawnBounds.min.x, spawnBounds.max.x);
            float y = Random.Range(spawnBounds.min.y, spawnBounds.max.y);
            Vector3 candidate = new Vector3(x, y, 0f);

            if (IsPositionInsideSpawnBox(candidate))
                return candidate;
        }

        Vector3 fallback = new Vector3(center.x, center.y, 0f);
        Vector3 clamped = ClampPositionToSpawnBox(fallback);

        if (!IsPositionInsideSpawnBox(clamped))
            Debug.LogError("EnemyWaveSpawner: Spawn validation failed. Clamped fallback is still outside spawn box.", this);

        return clamped;
    }

    Bounds GetSpawnBounds()
    {
        Vector3 center = areaCenter != null ? areaCenter.position : transform.position;
        Vector3 size = new Vector3(Mathf.Max(0.01f, areaSize.x), Mathf.Max(0.01f, areaSize.y), 0.01f);
        return new Bounds(new Vector3(center.x, center.y, 0f), size);
    }

    bool IsPositionInsideSpawnBox(Vector3 position)
    {
        Bounds spawnBounds = GetAllowedSpawnBounds();
        return
            position.x >= spawnBounds.min.x && position.x <= spawnBounds.max.x &&
            position.y >= spawnBounds.min.y && position.y <= spawnBounds.max.y;
    }

    Vector3 ClampPositionToSpawnBox(Vector3 position)
    {
        Bounds spawnBounds = GetAllowedSpawnBounds();
        float x = Mathf.Clamp(position.x, spawnBounds.min.x, spawnBounds.max.x);
        float y = Mathf.Clamp(position.y, spawnBounds.min.y, spawnBounds.max.y);
        return new Vector3(x, y, 0f);
    }

    Vector3 GetValidatedSpawnPosition(Vector3 position)
    {
        if (IsPositionInsideSpawnBox(position))
            return position;

        Vector3 clamped = ClampPositionToSpawnBox(position);
        if (!IsPositionInsideSpawnBox(clamped))
            Debug.LogError("EnemyWaveSpawner: Failed to clamp spawn into playable bounds.", this);

        return clamped;
    }

    Bounds GetAllowedSpawnBounds()
    {
        Bounds allowed = GetSpawnBounds();

        if (useWallDefinedPlayableArea && TryGetWallDefinedPlayableBounds(out Bounds wallBounds))
        {
            float wallMinX = wallBounds.min.x + spawnEdgePadding;
            float wallMaxX = wallBounds.max.x - spawnEdgePadding;
            float wallMinY = wallBounds.min.y + spawnEdgePadding;
            float wallMaxY = wallBounds.max.y - spawnEdgePadding;

            float wallClampedMinX = Mathf.Max(allowed.min.x, wallMinX);
            float wallClampedMaxX = Mathf.Min(allowed.max.x, wallMaxX);
            float wallClampedMinY = Mathf.Max(allowed.min.y, wallMinY);
            float wallClampedMaxY = Mathf.Min(allowed.max.y, wallMaxY);

            if (wallClampedMinX <= wallClampedMaxX && wallClampedMinY <= wallClampedMaxY)
            {
                Vector3 wallCenter = new Vector3((wallClampedMinX + wallClampedMaxX) * 0.5f, (wallClampedMinY + wallClampedMaxY) * 0.5f, 0f);
                Vector3 wallSize = new Vector3(Mathf.Max(0.01f, wallClampedMaxX - wallClampedMinX), Mathf.Max(0.01f, wallClampedMaxY - wallClampedMinY), 0.01f);
                return new Bounds(wallCenter, wallSize);
            }

            Debug.LogWarning("EnemyWaveSpawner: Wall-defined playable bounds do not overlap spawn area; falling back to other bounds checks.", this);
        }

        if (!constrainSpawnsToCameraView)
            return allowed;

        if (!TryGetCameraBounds(out Bounds cameraBounds))
            return allowed;

        float minX = Mathf.Max(allowed.min.x, cameraBounds.min.x + spawnEdgePadding);
        float maxX = Mathf.Min(allowed.max.x, cameraBounds.max.x - spawnEdgePadding);
        float minY = Mathf.Max(allowed.min.y, cameraBounds.min.y + spawnEdgePadding);
        float maxY = Mathf.Min(allowed.max.y, cameraBounds.max.y - spawnEdgePadding);

        if (minX > maxX || minY > maxY)
        {
            Debug.LogWarning("EnemyWaveSpawner: Spawn area does not overlap camera bounds; falling back to spawn area only.", this);
            return allowed;
        }

        Vector3 center = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
        Vector3 size = new Vector3(Mathf.Max(0.01f, maxX - minX), Mathf.Max(0.01f, maxY - minY), 0.01f);
        return new Bounds(center, size);
    }

    bool TryGetCameraBounds(out Bounds cameraBounds)
    {
        cameraBounds = default;
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic)
            return false;

        Vector3 camPos = cam.transform.position;
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        cameraBounds = new Bounds(
            new Vector3(camPos.x, camPos.y, 0f),
            new Vector3(halfWidth * 2f, halfHeight * 2f, 0.01f));

        return true;
    }

    public bool TryGetPlayableBounds(out Bounds bounds)
    {
        if (useWallDefinedPlayableArea && TryGetWallDefinedPlayableBounds(out bounds))
            return true;

        bounds = GetAllowedSpawnBounds();
        return true;
    }

    bool TryGetWallDefinedPlayableBounds(out Bounds bounds)
    {
        bounds = default;

        GameObject leftWall = GetCachedWall(ref cachedLeftWall, leftWallName);
        GameObject rightWall = GetCachedWall(ref cachedRightWall, rightWallName);
        GameObject topWall = GetCachedWall(ref cachedTopWall, topWallName);
        GameObject bottomWall = GetCachedWall(ref cachedBottomWall, bottomWallName);

        if (leftWall == null || rightWall == null || topWall == null || bottomWall == null)
            return false;

        float leftInner = GetWallInnerCoordinate(leftWall, true, true);
        float rightInner = GetWallInnerCoordinate(rightWall, true, false);
        float topInner = GetWallInnerCoordinate(topWall, false, false);
        float bottomInner = GetWallInnerCoordinate(bottomWall, false, true);

        if (leftInner >= rightInner || bottomInner >= topInner)
            return false;

        Vector3 center = new Vector3((leftInner + rightInner) * 0.5f, (bottomInner + topInner) * 0.5f, 0f);
        Vector3 size = new Vector3(rightInner - leftInner, topInner - bottomInner, 0.01f);
        bounds = new Bounds(center, size);
        return true;
    }

    GameObject GetCachedWall(ref GameObject cachedWall, string wallName)
    {
        if (cachedWall != null && cachedWall.name == wallName)
            return cachedWall;

        cachedWall = GameObject.Find(wallName);
        return cachedWall;
    }

    float GetWallInnerCoordinate(GameObject wall, bool xAxis, bool useMax)
    {
        Collider2D wallCollider = wall.GetComponent<Collider2D>();
        if (wallCollider != null)
        {
            if (xAxis)
                return useMax ? wallCollider.bounds.max.x : wallCollider.bounds.min.x;

            return useMax ? wallCollider.bounds.max.y : wallCollider.bounds.min.y;
        }

        return xAxis ? wall.transform.position.x : wall.transform.position.y;
    }

    GameObject CreateSummoningCircle(Vector3 position)
    {
        GameObject circleObject = new GameObject("Summoning Circle", typeof(SpriteRenderer));
        circleObject.transform.position = new Vector3(position.x, position.y, 0f);

        SpriteRenderer sr = circleObject.GetComponent<SpriteRenderer>();
        sr.sprite = GetSummonCircleSprite();
        sr.color = new Color(1f, 0f, 0f, 1f);

        if (!string.IsNullOrWhiteSpace(summonCircleSortingLayerName))
            sr.sortingLayerName = summonCircleSortingLayerName;

        sr.sortingOrder = summonCircleSortingOrder;
        return circleObject;
    }

    Sprite GetSummonCircleSprite()
    {
        if (summonCircleSprite != null)
            return summonCircleSprite;

        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;

        float outerRadius = (size - 1) * 0.5f;
        float innerRadius = outerRadius * Mathf.Clamp01(1f - summonCircleThickness);
        Vector2 center = new Vector2(outerRadius, outerRadius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                bool insideRing = dist <= outerRadius && dist >= innerRadius;
                texture.SetPixel(x, y, insideRing ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        summonCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size / (summonCircleRadius * 2f));
        return summonCircleSprite;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.35f);
        Vector3 center = areaCenter != null ? areaCenter.position : transform.position;
        Gizmos.DrawCube(center, new Vector3(areaSize.x, areaSize.y, 0.01f));
    }
}
