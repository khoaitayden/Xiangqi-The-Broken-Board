using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

public class MenuUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private CanvasGroup _mainMenuPanel;
    [SerializeField] private CanvasGroup _inputNamePanel;
    [SerializeField] private CanvasGroup _leaderboardPanel;
    [SerializeField] private Transform _leaderboardContent;
    [SerializeField] private GameObject _leaderboardEntryPrefab;

    [Header("Buttons")]
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _leaderboardButton;
    [SerializeField] private Button _closeLeaderboardButton;
    [SerializeField] private Button _exitButton;
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _backButton;

    [Header("Inputs")]
    [SerializeField] private TMP_InputField _nameInputField;
    [SerializeField] private TMP_InputField _phoneInputField;

    private void Start()
    {
        _startButton.onClick.AddListener(OnStartClicked);
        _leaderboardButton.onClick.AddListener(OnLeaderboardClicked);
        _closeLeaderboardButton.onClick.AddListener(OnCloseLeaderboardClicked);
        _exitButton.onClick.AddListener(() => Application.Quit());
        _playButton.onClick.AddListener(OnPlayClicked);
        _backButton.onClick.AddListener(OnBackClicked);

        _mainMenuPanel.interactable = true; _mainMenuPanel.blocksRaycasts = true;
        _inputNamePanel.interactable = false; _inputNamePanel.blocksRaycasts = false;
        UIManager.Instance.HidePanelInstant(_leaderboardPanel);

        // NEW: Hide the warning tooltip if they click the input field to fix their mistake
        _phoneInputField.onSelect.AddListener((string text) => { GameplayHUD.Instance.HideTooltip(); });
        _nameInputField.onSelect.AddListener((string text) => { GameplayHUD.Instance.HideTooltip(); });
    }

    private void OnStartClicked()
    {
        _mainMenuPanel.interactable = false; _mainMenuPanel.blocksRaycasts = false;
        _inputNamePanel.interactable = true; _inputNamePanel.blocksRaycasts = true;
        UIManager.Instance.menuSliderContainer.DOAnchorPos(new Vector2(-1920f, 0f), UIManager.Instance.tweenDuration).SetEase(Ease.InOutBack);
    }

    private void OnBackClicked()
    {
        _inputNamePanel.interactable = false; _inputNamePanel.blocksRaycasts = false;
        _mainMenuPanel.interactable = true; _mainMenuPanel.blocksRaycasts = true;
        UIManager.Instance.menuSliderContainer.DOAnchorPos(Vector2.zero, UIManager.Instance.tweenDuration).SetEase(Ease.InOutBack);
    }

    private void OnPlayClicked()
    {
        string pName = _nameInputField.text.Trim();
        string pPhone = _phoneInputField.text.Trim();

        // 1. Basic empty check
        if (string.IsNullOrEmpty(pName) || string.IsNullOrEmpty(pPhone))
        {
            if (string.IsNullOrEmpty(pName)) _nameInputField.transform.DOShakePosition(0.3f, new Vector3(10f, 0, 0), 20, 90f);
            if (string.IsNullOrEmpty(pPhone)) _phoneInputField.transform.DOShakePosition(0.3f, new Vector3(10f, 0, 0), 20, 90f);
            return;
        }

        // 2. Strict Security Validation
        ValidationResult validationResult = DataPersistenceManager.Instance.ValidateLogin(pName, pPhone);
        
        switch (validationResult)
        {
            case ValidationResult.NameTaken:
                _nameInputField.transform.DOShakePosition(0.3f, new Vector3(10f, 0, 0), 20, 90f);
                GameplayHUD.Instance.ShowWarningTooltip("LOGIN FAILED", "This name is already registered with a different phone number.", _nameInputField.transform.position);
                return; 

            case ValidationResult.PhoneTaken:
                _phoneInputField.transform.DOShakePosition(0.3f, new Vector3(10f, 0, 0), 20, 90f);
                GameplayHUD.Instance.ShowWarningTooltip("INVALID LOGIN", "This phone number is already registered to a different name.", _phoneInputField.transform.position);
                return; 
                
            case ValidationResult.Success:
                break; 
        }

        // 3. Success! Hide tooltip and save data.
        GameplayHUD.Instance.HideTooltip();

        PlayerPrefs.SetString("PlayerName", pName);
        PlayerPrefs.SetString("PlayerPhone", pPhone);
        PlayerPrefs.Save();

        _inputNamePanel.interactable = false; _inputNamePanel.blocksRaycasts = false;

        UIManager.Instance.menuSliderContainer.DOAnchorPos(new Vector2(-3840, 0), UIManager.Instance.tweenDuration).SetEase(Ease.InOutCubic).OnComplete(() =>
        {
            GameplayHUD.Instance.InitializeBuildLayout();
            RunManager.Instance.ResetEntireRun();
            LevelManager.Instance.StartGame();
        });
    }

    private void OnLeaderboardClicked()
    {
        _mainMenuPanel.interactable = false;
        foreach (Transform child in _leaderboardContent) Destroy(child.gameObject);
        
        List<PlayerRunData> topPlayers = DataPersistenceManager.Instance.GetLeaderboard();
        for (int i = 0; i < topPlayers.Count; i++)
        {
            GameObject entryObj = Instantiate(_leaderboardEntryPrefab, _leaderboardContent);
            entryObj.GetComponent<LeaderboardEntryUI>()?.Setup(i + 1, topPlayers[i]);
        }
        UIManager.Instance.ShowPanel(_leaderboardPanel);
    }

    private void OnCloseLeaderboardClicked()
    {
        UIManager.Instance.HidePanel(_leaderboardPanel);
        _mainMenuPanel.interactable = true;
    }
}