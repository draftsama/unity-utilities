using System;
using UnityEngine;

namespace Modules.Utilities
{


    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class ReadOnlyFieldAttribute : PropertyAttribute
    {
    }



    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ButtonAttribute : Attribute
    {
    }




}
