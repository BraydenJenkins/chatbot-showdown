using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AvatarDatabase", menuName = "AvatarDatabase")]
public class AvatarDatabase : ScriptableObject
{
    public ARAI.Avatar[] avatars;
}
