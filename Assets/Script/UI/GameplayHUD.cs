using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class GameplayHUD : MonoBehaviour
{
    public static GameplayHUD Instance { get; private set; }

    [Header("Player & Game Info")]
    [SerializeField] private TextMeshProUGUI _ammoText;
    [SerializeField] private TextMeshProUGUI _weaponStatsText; 
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _turnText;
    [SerializeField] private TextMeshProUGUI _timerText;
    [SerializeField] private Transform _armorLayoutGroup;
    [SerializeField] private GameObject _armorIconPrefab;
    private List<GameObject> _armorIcons = new List<GameObject>();

    [Header("Enemy Hover")]
    [SerializeField] private GameObject _enemyPanel;
    [SerializeField] private TextMeshProUGUI _enemyNameText, _enemyHPText, _enemyCooldownText;

    [Header("Build Layout & Tooltip")]
    [SerializeField] private Transform _yangLayoutGroup;
    [SerializeField] private Transform _yinLayoutGroup;
    [SerializeField] private GameObject _yangCardPrefab, _yinCardPrefab;
    [SerializeField] private GameObject _tooltipPanel;
    [SerializeField] private TextMeshProUGUI _tooltipTitleText, _tooltipDescText;
    
    private List<CardHoverHandler> _yangCardSlots = new List<CardHoverHandler>();
    private List<CardHoverHandler> _yinCardSlots = new List<CardHoverHandler>();

    private void Awake() { Instance = this; }

    private void Update()
    {
        if (TurnManager.Instance.CurrentTurn == TurnManager.TurnState.MainMenu) return;
        UpdatePlayerStats();
        UpdateEnemyHoverInfo();
        UpdateGameInfo();
    }

    private void UpdateGameInfo()
    {
        if (LevelManager.Instance != null) _levelText.text = $"Floor {LevelManager.Instance.CurrentLevelIndex + 1}";
        if (TurnManager.Instance != null) _turnText.text = $"Turn: {TurnManager.Instance.CurrentTurnNumber}";
        if (RunManager.Instance != null)
        {
            float time = RunManager.Instance.TotalRunTime;
            _timerText.text = string.Format("{0:00}:{1:00}", Mathf.FloorToInt(time / 60F), Mathf.FloorToInt(time % 60));
        }
    }

    private void UpdatePlayerStats()
    {
        PlayerGeneral player = TurnManager.Instance.activePlayer;
        if (player != null)
        {
            _ammoText.text = $"Ammo: {player.LoadedAmmo} / {player.MaxAmmo}";
            _weaponStatsText.text = $"Firepower: {player.Firepower} Pellets\nSpread Arc: {player.FireArc}°";
            UpdateArmorIcons(player.CurrentArmor);
        }
        else
        {
            _ammoText.text = "Ammo: 0"; _weaponStatsText.text = ""; UpdateArmorIcons(0);
        }
    }

    private void UpdateArmorIcons(int currentArmor)
    {
        while (_armorIcons.Count < currentArmor) _armorIcons.Add(Instantiate(_armorIconPrefab, _armorLayoutGroup));
        for (int i = 0; i < _armorIcons.Count; i++) _armorIcons[i].SetActive(i < currentArmor);
    }

    private void UpdateEnemyHoverInfo()
    {
        if (TurnManager.Instance.CurrentTurn != TurnManager.TurnState.PlayerTurn) { _enemyPanel.SetActive(false); return; }
        
        BoardNode hoveredNode = GridManager.Instance.GetNodeAtPosition(InputHandler.Instance.MouseWorldPosition);
        if (hoveredNode == null) { _enemyPanel.SetActive(false); return; }

        if (hoveredNode.currentPiece != null && !hoveredNode.currentPiece.IsPlayer)
        {
            Piece enemy = hoveredNode.currentPiece;
            _enemyNameText.text = enemy.gameObject.name.Replace("Enemy", "").Replace("(Clone)", "").ToUpper();
            _enemyHPText.text = $"HP: {enemy.CurrentHp} / {enemy.MaxHp}";
            _enemyCooldownText.text = $"Cooldown: {enemy.CurrentCooldown}";
            _enemyPanel.SetActive(true);
        }
        else if (hoveredNode.currentCorpse != null)
        {
            _enemyNameText.text = "CORPSE";
            _enemyHPText.text = $"Fades in: {hoveredNode.currentCorpse.turnsRemaining} turns";
            _enemyCooldownText.text = ""; 
            _enemyPanel.SetActive(true);
        }
        else _enemyPanel.SetActive(false);
    }

    public void InitializeBuildLayout()
    {
        foreach (Transform child in _yangLayoutGroup) Destroy(child.gameObject);
        foreach (Transform child in _yinLayoutGroup) Destroy(child.gameObject);
        _yangCardSlots.Clear(); _yinCardSlots.Clear();

        for (int i = 0; i < 8; i++) _yangCardSlots.Add(CreateSlot(_yangCardPrefab, _yangLayoutGroup));
        for (int i = 0; i < 8; i++) _yinCardSlots.Add(CreateSlot(_yinCardPrefab, _yinLayoutGroup));

        if (RunManager.Instance != null)
        {
            foreach (CardSO card in RunManager.Instance.ActiveCards)
            {
                if (card.alignment == CardAlignment.Yang) AddYangCardToUI(card);
                else AddYinCardToUI(card);
            }
        }
        if (_tooltipPanel != null) _tooltipPanel.SetActive(false);
    }

    private CardHoverHandler CreateSlot(GameObject prefab, Transform parent)
    {
        GameObject newSlot = Instantiate(prefab, parent);
        newSlot.transform.GetChild(0).GetComponent<Image>().color = new Color(1, 1, 1, 0);
        CardHoverHandler handler = newSlot.GetComponent<CardHoverHandler>();
        handler.assignedCard = null;
        return handler;
    }

    public void AddYangCardToUI(CardSO card) => AssignCardToSlot(card, _yangCardSlots);
    public void AddYinCardToUI(CardSO card) => AssignCardToSlot(card, _yinCardSlots);

    private void AssignCardToSlot(CardSO card, List<CardHoverHandler> slots)
    {
        foreach (var slot in slots)
        {
            if (slot.assignedCard == null)
            {
                slot.assignedCard = card;
                Image img = slot.transform.GetChild(0).GetComponent<Image>();
                if (card.cardIcon != null) { img.sprite = card.cardIcon; img.color = Color.white; }
                return;
            }
        }
    }

    public void ShowCardTooltip(CardSO card, Vector3 pos)
    {
        if (_tooltipPanel == null) return;
        _tooltipTitleText.text = card.cardName; _tooltipDescText.text = card.description;
        _tooltipPanel.transform.position = pos + new Vector3(card.alignment == CardAlignment.Yin ? -300 : 300, 0, 0);
        _tooltipPanel.SetActive(true);
    }

    public void HideCardTooltip() { if (_tooltipPanel != null) _tooltipPanel.SetActive(false); }

    public void ShowWarningTooltip(string title, string message, Vector3 position)
    {
        if (_tooltipPanel == null) return;
        
        _tooltipTitleText.text = title;
        _tooltipDescText.text = message;

        // Pop it directly above the input field
        _tooltipPanel.transform.position = position + new Vector3(0, 100f, 0);
        _tooltipPanel.SetActive(true);
    }

    public void HideTooltip()
    {
        if (_tooltipPanel != null) _tooltipPanel.SetActive(false);
    }
}