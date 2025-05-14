using System.Reflection;
using Modules.Utilities;
using UnityEditor;
using UnityEngine;
namespace Modules.Utilities.Editor
{


    [CustomPropertyDrawer(typeof(ReadOnlyFieldAttribute))]
    public class ReadOnlyFieldAttributeDrawer : PropertyDrawer
    {

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label);
            GUI.enabled = true;
        }

    }
    
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class ButtonEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            // Get the target object (the script instance)
            MonoBehaviour monoBehaviour = (MonoBehaviour)target;
            MethodInfo[] methods = monoBehaviour.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                // Check if the method has the [Button] attribute
                if (method.GetCustomAttribute<ButtonAttribute>() != null)
                {
                    if (GUILayout.Button(method.Name))
                    {
                        // Call the method
                        method.Invoke(monoBehaviour, null);
                    }
                }
            }
        }
    }
    

}