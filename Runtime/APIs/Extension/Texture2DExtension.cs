using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Modules.Utilities
{
    public enum TextureFlipMode
    {
        Vertical,
        Horizontal,
        Both
    }
    public static class Texture2DExtension
    {   

        /// <summary>
        /// Save Texture to file
        /// </summary>
        /// <param name="source">Texture2D</param>
        /// <param name="path">string</param>
        /// <example>
        public static async UniTask<bool> SaveFileAsync(this Texture2D source, string path, bool isOverwrite = true)
        {
            if (System.IO.File.Exists(path) && !isOverwrite)
            {
                Debug.LogWarning($"File already exists: {path}");
                return false;
            }

            var extension = System.IO.Path.GetExtension(path);

            byte[] bytes = null;

            switch (extension)
            {
                case ".png":
                    bytes = source.EncodeToPNG();
                    break;
                case ".jpg":
                    bytes = source.EncodeToJPG();
                    break;
                case ".exr":
                    bytes = source.EncodeToEXR();
                    break;
                default:
                    Debug.LogWarning($"Unsupported file extension: {extension}");
                    return false;
            }


            #if UNITY_2020
                //write another thread
                // System.IO.File.WriteAllBytes(path, bytes);

                await UniTask.Run(() =>
                {
                    System.IO.File.WriteAllBytes(path, bytes);
                });
            #else
                await System.IO.File.WriteAllBytesAsync(path, bytes);
            #endif

            bytes = null;

            return true;
        }
       

      
    }

}
