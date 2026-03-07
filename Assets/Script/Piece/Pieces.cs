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
    public Sprite deadSprite;
    
    [Header("Movement Settings")]
    public float moveDuration = 0.25f; // How long the hop takes (in seconds)
    public float jumpHeight = 0.5f;    // How high the piece arcs visually
    private float moveTimer;
    private bool isMoving;
    private Vector3 startMovePosition;

    [Header("Cooldown Settings")]
    public int maxCooldown;
    public int currentCooldown;
    public Vector3 targetPosition; 

    protected SpriteRenderer spriteRenderer;
    protected Color originalColor;
    protected bool isDead = false; 

    protected virtual void Awake()
    {
        currentHp = maxHp;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) originalColor = spriteRenderer.color;
    }

    public virtual void MoveTo(BoardNode targetNode)
    {
        // 1. Clear old node logic
        if (currentNode != null) currentNode.currentPiece = null; 
        
        // 2. Kill whatever is on the target node
        if (targetNode.currentPiece != null && targetNode.currentPiece != this)
        {
            Piece targetPiece = targetNode.currentPiece.GetComponent<Piece>();
            if (targetPiece != null) targetPiece.TakeDamage(999); 
            else Destroy(targetNode.currentPiece.gameObject);
        }

        // 3. Update Logical Coordinates
        currentX = targetNode.x;
        currentY = targetNode.y;
        currentNode = targetNode; 
        targetNode.currentPiece = this;
        
        // 4. SETUP MOVEMENT ANIMATION
        targetPosition = targetNode.nodeGameObject.transform.position;
        startMovePosition = transform.position; // Remember where we started
        moveTimer = 0f;                         // Reset timer
        isMoving = true;                        // Start animation loop
    }

    public virtual void TakeDamage(int damage)
    {
        // 1. If we are already dead, ignore further damage!
        if (isDead) return;

        currentHp -= damage;
        
        if (currentHp <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;
        // 1. Clear the "Alive Piece" data from the board
        if (currentNode != null) currentNode.currentPiece = null; 
        
        // 2. Remove from Enemy List
        if (!isPlayer) TurnManager.Instance.enemyPieces.Remove(this);

        // 3. SPAWN THE CORPSE CLONE
        GameObject corpseObj = Instantiate(gameObject, transform.position, Quaternion.identity);
        corpseObj.name = gameObject.name + "_Corpse";

        // Remove the Piece script from the clone
        Destroy(corpseObj.GetComponent<Piece>());

        // 4. SETUP VISUALS (Start Solid)
        SpriteRenderer corpseSr = corpseObj.GetComponent<SpriteRenderer>();
        if (corpseSr != null)
        {
            if (deadSprite != null) corpseSr.sprite = deadSprite;
            
            // GREY but SOLID (Alpha = 1.0f)
            corpseSr.color = new Color(0.6f, 0.6f, 0.6f, 1.0f); 
        }

        // 5. ADD CORPSE LOGIC
        Corpse corpseScript = corpseObj.AddComponent<Corpse>();
        corpseScript.Init(currentNode);

        // 6. REGISTER CORPSE
        if (currentNode != null) currentNode.currentCorpse = corpseScript;
        TurnManager.Instance.activeCorpses.Add(corpseScript);

        // 7. HIDE ORIGINAL
        gameObject.SetActive(false);  
    }
    public void SetTargeted(bool isTargeted)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = isTargeted ? Color.yellow : originalColor;
        }
    }

    public abstract bool IsValidMove(BoardNode targetNode, BoardNode[,] grid);
    public virtual BoardNode GetAIMove(BoardNode[,] grid) { return null; }
    
    // --- UPDATED ANIMATION LOGIC ---
    protected virtual void Update()
    {
        // 1. HANDLE MOVEMENT (Bezier Curve)
        if (isMoving)
        {
            moveTimer += Time.deltaTime;
            
            // 't' is the percentage of the move completed (0 to 1)
            float t = moveTimer / moveDuration;

            if (t >= 1f)
            {
                // Animation Finished
                transform.position = targetPosition;
                isMoving = false;
            }
            else
            {
                // Quadratic Bezier Curve Calculation
                // P0 = Start, P2 = End, P1 = Control Point (Midpoint + Height)
                Vector3 p0 = startMovePosition;
                Vector3 p2 = targetPosition;
                
                // Calculate "Control Point" (The peak of the jump)
                Vector3 midPoint = (p0 + p2) * 0.5f;
                Vector3 p1 = midPoint + new Vector3(0, jumpHeight, 0);

                // The Magic Formula
                // (1-t)^2 * P0 + 2(1-t)t * P1 + t^2 * P2
                Vector3 position = 
                    (1 - t) * (1 - t) * p0 + 
                    2 * (1 - t) * t * p1 + 
                    t * t * p2;

                transform.position = position;
            }
        }
        // 2. HANDLE IDLE JIGGLE (Only when not moving)
        else
        {
            // Ensure we are snapped to the grid
            if (targetPosition != Vector3.zero) transform.position = targetPosition;

            // Jiggle if cooldown is ready
            if (!isPlayer && currentCooldown <= 1)
            {
                float offsetY = Mathf.Sin(Time.time * 10f) * 0.05f;
                transform.position = targetPosition + new Vector3(0, offsetY, 0);
            }
        }
    }
}