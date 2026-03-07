using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 15f;
    public Vector3 startPosition;
    public float rangeX;
    public float rangeY;
    public int damage = 1;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        // Move forward
        transform.position += transform.right * speed * Time.deltaTime;

        float distanceTraveled = Vector3.Distance(startPosition, transform.position);

        // Falloff Zone Check (Between Range X and Range Y)
        if (distanceTraveled > rangeX && distanceTraveled < rangeY)
        {
            // Roll a probability check every frame to see if the bullet dissipates
            if (Random.value < 0.05f) // 5% chance to vanish per frame in falloff
            {
                Destroy(gameObject);
                return;
            }
        }
        // Max Range Check
        else if (distanceTraveled >= rangeY)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 1. Check if we hit an Alive Piece
        Piece hitPiece = collision.GetComponent<Piece>();
        if (hitPiece != null)
        {
            if (!hitPiece.isPlayer) // Don't shoot yourself
            {
                hitPiece.TakeDamage(damage);
                Destroy(gameObject); // Bullet destroyed on impact
            }
            return;
        }

        // 2. Check if we hit a Corpse (Cover)
        Corpse hitCorpse = collision.GetComponent<Corpse>();
        if (hitCorpse != null)
        {
            // Bullet hits the dead body and stops
            Destroy(gameObject); 
        }
    }
}