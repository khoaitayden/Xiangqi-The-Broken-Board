using UnityEngine;
using System.Collections.Generic;

public class DraftManager : MonoBehaviour
{
    public static DraftManager Instance { get; private set; }

    [Header("Card Database")]
    [SerializeField] private List<CardSO> _allYinCards;
    [SerializeField] private List<CardSO> _allYangCards;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;
    }

    // Called by the Enemy Boss when it dies
    public void StartDraft() 
    {
        TurnManager.Instance.CurrentTurn = TurnManager.TurnState.Drafting;

        List<CardSO> offeredYangCardsThisDraft = new List<CardSO>();

        // --- CALCUALTE PAIR 1 ---
        CardSO p1Yin = _allYinCards[Random.Range(0, _allYinCards.Count)];
        CardSO p1Yang = GetBalancedYangCard(p1Yin, offeredYangCardsThisDraft);
        offeredYangCardsThisDraft.Add(p1Yang); 

        // --- CALCULATE PAIR 2 ---
        CardSO p2Yin = _allYinCards[Random.Range(0, _allYinCards.Count)];
        CardSO p2Yang = GetBalancedYangCard(p2Yin, offeredYangCardsThisDraft);

        // Tell the UI Manager to display the results!
        UIManager.Instance.ShowDraftUI(p1Yin, p1Yang, p2Yin, p2Yang);
    }

    // Called by the UIManager when a player clicks a button
    public void ResolveDraftChoice(CardSO selectedYin, CardSO selectedYang)
    {
        RunManager.Instance.ApplyCard(selectedYin);
        RunManager.Instance.ApplyCard(selectedYang);

        UIManager.Instance.HideDraftUI();
        LevelManager.Instance.LoadNextLevel();
    }

    // --- THE BALANCING ALGORITHM (Unchanged) ---
    private CardSO GetBalancedYangCard(CardSO yinCard, List<CardSO> alreadyOfferedCards)
    {
        int targetWeight = Mathf.Abs(yinCard.weight); 
        List<CardSO> validYangs = new List<CardSO>();

        foreach (CardSO yang in _allYangCards)
        {
            if (alreadyOfferedCards.Contains(yang)) continue;
            if (Mathf.Abs(yang.weight - targetWeight) <= 1) validYangs.Add(yang);
        }

        if (validYangs.Count > 0) return validYangs[Random.Range(0, validYangs.Count)];

        List<CardSO> remainingUniqueYangs = new List<CardSO>();
        foreach (CardSO yang in _allYangCards)
        {
            if (!alreadyOfferedCards.Contains(yang)) remainingUniqueYangs.Add(yang);
        }

        if (remainingUniqueYangs.Count > 0) return remainingUniqueYangs[Random.Range(0, remainingUniqueYangs.Count)];

        return _allYangCards[Random.Range(0, _allYangCards.Count)];
    }
}