using UnityEngine;

public class PlayerBullet : MonoBehaviour
{
    private Vector2 direction = Vector2.right;
    private float speed = 10f;
    private int damage = 1;
    private float lifetime = 3f;
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

        EnemyChase enemy = other.GetComponent<EnemyChase>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger)
            Destroy(gameObject);
    }
}
