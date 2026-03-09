using UnityEngine;
using System.Collections.Generic;

public class EnemyCannon : Piece
{
    protected override void Awake()
    {
        base.Awake();  
    }

    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid) { return false; }

    public override BoardNode GetAIMove(BoardNode[,] grid)
    {
        List<BoardNode> validMoves = new List<BoardNode>();
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var dir in directions)
        {
            int checkX = X + dir.x;
            int checkY = Y + dir.y;
            bool hasJumped = false;

            while (checkX >= 0 && checkX <= 8 && checkY >= 0 && checkY <= 9)
            {
                BoardNode testNode = grid[checkX, checkY];

                if (!hasJumped)
                {
                    if (testNode.IsEmpty()) validMoves.Add(testNode); 
                    else hasJumped = true; // Hit a piece OR a corpse! Mount created!
                }
                else
                {
                    if (!testNode.IsEmpty()) // Hit second object
                    {
                        if (testNode.currentPiece != null && testNode.currentPiece.IsPlayer) return testNode; 
                        break; 
                    }
                }
                checkX += dir.x;
                checkY += dir.y;
            }
        }
        return validMoves.Count > 0 ? validMoves[Random.Range(0, validMoves.Count)] : null;
    }
}