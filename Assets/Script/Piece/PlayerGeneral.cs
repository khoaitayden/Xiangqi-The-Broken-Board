using UnityEngine;

public class PlayerGeneral : Piece
{
    void Start()
    {
        isPlayer = true;
    }

    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        // 1. Calculate distance in grid coordinates
        int distanceX = Mathf.Abs(targetNode.x - currentX);
        int distanceY = Mathf.Abs(targetNode.y - currentY);

        // 2. The player can move exactly 1 intersection in any 8 directions.
        // This means the distance in X and Y can be 0 or 1, but they can't BOTH be 0 (that's the current spot).
        if (distanceX <= 1 && distanceY <= 1 && !(distanceX == 0 && distanceY == 0))
        {
            // 3. Check if the target node is empty (we will add corpse checks later)
            if (targetNode.currentPiece == null)
            {
                return true;
            }
        }

        return false;
    }
}