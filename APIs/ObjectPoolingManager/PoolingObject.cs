using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolingObject : MonoBehaviour
{
    public string m_Group;
    public bool IsInitialized { private set; get; }

    public bool IsAlive { private set; get; }
    private void Awake()
    {

    }
    public void Init(string _group)
    {
        if (IsInitialized) return;
        this.IsInitialized = true;
        this.m_Group = _group;
        Wake();


    }

    public void Wake()
    {
        if (!IsInitialized) return;

        gameObject.SetActive(true);
        this.IsAlive = true;

        var poolEvents = GetComponents<IPoolingObjectEvent>();
        foreach (var poolEvent in poolEvents)
        {
            poolEvent?.OnStartObject();
        }


    }
    public void Kill(bool _terminate = false)
    {
        IsAlive = false;
        gameObject.SetActive(false);

        var poolEvents = GetComponents<IPoolingObjectEvent>();
        foreach (var poolEvent in poolEvents)
        {
            poolEvent?.OnEndObject();
        }

        if (_terminate)
        {
            DestroyImmediate(gameObject, true);
        }
    }




}
