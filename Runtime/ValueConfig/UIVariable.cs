using System.Collections.Generic;
using Modules.Utilities;
using TMPro;
using UnityEngine;

public class UIVariable : MonoBehaviour
{

    public ValueInspector m_Data;

    [SerializeField] private TextMeshProUGUI m_Title;

    [SerializeField] private List<TMP_InputField> m_InputFields;
    [SerializeField] private TMP_Dropdown m_Dropdowns;


    private void Awake()
    {

    }
    public void SetData(ValueInspector _data)
    {
        var variable = ValueConfig.GetVariable(_data.variable.key);
        m_Data = new ValueInspector();
        m_Data.title = _data.title;
        m_Data.requireRestart = _data.requireRestart;


        if (variable == null)
        {
            m_Data.variable = _data.variable.Clone();
        }
        else
        {
            m_Data.variable = variable;
        }

        foreach (var item in m_InputFields)
        {
            item.gameObject.SetActive(false);
        }
        m_Dropdowns.options.Clear();
        m_Dropdowns.options.Add(new TMP_Dropdown.OptionData("True"));
        m_Dropdowns.options.Add(new TMP_Dropdown.OptionData("False"));
        m_Dropdowns.gameObject.SetActive(false);

        var requireRestartText = "(Restart Required)";
        m_Title.text = $"{m_Data.title}{(m_Data.requireRestart ? requireRestartText : "")}:";


        switch (m_Data.variable.type)
        {
            case Variable.Type.String:
                m_InputFields[0].gameObject.SetActive(true);
                m_InputFields[0].contentType = TMP_InputField.ContentType.Standard;
                m_InputFields[0].text = m_Data.variable.stringValue;

                break;
            case Variable.Type.Int:
                m_InputFields[0].gameObject.SetActive(true);
                m_InputFields[0].contentType = TMP_InputField.ContentType.IntegerNumber;
                m_InputFields[0].text = m_Data.variable.intValue.ToString();
                break;

            case Variable.Type.Float:
                m_InputFields[0].gameObject.SetActive(true);
                m_InputFields[0].contentType = TMP_InputField.ContentType.DecimalNumber;
                m_InputFields[0].text = m_Data.variable.floatValue.ToString();
                break;
            case Variable.Type.Boolean:
                m_Dropdowns.gameObject.SetActive(true);
                m_Dropdowns.value = m_Data.variable.boolValue ? 0 : 1;
                break;
            case Variable.Type.Vector2:
                m_InputFields[0].gameObject.SetActive(true);
                m_InputFields[0].contentType = TMP_InputField.ContentType.DecimalNumber;
                m_InputFields[0].text = m_Data.variable.vector2Value.x.ToString();
                m_InputFields[1].gameObject.SetActive(true);
                m_InputFields[1].contentType = TMP_InputField.ContentType.DecimalNumber;
                m_InputFields[1].text = m_Data.variable.vector2Value.y.ToString();
                break;
            case Variable.Type.Vector3:
                m_InputFields[0].gameObject.SetActive(true);
                m_InputFields[0].contentType = TMP_InputField.ContentType.DecimalNumber;
                m_InputFields[0].text = m_Data.variable.vector3Value.x.ToString();
                m_InputFields[1].gameObject.SetActive(true);
                m_InputFields[1].contentType = TMP_InputField.ContentType.DecimalNumber;
                m_InputFields[1].text = m_Data.variable.vector3Value.y.ToString();
                m_InputFields[2].gameObject.SetActive(true);
                m_InputFields[2].contentType = TMP_InputField.ContentType.DecimalNumber;
                m_InputFields[2].text = m_Data.variable.vector3Value.z.ToString();
                break;

        }

    }

    public bool IsModify()
    {
        if (m_Data == null)
        {
            Debug.LogError("Data is null");
            return false;
        }

        switch (m_Data.variable.type)
        {
            case Variable.Type.String:
                return m_InputFields[0].text != m_Data.variable.stringValue;
                
            case Variable.Type.Int:
                if (int.TryParse(m_InputFields[0].text, out int intValue))
                {
                    return intValue != m_Data.variable.intValue;
                }
                return true; // If parsing fails, consider it modified
                
            case Variable.Type.Float:
                if (float.TryParse(m_InputFields[0].text, out float floatValue))
                {
                    return !Mathf.Approximately(floatValue, m_Data.variable.floatValue);
                }
                return true; // If parsing fails, consider it modified
                
            case Variable.Type.Boolean:
                bool dropdownBoolValue = m_Dropdowns.value == 0;
                return dropdownBoolValue != m_Data.variable.boolValue;
                
            case Variable.Type.Vector2:
                if (float.TryParse(m_InputFields[0].text, out float x2) && 
                    float.TryParse(m_InputFields[1].text, out float y2))
                {
                    Vector2 inputVector2 = new Vector2(x2, y2);
                    return !Mathf.Approximately(inputVector2.x, m_Data.variable.vector2Value.x) ||
                           !Mathf.Approximately(inputVector2.y, m_Data.variable.vector2Value.y);
                }
                return true; // If parsing fails, consider it modified
                
            case Variable.Type.Vector3:
                if (float.TryParse(m_InputFields[0].text, out float x3) && 
                    float.TryParse(m_InputFields[1].text, out float y3) &&
                    float.TryParse(m_InputFields[2].text, out float z3))
                {
                    Vector3 inputVector3 = new Vector3(x3, y3, z3);
                    return !Mathf.Approximately(inputVector3.x, m_Data.variable.vector3Value.x) ||
                           !Mathf.Approximately(inputVector3.y, m_Data.variable.vector3Value.y) ||
                           !Mathf.Approximately(inputVector3.z, m_Data.variable.vector3Value.z);
                }
                return true; // If parsing fails, consider it modified
                
            default:
                return false;
        }
    }

    public void ApplyValue()
    {

        if (m_Data == null)
        {
            Debug.LogError("Data is null");
            return;
        }


        switch (m_Data.variable.type)
        {
            case Variable.Type.String:
                ValueConfig.SetValue(m_Data.variable.key, m_InputFields[0].text);
                break;
            case Variable.Type.Int:
                ValueConfig.SetValue(m_Data.variable.key, int.Parse(m_InputFields[0].text));
                break;

            case Variable.Type.Float:
                ValueConfig.SetValue(m_Data.variable.key, float.Parse(m_InputFields[0].text));
                break;
            case Variable.Type.Boolean:
                ValueConfig.SetValue(m_Data.variable.key, m_Dropdowns.value == 0);
                break;
            case Variable.Type.Vector2:
                ValueConfig.SetValue(m_Data.variable.key, new Vector2(float.Parse(m_InputFields[0].text), float.Parse(m_InputFields[1].text)));
                break;
            case Variable.Type.Vector3:
                ValueConfig.SetValue(m_Data.variable.key, new Vector3(float.Parse(m_InputFields[0].text), float.Parse(m_InputFields[1].text), float.Parse(m_InputFields[2].text)));
                break;



        }

    }

    [System.Serializable]
    public class Data
    {
        [SerializeField] public string title;
        [SerializeField] public Variable variable;
    }


}
