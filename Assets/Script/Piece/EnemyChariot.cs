using UnityEngine;
using System.Collections.Generic;

public class EnemyChariot : Piece
{
    void Awake() { isPlayer = false; maxCooldown = 3; }
    void Start() { isPlayer = false; maxCooldown = 3; currentCooldown = maxCooldown; }

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

                if (testNode.currentPiece == null)
                {
                    validMoves.Add(testNode); // Empty space is a valid move
                }
                else
                {
                    if (testNode.currentPiece.isPlayer)
                    {
                        return testNode; // Instantly return the killing move!
                    }
                    break; // Blocked by another piece, stop sliding in this direction
                }

                checkX += dir.x;
                checkY += dir.y;
            }
        }
        return validMoves.Count > 0 ? validMoves[Random.Range(0, validMoves.Count)] : null;
    }
}