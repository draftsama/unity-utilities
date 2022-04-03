using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Modules.Utilities
{
    public class ValueConfig
    {
        private static ValueCollection _Current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {

            var path = Path.Combine(Environment.CurrentDirectory, "value.config.json");
            if (File.Exists(path))
            {
                var jsonString = File.ReadAllText(path);
                JSONObject jsonObject = new JSONObject(jsonString);
                if (jsonObject.Count > 0)
                {

                    _Current = new ValueCollection();

                    for (int i = 0; i < jsonObject.Count; i++)
                    {
                        var item = jsonObject[i];
                        var value = new Value();
                        value.key = item["key"].str;

                        value.valueType = (Value.ValueType)item["valueType"].i;

                        switch (value.valueType)
                        {
                            case Value.ValueType.StringType:
                                value.stringValue = item["value"].str;
                                // Debug.Log($"key : {value.key} value : {value.stringValue}");
                                break;
                            case Value.ValueType.IntType:
                                value.intValue = (int)item["value"].i;
                                // Debug.Log($"key : {value.key} value : {value.intValue}");
                                break;
                            case Value.ValueType.FloatType:
                                value.floatValue = item["value"].f;
                                // Debug.Log($"key : {value.key} value : {value.floatValue}");
                                break;
                            case Value.ValueType.BooleanType:
                                value.boolValue = item["value"].b;
                                // Debug.Log($"key : {value.key} value : {value.boolValue}");
                                break;
                            case Value.ValueType.Vector2Type:
                                // value.vector2Value = new Vector2(item["value"]["x"].f, item["value"]["y"].f);
                                value.vector2Value = JSONTemplates.ToVector2(item["value"]);
                                // Debug.Log($"key : {value.key} value : {value.vector2Value}");
                                break;
                            case Value.ValueType.Vector3Type:
                                value.vector3Value = JSONTemplates.ToVector3(item["value"]);
                                // Debug.Log($"key : {value.key} value : {value.vector3Value}");
                                break;
                        }

                        _Current.Add(value);

                    }
                }

            }
            else
            {
                var asset = Resources.Load<ValueConfigAsset>("Config/ValueConfig");
                ValueConfig.SaveValueConfig(asset.m_ValueCollection);
                _Current = asset.m_ValueCollection;

            }


        }

        public static void SaveCurrentValueConfig(ValueCollection valueCollection)
        {

            if (_Current != null)
                SaveValueConfig(_Current);
            else
                Debug.LogWarning("_Current ValueConfig is null");
        }

        public static void SaveValueConfig(ValueCollection valueCollection)
        {


            JSONObject jsonObject = new JSONObject();
            foreach (var item in valueCollection.Items)
            {
                switch (item.valueType)
                {
                    case Value.ValueType.StringType:
                        JSONObject valueJson = new JSONObject();
                        valueJson.AddField("key", item.key);
                        valueJson.AddField("valueType", (int)item.valueType);
                        valueJson.AddField("value", item.stringValue);
                        jsonObject.Add(valueJson);
                        break;
                    case Value.ValueType.IntType:
                        JSONObject valueJson1 = new JSONObject();
                        valueJson1.AddField("key", item.key);
                        valueJson1.AddField("valueType", (int)item.valueType);
                        valueJson1.AddField("value", item.intValue);
                        jsonObject.Add(valueJson1);
                        break;
                    case Value.ValueType.FloatType:
                        JSONObject valueJson2 = new JSONObject();
                        valueJson2.AddField("key", item.key);
                        valueJson2.AddField("valueType", (int)item.valueType);
                        valueJson2.AddField("value", item.floatValue);
                        jsonObject.Add(valueJson2);
                        break;
                    case Value.ValueType.BooleanType:
                        JSONObject valueJson3 = new JSONObject();
                        valueJson3.AddField("key", item.key);
                        valueJson3.AddField("valueType", (int)item.valueType);
                        valueJson3.AddField("value", item.boolValue);
                        jsonObject.Add(valueJson3);
                        break;
                    case Value.ValueType.Vector2Type:
                        JSONObject valueJson4 = new JSONObject();
                        valueJson4.AddField("key", item.key);
                        valueJson4.AddField("valueType", (int)item.valueType);
                        JSONObject vector2Json = new JSONObject();
                        vector2Json.AddField("x", item.vector2Value.x);
                        vector2Json.AddField("y", item.vector2Value.y);
                        valueJson4.AddField("value", vector2Json);
                        jsonObject.Add(valueJson4);
                        break;
                    case Value.ValueType.Vector3Type:
                        JSONObject valueJson5 = new JSONObject();
                        valueJson5.AddField("key", item.key);
                        valueJson5.AddField("valueType", (int)item.valueType);
                        JSONObject vector3Json = new JSONObject();
                        vector3Json.AddField("x", item.vector3Value.x);
                        vector3Json.AddField("y", item.vector3Value.y);
                        vector3Json.AddField("z", item.vector3Value.z);
                        valueJson5.AddField("value", vector3Json);
                        jsonObject.Add(valueJson5);
                        break;
                }
            }


            var path = Path.Combine(Environment.CurrentDirectory, "value.config.json");
            File.WriteAllText(path, jsonObject.ToString());

        }


        public static T GetValue<T>(string key)
        {
            if (_Current == null || _Current.Items.Count == 0) return default(T);

            var items = _Current.Items;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].key == key)
                {
                    switch (items[i].valueType)
                    {
                        case Value.ValueType.StringType:
                            return (T)System.Convert.ChangeType(items[i].stringValue, typeof(T));
                        case Value.ValueType.IntType:
                            return (T)System.Convert.ChangeType(items[i].intValue, typeof(T));
                        case Value.ValueType.FloatType:
                            return (T)System.Convert.ChangeType(items[i].floatValue, typeof(T));
                        case Value.ValueType.BooleanType:
                            return (T)System.Convert.ChangeType(items[i].boolValue, typeof(T));
                        case Value.ValueType.Vector2Type:
                            return (T)System.Convert.ChangeType(items[i].vector2Value, typeof(T));
                        case Value.ValueType.Vector3Type:
                            return (T)System.Convert.ChangeType(items[i].vector3Value, typeof(T));
                    }
                }
            }
            Debug.LogWarning("ValueConfig.GetValue not found key:" + key);
            return default(T);
        }
    }
}