using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace Modules.Utilities
{


    [System.Serializable]
    public class Value
    {
        [SerializeField] public string key;
        [SerializeField] public ValueType valueType;
        [SerializeField] public string stringValue;
        [SerializeField] public int intValue;
        [SerializeField] public float floatValue;
        [SerializeField] public bool boolValue;
        [SerializeField] public Vector2 vector2Value;
        [SerializeField] public Vector3 vector3Value;

        public enum ValueType
        {
            StringType = 0, IntType = 1, FloatType = 2, BooleanType = 3, Vector2Type = 4, Vector3Type = 5
        }


    }

    [System.Serializable]
    public class ValueCollection
    {
        [SerializeField] private List<Value> m_Items = new List<Value>();
        public Value this[string key]
        {
            get
            {
                return this.m_Items.FirstOrDefault(x => x.key == key);
            }
        }
        public List<Value> Items
        {
            get
            {
                return this.m_Items;
            }
        }
        public void Add(Value value)
        {
            this.m_Items.Add(value);
        }

        public void AddValue<T>(string key, T value)
        {
            var v = new Value();
            v.key = key;

            if (value is string)
            {
                v.valueType = Value.ValueType.StringType;
                v.stringValue = value as string;
            }
            else if (value is int)
            {
                v.valueType = Value.ValueType.IntType;
                v.intValue = (int)(object)value;
            }
            else if (value is float)
            {
                v.valueType = Value.ValueType.FloatType;
                v.floatValue = (float)(object)value;
            }
            else if (value is bool)
            {
                v.valueType = Value.ValueType.BooleanType;
                v.boolValue = (bool)(object)value;
            }
            else if (value is Vector2)
            {
                v.valueType = Value.ValueType.Vector2Type;
                v.vector2Value = (Vector2)(object)value;
            }
            else if (value is Vector3)
            {
                v.valueType = Value.ValueType.Vector3Type;
                v.vector3Value = (Vector3)(object)value;
            }
            else
            {
                throw new System.Exception("Unsupported type");
            }

            this.m_Items.Add(v);

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



    [CustomPropertyDrawer(typeof(Value))]
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

                var valueTypeProperty = PropertyField("Value Type", "valueType", property, ref currentRect);


                switch ((Value.ValueType)valueTypeProperty.enumValueIndex)
                {
                    case Value.ValueType.StringType:

                        PropertyField("String Value", "stringValue", property, ref currentRect);
                        break;
                    case Value.ValueType.IntType:
                        PropertyField("Int Value", "intValue", property, ref currentRect);
                        break;
                    case Value.ValueType.FloatType:
                        PropertyField("Float Value", "floatValue", property, ref currentRect);
                        break;
                    case Value.ValueType.BooleanType:
                        PropertyField("Bool Value", "boolValue", property, ref currentRect);
                        break;
                    case Value.ValueType.Vector2Type:
                        PropertyField("Vector2 Value", "vector2Value", property, ref currentRect);
                        break;
                    case Value.ValueType.Vector3Type:
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
