using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modules.Utilities
{
    public static class Vector3Extension
    {
        public static Vector2 ToXY(this Vector3 _vector){

            return new Vector2(_vector.x,_vector.y);
        }
         public static Vector2 ToXZ(this Vector3 _vector){

            return new Vector2(_vector.x,_vector.z);
        }
         public static Vector3 ToSpectifyAxis(this Vector3 _vector,Vector3 _select){
             _select.Normalize();
             return new Vector3(_vector.x * _select.x,_vector.y * _select.y,_vector.z * _select.z);
         }
    }

}
