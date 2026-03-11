using UnityEngine;

[CreateAssetMenu(fileName = "NewPieceStats", menuName = "Xiangqi/Piece Stats")]
public class PieceStatsSO : ScriptableObject
{
    [Header("Base Stats (Do NOT change during gameplay)")]
    public int baseMaxHp = 1;
    public int baseMaxCooldown = 3;

    [Header("Runtime Stats (Modified by Cards)")]
    [HideInInspector] public int runtimeMaxHp;
    [HideInInspector] public int runtimeMaxCooldown;

    // Called by RunManager when a new run begins
    public virtual void ResetToDefault()
    {
        runtimeMaxHp = baseMaxHp;
        runtimeMaxCooldown = baseMaxCooldown;
    }
}