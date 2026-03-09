using UnityEngine;
using System.Collections.Generic;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Header("Drafting Data")]
    [SerializeField] private List<CardSO> _activeCards = new List<CardSO>();
    public IReadOnlyList<CardSO> ActiveCards => _activeCards;

    [Header("All Piece Stats (To Reset on Start)")]
    public PlayerStatsSO playerStats;
    public PieceStatsSO pawnStats;
    public PieceStatsSO horseStats;
    public PieceStatsSO chariotStats;
    public PieceStatsSO elephantStats;
    public PieceStatsSO advisorStats;
    public PieceStatsSO cannonStats;
    public PieceStatsSO enemyGeneralStats;

    [Header("Non-Stat Modifiers")]
    [SerializeField] private int _bonusStartingPawns = 0;
    public int BonusStartingPawns => _bonusStartingPawns;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);

        ResetEntireRun();
    }

    // Called when a new run begins to wipe old upgrades
    public void ResetEntireRun()
    {
        _activeCards.Clear();
        _bonusStartingPawns = 0;

        // Reset all SOs to their base stats
        playerStats.ResetToDefault();
        pawnStats.ResetToDefault();
        horseStats.ResetToDefault();
        chariotStats.ResetToDefault();
        elephantStats.ResetToDefault();
        advisorStats.ResetToDefault();
        cannonStats.ResetToDefault();
        enemyGeneralStats.ResetToDefault();
    }

    public void ApplyCard(CardSO card)
    {
        _activeCards.Add(card);

        switch (card.effectID)
        {
            case CardEffectID.GunpowderGourd: 
                playerStats.runtimeMaxAmmo += 1; 
                break;

            case CardEffectID.JadeTalisman: 
                playerStats.runtimeMaxArmor += 1; 
                break;

            case CardEffectID.Conscription: 
                _bonusStartingPawns += 2; 
                break;

            case CardEffectID.IronPlating: 
                elephantStats.runtimeMaxHp += 1; 
                advisorStats.runtimeMaxHp += 1; 
                break;
        }
        
        Debug.Log($"Applied Card: {card.cardName}");
    }
}