using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Modules.Utilities;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.Events;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif






#if UNITY_EDITOR
using UnityEditor;
#endif
public class UIValueConfig : Singleton<UIValueConfig>
{

    [SerializeField][HideInInspector] private ValueInspector[] m_Data;

    [SerializeField] private CanvasGroup m_ContentCanvasGroup;

    [SerializeField] public TextMeshProUGUI m_HeaderText;
    [SerializeField] public KeyCode m_OpenKey = KeyCode.C;

    [SerializeField] private string m_Password = "112233";

    [SerializeField] private CanvasGroup m_ProtectedCanvasGroup;
    [SerializeField] private TMP_InputField m_PasswordInputField;
    [SerializeField] private Button m_SubmitPasswordButton;
    [SerializeField] private Button m_SaveButton;

    [SerializeField] private RectTransform m_ContainerRt;
    [SerializeField] private GameObject m_ValuePrefab;

    List<UIVariable> m_VariableList = new List<UIVariable>();


    bool m_IsOpen = false;

    private int tapCount = 0;
    private float lastTap = -1;


    public UnityEvent OnSaved = new UnityEvent();

#if ENABLE_INPUT_SYSTEM
    InputAction m_KeyboardAction;
    InputAction m_PointerAction;
#endif


    protected override void Awake()
    {
        base.Awake();
        m_ContentCanvasGroup.SetAlpha(0f);
    }
    
    void OnDestroy()
    {
        // Cleanup InputActions when object is destroyed
#if ENABLE_INPUT_SYSTEM
        m_KeyboardAction?.Disable();
        m_KeyboardAction?.Dispose();
        
        m_PointerAction?.Disable();
        m_PointerAction?.Dispose();
#endif
    }
    
    

    public bool OnKeyDown(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (m_KeyboardAction != null && m_KeyboardAction.WasPressedThisFrame())
        {
            return true;
        }
        
        // Alternative approach using Keyboard directly
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return key switch
            {
                KeyCode.A => keyboard.aKey.wasPressedThisFrame,
                KeyCode.B => keyboard.bKey.wasPressedThisFrame,
                KeyCode.C => keyboard.cKey.wasPressedThisFrame,
                KeyCode.D => keyboard.dKey.wasPressedThisFrame,
                KeyCode.E => keyboard.eKey.wasPressedThisFrame,
                KeyCode.F => keyboard.fKey.wasPressedThisFrame,
                KeyCode.G => keyboard.gKey.wasPressedThisFrame,
                KeyCode.H => keyboard.hKey.wasPressedThisFrame,
                KeyCode.I => keyboard.iKey.wasPressedThisFrame,
                KeyCode.J => keyboard.jKey.wasPressedThisFrame,
                KeyCode.K => keyboard.kKey.wasPressedThisFrame,
                KeyCode.L => keyboard.lKey.wasPressedThisFrame,
                KeyCode.M => keyboard.mKey.wasPressedThisFrame,
                KeyCode.N => keyboard.nKey.wasPressedThisFrame,
                KeyCode.O => keyboard.oKey.wasPressedThisFrame,
                KeyCode.P => keyboard.pKey.wasPressedThisFrame,
                KeyCode.Q => keyboard.qKey.wasPressedThisFrame,
                KeyCode.R => keyboard.rKey.wasPressedThisFrame,
                KeyCode.S => keyboard.sKey.wasPressedThisFrame,
                KeyCode.T => keyboard.tKey.wasPressedThisFrame,
                KeyCode.U => keyboard.uKey.wasPressedThisFrame,
                KeyCode.V => keyboard.vKey.wasPressedThisFrame,
                KeyCode.W => keyboard.wKey.wasPressedThisFrame,
                KeyCode.X => keyboard.xKey.wasPressedThisFrame,
                KeyCode.Y => keyboard.yKey.wasPressedThisFrame,
                KeyCode.Z => keyboard.zKey.wasPressedThisFrame,
                KeyCode.Space => keyboard.spaceKey.wasPressedThisFrame,
                KeyCode.Return => keyboard.enterKey.wasPressedThisFrame,
                KeyCode.Escape => keyboard.escapeKey.wasPressedThisFrame,
                _ => false
            };
        }
#else
        // Only use legacy input if Input System is not available
        return UnityEngine.Input.GetKeyDown(key);
#endif
        return false;
    }

    public bool OnPointerDown()
    {
#if ENABLE_INPUT_SYSTEM
        if (m_PointerAction != null && m_PointerAction.WasPressedThisFrame())
        {
            return true;
        }
        
        // Alternative approach using Mouse directly
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            return true;
        }
        
        // Check for touch input
        var touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
        {
            return true;
        }
#else
        // Only use legacy input if Input System is not available
        if (Input.GetMouseButtonDown(0))
        {
            return true;
        }
#endif

        return false;
    }

    public Vector2 GetPointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        // Try New Input System first
        if (Pointer.current != null)
        {
            return Pointer.current.position.ReadValue();
        }
        
        // Alternative: Try mouse position
        var mouse = Mouse.current;
        if (mouse != null)
        {
            return mouse.position.ReadValue();
        }
        
        // Alternative: Try touch position
        var touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.isInProgress)
        {
            return touchscreen.primaryTouch.position.ReadValue();
        }
#else
        // Only use legacy input if Input System is not available
        return Input.mousePosition;
#endif
        
        return Vector2.zero;
    }

   
   
    void Start()
    {
        var token = this.GetCancellationTokenOnDestroy();
        m_HeaderText.text = $"Value Config (App v{Application.version})";

#if ENABLE_INPUT_SYSTEM
        // Initialize the open action following KeyCode
        try
        {
            var binding = "Keyboard/" + m_OpenKey.ToString().ToLower();
            m_KeyboardAction = new InputAction(binding: binding);
            m_KeyboardAction.Enable();

            // Support both mouse and touch input
            m_PointerAction = new InputAction(binding: "<Pointer>/press");
            m_PointerAction.Enable();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to initialize Input System actions: {ex.Message}");
        }
#endif

        UniTaskAsyncEnumerable.EveryUpdate().ForEachAsync(_ =>
        {
            if (!m_IsOpen && OnKeyDown(m_OpenKey))
            {
                // Toggle the canvas group visibility
                m_IsOpen = true;
                m_PasswordInputField.text = string.Empty;

                m_ProtectedCanvasGroup.SetAlpha(string.IsNullOrEmpty(m_Password) ? 0f : 1f);
                m_ContentCanvasGroup.LerpAlphaAsync(300, 1f, _token: token).Forget();
            }
            if (!m_IsOpen && OnPointerDown())
            {
                Vector2 pos = GetPointerPosition();
                if (pos.x < 200 && pos.y < 200)
                {
                    if (Time.time - lastTap < 0.3f)
                    {
                        tapCount++;
                    }
                    else
                    {
                        tapCount = 1;
                    }
                    lastTap = Time.time;

                    if (tapCount == 6)
                    {
                        m_IsOpen = true;
                        m_PasswordInputField.text = string.Empty;

                        m_ProtectedCanvasGroup.SetAlpha(string.IsNullOrEmpty(m_Password) ? 0f : 1f);
                        m_ContentCanvasGroup.LerpAlphaAsync(300, 1f, _token: token).Forget();
                        tapCount = 0;
                    }
                }
            }
        }, token);

        m_SaveButton.OnClickAsAsyncEnumerable().ForEachAsync(_ =>
        {
            Debug.Log("Save Config");
            // Save all the values
            bool isRequireRestart = false;
            foreach (var item in m_VariableList)
            {
                if (item.IsModify())
                {
                    isRequireRestart = true;
                    item.ApplyValue();
                }
            }



            m_ContentCanvasGroup.LerpAlphaAsync(300, 0f, _token: token).ContinueWith(() =>
            {
                OnSaved?.Invoke();
                m_IsOpen = false;

            }).Forget();
            
             if (isRequireRestart)
            {
                Application.Quit();
                Debug.Log("Application will restart");
            }
        }, token);


        m_SubmitPasswordButton.OnClickAsAsyncEnumerable().ForEachAsync(_ =>
        {
            if (m_PasswordInputField.text == m_Password)
            {
                m_ProtectedCanvasGroup.SetAlpha(0f);
            }
            else
            {
                m_ContentCanvasGroup.LerpAlphaAsync(300, 0f, _token: token).ContinueWith(() =>
                {
                    m_IsOpen = false;
                }).Forget();
            }
        }, token);


        //load all the values
        for (int i = 0; i < m_Data.Length; i++)
        {

            var v = m_Data[i].variable;
            var variable = ValueConfig.GetVariable(v.key);
            var go = Instantiate(m_ValuePrefab, m_ContainerRt);
            var ui = go.GetComponent<UIVariable>();

            if (variable != null)
            {
                m_Data[i].variable = variable;
                ui.SetData(m_Data[i]);

            }
            else
            {
                var key = v.key;
                switch (v.type)
                {
                    case Variable.Type.String:
                        ValueConfig.SetValue(key, v.stringValue);
                        break;
                    case Variable.Type.Int:
                        ValueConfig.SetValue(key, v.intValue);
                        break;
                    case Variable.Type.Float:
                        ValueConfig.SetValue(key, v.floatValue);
                        break;
                    case Variable.Type.Boolean:
                        ValueConfig.SetValue(key, v.boolValue);
                        break;
                    case Variable.Type.Vector2:
                        ValueConfig.SetValue(key, v.vector2Value);
                        break;
                    case Variable.Type.Vector3:
                        ValueConfig.SetValue(key, v.vector3Value);
                        break;

                }
                m_Data[i].variable = ValueConfig.GetVariable(key);
                ui.SetData(m_Data[i]);

            }
            go.SetActive(true);
            m_VariableList.Add(ui);
        }




    }




}


#if UNITY_EDITOR

[CustomEditor(typeof(UIValueConfig), true)]
public class UIValueConfigEditor : Editor
{
    protected int _InputNameID;
    protected Variable[] _Results;
    string _SearchInput;

    SerializedProperty m_DataProperty;
    void OnEnable()
    {
        _InputNameID = GUIUtility.keyboardControl;
        _SearchInput = string.Empty;
        m_DataProperty = serializedObject.FindProperty("m_Data");
    }
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        serializedObject.Update();

        GUI.color = Color.teal;
        EditorGUILayout.BeginVertical("box");
        GUI.color = Color.white;
        EditorGUILayout.LabelField("Variables", EditorStyles.boldLabel);
        //indent level
        EditorGUI.indentLevel++;
        List<string> keys = new List<string>();
        for (int i = 0; i < m_DataProperty.arraySize; i++)
        {
            var element = m_DataProperty.GetArrayElementAtIndex(i);
            var title = element.FindPropertyRelative("title");
            var requireRestart = element.FindPropertyRelative("requireRestart");
            var variable = element.FindPropertyRelative("variable");
            GUI.color = Color.gray3;
            EditorGUILayout.BeginVertical("box");
            GUI.color = Color.white;

            EditorGUILayout.PropertyField(title);
            EditorGUILayout.PropertyField(requireRestart);
            EditorGUILayout.PropertyField(variable);

            // Show StringViewType and StringOptions for String variables
            var variableType = variable.FindPropertyRelative("type");
        
            if (variableType != null && variableType.intValue == (int)Variable.Type.String)
            {

                var stringValue = variable.FindPropertyRelative("stringValue");
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("String Display Options", EditorStyles.boldLabel);

                var stringViewType = element.FindPropertyRelative("stringViewType");
                var stringOptions = element.FindPropertyRelative("stringOptions");

                if (stringViewType != null)
                {
                    var previousViewType = stringViewType.intValue;
                    EditorGUILayout.PropertyField(stringViewType, new GUIContent("View Type"));
                    
                    // Check if view type changed to Dropdown
                    if (previousViewType != stringViewType.intValue && 
                        stringViewType.intValue == (int)Modules.Utilities.ValueInspector.StringViewType.Dropdown)
                    {
                        // Initialize dropdown options with current string value
                        if (stringOptions != null && stringValue != null)
                        {
                            stringOptions.ClearArray();
                            stringOptions.InsertArrayElementAtIndex(0);
                            stringOptions.GetArrayElementAtIndex(0).stringValue = stringValue.stringValue;
                        }
                    }

                    // Show stringOptions only if Dropdown is selected
                    if (stringOptions != null && stringViewType.intValue == (int)Modules.Utilities.ValueInspector.StringViewType.Dropdown)
                    {
                        // Ensure stringOptions has at least one element and it matches stringValue
                        if (stringOptions.arraySize == 0)
                        {
                            stringOptions.InsertArrayElementAtIndex(0);
                        }
                        
                        // Set first element to match current stringValue
                        if (stringValue != null && stringOptions.arraySize > 0)
                        {
                            var firstOption = stringOptions.GetArrayElementAtIndex(0);
                            if (firstOption.stringValue != stringValue.stringValue)
                            {
                                firstOption.stringValue = stringValue.stringValue;
                            }
                        }
                        
                        EditorGUILayout.PropertyField(stringOptions, new GUIContent("Dropdown Options"), true);
                        
                        // Update stringValue when first option changes
                        if (stringOptions.arraySize > 0)
                        {
                            var firstOption = stringOptions.GetArrayElementAtIndex(0);
                            if (stringValue != null && firstOption.stringValue != stringValue.stringValue)
                            {
                                stringValue.stringValue = firstOption.stringValue;
                            }
                        }
                    }
                }
            }

            var keyProp = variable.FindPropertyRelative("key");
            if (keyProp != null)
                keys.Add(keyProp.stringValue);
            //check for duplicates
            var duplicates = keys.FindIndex(x => x == keyProp.stringValue);
            if (duplicates != -1 && duplicates != i)
            {
                EditorGUILayout.HelpBox("Duplicate key found", MessageType.Error);
            }

            EditorGUILayout.BeginHorizontal();


            //snap to the right
            GUILayout.FlexibleSpace();
            GUI.color = Color.red;
            if (GUILayout.Button("Remove"))
            {
                m_DataProperty.DeleteArrayElementAtIndex(i);
                break;
            }
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
        EditorGUI.indentLevel--;


        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUI.color = Color.green;
        if (GUILayout.Button("New Variable"))
        {
            var length = m_DataProperty.arraySize;
            m_DataProperty.InsertArrayElementAtIndex(length);
            var newElement = m_DataProperty.GetArrayElementAtIndex(length);
            newElement.FindPropertyRelative("title").stringValue = "title value" + length;
            newElement.FindPropertyRelative("variable").FindPropertyRelative("key").stringValue = "key value" + length;
            newElement.FindPropertyRelative("variable").FindPropertyRelative("type").intValue = (int)Variable.Type.String;
            
            // Initialize string display options
            var stringViewType = newElement.FindPropertyRelative("stringViewType");
            if (stringViewType != null)
            {
                stringViewType.intValue = (int)Modules.Utilities.ValueInspector.StringViewType.TextField;
            }
            
            var stringOptions = newElement.FindPropertyRelative("stringOptions");
            if (stringOptions != null)
            {
                stringOptions.ClearArray();
                // If it's a dropdown, add the current string value as first option
                if (stringViewType != null && stringViewType.intValue == (int)Modules.Utilities.ValueInspector.StringViewType.Dropdown)
                {
                    var stringValue = newElement.FindPropertyRelative("variable").FindPropertyRelative("stringValue");
                    if (stringValue != null)
                    {
                        stringOptions.InsertArrayElementAtIndex(0);
                        stringOptions.GetArrayElementAtIndex(0).stringValue = stringValue.stringValue;
                    }
                }
            }
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        //line
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        if (EditorGUI.EndChangeCheck())
            _InputNameID = GUIUtility.keyboardControl;

        _SearchInput = EditorGUILayout.TextField("Search from save:", _SearchInput);

        if (!string.IsNullOrEmpty(_SearchInput))
        {
            Regex regexPattern = new Regex(_SearchInput, RegexOptions.IgnoreCase);

            // Filter out variables whose key already exists in m_DataProperty
            var existingKeys = new HashSet<string>();
            for (int i = 0; i < m_DataProperty.arraySize; i++)
            {
                var element = m_DataProperty.GetArrayElementAtIndex(i);
                var variable = element.FindPropertyRelative("variable");

                var keyProp = variable.FindPropertyRelative("key");
                if (keyProp != null)
                    existingKeys.Add(keyProp.stringValue);
            }

            _Results = ValueConfig.GetVariables()
                .Where(x => regexPattern.IsMatch(x.key) && !existingKeys.Contains(x.key))
                .ToArray();

        }

        if (_Results != null && _Results.Length > 0 && GUIUtility.keyboardControl == _InputNameID)
        {

            EditorGUILayout.BeginVertical("box");

            GUI.color = Color.cyan;
            foreach (var r in _Results)
            {


                if (GUILayout.Button(r.key))
                {
                    var length = m_DataProperty.arraySize;
                    m_DataProperty.InsertArrayElementAtIndex(length);

                    var newElement = m_DataProperty.GetArrayElementAtIndex(length);
                    newElement.FindPropertyRelative("title").stringValue = "title value" + length;
                    newElement.FindPropertyRelative("variable").FindPropertyRelative("key").stringValue = r.key;
                    newElement.FindPropertyRelative("variable").FindPropertyRelative("type").intValue = (int)r.type;
                    
                    // Initialize string display options for string variables
                    if (r.type == Variable.Type.String)
                    {
                        var stringViewType = newElement.FindPropertyRelative("stringViewType");
                        if (stringViewType != null)
                        {
                            stringViewType.intValue = (int)Modules.Utilities.ValueInspector.StringViewType.TextField;
                        }
                        
                        var stringOptions = newElement.FindPropertyRelative("stringOptions");
                        if (stringOptions != null)
                        {
                            stringOptions.ClearArray();
                            // If it's a dropdown, add the current string value as first option
                            if (stringViewType != null && stringViewType.intValue == (int)Modules.Utilities.ValueInspector.StringViewType.Dropdown)
                            {
                                stringOptions.InsertArrayElementAtIndex(0);
                                stringOptions.GetArrayElementAtIndex(0).stringValue = r.stringValue;
                            }
                        }
                    }
                    switch (r.type)
                    {
                        case Variable.Type.String:
                            newElement.FindPropertyRelative("variable").FindPropertyRelative("stringValue").stringValue = r.stringValue;
                            break;
                        case Variable.Type.Int:
                            newElement.FindPropertyRelative("variable").FindPropertyRelative("intValue").intValue = r.intValue;
                            break;
                        case Variable.Type.Float:
                            newElement.FindPropertyRelative("variable").FindPropertyRelative("floatValue").floatValue = r.floatValue;
                            break;
                        case Variable.Type.Boolean:
                            newElement.FindPropertyRelative("variable").FindPropertyRelative("boolValue").boolValue = r.boolValue;
                            break;
                        case Variable.Type.Vector2:
                            newElement.FindPropertyRelative("variable").FindPropertyRelative("vector2Value").vector2Value = r.vector2Value;
                            break;
                        case Variable.Type.Vector3:
                            newElement.FindPropertyRelative("variable").FindPropertyRelative("vector3Value").vector3Value = r.vector3Value;
                            break;

                    }

                    GUIUtility.keyboardControl = 0;

                    _SearchInput = string.Empty;
                    _Results = null;
                    break;

                }
            }
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();

    }
}

#endif
