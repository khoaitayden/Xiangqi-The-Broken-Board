using UnityEngine;
using UnityEngine.EventSystems; // Required for UI hovering

public class CardHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [HideInInspector] public CardSO assignedCard; 

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (assignedCard != null)
        {
            UIManager.Instance.ShowCardTooltip(assignedCard, transform.position);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (assignedCard != null)
        {
            UIManager.Instance.HideCardTooltip();
        }
    }
}