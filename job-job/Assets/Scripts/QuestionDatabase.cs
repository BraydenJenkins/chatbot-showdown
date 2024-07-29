using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "QuestionDatabase", menuName = "QuestionDatabase")]
public class QuestionDatabase : ScriptableObject
{
    public List<string> questions;
}
