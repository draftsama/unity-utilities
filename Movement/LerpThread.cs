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

        public static IObservable<float> FloatLerp(int _milliseconds, float _start, float _target, Easing.Ease _ease = Easing.Ease.EaseInOutQuad)
        {

            return Observable.Create<float>(_observer =>
            {
                var progress = 0f;
                IDisposable disposable = LerpThread
                    .Execute(
                        _milliseconds,
                        _count =>
                        {
                            progress += UnityEngine.Time.deltaTime / (_milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS);
                            var valueTarget = EasingFormula.EasingFloat(_ease, _start, _target, progress);
                            _observer.OnNext(valueTarget);

                        },
                        () =>
                        {

                            _observer.OnNext(_target);
                            _observer.OnCompleted();

                        }
                    );

                return Disposable.Create(() => disposable?.Dispose());
            });
        }

        //--------------------------------------------------------------------------------------------------------------
    }
}