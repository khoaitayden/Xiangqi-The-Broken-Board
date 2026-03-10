using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 15f;
    public Vector3 startPosition;
    public float rangeX;
    public float rangeY;
    public int damage = 1;
    private Vector3 startPos;
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

                // PIERCING DRAGON LOGIC
                float distance = Vector3.Distance(startPos, transform.position);
                bool hasPiercing = RunManager.Instance != null && RunManager.Instance.PiercingDragonEnabled;
                
                if (hasPiercing && distance <= 2.0f) // If fired point-blank
                {
                    // Raycast slightly forward to hit the guy behind him
                    RaycastHit2D hit = Physics2D.Raycast(transform.position + transform.right * 0.5f, transform.right, 2.0f);
                    if (hit.collider != null)
                    {
                        Piece behindPiece = hit.collider.GetComponent<Piece>();
                        if (behindPiece != null && !behindPiece.IsPlayer) behindPiece.TakeDamage(1);
                    }
                }

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