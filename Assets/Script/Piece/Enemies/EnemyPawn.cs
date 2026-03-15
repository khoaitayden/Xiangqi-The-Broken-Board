using System.Collections.Generic;
using UnityEngine;

public class EnemyPawn : Piece
{
    protected override void Awake()
    {
        base.Awake();    
    }

    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        int dirX = targetNode.x - X;
        int dirY = targetNode.y - Y;

        if (dirY > 0) return false;

        if (dirX == 0 && dirY == -1)
        {
            if (targetNode.IsEmpty() || (targetNode.currentPiece != null && targetNode.currentPiece.IsPlayer)) return true;
        }

        bool hasCrossedRiver = Y <= 4;
        if (hasCrossedRiver && dirY == 0 && Mathf.Abs(dirX) == 1)
        {
            if (targetNode.IsEmpty() || (targetNode.currentPiece != null && targetNode.currentPiece.IsPlayer)) return true;
        }

        bool bloodthirsty = RunManager.Instance != null && RunManager.Instance.PawnsAttackDiagonal;
        if (bloodthirsty && dirY == -1 && Mathf.Abs(dirX) == 1)
        {
            if (targetNode.IsEmpty() || (targetNode.currentPiece != null && targetNode.currentPiece.IsPlayer)) return true;
        }

        return false;
    }

    // --- THE FIX: A SMARTER GetAIMove THAT PRIORITIZES KILLS ---
    public override BoardNode GetAIMove(BoardNode[,] grid)
    {
        List<BoardNode> validMoves = new List<BoardNode>();
        PlayerGeneral player = TurnManager.Instance.activePlayer;
        if (player == null) return null;
        
        BoardNode playerNode = grid[player.X, player.Y];

        // 1. CHECK ALL POSSIBLE MOVES (Forward, Sideways, Diagonal)
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 0; dy++) // Only forward/sideways
            {
                if (dx == 0 && dy == 0) continue;

                int targetX = X + dx;
                int targetY = Y + dy;

                if (targetX >= 0 && targetX < GridManager.Instance.width && targetY >= 0 && targetY < GridManager.Instance.height)
                {
                    BoardNode testNode = grid[targetX, targetY];
                    if (IsValidMove(testNode, grid))
                    {
                        // INSTANT WIN: If this move kills the player, DO IT IMMEDIATELY!
                        if (testNode == playerNode)
                        {
                            return testNode;
                        }
                        
                        // Otherwise, add it to the list for the AI to score
                        validMoves.Add(testNode);
                    }
                }
            }
        }
        
        // 2. If no kill shot was found, let the AI Brain pick the best non-lethal move.
        return EvaluateAndPickBestMove(validMoves, grid);
    }
}