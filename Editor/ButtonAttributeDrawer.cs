
using System;
using System.Reflection;
using CustomAttributes;
using UnityEditor;
using UnityEngine;



[CustomEditor(typeof(MonoBehaviour), true)]
public class ButtonEditor : Editor
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

