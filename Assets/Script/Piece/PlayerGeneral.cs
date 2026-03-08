using UnityEngine;

public class PlayerGeneral : Piece
{
    [Header("Weapon: Fire Lance")]
    [SerializeField] private int _loadedAmmo = 2;
    [SerializeField] private int _maxAmmo = 2;
    [SerializeField] private int _firepower = 5;
    [SerializeField] private float _fireArc = 40f;
    [SerializeField] private float _rangeX = 3f;
    [SerializeField] private float _rangeY = 6f;

    // Public Properties for accessing stats
    public int LoadedAmmo { get { return _loadedAmmo; } set { _loadedAmmo = value; } }
    public int MaxAmmo => _maxAmmo;
    public int Firepower => _firepower;
    public float FireArc => _fireArc;
    public float RangeX => _rangeX;
    public float RangeY => _rangeY;

    [Header("Defense")]
    [SerializeField] private int _maxArmor = 2;
    [SerializeField] private int _currentArmor;
    public int CurrentArmor { get { return _currentArmor; } set { _currentArmor = value; } }
    public int MaxArmor => _maxArmor;

    protected override void Awake()
    {
        base.Awake();
        
        // Add RunManager Modifiers!
        if (RunManager.Instance != null)
        {
            LoadedAmmo = MaxAmmo + RunManager.Instance.BonusMaxAmmo;
            CurrentArmor = MaxArmor + RunManager.Instance.BonusArmorPerFloor;
        }
        else
        {
            LoadedAmmo = MaxAmmo;
            CurrentArmor = MaxArmor;
        }
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