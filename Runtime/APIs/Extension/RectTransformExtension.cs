using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;

namespace Modules.Utilities
{
    public static class RectTransformExtension
    {

        public static async UniTask ShakeAsync(this RectTransform _rectTransform, float _duration = -1, float interval = 0.1f, float _strength = 20f,
            CancellationToken _token = default)
        {
            var token = _token;
            if (token == default) token = _rectTransform.GetCancellationTokenOnDestroy();

            if (_duration < 0)
            {
                _duration = float.MaxValue;
            }

            var uts = new UniTaskCompletionSource();

            try
            {
                var startTime = Time.time;
                var originalPosition = _rectTransform.anchoredPosition;
                while (!token.IsCancellationRequested && (Time.time - startTime) < _duration)
                {
                    var randomVector2 = UnityEngine.Random.insideUnitCircle * _strength; // Ensure the random seed is different each time
                    _rectTransform.anchoredPosition = originalPosition + randomVector2;
                    await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: token);
                }

                _rectTransform.anchoredPosition = originalPosition;
                uts.TrySetResult();
            }
            catch (OperationCanceledException) when (_token.IsCancellationRequested)
            {
                uts.TrySetCanceled();
            }
            catch (System.Exception e)
            {
                uts.TrySetException(e);
            }finally
            {
                _rectTransform.anchoredPosition = _rectTransform.anchoredPosition; // Ensure the position is set back to the original
            }

            await uts.Task;

        }

        static Vector2 RandomPosition(float _radius)
        {
            float angle = UnityEngine.Random.Range(0, 360);
            float randomRadius = UnityEngine.Random.Range(0, _radius);
            return Quaternion.Euler(0, 0, angle) * new Vector3(0, randomRadius, 0);
        }



        public static IUniTaskAsyncEnumerable<AsyncUnit> FloatingAnimationAsyncEnumerable(
            this RectTransform _rectTransform, float _speed, float _radius,
            Easing.Ease _ease = Easing.Ease.EaseInOutQuad, bool _ignoreTimeScale = false)
        {
            // writer(IAsyncWriter<T>) has `YieldAsync(value)` method.
            return UniTaskAsyncEnumerable.Create<AsyncUnit>(async (writer, token) =>
            {
                float progress = 0;
                var anchoredPosition = _rectTransform.anchoredPosition;

                Vector2 currentPos = anchoredPosition;
                Vector2 startPos = anchoredPosition;
                Vector2 targetPos = RandomPosition(_radius) + startPos;

                await UniTask.Yield();
                while (!token.IsCancellationRequested)
                {
                    //  await writer.YieldAsync(default);
                    var deltaTime = _ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                    progress += deltaTime * _speed * 0.1f;
                    _rectTransform.anchoredPosition =
                        EasingFormula.EaseTypeVector(_ease, currentPos, targetPos, progress);

                    if (progress >= 1)
                    {
                        targetPos = RandomPosition(_radius) + startPos;
                        currentPos = _rectTransform.anchoredPosition;
                        progress = 0;
                    }

                    await UniTask.Yield();
                }
            });
        }

        public static UniTask LerpAnchorPositionAsync(this RectTransform _rectTransform, int _milliseconds,
            Vector2 _target, Easing.Ease _ease = Easing.Ease.EaseInOutQuad, bool _ignoreTimeScale = false,
            CancellationToken _token = default)
        {
            var token = _token;
            if (token == default) token = _rectTransform.GetCancellationTokenOnDestroy();

            var uts = new UniTaskCompletionSource();
            UniTask.Void(async () =>
            {
                try
                {
                    var progress = 0f;
                    var current = _rectTransform.anchoredPosition;
                    float seconds = _milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS;
                    Vector2 valueTarget;


                    while (!token.IsCancellationRequested)
                    {
                        var deltaTime = _ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                        progress += deltaTime / seconds;
                        if (progress < 1)
                        {
                            valueTarget.x = EasingFormula.EasingFloat(_ease, current.x, _target.x, progress);
                            valueTarget.y = EasingFormula.EasingFloat(_ease, current.y, _target.y, progress);
                            _rectTransform.anchoredPosition = valueTarget;
                        }
                        else
                        {
                            valueTarget = _target;
                            _rectTransform.anchoredPosition = valueTarget;
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

        public static UniTask LerpWidthAsync(this RectTransform _rectTransform, int _milliseconds, float _target,
            Easing.Ease _ease = Easing.Ease.EaseInOutQuad, bool _ignoreTimeScale = false,
            CancellationToken _token = default)
        {
            var sizeTarget = new Vector2(_target, _rectTransform.rect.height);
            return LerpSizeAsync(_rectTransform, _milliseconds, sizeTarget, _ease, _ignoreTimeScale, _token);
        }

        public static UniTask LerpHeightAsync(this RectTransform _rectTransform, int _milliseconds, float _target,
            Easing.Ease _ease = Easing.Ease.EaseInOutQuad, bool _ignoreTimeScale = false,
            CancellationToken _token = default)
        {
            var sizeTarget = new Vector2(_rectTransform.rect.width, _target);
            return LerpSizeAsync(_rectTransform, _milliseconds, sizeTarget, _ease, _ignoreTimeScale, _token);
        }

        public static UniTask LerpRotationZAsync(this RectTransform _rectTransform, int _milliseconds, float _target,
            Easing.Ease _ease = Easing.Ease.EaseInOutQuad, bool _ignoreTimeScale = false, bool _useLocal = false,
            CancellationToken _token = default)
        {
            var token = _token;
            if (token == default) token = _rectTransform.GetCancellationTokenOnDestroy();

            var uts = new UniTaskCompletionSource();
            UniTask.Void(async () =>
            {
                try
                {
                    var progress = 0f;
                    var currentRotation = _useLocal
                        ? _rectTransform.localRotation
                        : _rectTransform.rotation;
                    var rotationTarget = Quaternion.Euler(0, 0, _target);
                    float seconds = _milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS;
                    Quaternion valueTarget;

                    while (!token.IsCancellationRequested)
                    {
                        var deltaTime = _ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                        progress += deltaTime / seconds;
                        if (progress < 1)
                        {
                            valueTarget.x =
                                EasingFormula.EasingFloat(_ease, currentRotation.x, rotationTarget.x, progress);
                            valueTarget.y =
                                EasingFormula.EasingFloat(_ease, currentRotation.y, rotationTarget.y, progress);
                            valueTarget.z =
                                EasingFormula.EasingFloat(_ease, currentRotation.z, rotationTarget.z, progress);
                            valueTarget.w =
                                EasingFormula.EasingFloat(_ease, currentRotation.w, rotationTarget.w, progress);

                            if (_useLocal)
                                _rectTransform.localRotation = valueTarget;
                            else
                                _rectTransform.rotation = valueTarget;
                        }
                        else
                        {
                            valueTarget = rotationTarget;
                            if (_useLocal)
                                _rectTransform.localRotation = valueTarget;
                            else
                                _rectTransform.rotation = valueTarget;

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

        public static UniTask LerpSizeAsync(this RectTransform _rectTransform, int _milliseconds, Vector2 _target,
            Easing.Ease _ease = Easing.Ease.EaseInOutQuad, bool _ignoreTimeScale = false,
            CancellationToken _token = default)
        {
            var token = _token;
            if (token == default) token = _rectTransform.GetCancellationTokenOnDestroy();

            var uts = new UniTaskCompletionSource();
            UniTask.Void(async () =>
            {
                try
                {
                    var progress = 0f;
                    var rect = _rectTransform.rect;
                    var currentSize = new Vector2(rect.width, rect.height);
                    float seconds = _milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS;
                    Vector2 valueTarget;

                    while (!token.IsCancellationRequested)
                    {
                        var deltaTime = _ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                        progress += deltaTime / seconds;
                        if (progress < 1)
                        {
                            valueTarget.x = EasingFormula.EasingFloat(_ease, currentSize.x, _target.x, progress);
                            valueTarget.y = EasingFormula.EasingFloat(_ease, currentSize.y, _target.y, progress);
                            _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, valueTarget.x);
                            _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, valueTarget.y);
                        }
                        else
                        {
                            valueTarget = _target;
                            _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, valueTarget.x);
                            _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, valueTarget.y);
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
    }
}