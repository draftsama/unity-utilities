using System;
using UniRx;

namespace Modules.Utilities
{
	internal static class LerpThread
	{
		//--------------------------------------------------------------------------------------------------------------

		public static IDisposable Execute(int _miliseconds, Action<long> _onNext, Action _onComplete = null)
		{
			return Observable
				.EveryUpdate()
				.Take(TimeSpan.FromMilliseconds(_miliseconds))
				.Subscribe
				(
					_onNext,
					_onComplete
				);
		}
		
		//--------------------------------------------------------------------------------------------------------------
	}
}