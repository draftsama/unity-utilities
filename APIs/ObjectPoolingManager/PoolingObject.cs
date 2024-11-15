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

    }
    public bool Kill(bool _terminate = false)
    {
        IsAlive = false;
        return ObjectPoolingManager.Kill(this, _terminate);
    }




}
