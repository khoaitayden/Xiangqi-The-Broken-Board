using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class SystemUI : MonoBehaviour
{
    public static SystemUI Instance { get; private set; }

    [Header("Pause & Settings")]
    [SerializeField] private CanvasGroup _escPanel;
    [SerializeField] private CanvasGroup _settingsPanel;
    [SerializeField] private Button _resumeButton, _settingsButtonMenu, _settingsButtonEsc, _closeSettingsButton, _restartButton, _returnToMenuButton;

    [Header("Post-Game & Draft")]
    [SerializeField] private GameObject _deathPanel;
    [SerializeField] private GameObject _winPanel;
    [SerializeField] private GameObject _draftUIPanel;
    [SerializeField] private Button _tryAgainButton, _winReturnButton, _loseReturnButton;
    [SerializeField] private Button _selectPair1Button, _selectPair2Button;
    [SerializeField] private TextMeshProUGUI _p1Yin, _p1Yang, _p2Yin, _p2Yang;

    private CardSO _cP1Yin, _cP1Yang, _cP2Yin, _cP2Yang;

    private void Awake() { Instance = this; }

    private void Start()
    {
        UIManager.Instance.HidePanelInstant(_escPanel);
        UIManager.Instance.HidePanelInstant(_settingsPanel);
        _deathPanel.SetActive(false); _winPanel.SetActive(false); _draftUIPanel.SetActive(false);

        _resumeButton.onClick.AddListener(OnResumeClicked);
        _settingsButtonMenu.onClick.AddListener(() => UIManager.Instance.ShowPanel(_settingsPanel));
        _settingsButtonEsc.onClick.AddListener(() => UIManager.Instance.ShowPanel(_settingsPanel));
        _closeSettingsButton.onClick.AddListener(() => UIManager.Instance.HidePanel(_settingsPanel));
        _restartButton.onClick.AddListener(RestartRun);
        _tryAgainButton.onClick.AddListener(RestartRun);
        _returnToMenuButton.onClick.AddListener(ReturnToMenu);
        _winReturnButton.onClick.AddListener(ReturnToMenu);
        _loseReturnButton.onClick.AddListener(ReturnToMenu);


        _selectPair1Button.onClick.AddListener(() => DraftManager.Instance.ResolveDraftChoice(_cP1Yin, _cP1Yang));
        _selectPair2Button.onClick.AddListener(() => DraftManager.Instance.ResolveDraftChoice(_cP2Yin, _cP2Yang));
    }

    private void Update()
    {
        if (InputHandler.Instance.IsPauseTriggered) TogglePauseMenu();
    }

    private void TogglePauseMenu()
    {
        // THE FIX: Do not allow pausing unless we are actively playing!
        TurnManager.TurnState state = TurnManager.Instance.CurrentTurn;
        if (state == TurnManager.TurnState.MainMenu || state == TurnManager.TurnState.GameOver || state == TurnManager.TurnState.Drafting) return;

        if (_settingsPanel.gameObject.activeSelf) { UIManager.Instance.HidePanel(_settingsPanel); return; }

        if (_escPanel.gameObject.activeSelf) OnResumeClicked();
        else
        {
            TurnManager.Instance.PauseGame();
            UIManager.Instance.ShowPanel(_escPanel);
        }
    }

    private void OnResumeClicked()
    {
        UIManager.Instance.HidePanel(_escPanel);
        TurnManager.Instance.ResumeGame();
    }

    public void ShowDeathScreen() => _deathPanel.SetActive(true);
    public void ShowWinScreen() => _winPanel.SetActive(true);

    public void ShowDraftUI(CardSO p1Yin, CardSO p1Yang, CardSO p2Yin, CardSO p2Yang)
    {
        _cP1Yin = p1Yin; _cP1Yang = p1Yang; _cP2Yin = p2Yin; _cP2Yang = p2Yang;
        _p1Yin.text = $"{p1Yin.cardName}\n{p1Yin.description}";
        _p1Yang.text = $"{p1Yang.cardName}\n{p1Yang.description}";
        _p2Yin.text = $"{p2Yin.cardName}\n{p2Yin.description}";
        _p2Yang.text = $"{p2Yang.cardName}\n{p2Yang.description}";
        _draftUIPanel.SetActive(true);
    }
    public void HideDraftUI() => _draftUIPanel.SetActive(false);

    private void RestartRun()
    {
        DataPersistenceManager.Instance.SaveRunData("Defeat");
        RunManager.Instance.ResetEntireRun();
        _deathPanel.SetActive(false);
        UIManager.Instance.HidePanel(_escPanel);
        GameplayHUD.Instance.InitializeBuildLayout();
        LevelManager.Instance.StartGame();
    }

    private void ReturnToMenu()
    {
        DataPersistenceManager.Instance.SaveRunData("Defeat");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}