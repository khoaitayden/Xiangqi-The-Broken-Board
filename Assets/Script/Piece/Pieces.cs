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

    [Header("Visuals")]
    public Sprite deadSprite; // NEW: Drag the dead version here in Inspector!

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
        if (currentNode != null) currentNode.currentPiece = null; 
        
        if (targetNode.currentPiece != null && targetNode.currentPiece != this)
        {
            Destroy(targetNode.currentPiece.gameObject); 
        }

        currentX = targetNode.x;
        currentY = targetNode.y;
        currentNode = targetNode; 
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
        // 1. Clear the "Alive Piece" data from the board
        if (currentNode != null)
        {
            currentNode.currentPiece = null; 
        }

        // 2. Remove from Enemy List (so it stops moving/thinking)
        if (!isPlayer)
        {
            TurnManager.Instance.enemyPieces.Remove(this);
        }

        // 3. TRANSFORM INTO CORPSE
        // Change Sprite
        if (spriteRenderer != null && deadSprite != null)
        {
            spriteRenderer.sprite = deadSprite;
            // Tint it Grey and slightly transparent
            spriteRenderer.color = new Color(0.8f, 0.8f, 0.8f, 0.95f); 
        }

        // Add Corpse Component
        Corpse corpse = gameObject.AddComponent<Corpse>();
        corpse.Init(currentNode);

        // Register Corpse to Board and Manager
        if (currentNode != null) currentNode.currentCorpse = corpse;
        TurnManager.Instance.activeCorpses.Add(corpse);

        // 4. Destroy this Script (Piece/EnemyPawn), but keep the GameObject & Collider!
        Destroy(this); 
    }

    // ... Keep visual feedback and movement logic ...
    public void SetTargeted(bool isTargeted)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = isTargeted ? Color.yellow : originalColor;
        }
    }

    public abstract bool IsValidMove(BoardNode targetNode, BoardNode[,] grid);
    public virtual BoardNode GetAIMove(BoardNode[,] grid) { return null; }
    
    protected virtual void Update()
    {
        if (!isPlayer && currentCooldown <= 1)
        {
            float offsetY = Mathf.Sin(Time.time * 5f) * 0.05f;
            transform.position = targetPosition + new Vector3(0, offsetY, 0);
        }
        else if (targetPosition != Vector3.zero)
        {
            transform.position = targetPosition;
        }
    }
}