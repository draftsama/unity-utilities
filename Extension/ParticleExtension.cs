using System;

using UniRx;
using UnityEngine;

namespace Modules.Utilities
{
    public static class ParticleExtension
    {
        public static void SetEmission(this ParticleSystem _particle, bool _enable)
        {
            var emission = _particle.emission;
            emission.enabled = _enable;
        }

        public static IObservable<Unit> LerpAlpha(this ParticleSystem _particle, float _targetAlpha, float _second, Easing.Ease _ease = Easing.Ease.EaseInOutQuad)
        {
            return Observable.Create<Unit>
            (
                _observer => 
                {
                    var progress = 0f;
                    var mainParticle = _particle.main;
                    var main = mainParticle;
                    var startColor = mainParticle.startColor.color;
                    var startAlpha = startColor.a;
                    IDisposable disposable = LerpThread
                    .Execute
                    (
                        Mathf.RoundToInt(_second * GlobalConstant.SECONDS_TO_MILLISECONDS),
                        _count =>
                        {
                            progress += Time.deltaTime / _second;
                            var currentAlpha = EasingFormula.EasingFloat(_ease, startAlpha, _targetAlpha, progress);
                            var currentColor = startColor;
                            currentColor.a = currentAlpha;
                            main.startColor = currentColor;
                        },
                        () =>
                        {
                            var currentColor = startColor;
                            currentColor.a = _targetAlpha;
                            
                            main.startColor = currentColor;
                            _observer.OnNext(default(Unit));
                            _observer.OnCompleted();

                        }
                    );

                    return Disposable.Create(() => disposable?.Dispose());
                }
            );
        }
    }
}
