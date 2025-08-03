using UnityEngine;

namespace Modules.Utilities
{
    /// <summary>
    /// Custom attribute to display a different name in the Inspector
    /// </summary>
    public class DisplayNameAttribute : PropertyAttribute
    {
        public string DisplayName { get; }
        
        public DisplayNameAttribute(string displayName)
        {
            DisplayName = displayName;
        }
    }
}
