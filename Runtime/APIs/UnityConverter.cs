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

        public static byte[] GetBytes(short _short)
        {
            return BitConverter.GetBytes(_short);
        }
        public static short GetShort(byte[] _bytes)
        {
            return BitConverter.ToInt16(_bytes, 0);
        }

        public static byte[] GetBytes(byte _byte)
        {
            return new[] { _byte };
        }
        public static byte GetByte(byte[] _bytes)
        {
            return _bytes[0];
        }

        public static byte[] GetBytes(sbyte _sbyte)
        {
            return new[] { (byte)_sbyte };
        }
        public static sbyte GetSByte(byte[] _bytes)
        {
            return (sbyte)_bytes[0];
        }

        public static byte[] GetBytes(char _char)
        {
            return BitConverter.GetBytes(_char);
        }
        public static char GetChar(byte[] _bytes)
        {
            return BitConverter.ToChar(_bytes, 0);
        }

        public static byte[] GetBytes(DateTime _dateTime)
        {
            return BitConverter.GetBytes(_dateTime.ToBinary());
        }
        public static DateTime GetDateTime(byte[] _bytes)
        {
            return DateTime.FromBinary(BitConverter.ToInt64(_bytes, 0));
        }

        public static byte[] GetBytes(TimeSpan _timeSpan)
        {
            return BitConverter.GetBytes(_timeSpan.Ticks);
        }
        public static TimeSpan GetTimeSpan(byte[] _bytes)
        {
            return TimeSpan.FromTicks(BitConverter.ToInt64(_bytes, 0));
        }

        public static byte[] GetBytes(Guid _guid)
        {
            return _guid.ToByteArray();
        }
        public static Guid GetGuid(byte[] _bytes)
        {
            return new Guid(_bytes);
        }

        public static byte[] GetBytes(Vector2 _vector2)
        {
            var bytes = new byte[8];
            BitConverter.GetBytes(_vector2.x).CopyTo(bytes, 0);
            BitConverter.GetBytes(_vector2.y).CopyTo(bytes, 4);
            return bytes;
        }
        public static Vector2 GetVector2(byte[] _bytes)
        {
            return new Vector2(
                BitConverter.ToSingle(_bytes, 0),
                BitConverter.ToSingle(_bytes, 4)
            );
        }
        public static byte[] GetBytes(Vector2Int _vector2Int)
        {
            var bytes = new byte[8];
            BitConverter.GetBytes(_vector2Int.x).CopyTo(bytes, 0);
            BitConverter.GetBytes(_vector2Int.y).CopyTo(bytes, 4);
            return bytes;
        }
        public static Vector2Int GetVector2Int(byte[] _bytes)
        {
            return new Vector2Int(
                BitConverter.ToInt32(_bytes, 0),
                BitConverter.ToInt32(_bytes, 4)
            );
        }
        public static byte[] GetBytes(Vector3 _vector3)
        {
            var bytes = new byte[12];
            BitConverter.GetBytes(_vector3.x).CopyTo(bytes, 0);
            BitConverter.GetBytes(_vector3.y).CopyTo(bytes, 4);
            BitConverter.GetBytes(_vector3.z).CopyTo(bytes, 8);
            return bytes;
        }
        public static Vector3 GetVector3(byte[] _bytes)
        {
            return new Vector3(
                BitConverter.ToSingle(_bytes, 0),
                BitConverter.ToSingle(_bytes, 4),
                BitConverter.ToSingle(_bytes, 8)
            );
        }
        public static byte[] GetBytes(Vector3Int _vector3Int)
        {
            var bytes = new byte[12];
            BitConverter.GetBytes(_vector3Int.x).CopyTo(bytes, 0);
            BitConverter.GetBytes(_vector3Int.y).CopyTo(bytes, 4);
            BitConverter.GetBytes(_vector3Int.z).CopyTo(bytes, 8);
            return bytes;
        }
        public static Vector3Int GetVector3Int(byte[] _bytes)
        {
            return new Vector3Int(
                BitConverter.ToInt32(_bytes, 0),
                BitConverter.ToInt32(_bytes, 4),
                BitConverter.ToInt32(_bytes, 8)
            );
        }
        public static byte[] GetBytes(Vector4 _vector4)
        {
            var bytes = new byte[16];
            BitConverter.GetBytes(_vector4.x).CopyTo(bytes, 0);
            BitConverter.GetBytes(_vector4.y).CopyTo(bytes, 4);
            BitConverter.GetBytes(_vector4.z).CopyTo(bytes, 8);
            BitConverter.GetBytes(_vector4.w).CopyTo(bytes, 12);
            return bytes;
        }
        public static Vector4 GetVector4(byte[] _bytes)
        {
            return new Vector4(
                BitConverter.ToSingle(_bytes, 0),
                BitConverter.ToSingle(_bytes, 4),
                BitConverter.ToSingle(_bytes, 8),
                BitConverter.ToSingle(_bytes, 12)
            );
        }
        public static byte[] GetBytes(Quaternion _quaternion)
        {
            var bytes = new byte[16];
            BitConverter.GetBytes(_quaternion.x).CopyTo(bytes, 0);
            BitConverter.GetBytes(_quaternion.y).CopyTo(bytes, 4);
            BitConverter.GetBytes(_quaternion.z).CopyTo(bytes, 8);
            BitConverter.GetBytes(_quaternion.w).CopyTo(bytes, 12);
            return bytes;
        }
        public static Quaternion GetQuaternion(byte[] _bytes)
        {
            return new Quaternion(
                BitConverter.ToSingle(_bytes, 0),
                BitConverter.ToSingle(_bytes, 4),
                BitConverter.ToSingle(_bytes, 8),
                BitConverter.ToSingle(_bytes, 12)
            );
        }
        public static byte[] GetBytes(Color _color)
        {
            var bytes = new byte[16];
            BitConverter.GetBytes(_color.r).CopyTo(bytes, 0);
            BitConverter.GetBytes(_color.g).CopyTo(bytes, 4);
            BitConverter.GetBytes(_color.b).CopyTo(bytes, 8);
            BitConverter.GetBytes(_color.a).CopyTo(bytes, 12);
            return bytes;
        }
        public static Color GetColor(byte[] _bytes)
        {
            return new Color(
                BitConverter.ToSingle(_bytes, 0),
                BitConverter.ToSingle(_bytes, 4),
                BitConverter.ToSingle(_bytes, 8),
                BitConverter.ToSingle(_bytes, 12)
            );
        }
        public static byte[] GetBytes(Color32 _color32)
        {
            return new byte[] { _color32.r, _color32.g, _color32.b, _color32.a };
        }
        public static Color32 GetColor32(byte[] _bytes)
        {
            return new Color32(_bytes[0], _bytes[1], _bytes[2], _bytes[3]);
        }
        public static byte[] GetBytes(Rect _rect)
        {
            var bytes = new byte[16];
            BitConverter.GetBytes(_rect.x).CopyTo(bytes, 0);
            BitConverter.GetBytes(_rect.y).CopyTo(bytes, 4);
            BitConverter.GetBytes(_rect.width).CopyTo(bytes, 8);
            BitConverter.GetBytes(_rect.height).CopyTo(bytes, 12);
            return bytes;
        }
        public static Rect GetRect(byte[] _bytes)
        {
            return new Rect(
                BitConverter.ToSingle(_bytes, 0),
                BitConverter.ToSingle(_bytes, 4),
                BitConverter.ToSingle(_bytes, 8),
                BitConverter.ToSingle(_bytes, 12)
            );
        }
        public static byte[] GetBytes(Bounds _bounds)
        {
            var bytes = new byte[24]; // center(12) + size(12)
            // Center
            BitConverter.GetBytes(_bounds.center.x).CopyTo(bytes, 0);
            BitConverter.GetBytes(_bounds.center.y).CopyTo(bytes, 4);
            BitConverter.GetBytes(_bounds.center.z).CopyTo(bytes, 8);
            // Size
            BitConverter.GetBytes(_bounds.size.x).CopyTo(bytes, 12);
            BitConverter.GetBytes(_bounds.size.y).CopyTo(bytes, 16);
            BitConverter.GetBytes(_bounds.size.z).CopyTo(bytes, 20);
            return bytes;
        }
        public static Bounds GetBounds(byte[] _bytes)
        {
            var center = new Vector3(
                BitConverter.ToSingle(_bytes, 0),
                BitConverter.ToSingle(_bytes, 4),
                BitConverter.ToSingle(_bytes, 8)
            );
            var size = new Vector3(
                BitConverter.ToSingle(_bytes, 12),
                BitConverter.ToSingle(_bytes, 16),
                BitConverter.ToSingle(_bytes, 20)
            );
            return new Bounds(center, size);
        }
        public static byte[] GetBytes(Ray _ray)
        {
            var bytes = new byte[24]; // origin(12) + direction(12)
            // Origin
            BitConverter.GetBytes(_ray.origin.x).CopyTo(bytes, 0);
            BitConverter.GetBytes(_ray.origin.y).CopyTo(bytes, 4);
            BitConverter.GetBytes(_ray.origin.z).CopyTo(bytes, 8);
            // Direction
            BitConverter.GetBytes(_ray.direction.x).CopyTo(bytes, 12);
            BitConverter.GetBytes(_ray.direction.y).CopyTo(bytes, 16);
            BitConverter.GetBytes(_ray.direction.z).CopyTo(bytes, 20);
            return bytes;
        }
        public static Ray GetRay(byte[] _bytes)
        {
            var origin = new Vector3(
                BitConverter.ToSingle(_bytes, 0),
                BitConverter.ToSingle(_bytes, 4),
                BitConverter.ToSingle(_bytes, 8)
            );
            var direction = new Vector3(
                BitConverter.ToSingle(_bytes, 12),
                BitConverter.ToSingle(_bytes, 16),
                BitConverter.ToSingle(_bytes, 20)
            );
            return new Ray(origin, direction);
        }
        public static byte[] GetBytes(Ray2D _ray2D)
        {
            var bytes = new byte[16]; // origin(8) + direction(8)
            // Origin
            BitConverter.GetBytes(_ray2D.origin.x).CopyTo(bytes, 0);
            BitConverter.GetBytes(_ray2D.origin.y).CopyTo(bytes, 4);
            // Direction
            BitConverter.GetBytes(_ray2D.direction.x).CopyTo(bytes, 8);
            BitConverter.GetBytes(_ray2D.direction.y).CopyTo(bytes, 12);
            return bytes;
        }
        public static Ray2D GetRay2D(byte[] _bytes)
        {
            var origin = new Vector2(
                BitConverter.ToSingle(_bytes, 0),
                BitConverter.ToSingle(_bytes, 4)
            );
            var direction = new Vector2(
                BitConverter.ToSingle(_bytes, 8),
                BitConverter.ToSingle(_bytes, 12)
            );
            return new Ray2D(origin, direction);
        }
        public static byte[] GetBytes(Matrix4x4 _matrix4x4)
        {
            var bytes = new byte[64]; // 16 floats * 4 bytes
            for (int i = 0; i < 16; i++)
            {
                BitConverter.GetBytes(_matrix4x4[i]).CopyTo(bytes, i * 4);
            }
            return bytes;
        }
        public static Matrix4x4 GetMatrix4x4(byte[] _bytes)
        {
            var matrix = new Matrix4x4();
            for (int i = 0; i < 16; i++)
            {
                matrix[i] = BitConverter.ToSingle(_bytes, i * 4);
            }
            return matrix;
        }
        public static byte[] GetBytes(Transform _transform)
        {
            // Transform เป็น Component ไม่สามารถ serialize ได้โดยตรง
            // เก็บเฉพาะ position, rotation, scale
            var bytes = new byte[40]; // position(12) + rotation(16) + scale(12)
            
            // Position
            BitConverter.GetBytes(_transform.position.x).CopyTo(bytes, 0);
            BitConverter.GetBytes(_transform.position.y).CopyTo(bytes, 4);
            BitConverter.GetBytes(_transform.position.z).CopyTo(bytes, 8);
            
            // Rotation
            BitConverter.GetBytes(_transform.rotation.x).CopyTo(bytes, 12);
            BitConverter.GetBytes(_transform.rotation.y).CopyTo(bytes, 16);
            BitConverter.GetBytes(_transform.rotation.z).CopyTo(bytes, 20);
            BitConverter.GetBytes(_transform.rotation.w).CopyTo(bytes, 24);
            
            // Scale
            BitConverter.GetBytes(_transform.localScale.x).CopyTo(bytes, 28);
            BitConverter.GetBytes(_transform.localScale.y).CopyTo(bytes, 32);
            BitConverter.GetBytes(_transform.localScale.z).CopyTo(bytes, 36);
            
            return bytes;
        }
        public static Transform GetMatrix3x3(byte[] _bytes)
        {
            // Transform ไม่สามารถสร้างใหม่ได้ เพราะเป็น Component
            // Method นี้ไม่ควรใช้ ให้ return null
            Debug.LogWarning("Cannot create new Transform from bytes. Use SetTransformFromBytes instead.");
            return null;
        }
        
        /// <summary>
        /// Sets transform values from byte array
        /// </summary>
        public static void SetTransformFromBytes(Transform transform, byte[] _bytes)
        {
            if (transform == null || _bytes == null || _bytes.Length < 40) return;
            
            // Position
            var position = new Vector3(
                BitConverter.ToSingle(_bytes, 0),
                BitConverter.ToSingle(_bytes, 4),
                BitConverter.ToSingle(_bytes, 8)
            );
            
            // Rotation
            var rotation = new Quaternion(
                BitConverter.ToSingle(_bytes, 12),
                BitConverter.ToSingle(_bytes, 16),
                BitConverter.ToSingle(_bytes, 20),
                BitConverter.ToSingle(_bytes, 24)
            );
            
            // Scale
            var scale = new Vector3(
                BitConverter.ToSingle(_bytes, 28),
                BitConverter.ToSingle(_bytes, 32),
                BitConverter.ToSingle(_bytes, 36)
            );
            
            transform.position = position;
            transform.rotation = rotation;
            transform.localScale = scale;
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



        public static byte[] ToBytes<T>(T obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj), "Data cannot be null.");

            var type = typeof(T);

            // Handle string directly
            if (obj is string stringData)
            {
                return GetBytes(stringData);
            }

            // Handle primitive types
            if (type.IsPrimitive)
            {
                return SerializePrimitive(obj);
            }

            // Handle Unity value types
            if (TrySerializeUnityType(obj, out byte[] unityBytes))
            {
                return unityBytes;
            }

            // Handle System types
            if (TrySerializeSystemType(obj, out byte[] systemBytes))
            {
                return systemBytes;
            }

            // Handle byte array directly
            if (obj is byte[] byteArray)
            {
                return byteArray;
            }

            // Handle enums
            if (type.IsEnum)
            {
                var underlyingType = Enum.GetUnderlyingType(type);
                var enumValue = Convert.ChangeType(obj, underlyingType);
                return SerializePrimitive(enumValue);
            }

            // Handle nullable types
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                if (obj == null) return new byte[0];

                // Get the underlying value using reflection (Unity-compatible)
                var valueProperty = type.GetProperty("Value");
                var underlyingValue = valueProperty.GetValue(obj);
                var underlyingType = Nullable.GetUnderlyingType(type);
                var method = typeof(UnityConverter).GetMethod(nameof(ToBytes)).MakeGenericMethod(underlyingType);
                return (byte[])method.Invoke(null, new object[] { underlyingValue });
            }

            // Handle Texture2D
            if (obj is Texture2D texture)
            {
                return GetBytes(texture);
            }

            // Fallback: Use Marshal for value types, DataContractSerializer for reference types
            if (type.IsValueType)
            {
                try
                {
                    int size = Marshal.SizeOf(type);
                    byte[] arr = new byte[size];
                    IntPtr ptr = Marshal.AllocHGlobal(size);
                    Marshal.StructureToPtr(obj, ptr, true);
                    Marshal.Copy(ptr, arr, 0, size);
                    Marshal.FreeHGlobal(ptr);
                    return arr;
                }
                catch
                {
                    // If Marshal fails, try JSON
                    return SerializeAsJson(obj);
                }
            }
            else
            {
                // For reference types, try JSON first, then DataContract as fallback
                try
                {
                    return SerializeAsJson(obj);
                }
                catch
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        var serializer = new DataContractSerializer(type);
                        serializer.WriteObject(ms, obj);
                        return ms.ToArray();
                    }
                }
            }
        }

        public static T FromBytes<T>(byte[] data)
        {
            if (data == null || data.Length == 0) return default;

            var type = typeof(T);

            // Handle string directly
            if (type == typeof(string))
            {
                return (T)(object)GetString(data);
            }

            // Handle primitive types
            if (type.IsPrimitive)
            {
                return DeserializePrimitive<T>(data);
            }

            // Handle Unity value types
            if (TryDeserializeUnityType<T>(data, out T unityResult))
            {
                return unityResult;
            }

            // Handle System types
            if (TryDeserializeSystemType<T>(data, out T systemResult))
            {
                return systemResult;
            }

            // Handle byte array directly
            if (type == typeof(byte[]))
            {
                return (T)(object)data;
            }

            // Handle enums
            if (type.IsEnum)
            {
                var underlyingType = Enum.GetUnderlyingType(type);
                var enumValue = DeserializePrimitive(data, underlyingType);
                return (T)Enum.ToObject(type, enumValue);
            }

            // Handle nullable types
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                if (data.Length == 0) return default;

                var underlyingType = Nullable.GetUnderlyingType(type);
                var method = typeof(UnityConverter).GetMethod(nameof(FromBytes)).MakeGenericMethod(underlyingType);
                return (T)method.Invoke(null, new object[] { data });
            }

            // Handle Texture2D
            if (type == typeof(Texture2D))
            {
                return (T)(object)GetTexture2D(data);
            }

            // Fallback: Use Marshal for value types, DataContractSerializer for reference types
            if (type.IsValueType)
            {
                try
                {
                    int size = Marshal.SizeOf(type);
                    if (data.Length == size)
                    {
                        IntPtr ptr = Marshal.AllocHGlobal(size);
                        Marshal.Copy(data, 0, ptr, size);
                        T obj = Marshal.PtrToStructure<T>(ptr);
                        Marshal.FreeHGlobal(ptr);
                        return obj;
                    }
                    else
                    {
                        // Size mismatch, try JSON
                        return DeserializeFromJson<T>(data);
                    }
                }
                catch
                {
                    // If Marshal fails, try JSON
                    return DeserializeFromJson<T>(data);
                }
            }
            else
            {
                // For reference types, try JSON first, then DataContract as fallback
                try
                {
                    return DeserializeFromJson<T>(data);
                }
                catch
                {
                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        var serializer = new DataContractSerializer(type);
                        return (T)serializer.ReadObject(ms);
                    }
                }
            }
        }



        /// <summary>
        /// Serializes primitive types using appropriate converter methods
        /// </summary>
        private static byte[] SerializePrimitive<T>(T data)
        {
            if (data is bool boolVal) return GetBytes(boolVal);
            if (data is byte byteVal) return GetBytes(byteVal);
            if (data is sbyte sbyteVal) return GetBytes(sbyteVal);
            if (data is short shortVal) return GetBytes(shortVal);
            if (data is ushort ushortVal) return GetBytes(ushortVal);
            if (data is int intVal) return GetBytes(intVal);
            if (data is uint uintVal) return GetBytes(uintVal);
            if (data is long longVal) return GetBytes(longVal);
            if (data is ulong ulongVal) return GetBytes(ulongVal);
            if (data is float floatVal) return GetBytes(floatVal);
            if (data is double doubleVal) return GetBytes(doubleVal);
            if (data is char charVal) return GetBytes(charVal);

            throw new NotSupportedException($"Primitive type {typeof(T)} is not supported");
        }

        /// <summary>
        /// Tries to serialize Unity types
        /// </summary>
        private static bool TrySerializeUnityType<T>(T data, out byte[] bytes)
        {
            bytes = null;

            if (data is Vector2 vector2) { bytes = GetBytes(vector2); return true; }
            if (data is Vector2Int vector2Int) { bytes = GetBytes(vector2Int); return true; }
            if (data is Vector3 vector3) { bytes = GetBytes(vector3); return true; }
            if (data is Vector3Int vector3Int) { bytes = GetBytes(vector3Int); return true; }
            if (data is Vector4 vector4) { bytes = GetBytes(vector4); return true; }
            if (data is Quaternion quaternion) { bytes = GetBytes(quaternion); return true; }
            if (data is Color color) { bytes = GetBytes(color); return true; }
            if (data is Color32 color32) { bytes = GetBytes(color32); return true; }
            if (data is Rect rect) { bytes = GetBytes(rect); return true; }
            if (data is Bounds bounds) { bytes = GetBytes(bounds); return true; }
            if (data is Ray ray) { bytes = GetBytes(ray); return true; }
            if (data is Ray2D ray2D) { bytes = GetBytes(ray2D); return true; }
            if (data is Matrix4x4 matrix4x4) { bytes = GetBytes(matrix4x4); return true; }

            return false;
        }

        /// <summary>
        /// Tries to serialize System types
        /// </summary>
        private static bool TrySerializeSystemType<T>(T data, out byte[] bytes)
        {
            bytes = null;

            if (data is DateTime dateTime) { bytes = GetBytes(dateTime); return true; }
            if (data is TimeSpan timeSpan) { bytes = GetBytes(timeSpan); return true; }
            if (data is Guid guid) { bytes = GetBytes(guid); return true; }

            return false;
        }

        /// <summary>
        /// Deserializes primitive types using appropriate converter methods
        /// </summary>
        private static T DeserializePrimitive<T>(byte[] data)
        {
            var type = typeof(T);

            if (type == typeof(bool)) return (T)(object)GetBool(data);
            if (type == typeof(byte)) return (T)(object)GetByte(data);
            if (type == typeof(sbyte)) return (T)(object)GetSByte(data);
            if (type == typeof(short)) return (T)(object)GetShort(data);
            if (type == typeof(ushort)) return (T)(object)GetUShort(data);
            if (type == typeof(int)) return (T)(object)GetInt(data);
            if (type == typeof(uint)) return (T)(object)GetUInt(data);
            if (type == typeof(long)) return (T)(object)GetLong(data);
            if (type == typeof(ulong)) return (T)(object)GetULong(data);
            if (type == typeof(float)) return (T)(object)GetFloat(data);
            if (type == typeof(double)) return (T)(object)GetDouble(data);
            if (type == typeof(char)) return (T)(object)GetChar(data);

            throw new NotSupportedException($"Primitive type {type} is not supported");
        }

        /// <summary>
        /// Deserializes primitive types (non-generic version for enums)
        /// </summary>
        private static object DeserializePrimitive(byte[] data, Type type)
        {
            if (type == typeof(bool)) return GetBool(data);
            if (type == typeof(byte)) return GetByte(data);
            if (type == typeof(sbyte)) return GetSByte(data);
            if (type == typeof(short)) return GetShort(data);
            if (type == typeof(ushort)) return GetUShort(data);
            if (type == typeof(int)) return GetInt(data);
            if (type == typeof(uint)) return GetUInt(data);
            if (type == typeof(long)) return GetLong(data);
            if (type == typeof(ulong)) return GetULong(data);
            if (type == typeof(float)) return GetFloat(data);
            if (type == typeof(double)) return GetDouble(data);
            if (type == typeof(char)) return GetChar(data);

            throw new NotSupportedException($"Primitive type {type} is not supported");
        }

        /// <summary>
        /// Tries to deserialize Unity types
        /// </summary>
        private static bool TryDeserializeUnityType<T>(byte[] data, out T result)
        {
            result = default;
            var type = typeof(T);

            if (type == typeof(Vector2)) { result = (T)(object)GetVector2(data); return true; }
            if (type == typeof(Vector2Int)) { result = (T)(object)GetVector2Int(data); return true; }
            if (type == typeof(Vector3)) { result = (T)(object)GetVector3(data); return true; }
            if (type == typeof(Vector3Int)) { result = (T)(object)GetVector3Int(data); return true; }
            if (type == typeof(Vector4)) { result = (T)(object)GetVector4(data); return true; }
            if (type == typeof(Quaternion)) { result = (T)(object)GetQuaternion(data); return true; }
            if (type == typeof(Color)) { result = (T)(object)GetColor(data); return true; }
            if (type == typeof(Color32)) { result = (T)(object)GetColor32(data); return true; }
            if (type == typeof(Rect)) { result = (T)(object)GetRect(data); return true; }
            if (type == typeof(Bounds)) { result = (T)(object)GetBounds(data); return true; }
            if (type == typeof(Ray)) { result = (T)(object)GetRay(data); return true; }
            if (type == typeof(Ray2D)) { result = (T)(object)GetRay2D(data); return true; }
            if (type == typeof(Matrix4x4)) { result = (T)(object)GetMatrix4x4(data); return true; }

            return false;
        }

        /// <summary>
        /// Tries to deserialize System types
        /// </summary>
        private static bool TryDeserializeSystemType<T>(byte[] data, out T result)
        {
            result = default;
            var type = typeof(T);

            if (type == typeof(DateTime)) { result = (T)(object)GetDateTime(data); return true; }
            if (type == typeof(TimeSpan)) { result = (T)(object)GetTimeSpan(data); return true; }
            if (type == typeof(Guid)) { result = (T)(object)GetGuid(data); return true; }

            return false;
        }

        /// <summary>
        /// Serializes complex objects as JSON
        /// </summary>
        private static byte[] SerializeAsJson<T>(T data)
        {
            string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(data);
            return GetBytes(jsonString);
        }

        /// <summary>
        /// Deserializes complex objects from JSON
        /// </summary>
        private static T DeserializeFromJson<T>(byte[] data)
        {
            string jsonString = GetString(data);

            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(jsonString);

        }



    }
}