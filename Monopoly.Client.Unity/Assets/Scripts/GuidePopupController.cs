using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuidePopupController : MonoBehaviour
{
    [Serializable]
    public class GuidePage
    {
        public string Title;

        [TextArea(4, 12)]
        public string Content;
    }

    [Header("Popup")]
    [SerializeField] private GameObject panelGuidePopup;

    [Header("Text")]
    [SerializeField] private TMP_Text txtTitle;
    [SerializeField] private TMP_Text txtContent;
    [SerializeField] private TMP_Text txtPageNumber;

    [Header("Buttons")]
    [SerializeField] private Button btnOpen;
    [SerializeField] private Button btnBackdropClose;
    [SerializeField] private Button btnPrevious;
    [SerializeField] private Button btnNext;

    [Header("Guide Pages")]
    [SerializeField] private GuidePage[] pages;

    private int currentPageIndex = 0;

    private void Start()
    {
        if (btnOpen != null)
            btnOpen.onClick.AddListener(OpenGuide);

        if (btnBackdropClose != null)
            btnBackdropClose.onClick.AddListener(CloseGuide);

        if (btnPrevious != null)
            btnPrevious.onClick.AddListener(PreviousPage);

        if (btnNext != null)
            btnNext.onClick.AddListener(NextPage);

        if (panelGuidePopup != null)
            panelGuidePopup.SetActive(false);
    }

    public void OpenGuide()
    {
        currentPageIndex = 0;

        if (panelGuidePopup != null)
            panelGuidePopup.SetActive(true);

        ShowPage();
    }

    public void CloseGuide()
    {
        if (panelGuidePopup != null)
            panelGuidePopup.SetActive(false);
    }

    public void PreviousPage()
    {
        if (pages == null || pages.Length == 0)
            return;

        currentPageIndex--;

        if (currentPageIndex < 0)
            currentPageIndex = pages.Length - 1;

        ShowPage();
    }

    public void NextPage()
    {
        if (pages == null || pages.Length == 0)
            return;

        currentPageIndex++;

        if (currentPageIndex >= pages.Length)
            currentPageIndex = 0;

        ShowPage();
    }

    private void ShowPage()
    {
        if (pages == null || pages.Length == 0)
        {
            if (txtTitle != null)
                txtTitle.text = "HƯỚNG DẪN CHƠI";

            if (txtContent != null)
                txtContent.text = "Chưa có nội dung hướng dẫn.";

            if (txtPageNumber != null)
                txtPageNumber.text = "0/0";

            return;
        }

        GuidePage page = pages[currentPageIndex];

        if (txtTitle != null)
            txtTitle.text = page.Title;

        if (txtContent != null)
            txtContent.text = page.Content;

        if (txtPageNumber != null)
            txtPageNumber.text = $"{currentPageIndex + 1}/{pages.Length}";

        bool hasMultiplePages = pages.Length > 1;

        if (btnPrevious != null)
            btnPrevious.interactable = hasMultiplePages;

        if (btnNext != null)
            btnNext.interactable = hasMultiplePages;
    }
}