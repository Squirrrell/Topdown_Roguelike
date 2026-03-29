using UnityEngine;
using System;
using System.Collections.Generic;

public class PlayerBullet : MonoBehaviour
{
    private Vector2 direction = Vector2.right;
    private float speed = 10f;
    private int damage = 1;
    private float lifetime = 3f;
    private int remainingPierce = 0;
    private int remainingRicochet = 0;
    private GameObject owner;
    private float despawnTime = 0f;
    private bool isActiveBullet = false;
    private Action<PlayerBullet> releaseToPool;
    private readonly HashSet<int> hitEnemyIds = new HashSet<int>();

    public void Initialize(Vector2 moveDirection, float moveSpeed, int bulletDamage, float bulletLifetime, int bulletPierce, int bulletRicochet, GameObject bulletOwner, Action<PlayerBullet> releaseCallback)
    {
        direction = moveDirection.normalized;
        speed = moveSpeed;
        damage = bulletDamage;
        lifetime = bulletLifetime;
        remainingPierce = Mathf.Max(0, bulletPierce);
        remainingRicochet = Mathf.Max(0, bulletRicochet);
        owner = bulletOwner;
        releaseToPool = releaseCallback;
        despawnTime = Time.time + lifetime;
        isActiveBullet = true;
        hitEnemyIds.Clear();
    }

    void Update()
    {
        if (!isActiveBullet)
            return;

        transform.position += (Vector3)(direction * speed * Time.deltaTime);

        if (Time.time >= despawnTime)
            Release();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null && other.gameObject == owner)
            return;

        EnemyChase enemy = other.GetComponent<EnemyChase>();
        if (enemy != null)
        {
            int enemyId = enemy.gameObject.GetInstanceID();
            if (hitEnemyIds.Contains(enemyId))
                return;

            enemy.TakeDamage(damage);
            hitEnemyIds.Add(enemyId);

            if (TryRicochetFromCollider(other))
                return;

            if (remainingPierce > 0)
            {
                remainingPierce--;
                return;
            }

            Release();
            return;
        }

        if (!other.isTrigger)
        {
            if (TryRicochetFromCollider(other))
                return;

            Release();
        }
    }

    bool TryRicochetFromCollider(Collider2D other)
    {
        if (remainingRicochet <= 0)
            return false;

        Vector2 closest = other.ClosestPoint(transform.position);
        Vector2 normal = ((Vector2)transform.position - closest).normalized;
        if (normal.sqrMagnitude < 0.0001f)
            normal = -direction;

        direction = Vector2.Reflect(direction, normal).normalized;
        remainingRicochet--;

        // Move slightly along the new direction to avoid immediately re-triggering the same collider.
        transform.position += (Vector3)(direction * 0.05f);
        return true;
    }

    void Release()
    {
        if (!isActiveBullet)
            return;

        isActiveBullet = false;
        owner = null;
        hitEnemyIds.Clear();

        if (releaseToPool != null)
            releaseToPool(this);
        else
            Destroy(gameObject);
    }
}
