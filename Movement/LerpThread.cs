using System.Threading;
using System;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using UniRx;
using UnityEngine;

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

        public static IObservable<float> FloatLerp(int _milliseconds, float _start, float _target, Easing.Ease _ease = Easing.Ease.EaseInOutQuad, bool _useUnscaleTime = false)
        {

            return Observable.Create<float>(_observer =>
            {
                var progress = 0f;
                var seconds = (_milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS);

                IDisposable disposable = null;
                disposable = Observable.EveryUpdate()
                     .Subscribe(_ =>
                     {
                         var deltaTime = _useUnscaleTime ? Time.unscaledDeltaTime : Time.deltaTime;
                         progress += deltaTime / seconds;
                         if (progress < 1)
                         {
                             var valueTarget = EasingFormula.EasingFloat(_ease, _start, _target, progress);
                             _observer.OnNext(valueTarget);
                         }
                         else
                         {
                             _observer.OnNext(_target);
                             _observer.OnCompleted();
                             disposable?.Dispose();
                         }

                     });


                return Disposable.Create(() => disposable?.Dispose());
            });
        }

        //--------------------------------------------------------------------------------------------------------------

        public static IUniTaskAsyncEnumerable<float> FloatLerpAsyncEnumerable(int _milliseconds, float _start, float _target, Easing.Ease _ease = Easing.Ease.EaseInOutQuad, bool _useUnscaleTime = false)
        {

            return UniTaskAsyncEnumerable.Create<float>(async (writer, token) =>
            {
                var progress = 0f;
                await UniTask.Yield();

                while (!token.IsCancellationRequested)
                {
                    var deltaTime = _useUnscaleTime ? Time.unscaledDeltaTime : Time.deltaTime;

                    progress += deltaTime / (_milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS);
                    if (progress < 1)
                    {
                        var valueTarget = EasingFormula.EasingFloat(_ease, _start, _target, progress);
                        await writer.YieldAsync(valueTarget);
                    }
                    else
                    {
                        await writer.YieldAsync(_target);
                        break;
                    }
                    await UniTask.Yield();

                }

            });


        }
    }
}