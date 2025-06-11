using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using System.Linq;

public class ParticleSystemPooled : MonoBehaviour, IPoolingObjectEvent
{
    [SerializeField] private bool m_IncludeChildren = true;
    private ParticleSystem[] m_ParticleSystems;

    private PoolingObject _PoolingObject;
    private void Start()
    {
        _PoolingObject = GetComponent<PoolingObject>();
        m_ParticleSystems = m_IncludeChildren ? GetComponentsInChildren<ParticleSystem>() : GetComponents<ParticleSystem>();

    }
    public void OnEndObject()
    {
    }

    public async void OnStartObject()
    {
        //wait for play
        await UniTask.WaitUntil(() => m_ParticleSystems.Any(ps => ps.isPlaying));
        await UniTask.Yield();
        //wait for all particle systems to finish playing
        await UniTask.WaitUntil(() => m_ParticleSystems.All(ps => !ps.isPlaying));
        _PoolingObject.Kill();


    }
}
