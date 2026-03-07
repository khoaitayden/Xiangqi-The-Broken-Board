using UnityEngine;

public class EnemyPawn : Piece
{
    protected override void Awake()
    {
        base.Awake(); 
        MaxCooldown = 3;    
    }

    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        int dirX = targetNode.x - X; 
        int dirY = targetNode.y - Y;

        if (dirY > 0) return false;
        if (Mathf.Abs(dirX) > 0 && Mathf.Abs(dirY) > 0) return false;
        if (dirX == 0 && dirY == -1) return true;

        bool hasCrossedRiver = Y <= 4; // Capital Y
        if (hasCrossedRiver && dirY == 0 && Mathf.Abs(dirX) == 1) return true;

        return false;
    }

    public override BoardNode GetAIMove(BoardNode[,] grid)
    {
        if (Y - 1 >= 0)
        {
            BoardNode forwardNode = grid[X, Y - 1];
            if (forwardNode.IsEmpty() || (forwardNode.currentPiece != null && forwardNode.currentPiece.IsPlayer))
                return forwardNode;
        }

        if (Y <= 4)
        {
            if (X - 1 >= 0)
            {
                BoardNode leftNode = grid[X - 1, Y];
                if (leftNode.IsEmpty() || (leftNode.currentPiece != null && leftNode.currentPiece.IsPlayer)) return leftNode;
            }
            if (X + 1 <= 8)
            {
                BoardNode rightNode = grid[X + 1, Y];
                if (rightNode.IsEmpty() || (rightNode.currentPiece != null && rightNode.currentPiece.IsPlayer)) return rightNode;
            }
        }
        return null; 
    }
}