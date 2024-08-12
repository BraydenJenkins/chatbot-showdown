using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class AvatarButton : MonoBehaviour
{
    [SerializeField] private Image outline, border, altBorder;
    bool useAlt = false;
    bool selected = false;

    public Image avatarImage;

    private void Awake()
    {
        // 50 50 chance which border to show
        if (Random.value > 0.5f)
        {
            border.color = Color.black;
            altBorder.color = Color.clear;
        }
        else
        {
            border.color = Color.clear;
            altBorder.color = Color.black;
            useAlt = true;
        }

        outline.color = Color.black;
    }

    public void SetSelected()
    {
        if (selected)
        {
            return;
        }

        outline.DOColor(Color.white, 0.25f);
        if (useAlt)
        {
            altBorder.DOColor(Color.white, 0.25f);
        }
        else
        {
            border.DOColor(Color.white, 0.25f);
        }

        selected = true;
    }

    public void SetDeselected()
    {
        if (!selected)
        {
            return;
        }

        outline.DOColor(Color.black, 0.25f);
        if (useAlt)
        {
            altBorder.DOColor(Color.black, 0.25f);
        }
        else
        {
            border.DOColor(Color.black, 0.25f);
        }

        selected = false;
    }
}
