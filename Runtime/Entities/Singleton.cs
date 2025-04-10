using UnityEngine;

namespace Modules.Utilities
{
	public class Singleton<T> : MonoBehaviour
	{
		//-----------------------------------------------------------------

		public static T Instance;
	
		//-----------------------------------------------------------------

		protected virtual void Awake() => Instance    =   GetComponent<T>();

		//-----------------------------------------------------------------
	}
}