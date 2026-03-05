using UnityEngine;

public abstract class Piece : MonoBehaviour
{
    public int currentX;
    public int currentY;
    public bool isPlayer;

    public virtual void MoveTo(BoardNode targetNode)
    {
        // If there is a piece on the target node, and it's an enemy (Player being captured)
        if (targetNode.currentPiece != null && targetNode.currentPiece != this)
        {
            targetNode.currentPiece.Capture();
        }

        currentX = targetNode.x;
        currentY = targetNode.y;
        
        transform.position = targetNode.nodeGameObject.transform.position; 
    }

    public virtual void Capture()
    {
        // For now, simply destroy the GameObject
        Destroy(gameObject);
    }

    public abstract bool IsValidMove(BoardNode targetNode, BoardNode[,] grid);
}