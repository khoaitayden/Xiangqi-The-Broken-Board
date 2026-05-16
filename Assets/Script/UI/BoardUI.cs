using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LayoutElement))]
[ExecuteAlways]
public class SyncLayoutWidthToHeight : MonoBehaviour
{
    private LayoutElement layoutElement;
    private RectTransform rectTransform;

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

        // Only update if the value changed to prevent infinite layout loops
        if (Mathf.Abs(layoutElement.preferredWidth - currentHeight) > 0.1f)
        {
            layoutElement.preferredWidth = currentHeight;
        }
    }
}