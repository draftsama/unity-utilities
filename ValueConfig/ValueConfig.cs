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

        private static ValueCollection _Current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {

            var path = Path.Combine(Environment.CurrentDirectory, "value.config.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                _Current = JsonUtility.FromJson<ValueCollection>(json);
            }
            else
            {
                var asset = Resources.Load<ValueConfigAsset>("Config/ValueConfig");
                ValueConfig.SaveJsonFile(asset.m_ValueCollection);
                _Current = asset.m_ValueCollection;

            }

        }


        public static void SaveJsonFile(ValueCollection valueCollection)
        {
            var json = JsonUtility.ToJson(valueCollection);
            var path = Path.Combine(Environment.CurrentDirectory, "value.config.json");
            File.WriteAllText(path, json);
        }

       
        public static T GetValue<T>(string key)
        {
            var list = _Current.m_Items;
            if (list == null || list.Count == 0)
                return default(T);

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].key == key)
                {
                    switch (list[i].valueType)
                    {
                        case Value.ValueType.StringType:
                            return (T)System.Convert.ChangeType(list[i].stringValue, typeof(T));
                        case Value.ValueType.IntType:
                            return (T)System.Convert.ChangeType(list[i].intValue, typeof(T));
                        case Value.ValueType.FloatType:
                            return (T)System.Convert.ChangeType(list[i].floatValue, typeof(T));
                        case Value.ValueType.BooleanType:
                            return (T)System.Convert.ChangeType(list[i].boolValue, typeof(T));
                    }
                }
            }
            return default(T);
        }
    }
}