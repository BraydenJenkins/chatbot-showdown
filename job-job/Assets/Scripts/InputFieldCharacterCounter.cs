using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_InputField))]
public class InputFieldCharacterCounter : MonoBehaviour
{
    private TMP_InputField inputField;
    [SerializeField] private TMP_Text characterCountText;
    [SerializeField] private int minimumCharactersToShow = 0;

    private int limit;

    private void Awake()
    {
        inputField = GetComponent<TMP_InputField>();
        limit = inputField.characterLimit;
    }

    private void OnEnable()
    {
        inputField.onValueChanged.AddListener(UpdateCharacterCount);
    }

    private void OnDisable()
    {
        inputField.onValueChanged.RemoveListener(UpdateCharacterCount);
    }

    private void UpdateCharacterCount(string value)
    {
        if (value.Length < minimumCharactersToShow)
        {
            characterCountText.text = "";
            return;
        }
        characterCountText.text = value.Length.ToString() + "/" + limit;
    }
}
