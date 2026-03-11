using UnityEngine;
using System.Collections.Generic;

public class EnemyHorse : Piece
{
    protected override void Awake()
    {
        base.Awake(); 
    }

    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        int dirX = targetNode.x - X;
        int dirY = targetNode.y - Y;
        
        int absX = Mathf.Abs(dirX);
        int absY = Mathf.Abs(dirY);

        // Check if it's an "L" shape (2 steps one way, 1 step the other)
        if ((absX == 1 && absY == 2) || (absX == 2 && absY == 1))
        {
            // --- THE HOBBLING RULE ---
            int blockX = X;
            int blockY = Y;

            if (absX == 2) blockX += (int)Mathf.Sign(dirX);
            else blockY += (int)Mathf.Sign(dirY);

            // Check if blocked by piece or corpse
            if (grid[blockX, blockY].currentPiece != null || grid[blockX, blockY].currentCorpse != null)
            {
                return false;
            }

            if (targetNode.IsEmpty() || (targetNode.currentPiece != null && targetNode.currentPiece.IsPlayer))
            {
                return true;
            }
        }
        
        return false;
    }

    // --- AI FOR THE HORSE ---
    public override BoardNode GetAIMove(BoardNode[,] grid)
    {
        List<BoardNode> validMoves = new List<BoardNode>();
        
        // The 8 possible L-shape coordinates
        int[] dx = { 1, 1, -1, -1, 2, 2, -2, -2 };
        int[] dy = { 2, -2, 2, -2, 1, -1, 1, -1 };

        for (int i = 0; i < 8; i++)
        {
            int targetX = X + dx[i];
            int targetY = Y + dy[i];

            if (targetX >= 0 && targetX <= 8 && targetY >= 0 && targetY <= 9)
            {
                BoardNode testNode = grid[targetX, targetY];
                
                if (IsValidMove(testNode, grid))
                {
                    // Just gather all valid moves. The Brain will figure out if it's a kill!
                    validMoves.Add(testNode);
                }
            }
        }

        // --- LET THE NEW AI BRAIN DECIDE ---
        return EvaluateAndPickBestMove(validMoves, grid);
    }
}