using System.Threading;
using System;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using UnityEngine;

namespace Modules.Utilities
{
    public static class LerpThread
    {

        //--------------------------------------------------------------------------------------------------------------

        public static IUniTaskAsyncEnumerable<float> FloatLerpAsyncEnumerable(int _milliseconds, float _start, float _target, Easing.Ease _ease = Easing.Ease.EaseInOutQuad, bool _useUnscaleTime = false, CancellationToken _token = default)
        {

            return UniTaskAsyncEnumerable.Create<float>(async (writer, token) =>
            {
                var progress = 0f;
                await UniTask.Yield();

                var cancellationToken = _token != default ? _token : token;

                while (!cancellationToken.IsCancellationRequested)
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