using UnityEngine;

public class EnemyPawn : Piece
{
    protected override void Awake()
    {
        base.Awake(); 
        isPlayer = false; 
        maxCooldown = 3; 
    }
    void Start() { isPlayer = false; maxCooldown = 3; currentCooldown = maxCooldown; }
    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        // Enemy pawns move DOWN the board (Y decreases)
        int dirX = targetNode.x - currentX;
        int dirY = targetNode.y - currentY;

        // Cannot move backward (dirY > 0) or diagonally
        if (dirY > 0) return false;
        if (Mathf.Abs(dirX) > 0 && Mathf.Abs(dirY) > 0) return false;

        // Base move: 1 step straight forward
        if (dirX == 0 && dirY == -1) return true;

        // Crossed river check: Enemy River is Y <= 4
        bool hasCrossedRiver = currentY <= 4;
        
        // If crossed river, can move 1 step left or right
        if (hasCrossedRiver && dirY == 0 && Mathf.Abs(dirX) == 1) return true;

        return false;
    }

    // --- SIMPLE AI FOR ENEMY TURN ---
    public override BoardNode GetAIMove(BoardNode[,] grid)
    {
        // 1. Try to move forward first
        if (currentY - 1 >= 0)
        {
            BoardNode forwardNode = grid[currentX, currentY - 1];
            // If empty OR contains the player, it's a valid move
            if (forwardNode.currentPiece == null || forwardNode.currentPiece.isPlayer)
                return forwardNode;
        }

        // 2. If forward is blocked, and we crossed the river, try sideways
        if (currentY <= 4)
        {
            // Check left
            if (currentX - 1 >= 0)
            {
                BoardNode leftNode = grid[currentX - 1, currentY];
                if (leftNode.currentPiece == null || leftNode.currentPiece.isPlayer) return leftNode;
            }
            // Check right
            if (currentX + 1 <= 8)
            {
                BoardNode rightNode = grid[currentX + 1, currentY];
                if (rightNode.currentPiece == null || rightNode.currentPiece.isPlayer) return rightNode;
            }
        }

        // 3. If completely blocked, don't move
        return null; 
    }
}