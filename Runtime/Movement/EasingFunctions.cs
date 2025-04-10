using System;
using UnityEngine;

/*
 * Created by C.J. Kimberlin (http://cjkimberlin.com)
 * 
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * 
 * 
 * TERMS OF USE - EASING EQUATIONS
 * Open source under the BSD License.
 * Copyright (c)2001 Robert Penner
 * All rights reserved.
 * Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
 * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
 * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
 * Neither the name of the author nor the names of contributors may be used to endorse or promote products derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, 
 * THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE 
 * FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; 
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT 
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 *
 * ============= Description =============
 *
 * Below is an example of how to use the easing functions in the file. There is a getting function that will return the function
 * from an enum. This is useful since the enum can be exposed in the editor and then the function queried during Start().
 * 
 * EasingFunction.Ease ease = EasingFunction.Ease.EaseInOutQuad;
 * EasingFunction.EasingFunc func = GetEasingFunction(ease;
 * 
 * float value = func(0, 10, 0.67f);
 * 
 * EasingFunction.EaseingFunc derivativeFunc = GetEasingFunctionDerivative(ease);
 * 
 * float derivativeValue = derivativeFunc(0, 10, 0.67f);
 */

namespace Modules.Utilities
{
    public class Easing
    {
        public enum Ease
        {
            EaseInQuad = 0,
            EaseOutQuad,
            EaseInOutQuad,
            EaseInCubic,
            EaseOutCubic,
            EaseInOutCubic,
            EaseInQuart,
            EaseOutQuart,
            EaseInOutQuart,
            EaseInQuint,
            EaseOutQuint,
            EaseInOutQuint,
            EaseInSine,
            EaseOutSine,
            EaseInOutSine,
            EaseInExpo,
            EaseOutExpo,
            EaseInOutExpo,
            EaseInCirc,
            EaseOutCirc,
            EaseInOutCirc,
            Linear,
            Spring,
            EaseInBounce,
            EaseOutBounce,
            EaseInOutBounce,
            EaseInBack,
            EaseOutBack,
            EaseInOutBack,
            EaseInElastic,
            EaseOutElastic,
            EaseInOutElastic,
        }

        private const float _NATURAL_LOG_OF_2 = 0.693147181f;

        //
        // Easing functions
        //

        public static float Linear(float _start, float _end, float _value)
        {
            return Mathf.Lerp(_start, _end, _value);
        }

        public static float Spring(float _start, float _end, float _value)
        {
            _value = Mathf.Clamp01(_value);
            _value = (Mathf.Sin(_value * Mathf.PI * (0.2f + 2.5f * _value * _value * _value)) * Mathf.Pow(1f - _value, 2.2f) + _value) * (1f + (1.2f * (1f - _value)));
            return _start + (_end - _start) * _value;
        }

        public static float EaseInQuad(float _start, float _end, float _value)
        {
            _end -= _start;
            return _end * _value * _value + _start;
        }

        public static float EaseOutQuad(float _start, float _end, float _value)
        {
            _end -= _start;
            return -_end * _value * (_value - 2) + _start;
        }

        public static float EaseInOutQuad(float _start, float _end, float _value)
        {
            _value /= .5f;
            _end -= _start;
            if (_value < 1) return _end * 0.5f * _value * _value + _start;
            _value--;
            return -_end * 0.5f * (_value * (_value - 2) - 1) + _start;
        }

        public static float EaseInCubic(float _start, float _end, float _value)
        {
            _end -= _start;
            return _end * _value * _value * _value + _start;
        }

        public static float EaseOutCubic(float _start, float _end, float _value)
        {
            _value--;
            _end -= _start;
            return _end * (_value * _value * _value + 1) + _start;
        }

        public static float EaseInOutCubic(float _start, float _end, float _value)
        {
            _value /= .5f;
            _end -= _start;
            if (_value < 1) return _end * 0.5f * _value * _value * _value + _start;
            _value -= 2;
            return _end * 0.5f * (_value * _value * _value + 2) + _start;
        }

        public static float EaseInQuart(float _start, float _end, float _value)
        {
            _end -= _start;
            return _end * _value * _value * _value * _value + _start;
        }

        public static float EaseOutQuart(float _start, float _end, float _value)
        {
            _value--;
            _end -= _start;
            return -_end * (_value * _value * _value * _value - 1) + _start;
        }

        public static float EaseInOutQuart(float _start, float _end, float _value)
        {
            _value /= .5f;
            _end -= _start;
            if (_value < 1) return _end * 0.5f * _value * _value * _value * _value + _start;
            _value -= 2;
            return -_end * 0.5f * (_value * _value * _value * _value - 2) + _start;
        }

        public static float EaseInQuint(float _start, float _end, float _value)
        {
            _end -= _start;
            return _end * _value * _value * _value * _value * _value + _start;
        }

        public static float EaseOutQuint(float _start, float _end, float _value)
        {
            _value--;
            _end -= _start;
            return _end * (_value * _value * _value * _value * _value + 1) + _start;
        }

        public static float EaseInOutQuint(float _start, float _end, float _value)
        {
            _value /= .5f;
            _end -= _start;
            if (_value < 1) return _end * 0.5f * _value * _value * _value * _value * _value + _start;
            _value -= 2;
            return _end * 0.5f * (_value * _value * _value * _value * _value + 2) + _start;
        }

        public static float EaseInSine(float _start, float _end, float _value)
        {
            _end -= _start;
            return -_end * Mathf.Cos(_value * (Mathf.PI * 0.5f)) + _end + _start;
        }

        public static float EaseOutSine(float _start, float _end, float _value)
        {
            _end -= _start;
            return _end * Mathf.Sin(_value * (Mathf.PI * 0.5f)) + _start;
        }

        public static float EaseInOutSine(float _start, float _end, float _value)
        {
            _end -= _start;
            return -_end * 0.5f * (Mathf.Cos(Mathf.PI * _value) - 1) + _start;
        }

        public static float EaseInExpo(float _start, float _end, float _value)
        {
            _end -= _start;
            return _end * Mathf.Pow(2, 10 * (_value - 1)) + _start;
        }

        public static float EaseOutExpo(float _start, float _end, float _value)
        {
            _end -= _start;
            return _end * (-Mathf.Pow(2, -10 * _value) + 1) + _start;
        }

        public static float EaseInOutExpo(float _start, float _end, float _value)
        {
            _value /= .5f;
            _end -= _start;
            if (_value < 1) return _end * 0.5f * Mathf.Pow(2, 10 * (_value - 1)) + _start;
            _value--;
            return _end * 0.5f * (-Mathf.Pow(2, -10 * _value) + 2) + _start;
        }

        public static float EaseInCirc(float _start, float _end, float _value)
        {
            _end -= _start;
            return -_end * (Mathf.Sqrt(1 - _value * _value) - 1) + _start;
        }

        public static float EaseOutCirc(float _start, float _end, float _value)
        {
            _value--;
            _end -= _start;
            return _end * Mathf.Sqrt(1 - _value * _value) + _start;
        }

        public static float EaseInOutCirc(float _start, float _end, float _value)
        {
            _value /= .5f;
            _end -= _start;
            if (_value < 1) return -_end * 0.5f * (Mathf.Sqrt(1 - _value * _value) - 1) + _start;
            _value -= 2;
            return _end * 0.5f * (Mathf.Sqrt(1 - _value * _value) + 1) + _start;
        }

        public static float EaseInBounce(float _start, float _end, float _value)
        {
            _end -= _start;
            float d = 1f;
            return _end - EaseOutBounce(0, _end, d - _value) + _start;
        }

        public static float EaseOutBounce(float _start, float _end, float _value)
        {
            _value /= 1f;
            _end -= _start;
            if (_value < (1 / 2.75f))
            {
                return _end * (7.5625f * _value * _value) + _start;
            }
            else if (_value < (2 / 2.75f))
            {
                _value -= (1.5f / 2.75f);
                return _end * (7.5625f * (_value) * _value + .75f) + _start;
            }
            else if (_value < (2.5 / 2.75))
            {
                _value -= (2.25f / 2.75f);
                return _end * (7.5625f * (_value) * _value + .9375f) + _start;
            }
            else
            {
                _value -= (2.625f / 2.75f);
                return _end * (7.5625f * (_value) * _value + .984375f) + _start;
            }
        }

        public static float EaseInOutBounce(float _start, float _end, float _value)
        {
            _end -= _start;
            float d = 1f;
            if (_value < d * 0.5f) return EaseInBounce(0, _end, _value * 2) * 0.5f + _start;
            else return EaseOutBounce(0, _end, _value * 2 - d) * 0.5f + _end * 0.5f + _start;
        }

        public static float EaseInBack(float _start, float _end, float _value)
        {
            _end -= _start;
            _value /= 1;
            float s = 1.70158f;
            return _end * (_value) * _value * ((s + 1) * _value - s) + _start;
        }

        public static float EaseOutBack(float _start, float _end, float _value)
        {
            float s = 1.70158f;
            _end -= _start;
            _value = (_value) - 1;
            return _end * ((_value) * _value * ((s + 1) * _value + s) + 1) + _start;
        }

        public static float EaseInOutBack(float _start, float _end, float _value)
        {
            float s = 1.70158f;
            _end -= _start;
            _value /= .5f;
            if ((_value) < 1)
            {
                s *= (1.525f);
                return _end * 0.5f * (_value * _value * (((s) + 1) * _value - s)) + _start;
            }
            _value -= 2;
            s *= (1.525f);
            return _end * 0.5f * ((_value) * _value * (((s) + 1) * _value + s) + 2) + _start;
        }

        public static float EaseInElastic(float _start, float _end, float _value)
        {
            _end -= _start;

            float d = 1f;
            float p = d * .3f;
            float s;
            float a = 0;

            if (Math.Abs(_value) < GlobalConstant.FLOAT_MINIMUM_TOLERANCE) return _start;

            if (Math.Abs((_value /= d) - 1) < GlobalConstant.FLOAT_MINIMUM_TOLERANCE) return _start + _end;

            if (Math.Abs(a) < GlobalConstant.FLOAT_MINIMUM_TOLERANCE || a < Mathf.Abs(_end))
            {
                a = _end;
                s = p / 4;
            }
            else
            {
                s = p / (2 * Mathf.PI) * Mathf.Asin(_end / a);
            }

            return -(a * Mathf.Pow(2, 10 * (_value -= 1)) * Mathf.Sin((_value * d - s) * (2 * Mathf.PI) / p)) + _start;
        }

        public static float EaseOutElastic(float _start, float _end, float _value)
        {
            _end -= _start;

            float d = 1f;
            float p = d * .3f;
            float s;
            float a = 0;

            if (Math.Abs(_value) < GlobalConstant.FLOAT_MINIMUM_TOLERANCE) return _start;

            if (Math.Abs((_value /= d) - 1) < GlobalConstant.FLOAT_MINIMUM_TOLERANCE) return _start + _end;

            if (Math.Abs(a) < GlobalConstant.FLOAT_MINIMUM_TOLERANCE || a < Mathf.Abs(_end))
            {
                a = _end;
                s = p * 0.25f;
            }
            else
            {
                s = p / (2 * Mathf.PI) * Mathf.Asin(_end / a);
            }

            return (a * Mathf.Pow(2, -10 * _value) * Mathf.Sin((_value * d - s) * (2 * Mathf.PI) / p) + _end + _start);
        }

        public static float EaseInOutElastic(float _start, float _end, float _value)
        {
            _end -= _start;

            float d = 1f;
            float p = d * .3f;
            float s;
            float a = 0;

            if (Math.Abs(_value) < GlobalConstant.FLOAT_MINIMUM_TOLERANCE) return _start;

            if (Math.Abs((_value /= d * 0.5f) - 2) < GlobalConstant.FLOAT_MINIMUM_TOLERANCE) return _start + _end;

            if (Math.Abs(a) < GlobalConstant.FLOAT_MINIMUM_TOLERANCE || a < Mathf.Abs(_end))
            {
                a = _end;
                s = p / 4;
            }
            else
            {
                s = p / (2 * Mathf.PI) * Mathf.Asin(_end / a);
            }

            if (_value < 1) return -0.5f * (a * Mathf.Pow(2, 10 * (_value -= 1)) * Mathf.Sin((_value * d - s) * (2 * Mathf.PI) / p)) + _start;
            return a * Mathf.Pow(2, -10 * (_value -= 1)) * Mathf.Sin((_value * d - s) * (2 * Mathf.PI) / p) * 0.5f + _end + _start;
        }

        //
        // These are derived functions that the motor can use to get the speed at a specific time.
        //
        // The easing functions all work with a normalized time (0 to 1) and the returned value here
        // reflects that. Values returned here should be divided by the actual time.
        //
        // TODO: These functions have not had the testing they deserve. If there is odd behavior around
        //       dash speeds then this would be the first place I'd look.

        public static float LinearD(float _start, float _end, float _value)
        {
            return _end - _start;
        }

        public static float EaseInQuadD(float _start, float _end, float _value)
        {
            return 2f * (_end - _start) * _value;
        }

        public static float EaseOutQuadD(float _start, float _end, float _value)
        {
            if (_start <= 0) throw new ArgumentOutOfRangeException(nameof(_start));
            _end -= _start;
            return -_end * _value - _end * (_value - 2);
        }

        public static float EaseInOutQuadD(float _start, float _end, float _value)
        {
            _value /= .5f;
            _end -= _start;

            if (_value < 1)
            {
                return _end * _value;
            }

            _value--;

            return _end * (1 - _value);
        }

        public static float EaseInCubicD(float _start, float _end, float _value)
        {
            return 3f * (_end - _start) * _value * _value;
        }

        public static float EaseOutCubicD(float _start, float _end, float _value)
        {
            _value--;
            _end -= _start;
            return 3f * _end * _value * _value;
        }

        public static float EaseInOutCubicD(float _start, float _end, float _value)
        {
            _value /= .5f;
            _end -= _start;

            if (_value < 1)
            {
                return (3f / 2f) * _end * _value * _value;
            }

            _value -= 2;

            return (3f / 2f) * _end * _value * _value;
        }

        public static float EaseInQuartD(float _start, float _end, float _value)
        {
            return 4f * (_end - _start) * _value * _value * _value;
        }

        public static float EaseOutQuartD(float _start, float _end, float _value)
        {
            _value--;
            _end -= _start;
            return -4f * _end * _value * _value * _value;
        }

        public static float EaseInOutQuartD(float _start, float _end, float _value)
        {
            _value /= .5f;
            _end -= _start;

            if (_value < 1)
            {
                return 2f * _end * _value * _value * _value;
            }

            _value -= 2;

            return -2f * _end * _value * _value * _value;
        }

        public static float EaseInQuintD(float _start, float _end, float _value)
        {
            return 5f * (_end - _start) * _value * _value * _value * _value;
        }

        public static float EaseOutQuintD(float _start, float _end, float _value)
        {
            _value--;
            _end -= _start;
            return 5f * _end * _value * _value * _value * _value;
        }

        public static float EaseInOutQuintD(float _start, float _end, float _value)
        {
            _value /= .5f;
            _end -= _start;

            if (_value < 1)
            {
                return (5f / 2f) * _end * _value * _value * _value * _value;
            }

            _value -= 2;

            return (5f / 2f) * _end * _value * _value * _value * _value;
        }

        public static float EaseInSineD(float _start, float _end, float _value)
        {
            return (_end - _start) * 0.5f * Mathf.PI * Mathf.Sin(0.5f * Mathf.PI * _value);
        }

        public static float EaseOutSineD(float _start, float _end, float _value)
        {
            _end -= _start;
            return (Mathf.PI * 0.5f) * _end * Mathf.Cos(_value * (Mathf.PI * 0.5f));
        }

        public static float EaseInOutSineD(float _start, float _end, float _value)
        {
            _end -= _start;
            return _end * 0.5f * Mathf.PI * Mathf.Cos(Mathf.PI * _value);
        }
        public static float EaseInExpoD(float _start, float _end, float _value)
        {
            return (10f * _NATURAL_LOG_OF_2 * (_end - _start) * Mathf.Pow(2f, 10f * (_value - 1)));
        }

        public static float EaseOutExpoD(float _start, float _end, float _value)
        {
            _end -= _start;
            return 5f * _NATURAL_LOG_OF_2 * _end * Mathf.Pow(2f, 1f - 10f * _value);
        }

        public static float EaseInOutExpoD(float _start, float _end, float _value)
        {
            _value /= .5f;
            _end -= _start;

            if (_value < 1)
            {
                return 5f * _NATURAL_LOG_OF_2 * _end * Mathf.Pow(2f, 10f * (_value - 1));
            }

            _value--;

            return (5f * _NATURAL_LOG_OF_2 * _end) / (Mathf.Pow(2f, 10f * _value));
        }

        public static float EaseInCircD(float _start, float _end, float _value)
        {
            return ((_end - _start) * _value) / Mathf.Sqrt(1f - _value * _value);
        }

        public static float EaseOutCircD(float _start, float _end, float _value)
        {
            _value--;
            _end -= _start;
            return (-_end * _value) / Mathf.Sqrt(1f - _value * _value);
        }

        public static float EaseInOutCircD(float _start, float _end, float _value)
        {
            _value /= .5f;
            _end -= _start;

            if (_value < 1)
            {
                return (_end * _value) / (2f * Mathf.Sqrt(1f - _value * _value));
            }

            _value -= 2;

            return (-_end * _value) / (2f * Mathf.Sqrt(1f - _value * _value));
        }

        public static float EaseInBounceD(float _start, float _end, float _value)
        {
            _end -= _start;
            float d = 1f;

            return EaseOutBounceD(0, _end, d - _value);
        }

        public static float EaseOutBounceD(float _start, float _end, float _value)
        {
            _value /= 1f;
            _end -= _start;

            if (_value < (1 / 2.75f))
            {
                return 2f * _end * 7.5625f * _value;
            }
            else if (_value < (2 / 2.75f))
            {
                _value -= (1.5f / 2.75f);
                return 2f * _end * 7.5625f * _value;
            }
            else if (_value < (2.5 / 2.75))
            {
                _value -= (2.25f / 2.75f);
                return 2f * _end * 7.5625f * _value;
            }
            else
            {
                _value -= (2.625f / 2.75f);
                return 2f * _end * 7.5625f * _value;
            }
        }

        public static float EaseInOutBounceD(float _start, float _end, float _value)
        {
            _end -= _start;
            float d = 1f;

            if (_value < d * 0.5f)
            {
                return EaseInBounceD(0, _end, _value * 2) * 0.5f;
            }
            else
            {
                return EaseOutBounceD(0, _end, _value * 2 - d) * 0.5f;
            }
        }

        public static float EaseInBackD(float _start, float _end, float _value)
        {
            float s = 1.70158f;

            return 3f * (s + 1f) * (_end - _start) * _value * _value - 2f * s * (_end - _start) * _value;
        }

        public static float EaseOutBackD(float _start, float _end, float _value)
        {
            float s = 1.70158f;
            _end -= _start;
            _value = (_value) - 1;

            return _end * ((s + 1f) * _value * _value + 2f * _value * ((s + 1f) * _value + s));
        }

        public static float EaseInOutBackD(float _start, float _end, float _value)
        {
            float s = 1.70158f;
            _end -= _start;
            _value /= .5f;

            if ((_value) < 1)
            {
                s *= (1.525f);
                return 0.5f * _end * (s + 1) * _value * _value + _end * _value * ((s + 1f) * _value - s);
            }

            _value -= 2;
            s *= (1.525f);
            return 0.5f * _end * ((s + 1) * _value * _value + 2f * _value * ((s + 1f) * _value + s));
        }

        public static float EaseInElasticD(float _start, float _end, float _value)
        {
            if (_end <= 0) throw new ArgumentOutOfRangeException(nameof(_end));
            _end -= _start;

            float d = 1f;
            float p = d * .3f;
            float s;
            float a = 0;

            if (Math.Abs(a) < GlobalConstant.FLOAT_MINIMUM_TOLERANCE || a < Mathf.Abs(_end))
            {
                a = _end;
                s = p / 4;
            }
            else
            {
                s = p / (2 * Mathf.PI) * Mathf.Asin(_end / a);
            }

            float c = 2 * Mathf.PI;

            // From an online derivative calculator, kinda hoping it is right.
            return ((-a) * d * c * Mathf.Cos((c * (d * (_value - 1f) - s)) / p)) / p -
                   5f * _NATURAL_LOG_OF_2 * a * Mathf.Sin((c * (d * (_value - 1f) - s)) / p) *
                   Mathf.Pow(2f, 10f * (_value - 1f) + 1f);
        }

        public static float EaseOutElasticD(float _start, float _end, float _value)
        {
            _end -= _start;

            float d = 1f;
            float p = d * .3f;
            float s;
            float a = 0;

            if (Math.Abs(a) < GlobalConstant.FLOAT_MINIMUM_TOLERANCE || a < Mathf.Abs(_end))
            {
                a = _end;
                s = p * 0.25f;
            }
            else
            {
                s = p / (2 * Mathf.PI) * Mathf.Asin(_end / a);
            }

            return (a * Mathf.PI * d * Mathf.Pow(2f, 1f - 10f * _value) *
                    Mathf.Cos((2f * Mathf.PI * (d * _value - s)) / p)) / p - 5f * _NATURAL_LOG_OF_2 * a *
                   Mathf.Pow(2f, 1f - 10f * _value) * Mathf.Sin((2f * Mathf.PI * (d * _value - s)) / p);
        }

        public static float EaseInOutElasticD(float _start, float _end, float _value)
        {
            _end -= _start;

            float d = 1f;
            float p = d * .3f;
            float s;
            float a = 0;

            if (Math.Abs(a) < GlobalConstant.FLOAT_MINIMUM_TOLERANCE || a < Mathf.Abs(_end))
            {
                a = _end;
                s = p / 4;
            }
            else
            {
                s = p / (2 * Mathf.PI) * Mathf.Asin(_end / a);
            }

            if (_value < 1)
            {
                _value -= 1;

                return -5f * _NATURAL_LOG_OF_2 * a * Mathf.Pow(2f, 10f * _value) * Mathf.Sin(2 * Mathf.PI * (d * _value - 2f) / p) -
                       a * Mathf.PI * d * Mathf.Pow(2f, 10f * _value) * Mathf.Cos(2 * Mathf.PI * (d * _value - s) / p) / p;
            }

            _value -= 1;

            return a * Mathf.PI * d * Mathf.Cos(2f * Mathf.PI * (d * _value - s) / p) / (p * Mathf.Pow(2f, 10f * _value)) -
                   5f * _NATURAL_LOG_OF_2 * a * Mathf.Sin(2f * Mathf.PI * (d * _value - s) / p) / (Mathf.Pow(2f, 10f * _value));
        }

        public static float SpringD(float _start, float _end, float _value)
        {
            _value = Mathf.Clamp01(_value);
            _end -= _start;

            // Damn... Thanks http://www.derivative-calculator.net/
            return _end * (6f * (1f - _value) / 5f + 1f) * (-2.2f * Mathf.Pow(1f - _value, 1.2f) *
                                                            Mathf.Sin(Mathf.PI * _value * (2.5f * _value * _value * _value + 0.2f)) + Mathf.Pow(1f - _value, 2.2f) *
                                                            (Mathf.PI * (2.5f * _value * _value * _value + 0.2f) + 7.5f * Mathf.PI * _value * _value * _value) *
                                                            Mathf.Cos(Mathf.PI * _value * (2.5f * _value * _value * _value + 0.2f)) + 1f) -
                   6f * _end * (Mathf.Pow(1 - _value, 2.2f) * Mathf.Sin(Mathf.PI * _value * (2.5f * _value * _value * _value + 0.2f)) + _value
                                / 5f);

        }

        public delegate float Function(float _s, float _e, float _v);

        /// <summary>
        /// Returns the function associated to the easingFunction enum. This value returned should be cached as it allocates memory
        /// to return.
        /// </summary>
        /// <param name="_easingFunction">The enum associated with the easing function.</param>
        /// <returns>The easing function</returns>
        public static Function GetEasingFunction(Ease _easingFunction)
        {
            if (_easingFunction == Ease.EaseInQuad)
            {
                return EaseInQuad;
            }

            if (_easingFunction == Ease.EaseOutQuad)
            {
                return EaseOutQuad;
            }

            if (_easingFunction == Ease.EaseInOutQuad)
            {
                return EaseInOutQuad;
            }

            if (_easingFunction == Ease.EaseInCubic)
            {
                return EaseInCubic;
            }

            if (_easingFunction == Ease.EaseOutCubic)
            {
                return EaseOutCubic;
            }

            if (_easingFunction == Ease.EaseInOutCubic)
            {
                return EaseInOutCubic;
            }

            if (_easingFunction == Ease.EaseInQuart)
            {
                return EaseInQuart;
            }

            if (_easingFunction == Ease.EaseOutQuart)
            {
                return EaseOutQuart;
            }

            if (_easingFunction == Ease.EaseInOutQuart)
            {
                return EaseInOutQuart;
            }

            if (_easingFunction == Ease.EaseInQuint)
            {
                return EaseInQuint;
            }

            if (_easingFunction == Ease.EaseOutQuint)
            {
                return EaseOutQuint;
            }

            if (_easingFunction == Ease.EaseInOutQuint)
            {
                return EaseInOutQuint;
            }

            if (_easingFunction == Ease.EaseInSine)
            {
                return EaseInSine;
            }

            if (_easingFunction == Ease.EaseOutSine)
            {
                return EaseOutSine;
            }

            if (_easingFunction == Ease.EaseInOutSine)
            {
                return EaseInOutSine;
            }

            if (_easingFunction == Ease.EaseInExpo)
            {
                return EaseInExpo;
            }

            if (_easingFunction == Ease.EaseOutExpo)
            {
                return EaseOutExpo;
            }

            if (_easingFunction == Ease.EaseInOutExpo)
            {
                return EaseInOutExpo;
            }

            if (_easingFunction == Ease.EaseInCirc)
            {
                return EaseInCirc;
            }

            if (_easingFunction == Ease.EaseOutCirc)
            {
                return EaseOutCirc;
            }

            if (_easingFunction == Ease.EaseInOutCirc)
            {
                return EaseInOutCirc;
            }

            if (_easingFunction == Ease.Linear)
            {
                return Linear;
            }

            if (_easingFunction == Ease.Spring)
            {
                return Spring;
            }

            if (_easingFunction == Ease.EaseInBounce)
            {
                return EaseInBounce;
            }

            if (_easingFunction == Ease.EaseOutBounce)
            {
                return EaseOutBounce;
            }

            if (_easingFunction == Ease.EaseInOutBounce)
            {
                return EaseInOutBounce;
            }

            if (_easingFunction == Ease.EaseInBack)
            {
                return EaseInBack;
            }

            if (_easingFunction == Ease.EaseOutBack)
            {
                return EaseOutBack;
            }

            if (_easingFunction == Ease.EaseInOutBack)
            {
                return EaseInOutBack;
            }

            if (_easingFunction == Ease.EaseInElastic)
            {
                return EaseInElastic;
            }

            if (_easingFunction == Ease.EaseOutElastic)
            {
                return EaseOutElastic;
            }

            if (_easingFunction == Ease.EaseInOutElastic)
            {
                return EaseInOutElastic;
            }

            return null;
        }

        /// <summary>
        /// Gets the derivative function of the appropriate easing function. If you use an easing function for position then this
        /// function can get you the speed at a given time (normalized).
        /// </summary>
        /// <param name="easingFunction"></param>
        /// <returns>The derivative function</returns>
        public static Function GetEasingFunctionDerivative(Ease easingFunction)
        {
            if (easingFunction == Ease.EaseInQuad)
            {
                return EaseInQuadD;
            }

            if (easingFunction == Ease.EaseOutQuad)
            {
                return EaseOutQuadD;
            }

            if (easingFunction == Ease.EaseInOutQuad)
            {
                return EaseInOutQuadD;
            }

            if (easingFunction == Ease.EaseInCubic)
            {
                return EaseInCubicD;
            }

            if (easingFunction == Ease.EaseOutCubic)
            {
                return EaseOutCubicD;
            }

            if (easingFunction == Ease.EaseInOutCubic)
            {
                return EaseInOutCubicD;
            }

            if (easingFunction == Ease.EaseInQuart)
            {
                return EaseInQuartD;
            }

            if (easingFunction == Ease.EaseOutQuart)
            {
                return EaseOutQuartD;
            }

            if (easingFunction == Ease.EaseInOutQuart)
            {
                return EaseInOutQuartD;
            }

            if (easingFunction == Ease.EaseInQuint)
            {
                return EaseInQuintD;
            }

            if (easingFunction == Ease.EaseOutQuint)
            {
                return EaseOutQuintD;
            }

            if (easingFunction == Ease.EaseInOutQuint)
            {
                return EaseInOutQuintD;
            }

            if (easingFunction == Ease.EaseInSine)
            {
                return EaseInSineD;
            }

            if (easingFunction == Ease.EaseOutSine)
            {
                return EaseOutSineD;
            }

            if (easingFunction == Ease.EaseInOutSine)
            {
                return EaseInOutSineD;
            }

            if (easingFunction == Ease.EaseInExpo)
            {
                return EaseInExpoD;
            }

            if (easingFunction == Ease.EaseOutExpo)
            {
                return EaseOutExpoD;
            }

            if (easingFunction == Ease.EaseInOutExpo)
            {
                return EaseInOutExpoD;
            }

            if (easingFunction == Ease.EaseInCirc)
            {
                return EaseInCircD;
            }

            if (easingFunction == Ease.EaseOutCirc)
            {
                return EaseOutCircD;
            }

            if (easingFunction == Ease.EaseInOutCirc)
            {
                return EaseInOutCircD;
            }

            if (easingFunction == Ease.Linear)
            {
                return LinearD;
            }

            if (easingFunction == Ease.Spring)
            {
                return SpringD;
            }

            if (easingFunction == Ease.EaseInBounce)
            {
                return EaseInBounceD;
            }

            if (easingFunction == Ease.EaseOutBounce)
            {
                return EaseOutBounceD;
            }

            if (easingFunction == Ease.EaseInOutBounce)
            {
                return EaseInOutBounceD;
            }

            if (easingFunction == Ease.EaseInBack)
            {
                return EaseInBackD;
            }

            if (easingFunction == Ease.EaseOutBack)
            {
                return EaseOutBackD;
            }

            if (easingFunction == Ease.EaseInOutBack)
            {
                return EaseInOutBackD;
            }

            if (easingFunction == Ease.EaseInElastic)
            {
                return EaseInElasticD;
            }

            if (easingFunction == Ease.EaseOutElastic)
            {
                return EaseOutElasticD;
            }

            if (easingFunction == Ease.EaseInOutElastic)
            {
                return EaseInOutElasticD;
            }

            return null;
        }
    }
}
