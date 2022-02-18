using System;
using System.Threading;
using UniRx;
using UnityEngine;
#if UNITASK
using Cysharp.Threading.Tasks;
#endif


namespace Modules.Utilities
{
    public static class ParticleExtension
    {
        public static void SetEmission(this ParticleSystem _particle, bool _enable)
        {
            var emission = _particle.emission;
            emission.enabled = _enable;
        }

        public static IDisposable LerpAlpha(this ParticleSystem _particle, float _targetAlpha, float _second,
            Easing.Ease _ease = Easing.Ease.EaseInOutQuad, bool _ignoreTimeScale = false, Action _onCompleted = null)
        {
            var progress = 0f;
            var mainParticle = _particle.main;
            var main = mainParticle;
            var startColor = mainParticle.startColor.color;
            var startAlpha = startColor.a;

            IDisposable disposable = null;
            disposable = Observable.EveryUpdate().Subscribe(_ =>
            {
                var deltaTime = _ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;

                progress += deltaTime / _second;

                if (progress < 1)
                {
                    var currentAlpha = EasingFormula.EasingFloat(_ease, startAlpha, _targetAlpha, progress);
                    var currentColor = startColor;
                    currentColor.a = currentAlpha;
                    main.startColor = currentColor;
                }
                else
                {
                    disposable?.Dispose();
                    var currentColor = startColor;
                    currentColor.a = _targetAlpha;
                    main.startColor = currentColor;
                    _onCompleted?.Invoke();
                }
            });

            return disposable;
        }

#if UNITASK
        public static UniTask LerpAlphaAsync(this ParticleSystem _particle, float _targetAlpha, float _second,
            Easing.Ease _ease = Easing.Ease.EaseInOutQuad, bool _ignoreTimeScale = false,
            CancellationToken _token = default)
        {
            var token = _token;
            if (token == default) token = _particle.GetCancellationTokenOnDestroy();

            var uts = new UniTaskCompletionSource();
            UniTask.Void(async () =>
            {
                try
                {
                    var progress = 0f;
                    var mainParticle = _particle.main;
                    var main = mainParticle;
                    var startColor = mainParticle.startColor.color;
                    var startAlpha = startColor.a;


                    while (!token.IsCancellationRequested)
                    {
                        var deltaTime = _ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;

                        progress += deltaTime / _second;

                        if (progress < 1)
                        {
                            var currentAlpha = EasingFormula.EasingFloat(_ease, startAlpha, _targetAlpha, progress);
                            var currentColor = startColor;
                            currentColor.a = currentAlpha;
                            main.startColor = currentColor;
                        }
                        else
                        {
                            var currentColor = startColor;
                            currentColor.a = _targetAlpha;
                            main.startColor = currentColor;
                            uts.TrySetResult();
                            break;
                        }

                        await UniTask.Yield();
                    }
                }
                catch (OperationCanceledException) when (_token.IsCancellationRequested)
                {
                    uts.TrySetCanceled();
                }
                catch (System.Exception e)
                {
                    uts.TrySetException(e);
                }
            });


            return uts.Task;
        }
#endif
    }
}