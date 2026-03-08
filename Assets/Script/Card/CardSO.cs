using UnityEngine;

public enum CardAlignment { Yin, Yang } 
public enum CardEffectID 
{ 
    GunpowderGourd, // +1 Ammo
    JadeTalisman,   // +1 Armor
    Conscription,   // +2 Pawns
    IronPlating     // +1 HP
}

[CreateAssetMenu(fileName = "NewCard", menuName = "Xiangqi/Card")]
public class CardSO : ScriptableObject
{
    public string cardName;
    [TextArea] public string description;
    public CardAlignment alignment;
    public CardEffectID effectID;
}