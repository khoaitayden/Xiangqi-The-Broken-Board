using UnityEngine;
using System.Collections.Generic;

public class EnemyAdvisor : Piece
{
    protected override void Awake()
    {
        base.Awake(); 
    }

    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        if (!targetNode.isEnemyPalace) return false; 

        int absX = Mathf.Abs(targetNode.x - X);
        int absY = Mathf.Abs(targetNode.y - Y);

        if (absX == 1 && absY == 1)
        {
            // SAFE CHECK
            if (targetNode.IsEmpty() || (targetNode.currentPiece != null && targetNode.currentPiece.IsPlayer)) return true;
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
            int tX = X + dx[i];
            int tY = Y + dy[i];

            if (tX >= 3 && tX <= 5 && tY >= 7 && tY <= 9) 
            {
                BoardNode testNode = grid[tX, tY];
                if (IsValidMove(testNode, grid))
                {
                    // SAFE CHECK
                    if (testNode.currentPiece != null && testNode.currentPiece.IsPlayer) return testNode;
                    validMoves.Add(testNode);
                }
            }
        }
        return validMoves.Count > 0 ? validMoves[Random.Range(0, validMoves.Count)] : null;
    }
}