using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RolesActivity : MonoBehaviour
{
    public Transform navTarget;

    public string activityDescription;
    public string roleName;
    public string conversationStarter;

    // quick hack to put the avatar without needing to make any other prefab changes, but we will want to do this better later
    public int avatarIndex;
}
