using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using UnityEngine;

namespace Modules.Utilities
{

    public static class UnityConverter
    {

        public static byte[] GetBytes(string _string)
        {
            return Encoding.UTF8.GetBytes(_string);
        }
        public static string GetString(byte[] _bytes)
        {
            return Encoding.UTF8.GetString(_bytes);
        }
        public static byte[] GetBytes(int _int)
        {
            return BitConverter.GetBytes(_int);
        }
        public static int GetInt(byte[] _bytes)
        {
            return BitConverter.ToInt32(_bytes, 0);
        }
        public static byte[] GetBytes(float _float)
        {
            return BitConverter.GetBytes(_float);
        }
        public static float GetFloat(byte[] _bytes)
        {
            return BitConverter.ToSingle(_bytes, 0);
        }
        public static byte[] GetBytes(double _double)
        {
            return BitConverter.GetBytes(_double);
        }
        public static double GetDouble(byte[] _bytes)
        {
            return BitConverter.ToDouble(_bytes, 0);
        }
        public static byte[] GetBytes(long _long)
        {
            return BitConverter.GetBytes(_long);
        }
        public static long GetLong(byte[] _bytes)
        {
            return BitConverter.ToInt64(_bytes, 0);
        }
        public static byte[] GetBytes(bool _bool)
        {
            return BitConverter.GetBytes(_bool);
        }
        public static bool GetBool(byte[] _bytes)
        {
            return BitConverter.ToBoolean(_bytes, 0);
        }
        public static byte[] GetBytes(ushort _ushort)
        {
            return BitConverter.GetBytes(_ushort);
        }
        public static ushort GetUShort(byte[] _bytes)
        {
            return BitConverter.ToUInt16(_bytes, 0);
        }
        public static byte[] GetBytes(uint _uint)
        {
            return BitConverter.GetBytes(_uint);
        }
        public static uint GetUInt(byte[] _bytes)
        {
            return BitConverter.ToUInt32(_bytes, 0);
        }
        public static byte[] GetBytes(ulong _ulong)
        {
            return BitConverter.GetBytes(_ulong);
        }
        public static ulong GetULong(byte[] _bytes)
        {
            return BitConverter.ToUInt64(_bytes, 0);
        }

        public static byte[] GetBytes(Vector2 _vector2)
        {
            return ToBytes(_vector2);
        }
        public static Vector2 GetVector2(byte[] _bytes)
        {
            return FromBytes<Vector2>(_bytes);
        }
        public static byte[] GetBytes(Vector3 _vector3)
        {
            return ToBytes(_vector3);
        }
        public static Vector3 GetVector3(byte[] _bytes)
        {
            return FromBytes<Vector3>(_bytes);
        }
        public static byte[] GetBytes(Vector4 _vector4)
        {
            return ToBytes(_vector4);
        }
        public static Vector4 GetVector4(byte[] _bytes)
        {
            return FromBytes<Vector4>(_bytes);
        }
        public static byte[] GetBytes(Quaternion _quaternion)
        {
            return ToBytes(_quaternion);
        }
        public static Quaternion GetQuaternion(byte[] _bytes)
        {
            return FromBytes<Quaternion>(_bytes);
        }
        public static byte[] GetBytes(Color _color)
        {
            return ToBytes(_color);
        }
        public static Color GetColor(byte[] _bytes)
        {
            return FromBytes<Color>(_bytes);
        }
        public static byte[] GetBytes(Color32 _color32)
        {
            return ToBytes(_color32);
        }
        public static Color32 GetColor32(byte[] _bytes)
        {
            return FromBytes<Color32>(_bytes);
        }
        public static byte[] GetBytes(Rect _rect)
        {
            return ToBytes(_rect);
        }
        public static Rect GetRect(byte[] _bytes)
        {
            return FromBytes<Rect>(_bytes);
        }
        public static byte[] GetBytes(Bounds _bounds)
        {
            return ToBytes(_bounds);
        }
        public static Bounds GetBounds(byte[] _bytes)
        {
            return FromBytes<Bounds>(_bytes);
        }
        public static byte[] GetBytes(Ray _ray)
        {
            return ToBytes(_ray);
        }
        public static Ray GetRay(byte[] _bytes)
        {
            return FromBytes<Ray>(_bytes);
        }
        public static byte[] GetBytes(Ray2D _ray2D)
        {
            return ToBytes(_ray2D);
        }
        public static Ray2D GetRay2D(byte[] _bytes)
        {
            return FromBytes<Ray2D>(_bytes);
        }
        public static byte[] GetBytes(Matrix4x4 _matrix4x4)
        {
            return ToBytes(_matrix4x4);
        }
        public static Matrix4x4 GetMatrix4x4(byte[] _bytes)
        {
            return FromBytes<Matrix4x4>(_bytes);
        }
        public static byte[] GetBytes(Transform _matrix3x3)
        {
            return ToBytes(_matrix3x3);
        }
        public static Transform GetMatrix3x3(byte[] _bytes)
        {
            return FromBytes<Transform>(_bytes);
        }




        public static byte[] ToBytes<T>(T obj)
        {
            if (typeof(T).IsValueType)
            {
                int size = Marshal.SizeOf(typeof(T));
                byte[] arr = new byte[size];
                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(obj, ptr, true);
                Marshal.Copy(ptr, arr, 0, size);
                Marshal.FreeHGlobal(ptr);
                return arr;
            }
            else
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    var serializer = new DataContractSerializer(typeof(T));
                    serializer.WriteObject(ms, obj);
                    return ms.ToArray();
                }
            }
        }

        public static T FromBytes<T>(byte[] data)
        {
            if (typeof(T).IsValueType)
            {
                int size = Marshal.SizeOf(typeof(T));
                if (data.Length != size)
                    throw new ArgumentException("Byte array size does not match type size");

                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.Copy(data, 0, ptr, size);
                T obj = Marshal.PtrToStructure<T>(ptr);
                Marshal.FreeHGlobal(ptr);
                return obj;
            }
            else
            {
                using (MemoryStream ms = new MemoryStream(data))
                {
                    var serializer = new DataContractSerializer(typeof(T));
                    return (T)serializer.ReadObject(ms);
                }
            }
        }

        public static byte[] GetBytes(Texture2D texture)
        {
            if (texture == null)
            {
                Debug.LogError("Texture is null");
                return null;
            }

            // Check if the texture is readable
            if (!texture.isReadable)
            {
                Debug.LogError("Texture is not readable. Please set 'Read/Write Enabled' in the texture import settings.");
                return null;
            }

            // Get the raw texture data
            byte[] rawData = texture.GetRawTextureData();

            //header bytes member width, height, format
            byte[] header = new byte[12];
            System.BitConverter.GetBytes(texture.width).CopyTo(header, 0);
            System.BitConverter.GetBytes(texture.height).CopyTo(header, 4);
            System.BitConverter.GetBytes((int)texture.format).CopyTo(header, 8);
            // Combine header and raw data
            byte[] combinedData = new byte[header.Length + rawData.Length];
            header.CopyTo(combinedData, 0);
            rawData.CopyTo(combinedData, header.Length);

            return combinedData;
        }

        public static Texture2D GetTexture2D(byte[] data)
        {
            if (data == null || data.Length < 12)
            {
                Debug.LogError("Invalid data for Texture2D creation.");
                return null;
            }

            // Extract header information
            int width = BitConverter.ToInt32(data, 0);
            int height = BitConverter.ToInt32(data, 4);
            if (width <= 0 || height <= 0)
            {
                Debug.LogError("Invalid texture dimensions.");
                return null;
            }
            TextureFormat format = (TextureFormat)BitConverter.ToInt32(data, 8);

            // Create a new Texture2D
            var texture = new Texture2D(width, height, format, false);

            // Get the raw texture data from the byte array
            byte[] rawData = new byte[data.Length - 12];
            Array.Copy(data, 12, rawData, 0, rawData.Length);

            // Load the raw texture data into the texture
            texture.LoadRawTextureData(rawData);
            texture.Apply();

            return texture;
        }



    }
}