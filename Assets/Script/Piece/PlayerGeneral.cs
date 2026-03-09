using UnityEngine;

public class PlayerGeneral : Piece
{
    [Header("Player Stats Data")]
    [SerializeField] private PlayerStatsSO _playerStats;

    // UPDATE the Properties to read straight from the SO:
    public int MaxAmmo => _playerStats.runtimeMaxAmmo;
    public int Firepower => _playerStats.runtimeFirepower;
    public float FireArc => _playerStats.runtimeFireArc;
    public float RangeX => _playerStats.runtimeRangeX;
    public float RangeY => _playerStats.runtimeRangeY;
    public int MaxArmor => _playerStats.runtimeMaxArmor;

    [SerializeField] private int _loadedAmmo;
    public int LoadedAmmo { get { return _loadedAmmo; } set { _loadedAmmo = value; } }

    [SerializeField] private int _currentArmor;
    public int CurrentArmor { get { return _currentArmor; } set { _currentArmor = value; } }
    protected override void Awake()
    {
        base.Awake();
        CurrentArmor = MaxArmor;
        LoadedAmmo = MaxAmmo;
    }

    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        // Use property X and Y instead of currentX
        int distanceX = Mathf.Abs(targetNode.x - X);
        int distanceY = Mathf.Abs(targetNode.y - Y);

        if (distanceX <= 1 && distanceY <= 1 && !(distanceX == 0 && distanceY == 0))
        {
            if (targetNode.IsEmpty()) return true;
        }
        return false;
    }
}