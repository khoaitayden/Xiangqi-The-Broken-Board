using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text.RegularExpressions;
public class GameplayHUD : MonoBehaviour
{
    public static GameplayHUD Instance { get; private set; }

    [Header("Player & Game Info")]
    [SerializeField] private TextMeshProUGUI _weaponStatsText; 
    [SerializeField] private TextMeshProUGUI _enemyAndGameInfoText;
    [SerializeField] private TextMeshProUGUI _playerArmorText;
    [SerializeField] private TextMeshProUGUI _playerArrowText;
    [SerializeField] private Transform _armorLayoutGroup;
    [SerializeField] private Transform _arrowLayoutGroup;

    [Header("Button")]
    [SerializeField] private Button pauseButton;

    [Header("Build Layout & Tooltip")]
    [SerializeField] private Transform _yangLayoutGroup;
    [SerializeField] private Transform _yinLayoutGroup;
    [SerializeField] private GameObject _yangCardPrefab, _yinCardPrefab;
    [SerializeField] private GameObject _tooltipPanel;
    [SerializeField] private TextMeshProUGUI _tooltipTitleText, _tooltipDescText;
    
    private List<CardHoverHandler> _yangCardSlots = new List<CardHoverHandler>();
    private List<CardHoverHandler> _yinCardSlots = new List<CardHoverHandler>();

    private void Awake() { Instance = this; }
    private void Start()
    {
        pauseButton.onClick.AddListener(OnPausedClicked);
    }

    private void Update()
    {
        if (TurnManager.Instance.CurrentTurn == TurnManager.TurnState.MainMenu) return;
        UpdatePlayerStats();
        UpdateEnemyHoverInfo();
        UpdateGameInfo();
    }

    private void UpdateGameInfo()
    {
        if (_enemyAndGameInfoText != null && !UpdateEnemyHoverInfo())
        {
            float time = RunManager.Instance.TotalRunTime;
            _enemyAndGameInfoText.text = $"Floor: {LevelManager.Instance.CurrentLevelIndex + 1}\nTurn: {TurnManager.Instance.CurrentTurnNumber}\n{string.Format("{0:00}:{1:00}", Mathf.FloorToInt(time / 60F), Mathf.FloorToInt(time % 60))}";
        }
    }
    private void OnPausedClicked()
    {
        SystemUI.Instance.TogglePauseMenu();
    }

    private void UpdatePlayerStats()
    {
        PlayerGeneral player = TurnManager.Instance.activePlayer;
        if (player != null)
        {
            UpdateArrowIcons(player.LoadedAmmo);
            _weaponStatsText.text = $"Firepower: {player.Firepower}\nFire Arc\n{player.FireArc}°";
            UpdateArmorIcons(player.CurrentArmor);
        }
        else
        {
            UpdateArrowIcons(player.LoadedAmmo); _weaponStatsText.text = ""; UpdateArmorIcons(0);
        }
    }

    private void UpdateArmorIcons(int currentArmor)
    {
        _playerArmorText.text = $"<sprite=\"Armor\" index=0> X {currentArmor}";
    }

    private void UpdateArrowIcons(int currentArrow)
    {
        _playerArrowText.text = $"<sprite=\"arrow\" index=0> X {currentArrow}";
    }

    private bool UpdateEnemyHoverInfo()
    {
        if (TurnManager.Instance.CurrentTurn != TurnManager.TurnState.PlayerTurn) { return false; }
        
        if (PlayerActionController.Instance == null) return false;
        
        BoardNode selectedNode = PlayerActionController.Instance.SelectedEnemyNode;
        
        if (selectedNode == null) { return false; }

        if (selectedNode.currentPiece != null && !selectedNode.currentPiece.IsPlayer)
        {
            Piece enemy = selectedNode.currentPiece;
            string rawName = enemy.gameObject.name.Replace("Enemy", "").Replace("(Clone)", ""); 
            string formattedName = Regex.Replace(rawName, @"\s*\(.*?\)", "").Trim();
            _enemyAndGameInfoText.text = $"{formattedName}\n{enemy.CurrentHp} / {enemy.MaxHp}\n Cooldown: {enemy.CurrentCooldown}";
            return true;
        }
        else if (selectedNode.currentCorpse != null)
        {
            _enemyAndGameInfoText.text = $"CORPSE\nFades in: {selectedNode.currentCorpse.turnsRemaining} turns";
            return true;
        }
        
        return false;
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
            
            _tooltipTitleText.text = card.cardName; 
            _tooltipDescText.text = card.description;
            
            _tooltipPanel.transform.position = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
            
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