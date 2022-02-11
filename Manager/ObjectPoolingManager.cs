using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ObjectPoolingManager : MonoBehaviour
{
    private static ObjectPoolingManager instance;

    private static ObjectPoolingManager CreateInstance()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("ObjectPoolingManager");
            instance = go.AddComponent<ObjectPoolingManager>();
        }

        return instance;
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public  List<PoolObject> m_PoolObjectList = new List<PoolObject>();

    public static GameObject CreateObject(string _group, GameObject _prefab, Transform _parent = null)
    {
        return CreateObject(_group, _prefab, _prefab.transform.position, _prefab.transform.rotation, _parent);
    }

    public static GameObject CreateObject(string _group, GameObject _prefab, Vector3 _position, Quaternion _rotation, Transform _parent = null)
    {
        CreateInstance();
        GameObject result = null;
        for (int i = 0; i < instance.m_PoolObjectList.Count; i++)
        {
            var poolObj = instance.m_PoolObjectList[i];
            if (!poolObj.m_GameObject.activeInHierarchy && poolObj.m_Group.Equals(_group))
            {
              
                if(_parent != null)poolObj.m_GameObject.transform.SetParent(_parent,true);
                poolObj.m_GameObject.transform.position = _position;
                poolObj.m_GameObject.transform.rotation = _rotation;
                
                poolObj.m_GameObject.SetActive(true);
                result = poolObj.m_GameObject;
                break;
            }
        }

        if (result == null)
        {
            result = GameObject.Instantiate(_prefab, _position, _rotation, _parent);
            instance.m_PoolObjectList.Add(new PoolObject(_group, result));
        }

        var poolEvent = result.GetComponent<IPoolObjectEvent>();
        poolEvent?.OnStartObject();

        return result;
    }

    public static PoolObject[] GetObjects(string _group)
    {
        return instance.m_PoolObjectList.Where(_ => _.m_Group.Equals(_group)).ToArray();
    }
   
    public static bool Kill(GameObject _target)
    {
        for (int i = 0; i < instance.m_PoolObjectList.Count; i++)
        {
            var poolObj = instance.m_PoolObjectList[i];
            if (_target.GetInstanceID() == poolObj.m_GameObject.GetInstanceID() && poolObj.m_GameObject.activeInHierarchy && poolObj.m_GameObject.activeSelf)
            {
                poolObj.m_GameObject.SetActive(false);
                var poolEvent = poolObj.m_GameObject.GetComponent<IPoolObjectEvent>();
                poolEvent?.OnEndObject();

                return true;
            }
        }

        return false;
    }
    public static bool KillGroup(string _group)
    {
        var list = GetObjects(_group);
        if (list == null) return false;
        for (int i = 0; i < list.Length; i++)
        {
            var poolObj = list[i];
            poolObj.m_GameObject.SetActive(false);
            var poolEvent = poolObj.m_GameObject.GetComponent<IPoolObjectEvent>();
            poolEvent?.OnEndObject();
        }

        return true;
    }

    public static bool Terminate(GameObject _target)
    {
        for (int i = 0; i < instance.m_PoolObjectList.Count; i++)
        {
            var poolObj = instance.m_PoolObjectList[i];
            if (_target.GetInstanceID() == poolObj.m_GameObject.GetInstanceID() && poolObj.m_GameObject.activeInHierarchy && poolObj.m_GameObject.activeSelf)
            {
                instance.m_PoolObjectList.Remove(poolObj);
                GameObject.DestroyImmediate(poolObj.m_GameObject, true);

                return true;
            }
        }

        return false;
    }

    public static void TerminateAll()
    {
        for (int i = 0; i < instance.m_PoolObjectList.Count; i++)
        {
            var poolObj = instance.m_PoolObjectList[i];
            GameObject.DestroyImmediate(poolObj.m_GameObject, true);
        }
        instance.m_PoolObjectList.Clear();

    }

    private void OnDestroy()
    {
        TerminateAll();
    }
  
}

[System.Serializable]
public class PoolObject
{
    public GameObject m_GameObject;
    public string m_Group;
    public Transform m_Transform;

    public PoolObject(string _group, GameObject _gameObject)
    {
        this.m_Group = _group;
        this.m_GameObject = _gameObject;
        this.m_Transform = _gameObject.transform;
    }

}

public interface IPoolObjectEvent
{
    public void OnStartObject();
    public void OnEndObject();
}


