using UnityEngine;

public abstract class Piece : MonoBehaviour
{
    public int currentX;
    public int currentY;
    public bool isPlayer;

    // This method physically moves the piece to the new node
    public virtual void MoveTo(BoardNode targetNode)
    {
        currentX = targetNode.x;
        currentY = targetNode.y;
        
        // FIX: Snap exactly to the visual node's position, ensuring no offset
        transform.position = targetNode.nodeGameObject.transform.position; 
    }

    // Every piece will have different movement rules
    public abstract bool IsValidMove(BoardNode targetNode, BoardNode[,] grid);
}