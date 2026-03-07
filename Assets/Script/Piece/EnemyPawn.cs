using UnityEngine;

public class EnemyPawn : Piece
{
    protected override void Awake()
    {
        base.Awake(); 
        isPlayer = false; 
        maxCooldown = 3; 
    }
    // REMOVED Start()

    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        int dirX = targetNode.x - currentX;
        int dirY = targetNode.y - currentY;

        if (dirY > 0) return false;
        if (Mathf.Abs(dirX) > 0 && Mathf.Abs(dirY) > 0) return false;

        if (dirX == 0 && dirY == -1) return true;

        bool hasCrossedRiver = currentY <= 4;
        if (hasCrossedRiver && dirY == 0 && Mathf.Abs(dirX) == 1) return true;

        return false;
    }

    public override BoardNode GetAIMove(BoardNode[,] grid)
    {
        if (currentY - 1 >= 0)
        {
            BoardNode forwardNode = grid[currentX, currentY - 1];
            // SAFE CHECK: Empty OR (Not Null AND Player)
            if (forwardNode.IsEmpty() || (forwardNode.currentPiece != null && forwardNode.currentPiece.isPlayer))
                return forwardNode;
        }

        if (currentY <= 4)
        {
            if (currentX - 1 >= 0)
            {
                BoardNode leftNode = grid[currentX - 1, currentY];
                if (leftNode.IsEmpty() || (leftNode.currentPiece != null && leftNode.currentPiece.isPlayer)) return leftNode;
            }
            if (currentX + 1 <= 8)
            {
                BoardNode rightNode = grid[currentX + 1, currentY];
                if (rightNode.IsEmpty() || (rightNode.currentPiece != null && rightNode.currentPiece.isPlayer)) return rightNode;
            }
        }
        return null; 
    }
}