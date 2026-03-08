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
        // 1. LOCK THE PLAYER FROM SHOOTING!
        TurnManager.Instance.CurrentTurn = TurnManager.TurnState.Drafting;

        p1Yin = allYinCards[Random.Range(0, allYinCards.Count)];
        p1Yang = allYangCards[Random.Range(0, allYangCards.Count)];
        p2Yin = allYinCards[Random.Range(0, allYinCards.Count)];
        p2Yang = allYangCards[Random.Range(0, allYangCards.Count)];

        pair1YinText.text = $"[YIN] {p1Yin.cardName}\n{p1Yin.description}";
        pair1YangText.text = $"[YANG] {p1Yang.cardName}\n{p1Yang.description}";
        pair2YinText.text = $"[YIN] {p2Yin.cardName}\n{p2Yin.description}";
        pair2YangText.text = $"[YANG] {p2Yang.cardName}\n{p2Yang.description}";

        draftUIPanel.SetActive(true);
    }

    private void ChoosePair(CardSO yin, CardSO yang)
    {
        RunManager.Instance.ApplyCard(yin);
        RunManager.Instance.ApplyCard(yang);

        draftUIPanel.SetActive(false);

        LevelManager.Instance.LoadNextLevel();
    }
}