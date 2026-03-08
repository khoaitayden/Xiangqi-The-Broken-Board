using UnityEngine;
using System.Collections.Generic;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Header("Active Cards")]
    [SerializeField] private List<CardSO> _activeCards = new List<CardSO>();
    public IReadOnlyList<CardSO> ActiveCards => _activeCards;

    [Header("Player Modifiers (YANG)")]
    [SerializeField] private int _bonusMaxAmmo = 0;
    public int BonusMaxAmmo => _bonusMaxAmmo;

    [SerializeField] private int _bonusArmorPerFloor = 0;
    public int BonusArmorPerFloor => _bonusArmorPerFloor;

    [Header("Enemy Modifiers (YIN)")]
    [SerializeField] private int _bonusStartingPawns = 0;
    public int BonusStartingPawns => _bonusStartingPawns;

    [SerializeField] private int _bonusElephantAdvisorHP = 0;
    public int BonusElephantAdvisorHP => _bonusElephantAdvisorHP;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    public void ApplyCard(CardSO card)
    {
        _activeCards.Add(card);

        switch (card.effectID)
        {
            case CardEffectID.GunpowderGourd: _bonusMaxAmmo += 1; break;
            case CardEffectID.JadeTalisman: _bonusArmorPerFloor += 1; break;
            case CardEffectID.Conscription: _bonusStartingPawns += 2; break;
            case CardEffectID.IronPlating: _bonusElephantAdvisorHP += 1; break;
        }
        
        Debug.Log($"Applied Card: {card.cardName}");
    }
}