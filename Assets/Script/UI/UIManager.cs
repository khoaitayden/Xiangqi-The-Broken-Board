using UnityEngine;
using DG.Tweening;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Sliding Animation")]
    public RectTransform menuSliderContainer;
    public float tweenDuration = 0.4f;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;
        DOTween.Init();
    }

    // --- FACADES (So other scripts don't break!) ---
    public void ShowDeathScreen() => SystemUI.Instance.ShowDeathScreen();
    public void ShowWinScreen() => SystemUI.Instance.ShowWinScreen();
    public void ShowDraftUI(CardSO p1Yin, CardSO p1Yang, CardSO p2Yin, CardSO p2Yang) => SystemUI.Instance.ShowDraftUI(p1Yin, p1Yang, p2Yin, p2Yang);
    public void HideDraftUI() => SystemUI.Instance.HideDraftUI();
    public void AddYangCardToUI(CardSO card) => GameplayHUD.Instance.AddYangCardToUI(card);
    public void AddYinCardToUI(CardSO card) => GameplayHUD.Instance.AddYinCardToUI(card);
    public void ShowCardTooltip(CardSO card, Vector3 pos) => GameplayHUD.Instance.ShowCardTooltip(card, pos);
    public void HideCardTooltip() => GameplayHUD.Instance.HideCardTooltip();

    // --- DOTWEEN HELPERS ---
    public void ShowPanel(CanvasGroup panel)
    {
        panel.gameObject.SetActive(true);
        panel.DOKill(); panel.transform.DOKill();
        panel.DOFade(1f, tweenDuration);
        panel.transform.DOScale(Vector3.one, tweenDuration).SetEase(Ease.OutBack).OnComplete(() => {
            panel.interactable = true; panel.blocksRaycasts = true;
        });
    }

    public void HidePanel(CanvasGroup panel)
    {
        panel.interactable = false; panel.blocksRaycasts = false;
        panel.DOKill(); panel.transform.DOKill();
        panel.DOFade(0f, tweenDuration);
        panel.transform.DOScale(Vector3.one * 0.8f, tweenDuration).SetEase(Ease.InBack).OnComplete(() => {
            panel.gameObject.SetActive(false);
        });
    }

    public void HidePanelInstant(CanvasGroup panel)
    {
        panel.alpha = 0; panel.interactable = false; panel.blocksRaycasts = false;
        panel.transform.localScale = Vector3.one * 0.8f; panel.gameObject.SetActive(false);
    }
}