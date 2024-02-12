using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;
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
       


        /// <summary>
        /// Flip Texture
        /// </summary>
        /// <param name="source">Texture2D</param>
        /// <param name="flipMode">0: vertical, 1: horizontal, 2: both</param>
        /// <param name="format">TextureFormat</param>
        /// <returns>Texture2D</returns>
        /// <example>
        /// <code>
        /// var flippedTexture = m_Texture.Flip(0, TextureFormat.RGBA32);
        /// </code>
        public static Texture2D Flip(this Texture2D source, TextureFlipMode flipMode = TextureFlipMode.Vertical, TextureFormat format = TextureFormat.RGB24)
        {

            Color[] pixels = source.GetPixels();
            Color[] pixelsFlipped = new Color[pixels.Length];
            var width = source.width;
            var height = source.height;
            Texture2D flipped = new Texture2D(width, height, format, false);


            if (flipMode == TextureFlipMode.Vertical || flipMode == TextureFlipMode.Both)
            {
                for (int i = 0; i < height; i++)
                {
                    System.Array.Copy(pixels, i * width, pixelsFlipped, (height - i - 1) * width, width);
                }
            }

            if (flipMode == TextureFlipMode.Horizontal || flipMode == TextureFlipMode.Both)
            {
                for (int i = 0; i < height; i++)
                {
                    for (int j = 0; j < width; j++)
                    {
                        pixelsFlipped[i * width + j] = pixels[i * width + (width - j - 1)];
                    }
                }
            }


            flipped.SetPixels(pixelsFlipped);
            flipped.Apply();

            return flipped;
        }

        public static Texture2D ResizeTexture(this Texture2D source, int width, int height)
        {
            var resized = new Texture2D(width, height, source.format, source.mipmapCount > 1);
            //get all pixels
            var pixels = source.GetPixels(0);
            //calculate the ratio
            var ratioX = 1.0f / ((float)width / (source.width - 1));
            var ratioY = 1.0f / ((float)height / (source.height - 1));

            var newPixels = new Color[width * height];
            //loop through the new texture
            for (var i = 0; i < height; i++)
            {
                for (var j = 0; j < width; j++)
                {
                    //calculate the position in the old texture
                    var x = Mathf.FloorToInt(j * ratioX);
                    var y = Mathf.FloorToInt(i * ratioY);
                    
                    //set the pixel to the new texture
                    newPixels[i * width + j] = pixels[y * source.width + x];
                }
            }

            //set the pixels to the new texture
            resized.SetPixels(newPixels);

            //apply the changes
            resized.Apply();

          
            return resized;
        }
      
    }

}
