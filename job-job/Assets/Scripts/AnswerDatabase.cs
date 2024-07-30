using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AnswerDatabase", menuName = "AnswerDatabase")]
public class AnswerDatabase : ScriptableObject
{
    public List<string> answers;
}
