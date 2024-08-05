using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ActivityDatabase", menuName = "ActivityDatabase")]
public class ActivityDatabase : ScriptableObject
{
    public RolesActivity[] activities;
}
