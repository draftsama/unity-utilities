using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;


namespace Modules.Utilities
{
    public static class TransformExtension
    {
       


        public static UniTask LerpPositionAsync(this Transform _transform, int _milliseconds,
            Vector3 _target, bool _isLocal = false, Easing.Ease _ease = Easing.Ease.EaseInOutQuad,
            bool _ignoreTimeScale = false,
            CancellationToken _token = default)
        {
            var token = _token;
            if (token == default) token = _transform.GetCancellationTokenOnDestroy();

            var uts = new UniTaskCompletionSource();
            UniTask.Void(async () =>
            {
                try
                {
                    var progress = 0f;
                    var current = _isLocal ? _transform.localPosition : _transform.position;
                    float seconds = _milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS;
                    Vector3 valueTarget;


                    while (!token.IsCancellationRequested)
                    {
                        var deltaTime = _ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                        progress += deltaTime / seconds;
                        if (progress < 1)
                        {
                            valueTarget = EasingFormula.EaseTypeVector(_ease, current, _target, progress);

                            if (_isLocal)
                                _transform.localPosition = valueTarget;
                            else
                                _transform.position = valueTarget;
                        }
                        else
                        {
                            valueTarget = _target;
                            if (_isLocal)
                                _transform.localPosition = valueTarget;
                            else
                                _transform.position = valueTarget;

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


        public static UniTask LerpRotationAsync(this Transform _transform, int _milliseconds,
            Quaternion _target, bool _isLocal = false, Easing.Ease _ease = Easing.Ease.EaseInOutQuad,
            bool _ignoreTimeScale = false,
            CancellationToken _token = default)
        {
            var token = _token;
            if (token == default) token = _transform.GetCancellationTokenOnDestroy();

            var uts = new UniTaskCompletionSource();
            UniTask.Void(async () =>
            {
                try
                {
                    var progress = 0f;
                    var current = _isLocal ? _transform.localRotation : _transform.rotation;
                    float seconds = _milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS;
                    Quaternion valueTarget;


                    while (!token.IsCancellationRequested)
                    {
                        var deltaTime = _ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                        progress += deltaTime / seconds;
                        if (progress < 1)
                        {
                            valueTarget.x = EasingFormula.EasingFloat(_ease, current.x, _target.x, progress);
                            valueTarget.y = EasingFormula.EasingFloat(_ease, current.y, _target.y, progress);
                            valueTarget.z = EasingFormula.EasingFloat(_ease, current.z, _target.z, progress);
                            valueTarget.w = EasingFormula.EasingFloat(_ease, current.w, _target.w, progress);

                            if (_isLocal)
                                _transform.localRotation = valueTarget;
                            else
                                _transform.rotation = valueTarget;
                        }
                        else
                        {
                            valueTarget = _target;
                            if (_isLocal)
                                _transform.localRotation = valueTarget;
                            else
                                _transform.rotation = valueTarget;

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



        public static UniTask LerpScaleAsync(this Transform _transform, int _milliseconds,
            Vector3 _target, Easing.Ease _ease = Easing.Ease.EaseInOutQuad, bool _ignoreTimeScale = false,
            CancellationToken _token = default)
        {
            var token = _token;
            if (token == default) token = _transform.GetCancellationTokenOnDestroy();

            var uts = new UniTaskCompletionSource();
            UniTask.Void(async () =>
            {
                try
                {
                    var progress = 0f;
                    var current = _transform.localScale;
                    float seconds = _milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS;
                    Vector3 valueTarget;


                    while (!token.IsCancellationRequested)
                    {
                        var deltaTime = _ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                        progress += deltaTime / seconds;
                        if (progress < 1)
                        {
                            valueTarget = EasingFormula.EaseTypeVector(_ease, current, _target, progress);
                            _transform.localScale = valueTarget;
                        }
                        else
                        {
                            valueTarget = _target;
                            _transform.localScale = valueTarget;


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

        public static IUniTaskAsyncEnumerable<AsyncUnit> FloatingAnimationAsyncEnumerable(
            this Transform _transform, float _speed, float _radius, bool _isLocal = false,
            Easing.Ease _ease = Easing.Ease.EaseInOutQuad, bool _ignoreTimeScale = false)
        {
            // writer(IAsyncWriter<T>) has `YieldAsync(value)` method.
            return UniTaskAsyncEnumerable.Create<AsyncUnit>(async (writer, token) =>
            {
                float progress = 0;
                var position = _isLocal ? _transform.localPosition : _transform.position;

                Vector3 currentPos = position;
                Vector3 startPos = position;
                Vector3 targetPos = RandomPosition(_radius) + startPos;
                Vector3 valueTarget;
                await UniTask.Yield();
                while (!token.IsCancellationRequested)
                {
                    //  await writer.YieldAsync(default);
                    var deltaTime = _ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                    progress += deltaTime * _speed * 0.1f;
                    valueTarget = EasingFormula.EaseTypeVector(_ease, currentPos, targetPos, progress);

                    if (_isLocal)
                        _transform.localPosition = valueTarget;
                    else
                        _transform.position = valueTarget;

                    if (progress >= 1)
                    {
                        targetPos = RandomPosition(_radius) + startPos;
                        currentPos = _isLocal ? _transform.localPosition : _transform.position;
                        progress = 0;
                    }

                    await UniTask.Yield();
                }
            });
        }

        static Vector3 RandomPosition(float _radius)
        {
            float angle = UnityEngine.Random.Range(0, 360);
            float randomRadius = UnityEngine.Random.Range(0, _radius);
            return Quaternion.Euler(0, 0, angle) * new Vector3(0, randomRadius, 0);
        }

    }
}