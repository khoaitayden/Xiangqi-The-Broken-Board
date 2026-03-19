using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;

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
    [SerializeField] private RectTransform _draftUIPanel;
    [SerializeField] private Button _tryAgainButton, _winReturnButton, _loseReturnButton;
    [SerializeField] private CanvasGroup _pair1Group; // Upper Pair
    [SerializeField] private CanvasGroup _pair2Group; // Lower Pair
    [SerializeField] private Button _selectPair1Button, _selectPair2Button;
    [SerializeField] private TextMeshProUGUI _p1Yin, _p1Yang, _p2Yin, _p2Yang;

    private CardSO _cP1Yin, _cP1Yang, _cP2Yin, _cP2Yang;
    private VerticalLayoutGroup _draftLayoutGroup;

    private void Awake() { Instance = this; }

    private void Start()
    {
        // Cache the layout group that controls the two card pairs
        if (_pair1Group != null && _pair1Group.transform.parent != null)
        {
            _draftLayoutGroup = _pair1Group.transform.parent.GetComponent<VerticalLayoutGroup>();
        }

        UIManager.Instance.HidePanelInstant(_escPanel);
        UIManager.Instance.HidePanelInstant(_settingsPanel);
        _deathPanel.SetActive(false); _winPanel.SetActive(false); _draftUIPanel.gameObject.SetActive(false);

        _resumeButton.onClick.AddListener(OnResumeClicked);
        _settingsButtonMenu.onClick.AddListener(() => UIManager.Instance.ShowPanel(_settingsPanel));
        _settingsButtonEsc.onClick.AddListener(() => UIManager.Instance.ShowPanel(_settingsPanel));
        _closeSettingsButton.onClick.AddListener(() => UIManager.Instance.HidePanel(_settingsPanel));
        _restartButton.onClick.AddListener(RestartRun);
        _tryAgainButton.onClick.AddListener(RestartRun);
        _returnToMenuButton.onClick.AddListener(ReturnToMenu);
        _winReturnButton.onClick.AddListener(ReturnToMenu);
        _loseReturnButton.onClick.AddListener(ReturnToMenu);

        _selectPair1Button.onClick.AddListener(() => OnDraftChoiceSelected(1));
        _selectPair2Button.onClick.AddListener(() => OnDraftChoiceSelected(2));
    }

    private void Update()
    {
        if (InputHandler.Instance.IsPauseTriggered) TogglePauseMenu();
    }

    private void TogglePauseMenu()
    {
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

        // 1. Show the panel so Unity can calculate layout math
        _draftUIPanel.gameObject.SetActive(true);

        // 2. Turn ON Layout Group and force a UI update so they snap to their correct Y positions instantly in the background
        if (_draftLayoutGroup != null) _draftLayoutGroup.enabled = true;
        Canvas.ForceUpdateCanvases(); 

        // 3. Turn OFF Layout Group so we can freely animate their X positions
        if (_draftLayoutGroup != null) _draftLayoutGroup.enabled = false;

        RectTransform rect1 = _pair1Group.GetComponent<RectTransform>();
        RectTransform rect2 = _pair2Group.GetComponent<RectTransform>();

        // Kill any leftover tweens just in case
        rect1.DOKill();
        rect2.DOKill();

        // 4. Teleport them off-screen instantly
        rect1.anchoredPosition = new Vector2(-1500f, rect1.anchoredPosition.y);
        rect2.anchoredPosition = new Vector2(1500f, rect2.anchoredPosition.y);

        // 5. Ensure they are fully visible but NOT clickable while flying in
        _pair1Group.alpha = 1; _pair1Group.interactable = false; _pair1Group.blocksRaycasts = false;
        _pair2Group.alpha = 1; _pair2Group.interactable = false; _pair2Group.blocksRaycasts = false;

        // 6. Animate them sliding into the center (X = 0)
        Sequence slideInAnim = DOTween.Sequence();
        slideInAnim.Append(rect1.DOAnchorPosX(0f, 0.5f).SetEase(Ease.OutBack));
        slideInAnim.Join(rect2.DOAnchorPosX(0f, 0.5f).SetEase(Ease.OutBack));

        // 7. Once they arrive, make them clickable and turn the layout group back on
        slideInAnim.OnComplete(() => 
        {
            if (_draftLayoutGroup != null) _draftLayoutGroup.enabled = true;

            _pair1Group.interactable = true; _pair1Group.blocksRaycasts = true;
            _pair2Group.interactable = true; _pair2Group.blocksRaycasts = true;
        });
    }

    private void OnDraftChoiceSelected(int chosenPair)
    {
        // Prevent double clicking
        _pair1Group.interactable = false; _pair1Group.blocksRaycasts = false;
        _pair2Group.interactable = false; _pair2Group.blocksRaycasts = false;

        RectTransform rect1 = _pair1Group.GetComponent<RectTransform>();
        RectTransform rect2 = _pair2Group.GetComponent<RectTransform>();

        // Turn off the layout group so we can freely animate their positions sideways
        if (_draftLayoutGroup != null) _draftLayoutGroup.enabled = false;

        Sequence draftAnim = DOTween.Sequence();

        // Upper Card Pair slides off to the Left
        draftAnim.Append(rect1.DOAnchorPosX(-1500f, 0.5f).SetEase(Ease.InBack));
        
        // Lower Card Pair slides off to the Right (happens at the exact same time)
        draftAnim.Join(rect2.DOAnchorPosX(1500f, 0.5f).SetEase(Ease.InBack));

        // Once they are both off-screen, resolve the draft and hide the UI
        draftAnim.OnComplete(() => 
        {
            _draftUIPanel.gameObject.SetActive(false);
            
            if (chosenPair == 1) DraftManager.Instance.ResolveDraftChoice(_cP1Yin, _cP1Yang);
            else DraftManager.Instance.ResolveDraftChoice(_cP2Yin, _cP2Yang);
        });
    }

    public void HideDraftUI() => _draftUIPanel.gameObject.SetActive(false);

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