using UnityEngine;
using System;

public class PlayerBullet : MonoBehaviour
{
    private Vector2 direction = Vector2.right;
    private float speed = 10f;
    private int damage = 1;
    private float lifetime = 3f;
    private GameObject owner;
    private float despawnTime = 0f;
    private bool isActiveBullet = false;
    private Action<PlayerBullet> releaseToPool;

    public void Initialize(Vector2 moveDirection, float moveSpeed, int bulletDamage, float bulletLifetime, GameObject bulletOwner, Action<PlayerBullet> releaseCallback)
    {
        direction = moveDirection.normalized;
        speed = moveSpeed;
        damage = bulletDamage;
        lifetime = bulletLifetime;
        owner = bulletOwner;
        releaseToPool = releaseCallback;
        despawnTime = Time.time + lifetime;
        isActiveBullet = true;
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
            enemy.TakeDamage(damage);
            Release();
            return;
        }

        if (!other.isTrigger)
            Release();
    }

    void Release()
    {
        if (!isActiveBullet)
            return;

        isActiveBullet = false;
        owner = null;

        if (releaseToPool != null)
            releaseToPool(this);
        else
            Destroy(gameObject);
    }
}
