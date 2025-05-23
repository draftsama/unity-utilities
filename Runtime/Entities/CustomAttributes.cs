using System;
using System.Collections.Generic;
using System.Reflection;
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

    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class DropdownFieldAttribute : PropertyAttribute
    {
        public Dictionary<string, string> m_Options;


        public DropdownFieldAttribute(Type type)
        {
            m_Options = GetStringConstants(type);

        }

         private Dictionary<string, string> GetStringConstants(Type type)
        {
            // Get all the fields in the class
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
            Dictionary<string, string> constants = new Dictionary<string, string>();

            foreach (var field in fields)
            {
                // Check if the field is a string constant
                if (field.FieldType == typeof(string) && field.IsLiteral && !field.IsInitOnly)
                {
                    constants.Add(field.Name, (string)field.GetValue(null));
                }
            }

            return constants;
        }
    }



}
