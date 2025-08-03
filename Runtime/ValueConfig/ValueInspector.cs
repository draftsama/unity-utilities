using System;
using UnityEngine;

namespace Modules.Utilities
{



    [System.Serializable]
    public class ValueInspector
    {
        [SerializeField] public string title;
        [SerializeField] public bool requireRestart;
        [SerializeField] public Variable variable;

        [SerializeField] public string[] stringOptions;
        [SerializeField] public StringViewType stringViewType = StringViewType.TextField;

        public enum StringViewType
        {
             TextField,

            Dropdown
        }
      

    }

}