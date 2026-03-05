using UnityEngine;
using System.Collections.Generic;

public class EnemyHorse : Piece
{
    void Awake() { isPlayer = false; maxCooldown = 1; }
    void Start() { isPlayer = false; maxCooldown = 1; currentCooldown = maxCooldown; }
    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        int dirX = targetNode.x - currentX;
        int dirY = targetNode.y - currentY;
        
        int absX = Mathf.Abs(dirX);
        int absY = Mathf.Abs(dirY);

        // Check if it's an "L" shape (2 steps one way, 1 step the other)
        if ((absX == 1 && absY == 2) || (absX == 2 && absY == 1))
        {
            // --- THE HOBBLING RULE (Cản Mã) ---
            // Find the immediate adjacent node the horse is trying to move THROUGH
            int blockX = currentX;
            int blockY = currentY;

            // If it's moving 2 steps horizontally, the block is 1 step horizontally
            if (absX == 2) blockX += (int)Mathf.Sign(dirX);
            // If it's moving 2 steps vertically, the block is 1 step vertically
            else blockY += (int)Mathf.Sign(dirY);

            // If the blocking node has ANY piece on it, the move is illegal!
            if (grid[blockX, blockY].currentPiece != null)
            {
                return false; // HOBBLED!
            }

            // If not hobbled, check if target node is empty or has the player
            if (targetNode.currentPiece == null || targetNode.currentPiece.isPlayer)
            {
                return true;
            }
        }
        
        return false;
    }

    // --- AI FOR THE HORSE ---
    public override BoardNode GetAIMove(BoardNode[,] grid)
    {
        List<BoardNode> validMoves = new List<BoardNode>();
        
        // The 8 possible L-shape coordinates
        int[] dx = { 1, 1, -1, -1, 2, 2, -2, -2 };
        int[] dy = { 2, -2, 2, -2, 1, -1, 1, -1 };

        for (int i = 0; i < 8; i++)
        {
            int targetX = currentX + dx[i];
            int targetY = currentY + dy[i];

            // Make sure the target is inside the 9x10 board
            if (targetX >= 0 && targetX <= 8 && targetY >= 0 && targetY <= 9)
            {
                BoardNode testNode = grid[targetX, targetY];
                
                // If it's a valid move (which includes the Hobbling check)
                if (IsValidMove(testNode, grid))
                {
                    // If it kills the player, DO IT IMMEDIATELY
                    if (testNode.currentPiece != null && testNode.currentPiece.isPlayer)
                    {
                        return testNode;
                    }
                    validMoves.Add(testNode);
                }
            }
        }

        // Pick a random valid jump
        if (validMoves.Count > 0)
        {
            int randomIndex = Random.Range(0, validMoves.Count);
            return validMoves[randomIndex];
        }

        return null; // Completely trapped, cannot move
    }
}