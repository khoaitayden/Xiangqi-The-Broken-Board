using UnityEngine;
using System.Collections.Generic;

public class EnemyCannon : Piece
{
    void Awake() { isPlayer = false; maxCooldown = 2; }
    void Start() { isPlayer = false; maxCooldown = 2; currentCooldown = maxCooldown; }

    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid) { return false; }

    public override BoardNode GetAIMove(BoardNode[,] grid)
    {
        List<BoardNode> validMoves = new List<BoardNode>();
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var dir in directions)
        {
            int checkX = currentX + dir.x;
            int checkY = currentY + dir.y;
            bool hasJumped = false;

            while (checkX >= 0 && checkX <= 8 && checkY >= 0 && checkY <= 9)
            {
                BoardNode testNode = grid[checkX, checkY];

                if (!hasJumped)
                {
                    if (testNode.currentPiece == null)
                    {
                        validMoves.Add(testNode); // Move normally if haven't jumped
                    }
                    else
                    {
                        hasJumped = true; // We hit the "Screen/Mount". Now we look for a target.
                    }
                }
                else
                {
                    // If we have jumped, we ignore empty spaces and look for a piece
                    if (testNode.currentPiece != null)
                    {
                        if (testNode.currentPiece.isPlayer)
                        {
                            return testNode; // Instantly return the killing move!
                        }
                        break; // Hit a second piece, cannon cannot jump twice. Stop sliding.
                    }
                }

                checkX += dir.x;
                checkY += dir.y;
            }
        }
        return validMoves.Count > 0 ? validMoves[Random.Range(0, validMoves.Count)] : null;
    }
}