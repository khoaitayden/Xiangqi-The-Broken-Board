using UnityEngine;

public class PlayerGeneral : Piece
{
    [Header("Weapon: Fire Lance")]
    public int loadedAmmo = 2;
    public int maxAmmo = 2;
    public int firepower = 5;       
    public float fireArc = 40f;     
    public float rangeX = 3f;       
    public float rangeY = 6f;   
    [Header("Defense")]
    public int maxArmor = 2;
    public int currentArmor;    

    protected override void Awake()
    {
        base.Awake();
        isPlayer = true;
        currentArmor = maxArmor;
    }

    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        int distanceX = Mathf.Abs(targetNode.x - currentX);
        int distanceY = Mathf.Abs(targetNode.y - currentY);
        if (distanceX <= 1 && distanceY <= 1 && !(distanceX == 0 && distanceY == 0))
        {
            // Player cannot step on alive enemies OR corpses
            if (targetNode.IsEmpty()) return true; 
        }
        return false;
    }
}