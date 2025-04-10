using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;


namespace Modules.Utilities
{
    public static class ParticleExtension
    {
        public static void SetEmission(this ParticleSystem _particle, bool _enable)
        {
            var emission = _particle.emission;
            emission.enabled = _enable;
        }

        public static async UniTask SetEmissionAsync(this ParticleSystem _particle, bool _enable)
        {
            var emission = _particle.emission;
            emission.enabled = _enable;
            await UniTask.CompletedTask;
        }

        public static async UniTask LerpAlphaAsync(this ParticleSystem _particle, float _targetAlpha, float _second,
            Easing.Ease _ease = Easing.Ease.EaseInOutQuad, bool _ignoreTimeScale = false,
            CancellationToken _token = default)
        {
            var token = _token;
            if (token == default) token = _particle.GetCancellationTokenOnDestroy();

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
                    break;
                }

                await UniTask.Yield();
            }
        }
    }
}