using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.Serialization;

public class PlayerHealth : MonoBehaviour
{
    public enum UpgradeType
    {
        PlusOneHealth,
        PlusOneDamage,
        PlusOnePierce,
        PlusOneRicochet
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

    [Header("Level Up Rewards")]
    public bool pauseGameDuringUpgradeChoice = true;
    public int cardsPerLevelUp = 3;
    [FormerlySerializedAs("allowDuplicateCards")]
    public bool preventDuplicateCards = true;

    private int currentXp = 0;

    private Text healthText;
    private GameObject levelUpOverlay;
    private RectTransform cardsContainer;
    private readonly List<Button> upgradeButtons = new List<Button>(4);
    private bool isChoosingUpgrade = false;
    private int pendingUpgradeChoices = 0;
    private PlayerMovement movementControllerCached;

    public event Action<int> OnLevelUp;
    public event Action<UpgradeType> OnUpgradeChosen;

    void Start()
    {
        currentHealth = maxHealth;
        level = Mathf.Max(1, level);
        xpToNextLevel = Mathf.Max(1, xpToNextLevel);

        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        hitColliders = GetComponents<Collider2D>();
        movementController = GetComponent<PlayerMovement>();
        movementControllerCached = movementController;

        CreateOrFindHudText();
        CreateOrFindLevelUpOverlay();

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
        if (amount <= 0)
            return;

        currentHealth += amount;

        if (currentHealth > maxHealth)
            currentHealth = maxHealth;

        UpdateHealthUI();
    }

    public void IncreaseMaxHealth(int amount, bool healBySameAmount)
    {
        if (amount <= 0)
            return;

        maxHealth += amount;
        if (healBySameAmount)
            currentHealth += amount;

        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
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
            HandleLevelUp(level);
        }

        UpdateHealthUI();
    }

    void HandleLevelUp(int newLevel)
    {
        OnLevelUp?.Invoke(newLevel);
        pendingUpgradeChoices++;

        if (!isChoosingUpgrade)
            ShowUpgradeSelection();
    }

    void ShowUpgradeSelection()
    {
        if (pendingUpgradeChoices <= 0)
            return;

        if (levelUpOverlay == null)
            CreateOrFindLevelUpOverlay();

        if (levelUpOverlay == null)
            return;

        isChoosingUpgrade = true;
        if (pauseGameDuringUpgradeChoice)
            Time.timeScale = 0f;

        BuildUpgradeCards();
        levelUpOverlay.SetActive(true);
    }

    void BuildUpgradeCards()
    {
        if (cardsContainer == null)
            return;

        for (int i = 0; i < upgradeButtons.Count; i++)
        {
            if (upgradeButtons[i] != null)
                Destroy(upgradeButtons[i].gameObject);
        }

        upgradeButtons.Clear();

        List<UpgradeType> choices = GetRandomUpgradeChoices(Mathf.Max(1, cardsPerLevelUp));
        for (int i = 0; i < choices.Count; i++)
        {
            UpgradeType choice = choices[i];
            Button button = CreateUpgradeCardButton(choice);
            if (button != null)
                upgradeButtons.Add(button);
        }
    }

    List<UpgradeType> GetRandomUpgradeChoices(int count)
    {
        List<UpgradeType> all = new List<UpgradeType>
        {
            UpgradeType.PlusOneHealth,
            UpgradeType.PlusOneDamage,
            UpgradeType.PlusOnePierce,
            UpgradeType.PlusOneRicochet
        };

        int picks = Mathf.Clamp(count, 1, all.Count);
        List<UpgradeType> result = new List<UpgradeType>(picks);

        if (preventDuplicateCards)
        {
            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (all[i], all[j]) = (all[j], all[i]);
            }

            for (int i = 0; i < picks; i++)
                result.Add(all[i]);

            return result;
        }

        for (int i = 0; i < picks; i++)
        {
            int idx = UnityEngine.Random.Range(0, all.Count);
            result.Add(all[idx]);
        }

        return result;
    }

    Button CreateUpgradeCardButton(UpgradeType upgrade)
    {
        GameObject cardObject = new GameObject("Upgrade Card " + upgrade, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        cardObject.transform.SetParent(cardsContainer, false);

        Image background = cardObject.GetComponent<Image>();
        background.color = new Color(0.1f, 0.12f, 0.16f, 0.95f);

        LayoutElement layout = cardObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 320f;
        layout.preferredHeight = 210f;

        Button button = cardObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.95f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        button.colors = colors;
        button.onClick.AddListener(() => ChooseUpgrade(upgrade));

        GameObject textObject = new GameObject("Card Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(cardObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(16f, 16f);
        textRect.offsetMax = new Vector2(-16f, -16f);

        Text cardText = textObject.GetComponent<Text>();
        cardText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        cardText.alignment = TextAnchor.MiddleCenter;
        cardText.color = new Color(0.96f, 0.96f, 0.96f, 1f);
        cardText.fontSize = 26;
        cardText.supportRichText = true;
        cardText.text = "<b>" + GetUpgradeTitle(upgrade) + "</b>\n\n" + GetUpgradeDescription(upgrade);

        return button;
    }

    void ChooseUpgrade(UpgradeType chosenUpgrade)
    {
        ApplyUpgrade(chosenUpgrade);
        OnUpgradeChosen?.Invoke(chosenUpgrade);

        pendingUpgradeChoices = Mathf.Max(0, pendingUpgradeChoices - 1);
        if (pendingUpgradeChoices > 0)
        {
            BuildUpgradeCards();
            return;
        }

        isChoosingUpgrade = false;
        if (levelUpOverlay != null)
            levelUpOverlay.SetActive(false);

        if (pauseGameDuringUpgradeChoice)
            Time.timeScale = 1f;
    }

    string GetUpgradeTitle(UpgradeType upgrade)
    {
        switch (upgrade)
        {
            case UpgradeType.PlusOneHealth:
                return "+1 Health";
            case UpgradeType.PlusOneDamage:
                return "+1 Damage";
            case UpgradeType.PlusOnePierce:
                return "+1 Pierce";
            case UpgradeType.PlusOneRicochet:
                return "+1 Ricochet";
            default:
                return "Upgrade";
        }
    }

    string GetUpgradeDescription(UpgradeType upgrade)
    {
        switch (upgrade)
        {
            case UpgradeType.PlusOneHealth:
                return "Increase max HP by 1 and heal 1 HP.";
            case UpgradeType.PlusOneDamage:
                return "Player bullets deal 1 extra damage.";
            case UpgradeType.PlusOnePierce:
                return "Player bullets pass through 1 extra enemy.";
            case UpgradeType.PlusOneRicochet:
                return "Player bullets gain 1 extra bounce off walls or enemies.";
            default:
                return string.Empty;
        }
    }

    void ApplyUpgrade(UpgradeType upgrade)
    {
        if (movementControllerCached == null)
            movementControllerCached = GetComponent<PlayerMovement>();

        switch (upgrade)
        {
            case UpgradeType.PlusOneHealth:
                IncreaseMaxHealth(1, true);
                break;

            case UpgradeType.PlusOneDamage:
                if (movementControllerCached != null)
                    movementControllerCached.AddBulletDamage(1);
                break;

            case UpgradeType.PlusOnePierce:
                if (movementControllerCached != null)
                    movementControllerCached.AddBulletPierce(1);
                break;

            case UpgradeType.PlusOneRicochet:
                if (movementControllerCached != null)
                    movementControllerCached.AddBulletRicochet(1);
                break;
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

    void CreateOrFindLevelUpOverlay()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        EnsureEventSystemExists();

        GameObject existingOverlay = GameObject.Find("Level Up Overlay");
        if (existingOverlay != null)
        {
            levelUpOverlay = existingOverlay;
            Transform existingContainer = levelUpOverlay.transform.Find("Cards Container");
            if (existingContainer != null)
                cardsContainer = existingContainer as RectTransform;

            levelUpOverlay.SetActive(false);
            return;
        }

        levelUpOverlay = new GameObject("Level Up Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        levelUpOverlay.transform.SetParent(canvas.transform, false);

        RectTransform overlayRect = levelUpOverlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayBg = levelUpOverlay.GetComponent<Image>();
        overlayBg.color = new Color(0f, 0f, 0f, 0.72f);

        GameObject titleObject = new GameObject("Level Up Title", typeof(RectTransform), typeof(Text));
        titleObject.transform.SetParent(levelUpOverlay.transform, false);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -80f);
        titleRect.sizeDelta = new Vector2(700f, 80f);

        Text titleText = titleObject.GetComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 40;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.text = "LEVEL UP - CHOOSE A BUFF";

        GameObject cardsContainerObj = new GameObject("Cards Container", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        cardsContainerObj.transform.SetParent(levelUpOverlay.transform, false);
        cardsContainer = cardsContainerObj.GetComponent<RectTransform>();

        cardsContainer.anchorMin = new Vector2(0.5f, 0.5f);
        cardsContainer.anchorMax = new Vector2(0.5f, 0.5f);
        cardsContainer.pivot = new Vector2(0.5f, 0.5f);
        cardsContainer.sizeDelta = new Vector2(1080f, 260f);
        cardsContainer.anchoredPosition = new Vector2(0f, -30f);

        HorizontalLayoutGroup layout = cardsContainerObj.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 22f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        levelUpOverlay.SetActive(false);
    }

    void EnsureEventSystemExists()
    {
        EventSystem existing = FindFirstObjectByType<EventSystem>();
        if (existing != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    void OnDestroy()
    {
        if (pauseGameDuringUpgradeChoice && Time.timeScale == 0f)
            Time.timeScale = 1f;
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