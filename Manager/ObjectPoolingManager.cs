using System;
using System.Collections;
using System.Collections.Generic;
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

    public static void KillObject(GameObject _target)
    {

        for (int i = 0; i < instance.m_PoolObjectList.Count; i++)
        {
            var poolObj = instance.m_PoolObjectList[i];
            if (_target.GetInstanceID() == poolObj.m_GameObject.GetInstanceID() && poolObj.m_GameObject.activeInHierarchy && poolObj.m_GameObject.activeSelf)
            {
                poolObj.m_GameObject.SetActive(false);
                var poolEvent = poolObj.m_GameObject.GetComponent<IPoolObjectEvent>();
                poolEvent?.OnEndObject();
                break;
            }
        }
    }

    public static void ClearAll()
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
        instance.m_PoolObjectList.Clear();
    }
}

[System.Serializable]
public class PoolObject
{
    public GameObject m_GameObject;
    public string m_Group;

    public PoolObject(string _group, GameObject _gameObject)
    {
        this.m_Group = _group;
        this.m_GameObject = _gameObject;
    }

}

public interface IPoolObjectEvent
{
    public void OnStartObject();
    public void OnEndObject();
}


