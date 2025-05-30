using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Modules.Utilities
{
    public class ValueConfig
    {

        private static VariableCollection _Collection;
        public static VariableCollection GetCollection()
        {
            if (_Collection == null)
                LoadValueConfig();

            return _Collection;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnInitRuntime()
        {
            LoadValueConfig();
        }

        //#if UNITY_EDITOR
        //         [UnityEditor.InitializeOnLoadMethod]
        //         private static void OnAfterScriptCompile()
        //         {
        //             LoadValueConfig();
        //         }
        // #endif





        private static void LoadValueConfig()
        {
            _Collection = new VariableCollection();
            var path = Path.Combine(Application.persistentDataPath, "value.config.json");

            if (File.Exists(path))
            {
                var jsonString = File.ReadAllText(path);
                JSONObject jsonObjects = new JSONObject(jsonString);

                for (int i = 0; i < jsonObjects.Count; i++)
                {
                    var value = new Variable();

                    var jsonObject = jsonObjects[i];


                    if (jsonObject.HasFields(new string[3] { "key", "valueType", "value" }))
                    {
                        value.key = jsonObject["key"].str;
                        value.type = (Variable.Type)jsonObject["valueType"].i;
                        switch (value.type)
                        {
                            case Variable.Type.String:
                                value.stringValue = jsonObject["value"].str;
                                break;
                            case Variable.Type.Int:
                                value.intValue = (int)jsonObject["value"].i;
                                break;
                            case Variable.Type.Float:
                                value.floatValue = jsonObject["value"].f;
                                break;
                            case Variable.Type.Boolean:
                                value.boolValue = jsonObject["value"].b;
                                break;
                            case Variable.Type.Vector2:
                                value.vector2Value = JSONTemplates.ToVector2(jsonObject["value"]);
                                break;
                            case Variable.Type.Vector3:
                                value.vector3Value = JSONTemplates.ToVector3(jsonObject["value"]);
                                break;
                        }
                        _Collection.AddVariable(value);
                    }



                }

            }



        }


        private static void SaveValueConfig()
        {
            JSONObject jsonObject = new JSONObject();

            foreach (var item in _Collection.Items)
            {
                switch (item.type)
                {
                    case Variable.Type.String:
                        JSONObject valueJson = new JSONObject();
                        valueJson.AddField("key", item.key);
                        valueJson.AddField("valueType", (int)item.type);
                        valueJson.AddField("value", item.stringValue);
                        jsonObject.Add(valueJson);
                        break;
                    case Variable.Type.Int:
                        JSONObject valueJson1 = new JSONObject();
                        valueJson1.AddField("key", item.key);
                        valueJson1.AddField("valueType", (int)item.type);
                        valueJson1.AddField("value", item.intValue);
                        jsonObject.Add(valueJson1);
                        break;
                    case Variable.Type.Float:
                        JSONObject valueJson2 = new JSONObject();
                        valueJson2.AddField("key", item.key);
                        valueJson2.AddField("valueType", (int)item.type);
                        valueJson2.AddField("value", item.floatValue);
                        jsonObject.Add(valueJson2);
                        break;
                    case Variable.Type.Boolean:
                        JSONObject valueJson3 = new JSONObject();
                        valueJson3.AddField("key", item.key);
                        valueJson3.AddField("valueType", (int)item.type);
                        valueJson3.AddField("value", item.boolValue);
                        jsonObject.Add(valueJson3);
                        break;
                    case Variable.Type.Vector2:
                        JSONObject valueJson4 = new JSONObject();
                        valueJson4.AddField("key", item.key);
                        valueJson4.AddField("valueType", (int)item.type);
                        JSONObject vector2Json = new JSONObject();
                        vector2Json.AddField("x", item.vector2Value.x);
                        vector2Json.AddField("y", item.vector2Value.y);
                        valueJson4.AddField("value", vector2Json);
                        jsonObject.Add(valueJson4);
                        break;
                    case Variable.Type.Vector3:
                        JSONObject valueJson5 = new JSONObject();
                        valueJson5.AddField("key", item.key);
                        valueJson5.AddField("valueType", (int)item.type);
                        JSONObject vector3Json = new JSONObject();
                        vector3Json.AddField("x", item.vector3Value.x);
                        vector3Json.AddField("y", item.vector3Value.y);
                        vector3Json.AddField("z", item.vector3Value.z);
                        valueJson5.AddField("value", vector3Json);
                        jsonObject.Add(valueJson5);
                        break;
                }
            }

            var path = Path.Combine(Application.persistentDataPath, "value.config.json");
            File.WriteAllText(path, jsonObject.ToString(true));
        }

        public static bool SetValue<T>(string key, T value)
        {


            if (_Collection.SetValue(key, value))
            {
                SaveValueConfig();
                return true;
            }
            else
            {
                Debug.LogError($"Failed to set value for key: {key}");
                return false;
            }

        }

        

        public static bool TryGetValue<T>(string key, out T value)

        {
            value = GetValue<T>(key);
            return !EqualityComparer<T>.Default.Equals(value, default(T));
        }

        public static T GetValue<T>(string key, T defaultValue = default(T))
        {

            return GetCollection().GetValue(key, defaultValue);
        }

        public static bool Remove(string key)
        {

            var success = GetCollection().Remove(key);
            if (success)
            {
                SaveValueConfig();
            }

            return success;
        }
        public static void Clear()
        {
            GetCollection().Clear();
            SaveValueConfig();
        }

        public static Variable[] GetVariables()
        {
            return GetCollection().Items.ToArray();
        }

        public static Variable GetVariable(string key)
        {
            return GetCollection().Items.Find(x => x.key == key);
        }
        


    }
}