using UnityEngine;
using System.Collections.Generic;

public class EnemyAdvisor : Piece
{
    void Start() { isPlayer = false; }

    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        if (!targetNode.isEnemyPalace) return false; // Palace Rule

        int absX = Mathf.Abs(targetNode.x - currentX);
        int absY = Mathf.Abs(targetNode.y - currentY);

        // Exactly 1 step diagonally
        if (absX == 1 && absY == 1)
        {
            if (targetNode.currentPiece == null || targetNode.currentPiece.isPlayer) return true;
        }
        return false;
    }

    public override BoardNode GetAIMove(BoardNode[,] grid)
    {
        List<BoardNode> validMoves = new List<BoardNode>();
        int[] dx = { 1, 1, -1, -1 };
        int[] dy = { 1, -1, 1, -1 };

        for (int i = 0; i < 4; i++)
        {
            int tX = currentX + dx[i];
            int tY = currentY + dy[i];

            if (tX >= 3 && tX <= 5 && tY >= 7 && tY <= 9) // Palace bounds
            {
                BoardNode testNode = grid[tX, tY];
                if (IsValidMove(testNode, grid))
                {
                    if (testNode.currentPiece != null && testNode.currentPiece.isPlayer) return testNode;
                    validMoves.Add(testNode);
                }
            }
        }
        return validMoves.Count > 0 ? validMoves[Random.Range(0, validMoves.Count)] : null;
    }
}