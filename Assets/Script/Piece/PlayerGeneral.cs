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

    protected override void Awake()
    {
        base.Awake();
        isPlayer = true;
    }

    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        int distanceX = Mathf.Abs(targetNode.x - currentX);
        int distanceY = Mathf.Abs(targetNode.y - currentY);

        if (distanceX <= 1 && distanceY <= 1 && !(distanceX == 0 && distanceY == 0))
        {
            if (targetNode.currentPiece == null) return true;
        }
        return false;
    }
}