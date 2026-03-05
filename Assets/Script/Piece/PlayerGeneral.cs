using UnityEngine;

public class PlayerGeneral : Piece
{
    void Start()
    {
        isPlayer = true;
    }
    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        int distanceX = Mathf.Abs(targetNode.x - currentX);
        int distanceY = Mathf.Abs(targetNode.y - currentY);

        if (distanceX <= 1 && distanceY <= 1 && !(distanceX == 0 && distanceY == 0))
        {
            // The player can only walk onto empty spaces
            if (targetNode.currentPiece == null) 
            {
                return true;
            }
        }
        return false;
    }
}