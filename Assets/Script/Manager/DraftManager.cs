using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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

    public void StartDraft() 
    {
        TurnManager.Instance.CurrentTurn = TurnManager.TurnState.Drafting;

        // 1. FILTER OUT CARDS ALREADY OWNED BY THE PLAYER
        List<CardSO> availableYinCards = new List<CardSO>();
        List<CardSO> availableYangCards = new List<CardSO>();

        foreach (CardSO yin in _allYinCards)
        {
            if (!RunManager.Instance.ActiveCards.Contains(yin)) availableYinCards.Add(yin);
        }
        foreach (CardSO yang in _allYangCards)
        {
            if (!RunManager.Instance.ActiveCards.Contains(yang)) availableYangCards.Add(yang);
        }

        // SAFETY: If we run out of cards, just generate the next level immediately
        if (availableYinCards.Count == 0 || availableYangCards.Count == 0)
        {
            Debug.LogWarning("Ran out of unique cards! Skipping draft.");
            LevelManager.Instance.LoadNextLevel();
            return;
        }

        List<CardSO> offeredCardsThisDraft = new List<CardSO>();

        // --- CALCULATE PAIR 1 ---
        CardSO p1Yin = availableYinCards[Random.Range(0, availableYinCards.Count)];
        offeredCardsThisDraft.Add(p1Yin);
        
        CardSO p1Yang = GetBalancedYangCard(p1Yin, availableYangCards, offeredCardsThisDraft);
        if (p1Yang != null) offeredCardsThisDraft.Add(p1Yang); 

        // --- CALCULATE PAIR 2 ---
        // Ensure Pair 2 doesn't offer the exact same Yin card as Pair 1
        List<CardSO> p2AvailableYins = new List<CardSO>(availableYinCards);
        p2AvailableYins.Remove(p1Yin);
        
        CardSO p2Yin = null;
        if (p2AvailableYins.Count > 0)
        {
            p2Yin = p2AvailableYins[Random.Range(0, p2AvailableYins.Count)];
            offeredCardsThisDraft.Add(p2Yin);
        }
        else p2Yin = p1Yin; // Fallback if only 1 Yin card is left in the entire game

        CardSO p2Yang = GetBalancedYangCard(p2Yin, availableYangCards, offeredCardsThisDraft);

        // Tell the UI Manager to display the results!
        UIManager.Instance.ShowDraftUI(p1Yin, p1Yang, p2Yin, p2Yang);
    }

    public void ResolveDraftChoice(CardSO selectedYin, CardSO selectedYang)
    {
        // Give the cards to the RunManager 
        RunManager.Instance.ApplyCard(selectedYin);
        RunManager.Instance.ApplyCard(selectedYang);

        // Update the HUD
        UIManager.Instance.AddYinCardToUI(selectedYin);
        UIManager.Instance.AddYangCardToUI(selectedYang);

        // REMOVED: UIManager.Instance.HideDraftUI(); (SystemUI handles this now!)

        // Load the next floor
        LevelManager.Instance.LoadNextLevel();
    }

    // --- UPDATED BALANCING ALGORITHM ---
    private CardSO GetBalancedYangCard(CardSO yinCard, List<CardSO> availableYangs, List<CardSO> offeredCards)
    {
        if (yinCard == null) return availableYangs[0]; // Safety fallback

        int targetWeight = Mathf.Abs(yinCard.weight); 
        List<CardSO> validYangs = new List<CardSO>();

        // 1. Try to find a mathematically balanced card
        foreach (CardSO yang in availableYangs)
        {
            if (offeredCards.Contains(yang)) continue; // Don't offer duplicates on the same screen
            if (Mathf.Abs(yang.weight - targetWeight) <= 1) validYangs.Add(yang);
        }

        if (validYangs.Count > 0) return validYangs[Random.Range(0, validYangs.Count)];

        // 2. Fallback: Find ANY card that hasn't been offered on this screen yet
        List<CardSO> remainingUniqueYangs = new List<CardSO>();
        foreach (CardSO yang in availableYangs)
        {
            if (!offeredCards.Contains(yang)) remainingUniqueYangs.Add(yang);
        }

        if (remainingUniqueYangs.Count > 0) return remainingUniqueYangs[Random.Range(0, remainingUniqueYangs.Count)];

        // 3. Extreme Fallback: Just return the first available card
        return availableYangs[Random.Range(0, availableYangs.Count)];
    }
}