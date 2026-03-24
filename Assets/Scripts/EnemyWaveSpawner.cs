using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

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
    public bool randomizeTypeWhenUsingFallback = true;

    [Header("Spawn Area")]
    public Transform areaCenter;
    public Vector2 areaSize = new Vector2(16f, 9f);

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
    private SpawnerState state = SpawnerState.WaitingForActivation;
    private float nextDebugLogTime = 0f;

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

        if (FindObjectsByType<EnemyChase>(FindObjectsSortMode.None).Length == 0)
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
        for (int i = 0; i < count; i++)
        {
            EnemyChase prefabToSpawn = PickEnemyPrefab();
            if (prefabToSpawn == null)
            {
                Debug.LogWarning("EnemyWaveSpawner: Assign enemyPrefabs or fallbackEnemyPrefab in Inspector.");
                waitingForNextWave = false;
                SetState(SpawnerState.WaitingForActivation);
                yield break;
            }

            Vector3 spawnPosition = GetRandomSpawnPosition();
            GameObject summonCircle = CreateSummoningCircle(spawnPosition);
            yield return new WaitForSeconds(summonDelay);

            if (summonCircle != null)
                Destroy(summonCircle);

            EnemyChase enemy = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);

            if (randomizeTypeWhenUsingFallback && fallbackEnemyPrefab != null && prefabToSpawn == fallbackEnemyPrefab)
            {
                enemy.enemyType = (EnemyChase.EnemyType)Random.Range(0, 3);
                enemy.applyTypeDefaultsOnStart = true;
            }

            if (spawnSpacingWithinWave > 0f)
                yield return new WaitForSeconds(spawnSpacingWithinWave);
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
            " | liveEnemies=" + FindObjectsByType<EnemyChase>(FindObjectsSortMode.None).Length);
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
        GUILayout.Label("Live Enemies: " + FindObjectsByType<EnemyChase>(FindObjectsSortMode.None).Length);
        GUILayout.EndArea();
    }

    EnemyChase PickEnemyPrefab()
    {
        if (enemyPrefabs != null && enemyPrefabs.Length > 0)
        {
            int index = Random.Range(0, enemyPrefabs.Length);
            return enemyPrefabs[index];
        }

        return fallbackEnemyPrefab;
    }

    Vector3 GetRandomSpawnPosition()
    {
        Vector3 center = areaCenter != null ? areaCenter.position : transform.position;
        float halfX = areaSize.x * 0.5f;
        float halfY = areaSize.y * 0.5f;

        float x = Random.Range(center.x - halfX, center.x + halfX);
        float y = Random.Range(center.y - halfY, center.y + halfY);

        return new Vector3(x, y, 0f);
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
