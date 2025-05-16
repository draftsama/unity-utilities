using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;

namespace Modules.Utilities
{
	public static class ButtonExtension
	{
		//--------------------------------------------------------------------------------------------------------------

		public static bool SetInteractive(this Button _source, bool _interactAble)
		{
			return _source.interactable = _interactAble;
		}



		public static UniTask OnPointerDownAsync(this UIButton button, CancellationToken cancellationToken)
		{
			return new AsyncUnityEventHandler(button.onPointerDown, cancellationToken, true).OnInvokeAsync();
		}

		public static UniTask OnPointerUpAsync(this UIButton button, CancellationToken cancellationToken)
		{
			return new AsyncUnityEventHandler(button.onPointerUp, cancellationToken, true).OnInvokeAsync();
		}

		public static IUniTaskAsyncEnumerable<AsyncUnit> OnPointerDownEnumerable(this UIButton button, CancellationToken cancellationToken)
		{
			return new UnityEventHandlerAsyncEnumerable(button.onPointerDown, cancellationToken);
		}
		
		public static IUniTaskAsyncEnumerable<AsyncUnit> OnPointerUpEnumerable(this UIButton button, CancellationToken cancellationToken)
		{
			return new UnityEventHandlerAsyncEnumerable(button.onPointerUp, cancellationToken);
		}

		
		//--------------------------------------------------------------------------------------------------------------
	}
}