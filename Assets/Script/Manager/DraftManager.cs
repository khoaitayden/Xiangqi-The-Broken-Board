using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class DraftManager : MonoBehaviour
{
    [Header("Card Database")]
    public List<CardSO> allYinCards;
    public List<CardSO> allYangCards;

    [Header("UI References")]
    public GameObject draftUIPanel;
    public TextMeshProUGUI pair1YinText;
    public TextMeshProUGUI pair1YangText;
    public Button selectPair1Button;
    public TextMeshProUGUI pair2YinText;
    public TextMeshProUGUI pair2YangText;
    public Button selectPair2Button;

    private CardSO p1Yin, p1Yang, p2Yin, p2Yang;

    void Start()
    {
        draftUIPanel.SetActive(false);
        selectPair1Button.onClick.AddListener(() => ChoosePair(p1Yin, p1Yang));
        selectPair2Button.onClick.AddListener(() => ChoosePair(p2Yin, p2Yang));
    }

    public void ShowDraftScreen()
    {
        TurnManager.Instance.CurrentTurn = TurnManager.TurnState.Drafting;

        // Create a temporary list to track which Yang cards have already been offered on THIS screen
        List<CardSO> offeredYangCardsThisDraft = new List<CardSO>();

        // --- PAIR 1 ---
        p1Yin = allYinCards[Random.Range(0, allYinCards.Count)];
        p1Yang = GetBalancedYangCard(p1Yin, offeredYangCardsThisDraft);
        offeredYangCardsThisDraft.Add(p1Yang); // Remember it so we don't pick it again

        // --- PAIR 2 ---
        p2Yin = allYinCards[Random.Range(0, allYinCards.Count)];
        p2Yang = GetBalancedYangCard(p2Yin, offeredYangCardsThisDraft);

        // Update UI
        pair1YinText.text = $"[YIN {p1Yin.weight}] {p1Yin.cardName}\n{p1Yin.description}";
        pair1YangText.text = $"[YANG +{p1Yang.weight}] {p1Yang.cardName}\n{p1Yang.description}";

        pair2YinText.text = $"[YIN {p2Yin.weight}] {p2Yin.cardName}\n{p2Yin.description}";
        pair2YangText.text = $"[YANG +{p2Yang.weight}] {p2Yang.cardName}\n{p2Yang.description}";

        draftUIPanel.SetActive(true);
    }

    // --- THE BALANCING ALGORITHM (Now prevents duplicates) ---
    private CardSO GetBalancedYangCard(CardSO yinCard, List<CardSO> alreadyOfferedCards)
    {
        int targetWeight = Mathf.Abs(yinCard.weight); 
        
        List<CardSO> validYangs = new List<CardSO>();

        foreach (CardSO yang in allYangCards)
        {
            // 1. Check if the card is already on the screen
            if (alreadyOfferedCards.Contains(yang)) continue;

            // 2. Check if the weight matches
            if (Mathf.Abs(yang.weight - targetWeight) <= 1)
            {
                validYangs.Add(yang);
            }
        }

        // If we found valid balanced cards that haven't been offered yet, pick one
        if (validYangs.Count > 0)
        {
            return validYangs[Random.Range(0, validYangs.Count)];
        }

        // FALLBACK: If we couldn't find a perfectly balanced card (or ran out of unique ones),
        // we just find ANY unique Yang card.
        List<CardSO> remainingUniqueYangs = new List<CardSO>();
        foreach (CardSO yang in allYangCards)
        {
            if (!alreadyOfferedCards.Contains(yang)) remainingUniqueYangs.Add(yang);
        }

        if (remainingUniqueYangs.Count > 0)
        {
            return remainingUniqueYangs[Random.Range(0, remainingUniqueYangs.Count)];
        }

        // EXTREME FALLBACK: (You have less than 2 Yang cards total in your game)
        return allYangCards[Random.Range(0, allYangCards.Count)];
    }

    private void ChoosePair(CardSO yin, CardSO yang)
    {
        RunManager.Instance.ApplyCard(yin);
        RunManager.Instance.ApplyCard(yang);

        draftUIPanel.SetActive(false);

        LevelManager.Instance.LoadNextLevel();
    }
}