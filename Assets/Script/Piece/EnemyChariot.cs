using UnityEngine;
using System.Collections.Generic;

public class EnemyChariot : Piece
{
    protected override void Awake()
    {
        base.Awake(); 
        isPlayer = false; 
        maxCooldown = 3; 
    }

    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        // Handled directly inside GetAIMove for sliding pieces for better performance
        return false; 
    }

    public override BoardNode GetAIMove(BoardNode[,] grid)
    {
        List<BoardNode> validMoves = new List<BoardNode>();
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var dir in directions)
        {
            int checkX = currentX + dir.x;
            int checkY = currentY + dir.y;

            // Slide until we hit the edge of the board
            while (checkX >= 0 && checkX <= 8 && checkY >= 0 && checkY <= 9)
            {
                BoardNode testNode = grid[checkX, checkY];

                if (testNode.IsEmpty()) 
                {
                    validMoves.Add(testNode); 
                }
                else // Hit a piece OR a corpse
                {
                    if (testNode.currentPiece != null && testNode.currentPiece.isPlayer)
                    {
                        return testNode; // Hit player
                    }
                    break; // Stop sliding, hit a wall/corpse/teammate
                }

                checkX += dir.x;
                checkY += dir.y;
            }
        }
        return validMoves.Count > 0 ? validMoves[Random.Range(0, validMoves.Count)] : null;
    }
}