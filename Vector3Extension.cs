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
    }

}
