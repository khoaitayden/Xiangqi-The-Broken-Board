using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LayoutElement))]
[ExecuteAlways]
public class SyncLayoutWidthToHeight : MonoBehaviour
{
    private LayoutElement layoutElement;
    private RectTransform rectTransform;

    [Header("Aspect Ratio Settings")]
    [Tooltip("Calculated as Width / Height. For 576x640, this is 0.9")]
    [SerializeField] private float widthToHeightRatio = 576f / 640f; 

    void Awake()
    {
        layoutElement = GetComponent<LayoutElement>();
        rectTransform = GetComponent<RectTransform>();
    }

    // Fires automatically whenever UGUI recalculates screen or layout sizes
    void OnRectTransformDimensionsChange()
    {
        if (layoutElement == null || rectTransform == null) return;

        float currentHeight = rectTransform.rect.height;
        
        float targetWidth = currentHeight * widthToHeightRatio;

        if (Mathf.Abs(layoutElement.preferredWidth - targetWidth) > 0.1f)
        {
            layoutElement.preferredWidth = targetWidth;
        }
    }
}