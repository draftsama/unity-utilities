using System;
using System.Threading;
using UnityEngine;
using UniRx;

#if UNITASK
using Cysharp.Threading.Tasks;
#endif

namespace Modules.Utilities
{
    public static class CanvasGroupExtension
    {
        //--------------------------------------------------------------------------------------------------------------

        private const Easing.Ease _DEFAULT_EASE_TYPE = Easing.Ease.Linear;

        private const int _DEFAULT_SOURCE_CURRENT_ALPHA = -1;
        //--------------------------------------------------------------------------------------------------------------

        public static IDisposable LerpAlpha(this CanvasGroup _source, int _milliseconds, float _target,
            Action _onComplete = null)
        {
            return _source.EasingLerpAlpha(_milliseconds, _target, _DEFAULT_SOURCE_CURRENT_ALPHA, _DEFAULT_EASE_TYPE,
                true, false, _onComplete);
        }

        //--------------------------------------------------------------------------------------------------------------
        public static IDisposable LerpAlpha(this CanvasGroup _source, int _milliseconds, float _target,
            bool _ignoreTimeScale = false, Action _onComplete = null)
        {
            return _source.EasingLerpAlpha(_milliseconds, _target, _DEFAULT_SOURCE_CURRENT_ALPHA, _DEFAULT_EASE_TYPE,
                true, _ignoreTimeScale, _onComplete);
        }

        //--------------------------------------------------------------------------------------------------------------
        public static IDisposable LerpAlpha(this CanvasGroup _source, int _milliseconds, float _target,
            bool _adjustInteractAble = true, bool _ignoreTimeScale = false, Action _onComplete = null)
        {
            return _source.EasingLerpAlpha(_milliseconds, _target, _DEFAULT_SOURCE_CURRENT_ALPHA, _DEFAULT_EASE_TYPE,
                _adjustInteractAble, _ignoreTimeScale, _onComplete);
        }


        //--------------------------------------------------------------------------------------------------------------

        public static IDisposable LerpAlpha(this CanvasGroup _source, int _milliseconds, float _target, float _start,
            bool _adjustInteractAble = true, bool _ignoreTimeScale = false, Action _onComplete = null)
        {
            return _source.EasingLerpAlpha(_milliseconds, _target, _start, _DEFAULT_EASE_TYPE, _adjustInteractAble,
                _ignoreTimeScale, _onComplete);
        }

        //--------------------------------------------------------------------------------------------------------------

        public static IDisposable EasingLerpAlpha(this CanvasGroup _source, int _milliseconds, float _target,
            Easing.Ease _ease = _DEFAULT_EASE_TYPE, bool _adjustInteractAble = true, bool _ignoreTimeScale = false,
            Action _onComplete = null)
        {
            return _source.EasingLerpAlpha(_milliseconds, _target, _DEFAULT_SOURCE_CURRENT_ALPHA, _ease,
                _adjustInteractAble, _ignoreTimeScale, _onComplete);
        }

        //--------------------------------------------------------------------------------------------------------------

        public static IDisposable EasingLerpAlpha(this CanvasGroup _source, int _milliseconds, float _target,
            float _start, Easing.Ease _ease = _DEFAULT_EASE_TYPE, bool _adjustInteractAble = true,
            bool _ignoreTimeScale = false, Action _onComplete = null)
        {
            var progress = 0f;
            var current = Math.Abs(_start - _DEFAULT_SOURCE_CURRENT_ALPHA) < GlobalConstant.FLOAT_MINIMUM_TOLERANCE
                ? _source.alpha
                : _start;
            var different = _target - current;

            IDisposable disposable = null;
            disposable = Observable.EveryUpdate()
                .Subscribe(_ =>
                {
                    var deltaTime = _ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                    progress += deltaTime / (_milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS);
                    if (progress < 1)
                    {
                        _source.SetAlpha(
                            Mathf.Clamp01(current + EasingFormula.EasingFloat(_ease, 0f, 1f, progress) * different),
                            _adjustInteractAble);
                    }
                    else
                    {
                        _source.SetAlpha(Mathf.Clamp01(_target), _adjustInteractAble);
                        _onComplete?.Invoke();
                        disposable?.Dispose();
                    }
                })
                .AddTo(_source);

            return disposable;
        }

#if UNITASK

        //---------------------------------------------------------------------------------------------------------------
        public static UniTask LerpAlphaAsync(this CanvasGroup _source, int _milliseconds,
            float _target,
            Easing.Ease _ease = _DEFAULT_EASE_TYPE, bool _adjustInteractAble = true, bool _ignoreTimeScale = false,
            CancellationToken _token = default)
        {
            return LerpAlphaAsync(_source, _milliseconds, _target, _DEFAULT_SOURCE_CURRENT_ALPHA, _ease,
                _adjustInteractAble, _ignoreTimeScale, _token);
        }
        //--------------------------------------------------------------------------------------------------------------

        public static UniTask LerpAlphaAsync(this CanvasGroup _source, int _milliseconds, float _target, float _start,
            Easing.Ease _ease = _DEFAULT_EASE_TYPE, bool _adjustInteractAble = true, bool _ignoreTimeScale = false,
            CancellationToken _token = default)
        {
            var token = _token;
            if (token == default) token = _source.GetCancellationTokenOnDestroy();

            var uts = new UniTaskCompletionSource();
            UniTask.Void(async () =>
            {
                try
                {
                    var progress = 0f;
                    var current = Math.Abs(_start - _DEFAULT_SOURCE_CURRENT_ALPHA) <
                                  GlobalConstant.FLOAT_MINIMUM_TOLERANCE
                        ? _source.alpha
                        : _start;
                    var different = _target - current;

                    while (!token.IsCancellationRequested)
                    {
                        var deltaTime = _ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                        progress += deltaTime / (_milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS);
                        if (progress < 1)
                        {
                            _source.SetAlpha(
                                Mathf.Clamp01(current + EasingFormula.EasingFloat(_ease, 0f, 1f, progress) * different),
                                _adjustInteractAble);
                        }
                        else
                        {
                            _source.SetAlpha(Mathf.Clamp01(_target), _adjustInteractAble);
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
        //--------------------------------------------------------------------------------------------------------------

        public static bool SetInteractive(this CanvasGroup _source, bool _interactAble)
        {
            return _source.interactable = _interactAble;
        }

        //--------------------------------------------------------------------------------------------------------------

        public static bool SetBlocksraycasts(this CanvasGroup _source, bool _blocksraycasts)
        {
            return _source.blocksRaycasts = _blocksraycasts;
        }

        //--------------------------------------------------------------------------------------------------------------

        public static CanvasGroup SetAlpha(this CanvasGroup _source, float _alpha, bool _adjustInteractAble = true)
        {
            _source.alpha = _alpha;
            if (_adjustInteractAble)
            {
                _source.SetInteractive(_alpha >= GlobalConstant.ALPHA_VALUE_VISIBLE);
                _source.SetBlocksraycasts(_alpha > GlobalConstant.ALPHA_VALUE_INVISIBLE);
            }

            return _source;
        }

        //--------------------------------------------------------------------------------------------------------------
    }
}