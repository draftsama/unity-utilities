using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modules.Utilities {
    public class Flag {
        public Flag () : this (0) { }
        public Flag (int _mask) { _Value = _mask; }

        public int value => _Value;
        private int _Value;

        //Add the mask to flags
        public int TurnOn (int _mask) {
            return _Value |= _mask;
        }

        //Remove the mask from flags
        public int TurnOff (int _mask) {
            return _Value &= ~_mask;
        }

        //Toggle the mask into flags
        public int Toggle (int _mask) {
            return _Value ^= _mask;
        }

        //Check if mask is on
        public bool Check (int _mask) {
            return (_Value & _mask) == _mask;
        }
    }

    public static class Flags {
        public static void Set<T> (ref T _mask, T _flag) where T : struct {
            int maskValue = (int) (object) _mask;
            int flagValue = (int) (object) _flag;

            _mask = (T) (object) (maskValue | flagValue);
        }

        public static void Unset<T> (ref T _mask, T _flag) where T : struct {
            int maskValue = (int) (object) _mask;
            int flagValue = (int) (object) _flag;

            _mask = (T) (object) (maskValue & (~flagValue));
        }

        public static void Toggle<T> (ref T _mask, T _flag) where T : struct {
            if (Contains (_mask, _flag)) {
                Unset<T> (ref _mask, _flag);
            } else {
                Set<T> (ref _mask, _flag);
            }
        }

        private static bool Contains<T> (T _mask, T _flag) where T : struct {
            return Contains ((int) (object) _mask, (int) (object) _flag);
        }

        private static bool Contains (int _mask, int _flag) {
            return (_mask & _flag) != 0;
        }
    }

}