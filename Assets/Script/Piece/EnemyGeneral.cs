using UnityEngine;
using System.Collections.Generic;

public class EnemyGeneral : Piece
{
    protected override void Awake()
    {
        base.Awake();
        MaxCooldown = 2; 
    }

    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        if (!targetNode.isEnemyPalace) return false; 

        int absX = Mathf.Abs(targetNode.x - X);
        int absY = Mathf.Abs(targetNode.y - Y);

        if ((absX == 1 && absY == 0) || (absX == 0 && absY == 1))
        {
            if (targetNode.IsEmpty() || (targetNode.currentPiece != null && targetNode.currentPiece.IsPlayer)) return true;
        }
        return false;
    }

    public override BoardNode GetAIMove(BoardNode[,] grid)
    {
        List<BoardNode> validMoves = new List<BoardNode>();
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };

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

    protected override void Die()
    {
        base.Die();
        DraftManager draft = FindFirstObjectByType<DraftManager>();
        if (draft != null)
        {
            draft.ShowDraftScreen();
        }
    }
}