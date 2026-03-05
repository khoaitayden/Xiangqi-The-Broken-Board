using UnityEngine;

public abstract class Piece : MonoBehaviour
{
    public int currentX;
    public int currentY;
    public bool isPlayer;

    public virtual void MoveTo(BoardNode targetNode)
    {
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
        Destroy(gameObject);
    }

    public abstract bool IsValidMove(BoardNode targetNode, BoardNode[,] grid);

    // NEW: Allow BoardManager to ask ANY piece for its AI move
    public virtual BoardNode GetAIMove(BoardNode[,] grid)
    {
        return null;
    }
}