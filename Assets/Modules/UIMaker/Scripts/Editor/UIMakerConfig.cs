using System.Collections.Generic;
using UnityEngine;
namespace Modules.UIMaker
{
    [CreateAssetMenu(fileName = "UIMakerConfig", menuName = "UIMaker/UIMakerConfig", order = 1)]
    public class UIMakerConfig : ScriptableObject
    {
        public List<GameObject> prefabList = new();
    }

}

