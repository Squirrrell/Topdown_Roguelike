using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 5;
    private int currentHealth;

    public float invincibilityTime = 0.5f;
    private bool isDamageInvincible = false;
    private bool isExternalInvincible = false;

    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private Collider2D[] hitColliders;
    private PlayerMovement movementController;

    public bool respawnOnDeath = true;
    public float respawnDelay = 1.25f;

    private bool isDead = false;

    [Header("HUD")]
    public Vector2 hudOffset = new Vector2(20f, -20f);
    public int hudFontSize = 24;
    public Color hudTextColor = Color.white;

    [Header("Progression")]
    public int level = 1;
    public int xpToNextLevel = 5;
    public float levelXpGrowthFactor = 1.5f;

    private int currentXp = 0;

    private Text healthText;

    void Start()
    {
        currentHealth = maxHealth;
        level = Mathf.Max(1, level);
        xpToNextLevel = Mathf.Max(1, xpToNextLevel);

        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        hitColliders = GetComponents<Collider2D>();
        movementController = GetComponent<PlayerMovement>();

        CreateOrFindHudText();

        UpdateHealthUI();
    }

    public void TakeDamage(int damage)
    {
        if (IsInvincible() || isDead) return;

        currentHealth -= damage;

        if (currentHealth < 0)
            currentHealth = 0;

        UpdateHealthUI();

        StartCoroutine(DamageFlash());
        StartCoroutine(Invincibility());

        if (currentHealth == 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        currentHealth += amount;

        if (currentHealth > maxHealth)
            currentHealth = maxHealth;

        UpdateHealthUI();
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0)
            return;

        currentXp += amount;

        while (currentXp >= xpToNextLevel)
        {
            currentXp -= xpToNextLevel;
            level++;
            int nextRequirement = Mathf.CeilToInt(xpToNextLevel * Mathf.Max(1.01f, levelXpGrowthFactor));
            xpToNextLevel = Mathf.Max(xpToNextLevel + 1, nextRequirement);
        }

        UpdateHealthUI();
    }

    void UpdateHealthUI()
    {
        if (healthText != null)
            healthText.text =
                currentHealth + " / " + maxHealth + " HP" +
                "\nXP " + currentXp + " / " + xpToNextLevel + "  LVL " + level;
    }

    IEnumerator Invincibility()
    {
        isDamageInvincible = true;
        yield return new WaitForSeconds(invincibilityTime);
        isDamageInvincible = false;
    }

    public void SetExternalInvincibility(bool value)
    {
        isExternalInvincible = value;
    }

    bool IsInvincible()
    {
        return isDamageInvincible || isExternalInvincible;
    }

    IEnumerator DamageFlash()
    {
        if (sr == null)
            yield break;

        sr.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        sr.color = Color.white;
    }

    void CreateOrFindHudText()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        GameObject existingTextObject = GameObject.Find("Player Health Text");
        if (existingTextObject != null)
            healthText = existingTextObject.GetComponent<Text>();

        if (healthText != null)
            return;

        GameObject textObject = new GameObject("Player Health Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(canvas.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = hudOffset;
        textRect.sizeDelta = new Vector2(320f, 80f);

        Text label = textObject.GetComponent<Text>();
        label.alignment = TextAnchor.UpperLeft;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = hudFontSize;
        label.color = hudTextColor;
        label.raycastTarget = false;

        healthText = label;
    }

    void Die()
    {
        if (isDead)
            return;

        isDead = true;
        Debug.Log("Player died");

        if (respawnOnDeath)
        {
            StartCoroutine(RespawnRoutine());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    IEnumerator RespawnRoutine()
    {
        if (movementController != null)
            movementController.enabled = false;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (sr != null)
            sr.enabled = false;

        if (hitColliders != null)
        {
            for (int i = 0; i < hitColliders.Length; i++)
                hitColliders[i].enabled = false;
        }

        yield return new WaitForSeconds(respawnDelay);

        currentHealth = maxHealth;
        isDamageInvincible = false;
        isExternalInvincible = false;

        if (movementController != null)
            movementController.SnapToScreenCenter();

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (sr != null)
            sr.enabled = true;

        if (hitColliders != null)
        {
            for (int i = 0; i < hitColliders.Length; i++)
                hitColliders[i].enabled = true;
        }

        if (movementController != null)
            movementController.enabled = true;

        isDead = false;
        UpdateHealthUI();
    }
}