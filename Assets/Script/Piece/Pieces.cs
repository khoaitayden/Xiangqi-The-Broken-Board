using UnityEngine;

public abstract class Piece : MonoBehaviour
{
    public int currentX;
    public int currentY;
    public bool isPlayer;
    public BoardNode currentNode;

    [Header("Stats")]
    public int maxHp = 1; 
    public int currentHp;

    [Header("Cooldown Settings")]
    public int maxCooldown;
    public int currentCooldown;
    public Vector3 targetPosition;


    protected SpriteRenderer spriteRenderer;
    protected Color originalColor;

    protected virtual void Awake()
    {
        currentHp = maxHp;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) originalColor = spriteRenderer.color;
    }
    public virtual void MoveTo(BoardNode targetNode)
    {
        if (currentNode != null) currentNode.currentPiece = null; // Clear old node
        
        // Capture logic for enemies walking onto the player
        if (targetNode.currentPiece != null && targetNode.currentPiece != this)
        {
            Destroy(targetNode.currentPiece.gameObject); // Instant kill (Player capture)
        }

        currentX = targetNode.x;
        currentY = targetNode.y;
        currentNode = targetNode; // Update current node
        targetNode.currentPiece = this;
        
        targetPosition = targetNode.nodeGameObject.transform.position; 
    }

    public virtual void TakeDamage(int damage)
    {
        currentHp -= damage;
        if (currentHp <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        if (currentNode != null) currentNode.currentPiece = null;
        Destroy(gameObject);
    }

    public void SetTargeted(bool isTargeted)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = isTargeted ? Color.yellow : originalColor;
        }
    }

    public virtual void Capture()
    {
        Destroy(gameObject);
    }

    public abstract bool IsValidMove(BoardNode targetNode, BoardNode[,] grid);

    public virtual BoardNode GetAIMove(BoardNode[,] grid)
    {
        return null;
    }

    // NEW: The Jiggle Animation Logic!
    protected virtual void Update()
    {
        // Only enemies jiggle, and only if their cooldown is 1 (next turn) or 0 (ready but was blocked)
        if (!isPlayer && currentCooldown <= 1)
        {
            // Create a fast left-to-right sine wave
            float offsetX = Mathf.Sin(Time.time * 10f) * 0.03f;
            transform.position = targetPosition + new Vector3(offsetX, 0, 0);
        }
        else
        {
            // Lock safely to the exact node position
            if (targetPosition != Vector3.zero)
            {
                transform.position = targetPosition;
            }
        }
    }
}