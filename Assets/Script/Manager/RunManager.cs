using UnityEngine;
using System.Collections.Generic;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Header("Drafting Data")]
    [SerializeField] private List<CardSO> _activeCards = new List<CardSO>();
    public IReadOnlyList<CardSO> ActiveCards => _activeCards;

    [Header("All Piece Stats")]
    public PlayerStatsSO playerStats;
    public PieceStatsSO pawnStats;
    public PieceStatsSO horseStats;
    public PieceStatsSO chariotStats;
    public PieceStatsSO elephantStats;
    public PieceStatsSO advisorStats;
    public PieceStatsSO cannonStats;
    public PieceStatsSO enemyGeneralStats;

    // --- YIN MODIFIERS ---
    public int BonusStartingPawns { get; private set; }
    public int BonusStartingChariots { get; private set; }
    public int BonusStartingCannons { get; private set; }
    public bool BossLeavesPalace { get; private set; }
    public bool AdvisorsProtectGeneral { get; private set; }
    public bool ElephantsCrossRiver { get; private set; }
    public bool PawnsAttackDiagonal { get; private set; }

    // --- YANG MODIFIERS ---
    public bool RedHareEnabled { get; private set; }
    public bool CloudStepEnabled { get; private set; }
    public bool PiercingDragonEnabled { get; private set; }
    public bool CrouchingTigerEnabled { get; private set; }
    public bool MandateOfHeavenEnabled { get; private set; }
    public bool ArtOfWarEnabled { get; private set; }
    
    public bool ArtOfWarUsedThisFloor { get; set; } 

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this.gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
        ResetEntireRun();
    }

    public void ResetEntireRun()
    {
        _activeCards.Clear();
        
        BonusStartingPawns = 0; BonusStartingChariots = 0; BonusStartingCannons = 0;
        BossLeavesPalace = false; AdvisorsProtectGeneral = false;
        ElephantsCrossRiver = false; PawnsAttackDiagonal = false;

        RedHareEnabled = false; CloudStepEnabled = false; PiercingDragonEnabled = false;
        CrouchingTigerEnabled = false; MandateOfHeavenEnabled = false; ArtOfWarEnabled = false;
        ArtOfWarUsedThisFloor = false;

        if(playerStats) playerStats.ResetToDefault();
        if(pawnStats) pawnStats.ResetToDefault();
        if(horseStats) horseStats.ResetToDefault();
        if(chariotStats) chariotStats.ResetToDefault();
        if(elephantStats) elephantStats.ResetToDefault();
        if(advisorStats) advisorStats.ResetToDefault();
        if(cannonStats) cannonStats.ResetToDefault();
        if(enemyGeneralStats) enemyGeneralStats.ResetToDefault();
    }

    public void ApplyCard(CardSO card)
    {
        _activeCards.Add(card);
        switch (card.effectID)
        {
            case CardEffectID.Conscription: BonusStartingPawns += 2; break;
            case CardEffectID.TheVanguard: BonusStartingChariots += 1; break;
            case CardEffectID.ArtilleryBackup: BonusStartingCannons += 1; break;
            case CardEffectID.Desperation: BossLeavesPalace = true; break;
            case CardEffectID.ImperialMandate: AdvisorsProtectGeneral = true; break;
            case CardEffectID.Drought: ElephantsCrossRiver = true; break;
            case CardEffectID.BloodthirstyPawns: PawnsAttackDiagonal = true; break;
            case CardEffectID.HeavyArmor: elephantStats.runtimeMaxHp += 2; break;

            case CardEffectID.GunpowderGourd: playerStats.runtimeMaxAmmo += 1; break;
            case CardEffectID.JadeTalisman: playerStats.runtimeMaxArmor += 1; break;
            case CardEffectID.TheRedHare: RedHareEnabled = true; break;
            case CardEffectID.CloudStep: CloudStepEnabled = true; break;
            case CardEffectID.PiercingDragon: PiercingDragonEnabled = true; break;
            case CardEffectID.TheCrouchingTiger: CrouchingTigerEnabled = true; break;
            case CardEffectID.MandateOfHeaven: MandateOfHeavenEnabled = true; break;
            case CardEffectID.ArtOfWar: ArtOfWarEnabled = true; break;
        }
    }
}