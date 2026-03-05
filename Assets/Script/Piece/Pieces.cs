using UnityEngine;

public abstract class Piece : MonoBehaviour
{
    public int currentX;
    public int currentY;
    public bool isPlayer;

    [Header("Cooldown Settings")]
    public int maxCooldown;
    public int currentCooldown;

    // We use this to remember where the piece should be, so we can jiggle it without losing its real position
    public Vector3 targetPosition;

    public virtual void MoveTo(BoardNode targetNode)
    {
        if (targetNode.currentPiece != null && targetNode.currentPiece != this)
        {
            targetNode.currentPiece.Capture();
        }

        currentX = targetNode.x;
        currentY = targetNode.y;
        
        // Update the visual target position
        targetPosition = targetNode.nodeGameObject.transform.position; 
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