using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Modules.Utilities
{
	public class Loom : MonoBehaviour
	{
		public static int MaxThreads = 8;
		static int _NumThreads;

		private static Loom _Current;
		private int _Count;
		public static Loom Current
		{
			get
			{
				Initialize();
				return _Current;
			}
		}

		void Awake()
		{
			_Current = this;
			_Initialized = true;
		}

		static bool _Initialized;

		static void Initialize()
		{
			if (!_Initialized)
			{

				if (!Application.isPlaying)
					return;
				_Initialized = true;
				var g = new GameObject("Loom");
				_Current = g.AddComponent<Loom>();
			}

		}

		private List<Action> _actions = new List<Action>();
		public struct DelayedQueueItem
		{
			public float time;
			public Action action;
		}
		private List<DelayedQueueItem> _delayed = new List<DelayedQueueItem>();

		List<DelayedQueueItem> _currentDelayed = new List<DelayedQueueItem>();

		public static void QueueOnMainThread(Action action)
		{
			QueueOnMainThread(action, 0f);
		}
		public static void QueueOnMainThread(Action action, float time)
		{
			if (time != 0)
			{
				lock (Current._delayed)
				{
					Current._delayed.Add(new DelayedQueueItem { time = Time.time + time, action = action });
				}
			}
			else
			{
				lock (Current._actions)
				{
					Current._actions.Add(action);
				}
			}
		}

		public static Thread RunAsync(Action a)
		{
			Initialize();
			while (_NumThreads >= MaxThreads)
			{
				Thread.Sleep(1);
			}
			Interlocked.Increment(ref _NumThreads);
			ThreadPool.QueueUserWorkItem(RunAction, a);
			return null;
		}

		private static void RunAction(object action)
		{
			try
			{
				((Action)action)();
			}
			catch
			{
			}
			finally
			{
				Interlocked.Decrement(ref _NumThreads);
			}

		}


		void OnDisable()
		{
			if (_Current == this)
			{

				_Current = null;
			}
		}



		// Use this for initialization
		void Start()
		{
			_actions = new List<Action>();
			_currentActions = new List<Action>();
			_delayed = new List<DelayedQueueItem>();
			_currentDelayed = new List<DelayedQueueItem>();
		}

		List<Action> _currentActions = new List<Action>();

		public Loom(int _count)
		{
			this._Count = _count;
		}

		// Update is called once per frame
		void Update()
		{
			lock (_actions)
			{
				_currentActions.Clear();
				_currentActions.AddRange(_actions);
				_actions.Clear();
			}
			foreach (var a in _currentActions)
			{
				a();
			}
			lock (_delayed)
			{
				_currentDelayed.Clear();
				_currentDelayed.AddRange(_delayed.Where(d => d.time <= Time.time));
				foreach (var item in _currentDelayed)
					_delayed.Remove(item);
			}
			foreach (var delayed in _currentDelayed)
			{
				delayed.action();
			}



		}
	}
}