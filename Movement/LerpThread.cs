using System;
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
    }
}