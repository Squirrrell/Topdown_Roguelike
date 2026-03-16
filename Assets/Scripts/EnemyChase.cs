using UnityEngine;

public class EnemyChase : MonoBehaviour
{
    public float speed = 3f;
    public float stopDistance = 0.35f;
    public int contactDamage = 1;
    public float damageInterval = 0.6f;

    private Rigidbody2D rb;
    private Transform player;
    private PlayerHealth playerHealth;
    private Collider2D[] enemyColliders;
    private float nextDamageTime = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        enemyColliders = GetComponents<Collider2D>();
    }

    void Start()
    {
        TryFindPlayer();

        // Kinematic movement avoids unstable physics impulses while chasing.
        if (rb != null)
            rb.bodyType = RigidbodyType2D.Kinematic;

        IgnorePlayerCollisions();
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
}