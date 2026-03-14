using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using DG.Tweening;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Player UI")]
    [SerializeField] private TextMeshProUGUI _ammoText;
    [SerializeField] private TextMeshProUGUI _weaponStatsText;
    [SerializeField] private Transform _armorLayoutGroup; 
    [SerializeField] private GameObject _armorIconPrefab; 

    [Header("Gameplay UI Panels")]
    [SerializeField] private GameObject _gameplayUIPanel;
    [SerializeField] private GameObject _cardPanel;

    [Header("Enemy Hover UI")]
    [SerializeField] private GameObject enemyPanel; 
    [SerializeField] private TextMeshProUGUI enemyNameText;
    [SerializeField] private TextMeshProUGUI enemyHPText;
    [SerializeField] private TextMeshProUGUI enemyCooldownText;

    [Header("Game Info UI")]
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _turnText;
    [SerializeField] private TextMeshProUGUI _timerText;

    [Header("Draft UI")]
    [SerializeField] private GameObject _draftUIPanel;
    [SerializeField] private TextMeshProUGUI _pair1YinText;
    [SerializeField] private TextMeshProUGUI _pair1YangText;
    [SerializeField] private Button _selectPair1Button;
    [SerializeField] private TextMeshProUGUI _pair2YinText;
    [SerializeField] private TextMeshProUGUI _pair2YangText;
    [SerializeField] private Button _selectPair2Button;

    [Header("Death UI")]
    [SerializeField] private GameObject _deathPanel;
    [SerializeField] private Button _tryAgainButton;
    [SerializeField] private Button _returnToMainMenuButton;

    [Header("Win UI")]
    [SerializeField] private GameObject _winPanel;
    [SerializeField] private Button _winReturnToMenuButton;

    [Header("Active Build UI")]
    [SerializeField] private Transform _yangLayoutGroup; 
    [SerializeField] private GameObject _yangCardPrefab;
    [SerializeField] private Transform _yinLayoutGroup; 
    [SerializeField] private GameObject _yinCardPrefab; 

    [Header("Card Tooltip UI")]
    [SerializeField] private GameObject _tooltipPanel;
    [SerializeField] private TextMeshProUGUI _tooltipTitleText;
    [SerializeField] private TextMeshProUGUI _tooltipDescText;

    [Header("Sliding Menu UI")]
    [SerializeField] private RectTransform _menuSliderContainer;

    [Header("Main Menu UI")]
    [SerializeField] private CanvasGroup _mainMenuPanel;
    [SerializeField] private CanvasGroup _inputNamePanel;
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _leaderboardButton;
    [SerializeField] private Button _exitButton;

    [Header("Input Name UI")]
    [SerializeField] private TMP_InputField _nameInputField;
    [SerializeField] private TMP_InputField _phoneInputField; 
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _backButton;    [SerializeField] private float _tweenDuration = 0.4f;
    private List<CardHoverHandler> _yangCardSlots = new List<CardHoverHandler>(); 
    private List<CardHoverHandler> _yinCardSlots = new List<CardHoverHandler>();
    private List<GameObject> _armorIcons = new List<GameObject>(); 

    private CardSO _currentP1Yin, _currentP1Yang, _currentP2Yin, _currentP2Yang;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;
        
        DOTween.Init();
    }

    private void Start()
    {
        _draftUIPanel.SetActive(false);
        _deathPanel.SetActive(false);


        _selectPair1Button.onClick.AddListener(OnPair1Clicked);
        _selectPair2Button.onClick.AddListener(OnPair2Clicked);
        _tryAgainButton.onClick.AddListener(RestartRun);
        _returnToMainMenuButton.onClick.AddListener(ReturnToMainMenu);
        _winReturnToMenuButton.onClick.AddListener(ReturnToMainMenuFromWin);

        _startButton.onClick.AddListener(OnStartClicked);
        _exitButton.onClick.AddListener(OnExitClicked);
        _playButton.onClick.AddListener(OnPlayClicked);
        _backButton.onClick.AddListener(OnBackClicked);

        //_menuSliderContainer.anchoredPosition = Vector2.zero;

        _mainMenuPanel.interactable = true;
        _mainMenuPanel.blocksRaycasts = true;

        _inputNamePanel.interactable = false; 
        _inputNamePanel.blocksRaycasts = false;

        _phoneInputField.onSelect.AddListener((string text) => { HideCardTooltip(); });
        _nameInputField.onSelect.AddListener((string text) => { HideCardTooltip(); });
    }
    void Update()
    {
        UpdatePlayerStats();
        UpdateEnemyHoverInfo();
        UpdateGameInfo();
    }
    private void UpdateGameInfo()
    {
        // 1. Level Info
        if (LevelManager.Instance != null)
        {
            _levelText.text = $"Floor {LevelManager.Instance.CurrentLevelIndex + 1}";
        }

        // 2. Turn Info
        if (TurnManager.Instance != null)
        {
            _turnText.text = $"Turn: {TurnManager.Instance.CurrentTurnNumber}";
        }

        // 3. Timer Info
        if (RunManager.Instance != null)
        {
            float time = RunManager.Instance.TotalRunTime;
            int minutes = Mathf.FloorToInt(time / 60F);
            int seconds = Mathf.FloorToInt(time - minutes * 60);
            
            // Format as 00:00
            _timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
    private void InitializeBuildLayout()
    {
        // 1. Clear both layouts
        foreach (Transform child in _yangLayoutGroup) Destroy(child.gameObject);
        foreach (Transform child in _yinLayoutGroup) Destroy(child.gameObject); // NEW
        
        _yangCardSlots.Clear();
        _yinCardSlots.Clear(); // NEW

        // 2. Spawn 8 empty Yang slots
        for (int i = 0; i < 8; i++)
        {
            GameObject newSlot = Instantiate(_yangCardPrefab, _yangLayoutGroup);
            Image symbolImage = newSlot.transform.GetChild(0).GetComponent<Image>();
            symbolImage.color = new Color(1, 1, 1, 0); 
            CardHoverHandler hoverHandler = newSlot.GetComponent<CardHoverHandler>();
            hoverHandler.assignedCard = null; 
            _yangCardSlots.Add(hoverHandler);
        }

        // 3. Spawn 8 empty Yin slots 
        for (int i = 0; i < 8; i++)
        {
            GameObject newSlot = Instantiate(_yinCardPrefab, _yinLayoutGroup);
            Image symbolImage = newSlot.transform.GetChild(0).GetComponent<Image>();
            symbolImage.color = new Color(1, 1, 1, 0); 
            CardHoverHandler hoverHandler = newSlot.GetComponent<CardHoverHandler>();
            hoverHandler.assignedCard = null; 
            _yinCardSlots.Add(hoverHandler);
        }

        // 4. Refill cards if we restarted the scene
        if (RunManager.Instance != null)
        {
            foreach (CardSO card in RunManager.Instance.ActiveCards)
            {
                if (card.alignment == CardAlignment.Yang) AddYangCardToUI(card);
                else if (card.alignment == CardAlignment.Yin) AddYinCardToUI(card);
            }
        }
        
        if (_tooltipPanel != null) _tooltipPanel.SetActive(false);
    }
    public void AddYangCardToUI(CardSO card)
    {
        foreach (CardHoverHandler slot in _yangCardSlots)
        {
            if (slot.assignedCard == null) 
            {
                slot.assignedCard = card;

                // 2. Update the image visually
                Image symbolImage = slot.transform.GetChild(0).GetComponent<Image>();
                if (card.cardIcon != null)
                {
                    symbolImage.sprite = card.cardIcon;
                    symbolImage.color = new Color(1, 1, 1, 1); 
                }
                return;
            }
        }
        Debug.LogWarning("Yang Layout is full!");
    }

    public void AddYinCardToUI(CardSO card)
    {
        foreach (CardHoverHandler slot in _yinCardSlots)
        {
            if (slot.assignedCard == null) 
            {
                slot.assignedCard = card;

                Image symbolImage = slot.transform.GetChild(0).GetComponent<Image>();
                if (card.cardIcon != null)
                {
                    symbolImage.sprite = card.cardIcon;
                    symbolImage.color = new Color(1, 1, 1, 1); 
                }
                return;
            }
        }
        Debug.LogWarning("Yin Layout is full!");
    }
    private void UpdatePlayerStats()
    {
        PlayerGeneral player = TurnManager.Instance.activePlayer;

        if (player != null)
        {
            _ammoText.text = $"Ammo: {player.LoadedAmmo} / {player.MaxAmmo}";
            _weaponStatsText.text = $"Firepower: {player.Firepower} Pellets\nSpread Arc: {player.FireArc}°";

            // Update Armor Icons
            UpdateArmorIcons(player.CurrentArmor);
        }
        else
        {
            _ammoText.text = "Ammo: 0";
            if (_weaponStatsText != null) _weaponStatsText.text = "";
            UpdateArmorIcons(0); // Hide all armor when dead
        }
    }

    private void UpdateArmorIcons(int currentArmor)
    {
        // 1. If the player somehow got MORE armor than we have icons for, spawn new ones!
        while (_armorIcons.Count < currentArmor)
        {
            GameObject newIcon = Instantiate(_armorIconPrefab, _armorLayoutGroup);
            _armorIcons.Add(newIcon);
        }

        // 2. Loop through all our spawned icons and turn them ON or OFF
        for (int i = 0; i < _armorIcons.Count; i++)
        {
            if (i < currentArmor)
            {
                _armorIcons[i].SetActive(true); // Player has this armor
            }
            else
            {
                _armorIcons[i].SetActive(false); // Player lost this armor
            }
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

    public void ShowDeathScreen()
    {
        _deathPanel.SetActive(true);
    }

    private void RestartRun()
    {
        DataPersistenceManager.Instance.SaveRunData("Defeat");

        _deathPanel.SetActive(false);
        InitializeBuildLayout(); 
        LevelManager.Instance.StartGame();
    }


    private void ReturnToMainMenu()
    {
        DataPersistenceManager.Instance.SaveRunData("Defeat");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ShowCardTooltip(CardSO card, Vector3 cardPosition)
    {
        if (_tooltipPanel == null) return;

        _tooltipTitleText.text = card.cardName;
        _tooltipDescText.text = card.description;

        if (card.alignment == CardAlignment.Yin)
        {
            _tooltipPanel.transform.position = cardPosition + new Vector3(-300, 0, 0);
        }
        else 
        {
            _tooltipPanel.transform.position = cardPosition + new Vector3(300, 0, 0);
        }

        _tooltipPanel.SetActive(true);
    }
    public void HideCardTooltip()
    {
        if (_tooltipPanel != null) _tooltipPanel.SetActive(false);
    }

    private void OnStartClicked()
    {
        _mainMenuPanel.interactable = false;
        _mainMenuPanel.blocksRaycasts = false;
        _inputNamePanel.interactable = true;
        _inputNamePanel.blocksRaycasts = true;

        _menuSliderContainer.DOAnchorPos(new Vector2(-1920f, 0f), _tweenDuration).SetEase(Ease.InOutBack);
    }

    private void OnBackClicked()
    {
        _inputNamePanel.interactable = false;
        _inputNamePanel.blocksRaycasts = false;
        _mainMenuPanel.interactable = true;
        _mainMenuPanel.blocksRaycasts = true;

        _menuSliderContainer.DOAnchorPos(Vector2.zero, _tweenDuration).SetEase(Ease.InOutBack);
    }

    private void OnPlayClicked()
    {
        string playerName = _nameInputField.text.Trim();
        string playerPhone = _phoneInputField.text.Trim();

        // 1. BASIC VALIDATION: Empty fields
        if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(playerPhone))
        {
            if (string.IsNullOrEmpty(playerName)) 
                _nameInputField.transform.DOShakePosition(0.3f, new Vector3(10f, 0, 0), 20, 90f);
            
            if (string.IsNullOrEmpty(playerPhone)) 
                _phoneInputField.transform.DOShakePosition(0.3f, new Vector3(10f, 0, 0), 20, 90f);
            
            return; 
        }

        // 2. SECURITY VALIDATION: Is this phone number taken by someone else?
        if (DataPersistenceManager.Instance.IsPhoneStolen(playerName, playerPhone))
        {
            // Shake the phone field
            _phoneInputField.transform.DOShakePosition(0.3f, new Vector3(10f, 0, 0), 20, 90f);

            // Show the Tooltip Warning right above the Phone Input Field!
            _tooltipTitleText.text = "INVALID LOGIN";
            _tooltipDescText.text = "This phone number is already registered to a different name.";
            
            // Position tooltip slightly above the Phone input box
            _tooltipPanel.transform.position = _phoneInputField.transform.position + new Vector3(0, 100f, 0);
            _tooltipPanel.SetActive(true);

            return; // STOP! Don't let them play.
        }

        // Hide tooltip if it was showing from a previous error
        HideCardTooltip(); 

        // 3. CHECK DATABASE (For logging)
        if (DataPersistenceManager.Instance.DoesPlayerExist(playerName, playerPhone))
        {
            Debug.Log($"Welcome back, {playerName}! Overwriting previous run.");
        }
        else
        {
            Debug.Log($"Welcome, {playerName}! New profile created.");
        }

        // 4. SAVE TO PREFS & START
        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.SetString("PlayerPhone", playerPhone);
        PlayerPrefs.Save();

        _inputNamePanel.interactable = false;
        _inputNamePanel.blocksRaycasts = false;

        _menuSliderContainer.DOAnchorPos(new Vector2(-1920f, -1080f), _tweenDuration).SetEase(Ease.InOutCubic).OnComplete(() =>
        {
            InitializeBuildLayout(); 
            LevelManager.Instance.StartGame();
        });
    }

    private void OnExitClicked()
    {
        Application.Quit();
    }

    public void ShowWinScreen()
    {
        _winPanel.SetActive(true);
    }

    private void ReturnToMainMenuFromWin()
    {
        DataPersistenceManager.Instance.SaveRunData("Victory");

        ReturnToMainMenu();
    }

}