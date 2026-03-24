using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    public enum HudAnchorPreset
    {
        TopLeft,
        LeftCenter
    }

    public int maxHealth = 5;
    private int currentHealth;

    public float invincibilityTime = 0.5f;
    private bool isDamageInvincible = false;
    private bool isExternalInvincible = false;

    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private Collider2D[] hitColliders;
    private PlayerMovement movementController;

    public Slider healthSlider; // UI health bar
    public bool respawnOnDeath = true;
    public float respawnDelay = 1.25f;

    private bool isDead = false;

    [Header("HUD")]
    public HudAnchorPreset hudAnchor = HudAnchorPreset.TopLeft;
    public Vector2 hudOffset = new Vector2(30f, -34f);
    public Vector2 hudSize = new Vector2(260f, 24f);
    public Vector2 hudLeftSideOffset = new Vector2(24f, 0f);
    public Color hudFillColor = Color.white;
    public Color hudBackgroundColor = new Color(0f, 0f, 0f, 0.7f);

    private Text healthText;

    void Start()
    {
        currentHealth = maxHealth;

        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        hitColliders = GetComponents<Collider2D>();
        movementController = GetComponent<PlayerMovement>();

        if (healthSlider == null)
            healthSlider = FindFirstObjectByType<Slider>();

        if (healthSlider == null)
            CreateRuntimeHealthBar();

        if (healthSlider != null)
        {
            healthSlider.minValue = 0;
            healthSlider.maxValue = maxHealth;
            healthSlider.wholeNumbers = true;
            ConfigureHud(healthSlider);
        }

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

    void UpdateHealthUI()
    {
        if (healthSlider != null)
            healthSlider.value = currentHealth;

        if (healthText != null)
            healthText.text = "HP " + currentHealth + " / " + maxHealth;
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

    void CreateRuntimeHealthBar()
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

        GameObject sliderObject = new GameObject("Player Health Bar", typeof(RectTransform), typeof(Image), typeof(Slider));
        sliderObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = sliderObject.GetComponent<RectTransform>();
        ApplyHudPlacement(rect);

        Image backgroundImage = sliderObject.GetComponent<Image>();
        backgroundImage.color = hudBackgroundColor;

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.targetGraphic = backgroundImage;

        GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(3f, 3f);
        fillAreaRect.offsetMax = new Vector2(-3f, -3f);

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(fillAreaObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.color = hudFillColor;

        slider.fillRect = fillRect;
        healthSlider = slider;

        GameObject textObject = new GameObject("Health Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(sliderObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text label = textObject.GetComponent<Text>();
        label.alignment = TextAnchor.MiddleCenter;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 16;
        label.color = Color.white;
        label.raycastTarget = false;

        healthText = label;
    }

    void ConfigureHud(Slider slider)
    {
        if (slider == null)
            return;

        RectTransform rect = slider.GetComponent<RectTransform>();
        if (rect != null)
            ApplyHudPlacement(rect);

        Image backgroundImage = slider.GetComponent<Image>();
        if (backgroundImage != null)
            backgroundImage.color = hudBackgroundColor;

        if (slider.fillRect != null)
        {
            Image fillImage = slider.fillRect.GetComponent<Image>();
            if (fillImage != null)
                fillImage.color = hudFillColor;
        }

        EnsureHealthText(slider);
    }

    void EnsureHealthText(Slider slider)
    {
        if (healthText != null || slider == null)
            return;

        Text existingText = slider.GetComponentInChildren<Text>();
        if (existingText != null)
        {
            healthText = existingText;
            healthText.color = Color.white;
            healthText.alignment = TextAnchor.MiddleCenter;
            healthText.raycastTarget = false;
            return;
        }

        GameObject textObject = new GameObject("Health Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(slider.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text label = textObject.GetComponent<Text>();
        label.alignment = TextAnchor.MiddleCenter;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 16;
        label.color = Color.white;
        label.raycastTarget = false;
        healthText = label;
    }

    void ApplyHudPlacement(RectTransform rect)
    {
        if (rect == null)
            return;

        if (hudAnchor == HudAnchorPreset.LeftCenter)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = hudLeftSideOffset;
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = hudOffset;
        }

        rect.sizeDelta = hudSize;
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