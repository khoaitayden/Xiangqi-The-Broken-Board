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
        transform.position += transform.right * speed * Time.deltaTime;

        float distanceTraveled = Vector3.Distance(startPosition, transform.position);

        if (distanceTraveled > rangeX && distanceTraveled < rangeY)
        {
            if (Random.value < 0.05f) 
            {
                Destroy(gameObject);
                return;
            }
        }
        else if (distanceTraveled >= rangeY)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Piece hitPiece = collision.GetComponent<Piece>();
        if (hitPiece != null)
        {
            if (!hitPiece.IsPlayer) 
            {
                hitPiece.TakeDamage(damage);
                Destroy(gameObject); 
            }
            return;
        }

        Corpse hitCorpse = collision.GetComponent<Corpse>();
        if (hitCorpse != null)
        {
            Destroy(gameObject); 
        }
    }
}