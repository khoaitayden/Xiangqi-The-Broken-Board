using UnityEngine;
using System.Collections.Generic;

public class EnemyElephant : Piece
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
        // River Rule: Enemy Elephant must stay on top half (Y from 5 to 9)
        if (targetNode.y < 5) return false; 

        int dirX = targetNode.x - currentX;
        int dirY = targetNode.y - currentY;

        // Exactly 2 steps diagonally
        if (Mathf.Abs(dirX) == 2 && Mathf.Abs(dirY) == 2)
        {
            // The "Blocking the Eye" Rule (Chặn mắt tượng)
            int eyeX = currentX + (dirX / 2);
            int eyeY = currentY + (dirY / 2);

            // FIX: If there is ANY piece OR Corpse in the middle, it cannot move
            if (!grid[eyeX, eyeY].IsEmpty()) return false;

            // FIX: Safe check! Empty OR (Not Null and Player)
            if (targetNode.IsEmpty() || (targetNode.currentPiece != null && targetNode.currentPiece.isPlayer)) return true;
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
            int tX = currentX + dx[i];
            int tY = currentY + dy[i];

            if (tX >= 0 && tX <= 8 && tY >= 5 && tY <= 9) // River bounds check
            {
                BoardNode testNode = grid[tX, tY];
                if (IsValidMove(testNode, grid))
                {
                    // FIX: Safe check! We only care if it's the player here.
                    if (testNode.currentPiece != null && testNode.currentPiece.isPlayer) return testNode;
                    validMoves.Add(testNode);
                }
            }
        }
        return validMoves.Count > 0 ? validMoves[Random.Range(0, validMoves.Count)] : null;
    }
}