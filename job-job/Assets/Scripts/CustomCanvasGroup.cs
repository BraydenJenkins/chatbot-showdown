using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class CustomCanvasGroup : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    [SerializeField] private float defaultDuration = 0.5f;

    [SerializeField] private bool startVisible = false;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (startVisible)
        {
            canvasGroup.alpha = 1;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        else
        {
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void FadeIn(float duration)
    {
        canvasGroup.DOFade(1, duration);
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }
    public void FadeIn()
    {
        FadeIn(defaultDuration);
    }

    public void FadeOut(float duration)
    {
        canvasGroup.DOFade(0, duration);
        canvasGroup.blocksRaycasts = false;

        StartCoroutine(FadeOutCoroutine(duration));
    }
    private IEnumerator FadeOutCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        canvasGroup.interactable = false;
    }

    public void FadeOut()
    {
        FadeOut(defaultDuration);
    }
}
