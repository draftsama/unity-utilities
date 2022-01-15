using System;
using UnityEngine;

namespace Modules.Utilities
{
    public static class CanvasGroupExtension
    {
        //--------------------------------------------------------------------------------------------------------------

        private const Easing.Ease _DEFAULT_EASE_TYPE = Easing.Ease.Linear;
        private const int _DEFAULT_SOURCE_CURRENT_ALPHA = -1;
        
        //--------------------------------------------------------------------------------------------------------------
          public static IDisposable LerpAlpha(this CanvasGroup _source, int _milliseconds, float _target, bool _adjustInteractAble = true, Action _onComplete = null)
        {
            return _source.EasingLerpAlpha(_milliseconds, _target, _DEFAULT_SOURCE_CURRENT_ALPHA, _DEFAULT_EASE_TYPE, _adjustInteractAble, _onComplete);
        }


        //--------------------------------------------------------------------------------------------------------------

        public static IDisposable LerpAlpha(this CanvasGroup _source, int _milliseconds, float _target, float _start, bool _adjustInteractAble = true, Action _onComplete = null)
        {
            return _source.EasingLerpAlpha(_milliseconds, _target, _start, _DEFAULT_EASE_TYPE, _adjustInteractAble, _onComplete);
        }

        //--------------------------------------------------------------------------------------------------------------

        public static IDisposable EasingLerpAlpha(this CanvasGroup _source, int _milliseconds, float _target, Easing.Ease _ease = _DEFAULT_EASE_TYPE, bool _adjustInteractAble = true, Action _onComplete = null)
        {
            return _source.EasingLerpAlpha(_milliseconds, _target, _DEFAULT_SOURCE_CURRENT_ALPHA, _ease, _adjustInteractAble, _onComplete);
        }

        //--------------------------------------------------------------------------------------------------------------

        public static IDisposable EasingLerpAlpha(this CanvasGroup _source, int _milliseconds, float _target, float _start, Easing.Ease _ease = _DEFAULT_EASE_TYPE, bool _adjustInteractAble = true, Action _onComplete = null)
        {
            var progress = 0f;
            var current = Math.Abs(_start - _DEFAULT_SOURCE_CURRENT_ALPHA) < GlobalConstant.FLOAT_MINIMUM_TOLERANCE ? _source.alpha : _start;
            var different = _target - current;

            return LerpThread
                .Execute
                (
                    _milliseconds,
                    _count =>
                    {
                        progress += Time.deltaTime / (_milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS);
                        _source.SetAlpha(Mathf.Clamp01(current + EasingFormula.EasingFloat(_ease, 0f, 1f, progress) * different), _adjustInteractAble);
                    },
                    () =>
                    {
                        _source.SetAlpha(Mathf.Clamp01(_target), _adjustInteractAble);
                        _onComplete?.Invoke();
                    }
                );
        }

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