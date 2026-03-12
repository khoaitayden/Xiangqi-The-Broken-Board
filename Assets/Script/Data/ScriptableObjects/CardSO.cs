using UnityEngine;

public enum CardAlignment { Yin, Yang } 

public enum CardEffectID 
{ 
    // YIN (Black)
    Conscription,
    Desperation,
    TheVanguard,
    ArtilleryBackup,
    ImperialMandate,
    Drought,
    BloodthirstyPawns,
    HeavyArmor,

    // YANG (White)
    GunpowderGourd,
    TheRedHare,
    CloudStep,
    PiercingDragon,
    TheCrouchingTiger,
    JadeTalisman,
    MandateOfHeaven,
    ArtOfWar
}

[CreateAssetMenu(fileName = "NewCard", menuName = "Xiangqi/Card")]
public class CardSO : ScriptableObject
{
    public string cardName;
    [TextArea] public string description;
    public CardAlignment alignment;
    public CardEffectID effectID;
    
    [Header("Balance")]
    public int weight; 
    public Sprite cardIcon;
}