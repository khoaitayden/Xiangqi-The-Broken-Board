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

        // Pawns can never move backward
        if (dirY > 0) return false;

        // 1. Forward 1 step
        if (dirX == 0 && dirY == -1)
        {
            if (targetNode.IsEmpty() || (targetNode.currentPiece != null && targetNode.currentPiece.IsPlayer)) return true;
        }

        // 2. Sideways
        bool hasCrossedRiver = Y <= 4;
        if (hasCrossedRiver && dirY == 0 && Mathf.Abs(dirX) == 1)
        {
            if (targetNode.IsEmpty() || (targetNode.currentPiece != null && targetNode.currentPiece.IsPlayer)) return true;
        }

        // --- BLOODTHIRSTY PAWNS MOVES ---
        bool bloodthirsty = RunManager.Instance != null && RunManager.Instance.PawnsAttackDiagonal;
        if (bloodthirsty && dirY == -1 && Mathf.Abs(dirX) == 1)
        {
            if (targetNode.IsEmpty() || (targetNode.currentPiece != null && targetNode.currentPiece.IsPlayer)) return true;
        }

        return false;
    }

    public override BoardNode GetAIMove(BoardNode[,] grid)
    {
        List<BoardNode> validMoves = new List<BoardNode>();

        // 1. Try forward
        if (Y - 1 >= 0)
        {
            BoardNode forwardNode = grid[X, Y - 1];
            if (forwardNode.IsEmpty() || (forwardNode.currentPiece != null && forwardNode.currentPiece.IsPlayer))
                validMoves.Add(forwardNode);
        }

        // 2. Try sideways
        if (Y <= 4)
        {
            if (X - 1 >= 0)
            {
                BoardNode leftNode = grid[X - 1, Y];
                if (leftNode.IsEmpty() || (leftNode.currentPiece != null && leftNode.currentPiece.IsPlayer)) validMoves.Add(leftNode);
            }
            if (X + 1 <= 8)
            {
                BoardNode rightNode = grid[X + 1, Y];
                if (rightNode.IsEmpty() || (rightNode.currentPiece != null && rightNode.currentPiece.IsPlayer)) validMoves.Add(rightNode);
            }
        }

        // 3. Let the new AI brain pick the best option!
        return EvaluateAndPickBestMove(validMoves, grid);
    }
}