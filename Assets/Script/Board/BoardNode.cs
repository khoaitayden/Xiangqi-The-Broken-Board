using UnityEngine;

[System.Serializable]
public class BoardNode
{
    public int x; 
    public int y; 
    public Vector2 worldPosition;
    
    public bool isPlayerPalace;
    public bool isEnemyPalace;
    public bool isRiver; 

    public GameObject nodeGameObject;
    
    // NEW: The piece currently occupying this intersection
    public Piece currentPiece; 

    public BoardNode(int x, int y, Vector2 worldPosition)
    {
        this.x = x;
        this.y = y;
        this.worldPosition = worldPosition;
        
        isPlayerPalace = (x >= 3 && x <= 5) && (y >= 0 && y <= 2);
        isEnemyPalace = (x >= 3 && x <= 5) && (y >= 7 && y <= 9);
        isRiver = (y == 4 || y == 5);
    }
}