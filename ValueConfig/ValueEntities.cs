using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
            StringType = 0, IntType = 1, FloatType = 2, BooleanType = 3,Vector2Type = 4,Vector3Type = 5
        }

    }

    [System.Serializable]
    public class ValueCollection : List<Value>
    {
        public List<Value> m_Items;

        public ValueCollection(List<Value> valueList)
        {
            m_Items = valueList;
        }

    }



#if UNITY_EDITOR


    [CustomPropertyDrawer(typeof(Value))]
    public class ValuePropertyDrawer : PropertyDrawer
    {
        bool show = true;
        float height = 92;
        float heightLine = 18;
        float space = 5;



        private void OnEnable()
        {
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var currentRect = new Rect(position.x, position.y + heightLine + space, position.width, heightLine);




            PropertyField("Key", "key", property, ref currentRect);


          
                // EditorGUI.indentLevel++;
                // GUI.color = Color.red;
                // EditorGUI.HelpBox(currentRect, "This key is exist.", MessageType.Error);
                // currentRect.y += heightLine + space;

                // GUI.color = Color.white;
                // EditorGUI.indentLevel--;
            

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

            height = currentRect.y - position.y + 18;
            EditorGUI.EndProperty();

        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return height;
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
