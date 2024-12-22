using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class TextureLoader : MonoBehaviour
{   

    [SerializeField]private string m_Path;
    [SerializeField]private Texture2D m_Texture;

    private bool _IsFinished = false;

    public async UniTask<Texture2D> LoadTextureAsync(string path)
    {

        try
        {
            using (var request = UnityWebRequestTexture.GetTexture(path)){
                await request.SendWebRequest();
                if (request.isDone)
                {
                    m_Texture = DownloadHandlerTexture.GetContent(request);
                    _IsFinished = true;
                }else{
                    throw new System.Exception("Failed to load texture");
                }
            }
        }
        catch (System.Exception e)
        {
            throw e;
        }

         return m_Texture;

    }

    






    //static list of loaders
    public static List<TextureLoader> m_Loaders = new List<TextureLoader>();


    public static async UniTask<Texture2D> GetTextureAsync(string path){

        //find in list
        var loader = m_Loaders.Find(x => x.m_Path == path);
        if (loader != null)
        {
            //wait for loading
            await UniTask.WaitUntil(() => loader._IsFinished);

            return await UniTask.FromResult(loader.m_Texture);
        }


        //create new loader
        var uuid = System.Guid.NewGuid().ToString();
        var newLoader = new GameObject("TextureLoader_"+uuid).AddComponent<TextureLoader>();
        newLoader.m_Path = path;
        
        m_Loaders.Add(newLoader);

        return await newLoader.LoadTextureAsync(path);
        
    }

    public static void RemoveLoader(string path){
        var loader = m_Loaders.Find(x => x.m_Path == path);
        if(loader != null)
            RemoveLoader(loader);
    }
    public static void RemoveLoader(TextureLoader loader)
    {
        if(loader.m_Texture != null)
            DestroyImmediate(loader.m_Texture);

        m_Loaders.Remove(loader);

        DestroyImmediate(loader.gameObject);
    }

    public static void RemoveAllLoaders()
    {
        foreach (var loader in m_Loaders)
        {
            if(loader.m_Texture != null)
                DestroyImmediate(loader.m_Texture);
            DestroyImmediate(loader.gameObject);
        }

        m_Loaders.Clear();
    }


}
