using UnityEngine;
using System.Collections.Generic;

public class EnemyElephant : Piece
{
    protected override void Awake()
    {
        base.Awake(); 
    }

    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        if (targetNode.y < 5) return false; 

        int dirX = targetNode.x - X;
        int dirY = targetNode.y - Y;

        // Exactly 2 steps diagonally
        if (Mathf.Abs(dirX) == 2 && Mathf.Abs(dirY) == 2)
        {
            int eyeX = X + (dirX / 2);
            int eyeY = Y + (dirY / 2);

            if (!grid[eyeX, eyeY].IsEmpty()) return false;

            if (targetNode.IsEmpty() || (targetNode.currentPiece != null && targetNode.currentPiece.IsPlayer)) return true;
        }
        return false;
    }

    public override BoardNode GetAIMove(BoardNode[,] grid)
    {
        List<BoardNode> validMoves = new List<BoardNode>();
        int[] dx = { 2, 2, -2, -2 };
        int[] dy = { 2, -2, 2, -2 };

        for (int i = 0; i < 4; i++)
        {
            int tX = X + dx[i];
            int tY = Y + dy[i];

            if (tX >= 0 && tX <= 8 && tY >= 5 && tY <= 9)
            {
                BoardNode testNode = grid[tX, tY];
                if (IsValidMove(testNode, grid))
                {
                    // FIX: Safe check! We only care if it's the player here.
                    if (testNode.currentPiece != null && testNode.currentPiece.IsPlayer) return testNode;
                    validMoves.Add(testNode);
                }
            }
        }
        return validMoves.Count > 0 ? validMoves[Random.Range(0, validMoves.Count)] : null;
    }
}