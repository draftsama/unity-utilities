using UnityEngine;

namespace Modules.Utilities
{

    [System.Serializable]
    public class ValueInspector
    {
        [SerializeField] public string title;
        [SerializeField] public bool requireRestart;
        [SerializeField] public Variable variable;

    }

}