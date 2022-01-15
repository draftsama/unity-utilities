using System;
using UniRx;
using UnityEngine;

namespace Modules.Utilities
{
    public static class TransformExtension
    {
        //--------------------------------------------------------------------------------------------------------------
        public static IObservable<Unit> LerpScale(this Transform _transform, int _milliseconds, Vector3 _targetScale, Easing.Ease _ease = Easing.Ease.EaseInOutQuad)
        {

            return Observable.Create<Unit>(_observer =>
            {

                var progress = 0f;
                var startScale = _transform.localScale;

                IDisposable disposable = LerpThread
                    .Execute(
                        _milliseconds,
                        _count =>
                        {
                            progress += Time.deltaTime / (_milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS);
                            var targetInProgress = EasingFormula.EaseTypeVector(_ease, startScale, _targetScale, progress);
                            _transform.localScale = targetInProgress;
                        },
                        () =>
                        {
                            _transform.localScale = _targetScale;
                            _observer.OnNext(default(Unit));
                            _observer.OnCompleted();

                        }
                    );

                return Disposable.Create(() => disposable?.Dispose());
            });
        }
        //--------------------------------------------------------------------------------------------------------------
        public static IObservable<Unit> LerpScale(this Transform _transform, int _milliseconds, Vector3 _targetScale, AnimationCurve _curve)
        {

            return Observable.Create<Unit>(_observer =>
            {

                var progress = 0f;
                var startScale = _transform.localScale;

                IDisposable disposable = LerpThread
                    .Execute(
                        _milliseconds,
                        _count =>
                        {
                            progress += Time.deltaTime / (_milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS);
                            var curveProgress = _curve.Evaluate(progress);
                            var targetInProgress = LerpVector(startScale, _targetScale, curveProgress);
                            _transform.localScale = targetInProgress;
                        },
                        () =>
                        {
                            _transform.localScale = _targetScale;
                            _observer.OnNext(default(Unit));
                            _observer.OnCompleted();

                        }
                    );

                return Disposable.Create(() => disposable?.Dispose());
            });
        }
        public static Vector3 LerpVector(Vector3 _start, Vector3 _target, float _progress)
        {
            var delta = _target - _start;
            return _start + delta * _progress;
        }
        //--------------------------------------------------------------------------------------------------------------
        public static IObservable<Unit> LerpPosition(this Transform _transform, int _milliseconds, Vector3 _targetPosition, bool _isLocal, Easing.Ease _ease = Easing.Ease.EaseInOutQuad)
        {

            return Observable.Create<Unit>(_observer =>
            {

                var progress = 0f;
                var startPosition = _isLocal ? _transform.localPosition : _transform.position;

                IDisposable disposable = LerpThread
                    .Execute(
                        _milliseconds,
                        _count =>
                        {
                            progress += Time.deltaTime / (_milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS);
                            var targetInProgress = EasingFormula.EaseTypeVector(_ease, startPosition, _targetPosition, progress);
                            if (_isLocal)
                                _transform.localPosition = targetInProgress;
                            else
                                _transform.position = targetInProgress;


                        },
                        () =>
                        {
                            if (_isLocal)
                                _transform.localPosition = _targetPosition;
                            else
                                _transform.position = _targetPosition;

                            _observer.OnNext(default(Unit));
                            _observer.OnCompleted();

                        }
                    );

                return Disposable.Create(() => disposable?.Dispose());
            });
        }
        //--------------------------------------------------------------------------------------------------------------
        public static IObservable<Unit> LerpRotation(this Transform _transform, int _milliseconds, Vector3 _targetRotation, bool _isLocal, Easing.Ease _ease = Easing.Ease.EaseInOutQuad)
        {

            return Observable.Create<Unit>(_observer =>
            {

                var progress = 0f;
                var startAngle = _isLocal ? _transform.localEulerAngles : _transform.eulerAngles;

                IDisposable disposable = LerpThread
                    .Execute(
                        _milliseconds,
                        _count =>
                        {
                            progress += Time.deltaTime / (_milliseconds * GlobalConstant.MILLISECONDS_TO_SECONDS);
                            var targetInProgress = EasingFormula.EaseTypeVector(_ease, startAngle, _targetRotation, progress);
                            if (_isLocal)
                                _transform.localEulerAngles = targetInProgress;
                            else
                                _transform.eulerAngles = targetInProgress;


                        },
                        () =>
                        {
                            if (_isLocal)
                                _transform.localEulerAngles = _targetRotation;
                            else
                                _transform.eulerAngles = _targetRotation;

                            _observer.OnNext(default(Unit));
                            _observer.OnCompleted();

                        }
                    );

                return Disposable.Create(() => disposable?.Dispose());
            });
        }


    }
}
