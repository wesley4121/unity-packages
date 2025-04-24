using System.Collections.Generic;
using UnityEngine;
namespace Modules.UIMaker
{
    [CreateAssetMenu(fileName = "UISpawnerConfig", menuName = "UISpawner/UISpawnerConfig", order = 1)]
    public class UISpawnerConfig : ScriptableObject
    {
        public List<GameObject> prefabList = new();
    }

}

