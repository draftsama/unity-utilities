﻿using System;
using UniRx;
using UnityEditor;
using UnityEngine;

namespace Modules.Utilities
{
    public static class RectTransformExtension
    {

        public static IDisposable LerpTranslate(this RectTransform _rectTransform, Vector2 _target, int _milliseconds, Easing.Ease _ease = Easing.Ease.EaseInOutQuad, Action _completed = null)
        {

            var progress = 0f;
            var current = _rectTransform.anchoredPosition;
            float seconds = _milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS;

            Vector2 valueTarget;
            return LerpThread
                 .Execute
                 (
                     _milliseconds,
                     _count =>
                     {
                         progress += Time.deltaTime / seconds;
                         valueTarget.x = EasingFormula.EasingFloat(_ease, current.x, _target.x, progress);
                         valueTarget.y = EasingFormula.EasingFloat(_ease, current.y, _target.y, progress);
                         _rectTransform.anchoredPosition = valueTarget;

                     },
                     () =>
                     {
                         valueTarget = _target;
                         _rectTransform.anchoredPosition = valueTarget;
                         _completed?.Invoke();
                     }
                 );

        }


        public static IDisposable LerpWidth(this RectTransform _rectTransform, float _target, int _milliseconds, Easing.Ease _ease = Easing.Ease.EaseInOutQuad, Action _completed = null)
        {
            var progress = 0f;
            var currentWidth = _rectTransform.rect.width;

            float seconds = _milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS;

            float valueTarget;

            return LerpThread
                .Execute
                (
                    _milliseconds,
                    _count =>
                    {
                        progress += Time.deltaTime / seconds;
                        valueTarget = EasingFormula.EasingFloat(_ease, currentWidth, _target, progress);
                        _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, valueTarget);
                    },
                    () =>
                    {
                        valueTarget = _target;
                        _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, valueTarget);
                        _completed?.Invoke();
                    }
                );
        }



        public static IDisposable LerpHeight(this RectTransform _rectTransform, float _target, int _milliseconds, Easing.Ease _ease = Easing.Ease.EaseInOutQuad, Action _completed = null)
        {
            var progress = 0f;
            var currentHeight = _rectTransform.rect.height;
            float seconds = _milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS;
            float valueTarget;


            return LerpThread
                .Execute
                (
                    _milliseconds,
                    _count =>
                    {
                        progress += Time.deltaTime / seconds;
                        valueTarget = EasingFormula.EasingFloat(_ease, currentHeight, _target, progress);
                        _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, valueTarget);

                    },
                    () =>
                    {
                        valueTarget = _target;
                        _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, valueTarget);
                        _completed?.Invoke();
                    }
                );
        }


        public static IDisposable LerpSize(this RectTransform _rectTransform, Vector2 _target, int _milliseconds, Easing.Ease _ease = Easing.Ease.EaseInOutQuad, Action _completed = null)
        {
            var progress = 0f;
            var rect = _rectTransform.rect;
            var currentSize = new Vector2(rect.width, rect.height);
            float seconds = _milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS;

            Vector2 valueTarget;

            return LerpThread
                 .Execute
                 (
                     _milliseconds,
                     _count =>
                     {
                         progress += Time.deltaTime / seconds;
                         valueTarget.x = EasingFormula.EasingFloat(_ease, currentSize.x, _target.x, progress);
                         valueTarget.y = EasingFormula.EasingFloat(_ease, currentSize.y, _target.y, progress);
                         _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, valueTarget.y);
                         _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, valueTarget.x);


                     },
                     () =>
                     {
                         valueTarget = _target;
                         _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, valueTarget.y);
                         _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, valueTarget.x);
                         _completed?.Invoke();
                     }
                 );
        }


        public static IDisposable FloatingAnimation(this RectTransform _rectTransform, float _speed, float _radius, Easing.Ease _ease = Easing.Ease.EaseInOutQuad)
        {

            float progress = 0;

            Vector2 currentPos = _rectTransform.anchoredPosition;
            Vector2 startPos = _rectTransform.anchoredPosition3D;
            Vector2 targetPos = RandomPosition() + startPos;

            Vector2 RandomPosition()
            {
                float angle = UnityEngine.Random.Range(0, 360);
                float randomRadius = UnityEngine.Random.Range(0, _radius);
                return Quaternion.Euler(0, 0, angle) * new Vector3(0, randomRadius, 0);
            }
            return Observable.EveryUpdate().Subscribe(_ =>
            {
                progress += Time.deltaTime * _speed * 0.1f;
                _rectTransform.anchoredPosition3D = EasingFormula.EaseTypeVector(Easing.Ease.EaseInOutQuad, currentPos, targetPos, progress);


                if (progress >= 1)
                {
                    targetPos = RandomPosition()+startPos;
                    currentPos = _rectTransform.anchoredPosition;
                    progress = 0;
                }
            });
        }

    }
}
