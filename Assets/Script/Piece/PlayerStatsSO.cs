using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStats", menuName = "Xiangqi/Player Stats")]
public class PlayerStatsSO : PieceStatsSO
{
    [Header("Base Weapon Stats")]
    public int baseMaxAmmo = 2;
    public int baseFirepower = 5;
    public float baseFireArc = 40f;
    public float baseRangeX = 3f;
    public float baseRangeY = 6f;
    
    [Header("Base Defense")]
    public int baseMaxArmor = 2;

    [Header("Runtime Stats")]
    [HideInInspector] public int runtimeMaxAmmo;
    [HideInInspector] public int runtimeFirepower;
    [HideInInspector] public float runtimeFireArc;
    [HideInInspector] public float runtimeRangeX;
    [HideInInspector] public float runtimeRangeY;
    [HideInInspector] public int runtimeMaxArmor;

    public override void ResetToDefault()
    {
        base.ResetToDefault(); // Resets HP and Cooldown
        runtimeMaxAmmo = baseMaxAmmo;
        runtimeFirepower = baseFirepower;
        runtimeFireArc = baseFireArc;
        runtimeRangeX = baseRangeX;
        runtimeRangeY = baseRangeY;
        runtimeMaxArmor = baseMaxArmor;
    }
}