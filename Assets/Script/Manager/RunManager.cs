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

    [Header("Enemy Modifiers (YIN)")]
    [SerializeField] private int _bonusStartingPawns = 0;
    public int BonusStartingPawns => _bonusStartingPawns;

    [SerializeField] private int _bonusStartingChariots = 0;
    public int BonusStartingChariots => _bonusStartingChariots;

    [SerializeField] private bool _bossLeavesPalace = false;
    public bool BossLeavesPalace => _bossLeavesPalace;

    [Header("Player Modifiers (YANG)")]
    [SerializeField] private bool _redHareEnabled = false;
    public bool RedHareEnabled => _redHareEnabled;

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

    public void ResetEntireRun()
    {
        _activeCards.Clear();
        _bonusStartingPawns = 0;
        _bonusStartingChariots = 0;
        _bossLeavesPalace = false;
        _redHareEnabled = false;

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
            case CardEffectID.GunpowderGourd: playerStats.runtimeMaxAmmo += 1; break;
            case CardEffectID.JadeTalisman: playerStats.runtimeMaxArmor += 1; break;
            case CardEffectID.Conscription: _bonusStartingPawns += 2; break;
            case CardEffectID.TheVanguard: _bonusStartingChariots += 1; break;
            case CardEffectID.Desperation: _bossLeavesPalace = true; break;
            case CardEffectID.TheRedHare: _redHareEnabled = true; break;
        }
        
        Debug.Log($"Applied Card: {card.cardName}");
    }
}