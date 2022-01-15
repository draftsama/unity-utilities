using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolingManager
{


    public static List<PoolObject> m_PoolObjectList = new List<PoolObject>();
    public static GameObject CreateObject(string _group, GameObject _prefab, Vector3 _position, Quaternion _rotation, Transform _parent = null)
    {

        GameObject result = null;
        for (int i = 0; i < m_PoolObjectList.Count; i++)
        {
            var poolObj = m_PoolObjectList[i];
            if (!poolObj.m_GameObject.activeInHierarchy && poolObj.m_Group.Equals(_group))
            {
                poolObj.m_GameObject.transform.position = _position;
                poolObj.m_GameObject.transform.rotation = _rotation;
                if(_parent != null)poolObj.m_GameObject.transform.SetParent(_parent);
                poolObj.m_GameObject.SetActive(true);
                result = poolObj.m_GameObject;
                break;
            }
        }

        if (result == null)
        {
            result = GameObject.Instantiate(_prefab, _position, _rotation, _parent);
            m_PoolObjectList.Add(new PoolObject(_group, result));
        }

        var poolEvent = result.GetComponent<IPoolObjectEvent>();
        poolEvent?.OnStartObject();

        return result;
    }

    public static void KillObject(GameObject _target)
    {

        for (int i = 0; i < m_PoolObjectList.Count; i++)
        {
            var poolObj = m_PoolObjectList[i];
            if (_target.GetInstanceID() == poolObj.m_GameObject.GetInstanceID() && poolObj.m_GameObject.activeInHierarchy)
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
        for (int i = 0; i < m_PoolObjectList.Count; i++)
        {
            var poolObj = m_PoolObjectList[i];
            GameObject.DestroyImmediate(poolObj.m_GameObject, true);
        }
        m_PoolObjectList.Clear();

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


