using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InfoPopup : MonoBehaviour
{
    public Button openButton;
    public GameObject popupPanel;
    public Button closeButton;
    public Button backdropButton;

    public int popupSortOrder = 100;

    void Awake()
    {
        if (popupPanel != null)
            popupPanel.SetActive(false);

        if (openButton != null)
            openButton.onClick.AddListener(Show);

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (backdropButton != null)
            backdropButton.onClick.AddListener(Hide);
    }

    void Update()
    {
        if (popupPanel != null && popupPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            Hide();
    }

    public void Show()
    {
        if (popupPanel == null) return;
        popupPanel.SetActive(true);
        var canvas = popupPanel.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.overrideSorting)
            canvas.sortingOrder = popupSortOrder;
        else if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = popupSortOrder;
        }
    }

    public void Hide()
    {
        if (popupPanel != null)
            popupPanel.SetActive(false);
    }
}
