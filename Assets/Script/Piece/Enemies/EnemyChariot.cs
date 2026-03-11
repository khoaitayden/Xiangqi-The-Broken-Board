using UnityEngine;
using System.Collections.Generic;

public class EnemyChariot : Piece
{
    protected override void Awake()
    {
        base.Awake(); 
    }

    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        return GetValidMoves(grid).Contains(targetNode); 
    }

    public override List<BoardNode> GetValidMoves(BoardNode[,] grid)
    {
        List<BoardNode> validMoves = new List<BoardNode>();
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var dir in directions)
        {
            int checkX = X + dir.x;
            int checkY = Y + dir.y;

            while (checkX >= 0 && checkX <= 8 && checkY >= 0 && checkY <= 9)
            {
                BoardNode testNode = grid[checkX, checkY];

                if (testNode.IsEmpty()) 
                {
                    validMoves.Add(testNode); 
                }
                else 
                {
                    if (testNode.currentPiece != null && testNode.currentPiece.IsPlayer)
                    {
                        validMoves.Add(testNode); 
                    }
                    break; 
                }
                checkX += dir.x;
                checkY += dir.y;
            }
        }
        return validMoves; 
    }
}