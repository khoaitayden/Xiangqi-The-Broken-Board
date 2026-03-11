using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class DraftManager : MonoBehaviour
{
    [Header("Card Database")]
    [SerializeField] private List<CardSO> allYinCards;
    [SerializeField] private List<CardSO> allYangCards;

    [Header("UI References")]
    [SerializeField] private GameObject draftUIPanel;
    [SerializeField] private TextMeshProUGUI pair1YinText;
    [SerializeField] private TextMeshProUGUI pair1YangText;
    [SerializeField] private Button selectPair1Button;
    [SerializeField] private TextMeshProUGUI pair2YinText;
    [SerializeField] private TextMeshProUGUI pair2YangText;
    [SerializeField] private Button selectPair2Button;

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
        pair1YinText.text = $"{p1Yin.cardName}\n{p1Yin.description}";
        pair1YangText.text = $"{p1Yang.cardName}\n{p1Yang.description}";

        pair2YinText.text = $"{p2Yin.cardName}\n{p2Yin.description}";
        pair2YangText.text = $"{p2Yang.cardName}\n{p2Yang.description}";

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

        List<CardSO> remainingUniqueYangs = new List<CardSO>();
        foreach (CardSO yang in allYangCards)
        {
            if (!alreadyOfferedCards.Contains(yang)) remainingUniqueYangs.Add(yang);
        }

        if (remainingUniqueYangs.Count > 0)
        {
            return remainingUniqueYangs[Random.Range(0, remainingUniqueYangs.Count)];
        }

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