using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class TextureLoader : MonoBehaviour
{

    [SerializeField] private string m_Path;
    [SerializeField] private Texture2D m_Texture;

    private enum State { Idle, Loading, Done, Failed }
    private State _State = State.Idle;
    private UniTask<Texture2D> _LoadingTask;

    public UniTask<Texture2D> LoadTextureAsync(string path)
    {
        // dedup: reuse the in-flight load instead of firing a second request
        if (_State == State.Loading) return _LoadingTask;

        _LoadingTask = LoadTextureInternalAsync(path).Preserve();
        return _LoadingTask;
    }

    private async UniTask<Texture2D> LoadTextureInternalAsync(string path)
    {
        _State = State.Loading;
        try
        {
            using (var request = UnityWebRequestTexture.GetTexture(path))
            {
                await request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                    throw new System.Exception($"Failed to load texture ({path}): {request.error}");

                if (m_Texture) DestroyImmediate(m_Texture);
                m_Texture = DownloadHandlerTexture.GetContent(request);
                m_Texture.name = System.IO.Path.GetFileName(path);
                _State = State.Done;
                return m_Texture;
            }
        }
        catch
        {
            // fault the task so waiters get the exception instead of hanging forever
            _State = State.Failed;
            throw;
        }
    }








    //static list of loaders
    public static List<TextureLoader> m_Loaders = new List<TextureLoader>();
    public static Transform m_Parent = null;


    // local file paths without a scheme get file:// so UnityWebRequest can read them
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        if (path.Contains("://")) return path; // already has a scheme (http/https/file/jar...)
        return "file://" + path;
    }

    public static async UniTask<Texture2D> GetTextureAsync(string path, bool reload = false)
    {
        path = NormalizePath(path);

        //find in list
        var loader = m_Loaders.Find(x => x.m_Path == path);
        if (loader != null)
        {
            //in-flight → join the same load (dedup, also covers reload-while-loading)
            if (loader._State == State.Loading)
                return await loader._LoadingTask;

            //already loaded and not forcing reload → return cached texture
            if (loader._State == State.Done && !reload)
                return loader.m_Texture;

            //idle / failed / reload → start a fresh load (also retries after a failure)
            return await loader.LoadTextureAsync(path);
        }


        //create new loader
        var uuid = System.Guid.NewGuid().ToString();
        var newLoader = new GameObject("TextureLoader_" + uuid).AddComponent<TextureLoader>();
        newLoader.m_Path = path;

        if (m_Parent == null)
        {
            var parentGo = new GameObject("TextureLoaders");
            m_Parent = parentGo.transform;

        }
        newLoader.transform.SetParent(m_Parent);

        m_Loaders.Add(newLoader);

        return await newLoader.LoadTextureAsync(path);

    }

    public static void RemoveLoader(string path, bool destroyTexture = true)
    {
        var loader = m_Loaders.Find(x => x.m_Path == path);
        if (loader != null)
            RemoveLoader(loader, destroyTexture);
    }
    public static void RemoveLoader(TextureLoader loader, bool destroyTexture = true)
    {
        if (loader.m_Texture != null && destroyTexture)
            DestroyImmediate(loader.m_Texture);

        m_Loaders.Remove(loader);

        if (loader.gameObject != null)
            DestroyImmediate(loader.gameObject);


    }

    public static void RemoveAllLoaders(bool destroyTextures = true)
    {
        foreach (var loader in m_Loaders)
        {
            if (loader.m_Texture != null && destroyTextures)
                DestroyImmediate(loader.m_Texture);
            if (loader.gameObject != null)
                DestroyImmediate(loader.gameObject);
        }

        m_Loaders.Clear();
    }


}
