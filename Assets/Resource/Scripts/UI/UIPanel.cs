using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public abstract class UIPanel : MonoBehaviour
{
    [SerializeField] protected UIPanelType panelType = UIPanelType.None;
    [SerializeField] protected bool hideOnStart = true;

    protected CanvasGroup canvasGroup { get; private set; }
    public UIPanelType Type => panelType;
    public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0f;

    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    protected virtual void Start()
    {
        if (hideOnStart) Hide(true);
    }

    public virtual void Show(bool instant = false)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        gameObject.SetActive(true);
    }

    public virtual void Hide(bool instant = false)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    public virtual void OnFocusGained() { }
    public virtual void OnFocusLost() { }
}
