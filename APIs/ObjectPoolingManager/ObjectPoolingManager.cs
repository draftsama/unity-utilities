using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ObjectPoolingManager : MonoBehaviour
{
    private static ObjectPoolingManager instance;

    private static ObjectPoolingManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<ObjectPoolingManager>();
                if(instance != null)return instance;

                var go = new GameObject("ObjectPoolingManager");
                instance = go.AddComponent<ObjectPoolingManager>();
            }


            return instance;
        }

    }

    private void Awake()
    {
    }

    public List<PoolingObject> m_PoolingObjectList = new List<PoolingObject>();

    public static GameObject CreateObject(string _group, GameObject _prefab, Transform _parent = null)
    {
        return CreateObject(_group, _prefab, _prefab.transform.position, _prefab.transform.rotation, _parent);
    }

    public static GameObject CreateObject(string _group, GameObject _prefab, Vector3 _position, Quaternion _rotation, Transform _parent = null)
    {
        GameObject result = null;
        for (int i = 0; i < Instance.m_PoolingObjectList.Count; i++)
        {
            var poolObj = Instance.m_PoolingObjectList[i];
            if (poolObj == null)
            {
                //clean missing object
                Instance.m_PoolingObjectList.RemoveAt(i);
                continue;
            }
            var obj = poolObj.gameObject;
            var trans = poolObj.transform;

            if (!obj.activeInHierarchy && poolObj.m_Group.Equals(_group))
            {

                if (_parent != null) trans.SetParent(_parent, true);
                trans.position = _position;
                trans.rotation = _rotation;

                obj.SetActive(true);
                result = obj;
                break;
            }
        }

        if (result == null)
        {
            result = GameObject.Instantiate(_prefab, _position, _rotation, _parent);
            var poolingObject = result.AddComponent<PoolingObject>();
            poolingObject.Init(_group);
            Instance.m_PoolingObjectList.Add(poolingObject);
        }

        var poolEvent = result.GetComponent<IPoolingObjectEvent>();
        poolEvent?.OnStartObject();

        return result;
    }

    public static PoolingObject[] GetObjects(string _group)
    {
        return Instance.m_PoolingObjectList.Where(_ => _.m_Group.Equals(_group)).ToArray();
    }
    public static PoolingObject[] GetObjects()
    {
        return Instance.m_PoolingObjectList.ToArray();
    }

    public static bool RegisterObject(string _group, GameObject _go)
    {
        //check if object already registered
        if (Instance.m_PoolingObjectList.Any(_ => _.gameObject == _go))
            return false;

        var poolingObject = _go.AddComponent<PoolingObject>();
        poolingObject.Init(_group);
        Instance.m_PoolingObjectList.Add(poolingObject);
        return true;
    }

    public static bool UnregisterObject(GameObject _go)
    {
        var poolObj = _go.GetComponent<PoolingObject>();
        if (poolObj == null) return false;
        if (!Instance.m_PoolingObjectList.Contains(poolObj)) return false;
        Instance.m_PoolingObjectList.Remove(poolObj);
        return true;
    }

    public static bool Kill(GameObject _object, bool _terminate = false)
    {
        PoolingObject poolObj = _object.GetComponent<PoolingObject>();

        return Kill(poolObj, _terminate);
    }

    public static bool Kill(PoolingObject _poolObj, bool _terminate = false)
    {
        if (_poolObj == null) return false;
        if (!Instance.m_PoolingObjectList.Contains(_poolObj)) return false;
        var obj = _poolObj.gameObject;
        var poolEvent = obj.GetComponent<IPoolingObjectEvent>();
        poolEvent?.OnEndObject();
        obj.SetActive(false);

        if (_terminate)
        {
            Instance.m_PoolingObjectList.Remove(_poolObj);
            GameObject.DestroyImmediate(obj, true);
        }

        return true;
    }
    public static bool KillGroup(string _group, bool _terminate = false)
    {
        var list = GetObjects(_group);
        if (list == null) return false;
        for (int i = 0; i < list.Length; i++)
        {
            var poolObj = list[i];
            Kill(poolObj, _terminate);
        }

        return true;
    }

    public static void KillAll(bool _terminate = false)
    {

        try
        {
            // Debug.Log($"KillAll : {Instance.m_PoolingObjectList.Count}");
            for (int i = Instance.m_PoolingObjectList.Count - 1; i >= 0; i--)
            {
                var poolObj = Instance.m_PoolingObjectList[i];
                var result = Kill(poolObj, _terminate);
                // Debug.Log($"{i} : {result}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"KillAll : {e.Message}");
        }



    }





    private void OnDestroy()
    {
        instance = null;
    }

}



public interface IPoolingObjectEvent
{
    public void OnStartObject();
    public void OnEndObject();
}


