using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace Modules.Utilities
{


    [System.Serializable]
    public class Variable
    {
        [SerializeField] public string key;
        [SerializeField] public Type type;
        [SerializeField] public string stringValue;
        [SerializeField] public int intValue;
        [SerializeField] public float floatValue;
        [SerializeField] public bool boolValue;
        [SerializeField] public Vector2 vector2Value;
        [SerializeField] public Vector3 vector3Value;
        public enum Type
        {
            String = 0, Int = 1, Float = 2, Boolean = 3, Vector2 = 4, Vector3 = 5
        }


        public static Type GetType<T>(T _value)
        {
            if (_value is string)
            {
                return Type.String;
            }
            else if (_value is int)
            {
                return Type.Int;
            }
            else if (_value is float)
            {
                return Type.Float;
            }
            else if (_value is bool)
            {
                return Type.Boolean;
            }
            else if (_value is Vector2)
            {
                return Type.Vector2;
            }
            else if (_value is Vector3)
            {
                return Type.Vector3;
            }
            else
            {
                throw new System.Exception("Unsupported type");
            }
        }

        public void Set(object value)
        {
            if (value is string)
            {
                this.type = Type.String;
                this.stringValue = value as string;
            }
            else if (value is int)
            {
                this.type = Type.Int;
                this.intValue = (int)(object)value;
            }
            else if (value is float)
            {
                this.type = Type.Float;
                this.floatValue = (float)(object)value;
            }
            else if (value is bool)
            {
                this.type = Type.Boolean;
                this.boolValue = (bool)(object)value;
            }
            else if (value is Vector2)
            {
                this.type = Type.Vector2;
                this.vector2Value = (Vector2)(object)value;
            }
            else if (value is Vector3)
            {
                this.type = Type.Vector3;
                this.vector3Value = (Vector3)(object)value;
            }
        }
        public T Get<T>()
        {
            object value = type switch
            {
                Type.String => stringValue,
                Type.Int => intValue,
                Type.Float => floatValue,
                Type.Boolean => boolValue,
                Type.Vector2 => vector2Value,
                Type.Vector3 => vector3Value,
                _ => throw new Exception("Unsupported type"),
            };

           //cast the value to the requested type
            return (T)Convert.ChangeType(value, typeof(T));
        }

        
    }

    [System.Serializable]
    public class VariableCollection
    {
        [SerializeField] private List<Variable> m_Items = new List<Variable>();
        public Variable this[string key]
        {
            get
            {
                return this.m_Items.FirstOrDefault(x => x.key == key);
            }
        }
        public List<Variable> Items
        {
            get
            {
                return this.m_Items;
            }
        }

        public int Count
        {
            get
            {
                return this.m_Items.Count;
            }
        }

        public void AddVariable(Variable value)
        {
            var existingValue = this.m_Items.Find(x => x.key == value.key);
            if (existingValue != null)
            {
                this.m_Items.Remove(existingValue);
            }
            this.m_Items.Add(value);
        }

        public T GetValue<T>(string key, T defaultValue = default(T))

        {
            var valueItem = this.m_Items.Find(x => x.key == key);
            if (valueItem != null)
            {
                return valueItem.Get<T>();

            }
            else
            {
                return defaultValue;
            }
        }

        public bool TryGetValue<T>(string key, out T value)
        {
            value = GetValue<T>(key);
            return !EqualityComparer<T>.Default.Equals(value, default(T));
        }

        public bool SetValue<T>(string key, T value)
        {
            try
            {
                var valueItem = this.m_Items.Find(x => x.key == key);

                if (valueItem == null)
                {
                    // Create a new Value item if it doesn't exist
                    valueItem = new Variable();
                    valueItem.key = key;
                    this.m_Items.Add(valueItem);
                }
                // Set the value based on its type
                valueItem.Set(value);

                return true;



            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return false;
            }
        }

        public bool Remove(string key)
        {
            var item = this.m_Items.FirstOrDefault(x => x.key == key);
            if (item != null)
            {
                this.m_Items.Remove(item);
                return true;
            }
            return false;
        }
        public void Clear()
        {
            this.m_Items.Clear();
        }
        public bool ContainsKey(string key)
        {
            return this.m_Items.Any(x => x.key == key);
        }

        public VariableCollection CloneCollection()
        {
            VariableCollection clone = new VariableCollection();
            foreach (var item in this.m_Items)
            {
                Variable newItem = new Variable();
                newItem.key = item.key;
                newItem.type = item.type;
                newItem.stringValue = item.stringValue;
                newItem.intValue = item.intValue;
                newItem.floatValue = item.floatValue;
                newItem.boolValue = item.boolValue;
                newItem.vector2Value = item.vector2Value;
                newItem.vector3Value = item.vector3Value;

                clone.m_Items.Add(newItem);
            }
            return clone;
        }


    }





#if UNITY_EDITOR
    // [CustomEditor(typeof(ValueCollection))]
    // public class ValueCollectionEditor : Editor
    // {
    //     public override void OnInspectorGUI()
    //     {
    //         base.OnInspectorGUI();

    //         if (GUI.changed)
    //         {
    //         }
    //     }
    // }



    [CustomPropertyDrawer(typeof(Variable))]
    public class ValuePropertyDrawer : PropertyDrawer
    {
        float height = 92;
        float heightLine = 18;
        float space = 5;


        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {

            EditorGUI.BeginProperty(position, label, property);

            var currentRect = new Rect(position.x, position.y, position.width, heightLine);


            property.isExpanded = EditorGUI.Foldout(currentRect, property.isExpanded, label);
            currentRect.y += heightLine + space;


            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                PropertyField("Key", "key", property, ref currentRect);

                var valueTypeProperty = PropertyField("Value Type", "type", property, ref currentRect);


                switch ((Variable.Type)valueTypeProperty.enumValueIndex)
                {
                    case Variable.Type.String:

                        PropertyField("String Value", "stringValue", property, ref currentRect);
                        break;
                    case Variable.Type.Int:
                        PropertyField("Int Value", "intValue", property, ref currentRect);
                        break;
                    case Variable.Type.Float:
                        PropertyField("Float Value", "floatValue", property, ref currentRect);
                        break;
                    case Variable.Type.Boolean:
                        PropertyField("Bool Value", "boolValue", property, ref currentRect);
                        break;
                    case Variable.Type.Vector2:
                        PropertyField("Vector2 Value", "vector2Value", property, ref currentRect);
                        break;
                    case Variable.Type.Vector3:
                        PropertyField("Vector3 Value", "vector3Value", property, ref currentRect);
                        break;
                }
                EditorGUI.indentLevel--;
                height = currentRect.y - position.y + 18;

            }

            EditorGUI.EndProperty();

            if (GUI.changed)
            {
                // property.serializedObject.ApplyModifiedProperties();
                // EditorUtility.SetDirty(property.serializedObject.targetObject);

            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {

            return property.isExpanded ? height : heightLine;
        }

        SerializedProperty PropertyField(string label, string key, SerializedProperty propertyParent, ref Rect position)
        {
            var property = propertyParent.FindPropertyRelative(key);
            var rect = new Rect(position.x, position.y, position.width, position.height);
            EditorGUI.PropertyField(rect, property, new GUIContent(label));
            position.y += position.height + space;

            return property;
        }
    }
#endif

}
