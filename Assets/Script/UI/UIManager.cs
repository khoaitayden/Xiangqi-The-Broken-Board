using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Player UI")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI armorText;
    [SerializeField] private TextMeshProUGUI weaponStatsText; // NEW: Drag your new Text object here

    [Header("Enemy Hover UI")]
    [SerializeField] private GameObject enemyPanel; 
    [SerializeField] private TextMeshProUGUI enemyNameText;
    [SerializeField] private TextMeshProUGUI enemyHPText;
    [SerializeField] private TextMeshProUGUI enemyCooldownText;

    [Header("Draft UI")]
    [SerializeField] private GameObject _draftUIPanel;
    [SerializeField] private TextMeshProUGUI _pair1YinText;
    [SerializeField] private TextMeshProUGUI _pair1YangText;
    [SerializeField] private Button _selectPair1Button;
    [SerializeField] private TextMeshProUGUI _pair2YinText;
    [SerializeField] private TextMeshProUGUI _pair2YangText;
    [SerializeField] private Button _selectPair2Button;

    private CardSO _currentP1Yin, _currentP1Yang, _currentP2Yin, _currentP2Yang;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;
    }

    private void Start()
    {
        _draftUIPanel.SetActive(false);

        _selectPair1Button.onClick.AddListener(OnPair1Clicked);
        _selectPair2Button.onClick.AddListener(OnPair2Clicked);
    }

    void Update()
    {
        UpdatePlayerStats();
        UpdateEnemyHoverInfo();
    }

    private void UpdatePlayerStats()
    {
        PlayerGeneral player = TurnManager.Instance.activePlayer;

        if (player != null)
        {
            ammoText.text = $"Ammo: {player.LoadedAmmo} / {player.MaxAmmo}";
            armorText.text = $"Armor: {player.CurrentArmor}";
            
            // NEW: Show Firepower and Arc
            weaponStatsText.text = $"Firepower: {player.Firepower} Pellets\nSpread Arc: {player.FireArc}°";
        }
        else
        {
            ammoText.text = "Ammo: 0";
            armorText.text = "Armor: 0";
            if (weaponStatsText != null) weaponStatsText.text = "";
        }
    }

    private void UpdateEnemyHoverInfo()
    {
        // THE FIX: If it's not the player's turn, hide the panel and do nothing.
        if (TurnManager.Instance.CurrentTurn != TurnManager.TurnState.PlayerTurn)
        {
            enemyPanel.SetActive(false);
            return;
        }
        
        Vector2 mouseWorldPos = InputHandler.Instance.MouseWorldPosition;
        BoardNode hoveredNode = GridManager.Instance.GetNodeAtPosition(mouseWorldPos);

        if (hoveredNode == null)
        {
            enemyPanel.SetActive(false);
            return;
        }

        if (hoveredNode.currentPiece != null && !hoveredNode.currentPiece.IsPlayer)
        {
            Piece enemy = hoveredNode.currentPiece;
            
            string cleanName = enemy.gameObject.name.Replace("Enemy", "").Replace("(Clone)", "");

            enemyNameText.text = cleanName.ToUpper();
            enemyHPText.text = $"HP: {enemy.CurrentHp} / {enemy.MaxHp}";
            enemyCooldownText.text = $"Cooldown: {enemy.CurrentCooldown}";
            
            enemyPanel.SetActive(true);
        }
        else if (hoveredNode.currentCorpse != null)
        {
            Corpse corpse = hoveredNode.currentCorpse;
            
            enemyNameText.text = "CORPSE";
            enemyHPText.text = $"Fades in: {corpse.turnsRemaining} turns";
            enemyCooldownText.text = ""; 
            
            enemyPanel.SetActive(true);
        }
        else
        {
            enemyPanel.SetActive(false);
        }
    }

    public void ShowDraftUI(CardSO p1Yin, CardSO p1Yang, CardSO p2Yin, CardSO p2Yang)
    {
        // Save references for the buttons
        _currentP1Yin = p1Yin; _currentP1Yang = p1Yang;
        _currentP2Yin = p2Yin; _currentP2Yang = p2Yang;

        // Update Text
        _pair1YinText.text = $"{p1Yin.cardName}\n{p1Yin.description}";
        _pair1YangText.text = $"{p1Yang.cardName}\n{p1Yang.description}";

        _pair2YinText.text = $"{p2Yin.cardName}\n{p2Yin.description}";
        _pair2YangText.text = $"{p2Yang.cardName}\n{p2Yang.description}";

        _draftUIPanel.SetActive(true);
    }

    public void HideDraftUI()
    {
        _draftUIPanel.SetActive(false);
    }

    private void OnPair1Clicked()
    {
        DraftManager.Instance.ResolveDraftChoice(_currentP1Yin, _currentP1Yang);
    }

    private void OnPair2Clicked()
    {
        DraftManager.Instance.ResolveDraftChoice(_currentP2Yin, _currentP2Yang);
    }
}