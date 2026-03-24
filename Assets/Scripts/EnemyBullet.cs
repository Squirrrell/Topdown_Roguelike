using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    private Vector2 direction = Vector2.right;
    private float speed = 4f;
    private int damage = 1;
    private float lifetime = 4f;
    private GameObject owner;

    public void Initialize(Vector2 moveDirection, float moveSpeed, int bulletDamage, float bulletLifetime, GameObject bulletOwner)
    {
        direction = moveDirection.normalized;
        speed = moveSpeed;
        damage = bulletDamage;
        lifetime = bulletLifetime;
        owner = bulletOwner;

        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.position += (Vector3)(direction * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null && other.gameObject == owner)
            return;

        if (other.CompareTag("Player"))
        {
            PlayerHealth health = other.GetComponent<PlayerHealth>();
            if (health != null)
                health.TakeDamage(damage);

            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger)
            Destroy(gameObject);
    }
}
