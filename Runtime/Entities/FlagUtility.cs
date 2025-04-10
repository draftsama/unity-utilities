using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Modules.Utilities
{
  public class Flag
	{
		public Flag() : this(0) { }
		public Flag(int mask) { m_Value = mask; }

		public int value { get { return m_Value; } }
		private int m_Value;

		//Add the mask to flags
		public int TurnOn(int mask)
		{
			return m_Value |= mask;
		}

		//Remove the mask from flags
		public int TurnOff(int mask)
		{
			return m_Value &= ~mask;
		}

		//Toggle the mask into flags
		public int Toggle(int mask)
		{
			return m_Value ^= mask;
		}

		//Check if mask is on
		public bool Check(int mask)
		{
			return (m_Value & mask) == mask;
		}
	}

	public static class Flags
	{
		public static void Set<T>(ref T mask, T flag) where T : struct
		{
			int maskValue = (int)(object)mask;
			int flagValue = (int)(object)flag;

			mask = (T)(object)(maskValue | flagValue);
		}

		public static void Unset<T>(ref T mask, T flag) where T : struct
		{
			int maskValue = (int)(object)mask;
			int flagValue = (int)(object)flag;

			mask = (T)(object)(maskValue & (~flagValue));
		}

		public static void Toggle<T>(ref T mask, T flag) where T : struct
		{
			if (Contains(mask, flag))
			{
				Unset<T>(ref mask, flag);
			}
			else
			{
				Set<T>(ref mask, flag);
			}
		}

		public static bool Contains<T>(T mask, T flag) where T : struct
		{
			return Contains((int)(object)mask, (int)(object)flag);
		}

		public static bool Contains(int mask, int flag)
		{
			return (mask & flag) != 0;
		}
	}

    
}
