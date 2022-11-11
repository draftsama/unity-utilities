using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modules.Utilities
{
    public static class ValueChecker
    {
        public static bool ValidateCheckEmptyString(Object thisObject, string fieldName, string stringToCheck)
        {
            if (string.IsNullOrEmpty(stringToCheck))
            {
                Debug.LogWarning($"[{fieldName}] is empty and must contain a value in object [{thisObject.name.ToString()}]");
                return true;
            }
            return false;
        }



        public static bool ValidateCheckNullValue(Object thisObject, string fieldName, UnityEngine.Object objectToCheck)
        {
            if (objectToCheck == null)
            {
                Debug.LogWarning($"[{fieldName}] is null and must contain a value in object [{thisObject.name.ToString()}]");
                return true;
            }
            return false;
        }



        public static bool ValidateCheckEnumerableValues(Object thisObject, string fieldName, IEnumerable enumerableObjectToCheck)
        {
            bool isError = false;
            int count = 0;

            if (enumerableObjectToCheck == null)
            {

                Debug.LogWarning($"[{fieldName}] is null in object [{thisObject.name.ToString()}]");
                return true;
            }


            foreach (var item in enumerableObjectToCheck)
            {
                if (item == null)
                {
                    Debug.LogWarning($"[{fieldName}] has null values in object [{thisObject.name.ToString()}]");
                    isError = true;
                }
                else
                    count++;
            }

            if (count == 0)
            {
                isError = true;

                Debug.LogWarning($"[{fieldName}] has no value in object [{thisObject.name.ToString()}]");
            }

            return isError;
        }
        public static bool ValidateCheckPositiveValue(Object thisObject, string fieldName, int valueToCheck, bool isZeroAllowed)
        {
            bool error = false;

            if (isZeroAllowed)
            {
                if (valueToCheck < 0)
                {
                    Debug.LogWarning($"[{fieldName}] must contain a positive value or zero in object [{thisObject.name.ToString()}]");
                    error = true;
                }
            }
            else
            {
                if (valueToCheck <= 0)
                {
                    Debug.LogWarning($"[{fieldName}] must contain a positive value or zero in object [{thisObject.name.ToString()}]");
                    error = true;
                }
            }


            return error;
        }

        public static bool ValidateCheckPositiveValue(Object thisObject, string fieldName, float valueToCheck, bool isZeroAllowed)
        {
            bool error = false;

            if (isZeroAllowed)
            {
                if (valueToCheck < 0f)
                {
                    Debug.LogWarning($"[{fieldName}] must contain a positive value or zero in object [{thisObject.name.ToString()}]");
                    error = true;
                }
            }
            else
            {
                if (valueToCheck <= 0f)
                {
                    Debug.LogWarning($"[{fieldName}] must contain a positive value or zero in object [{thisObject.name.ToString()}]");
                    error = true;
                }
            }


            return error;
        }
        public static bool ValidateCheckPositiveRange(Object thisObject, string fieldNameMinimum, float valueToCheckMinimum, string fieldNameMaximum, float valueToCheckMaximum, bool isZeroAllowed)
        {
            bool error = false;

            if (valueToCheckMinimum > valueToCheckMaximum)
            {
                Debug.LogWarning($"[{fieldNameMinimum}] must be less than or equal to [{fieldNameMaximum}] to object [{thisObject.name.ToString()}]");
                error = true;
            }

            if (ValidateCheckPositiveValue(thisObject, fieldNameMinimum, valueToCheckMinimum, isZeroAllowed)) error = true;
            if (ValidateCheckPositiveValue(thisObject, fieldNameMaximum, valueToCheckMaximum, isZeroAllowed)) error = true;


            return error;
        }
        public static bool ValidateCheckPositiveRange(Object thisObject, string fieldNameMinimum, int valueToCheckMinimum, string fieldNameMaximum, int valueToCheckMaximum, bool isZeroAllowed)
        {
            bool error = false;

            if (valueToCheckMinimum > valueToCheckMaximum)
            {
                Debug.LogWarning($"[{fieldNameMinimum}] must be less than or equal to [{fieldNameMaximum}] to object [{thisObject.name.ToString()}]");
                error = true;
            }

            if (ValidateCheckPositiveValue(thisObject, fieldNameMinimum, valueToCheckMinimum, isZeroAllowed)) error = true;
            if (ValidateCheckPositiveValue(thisObject, fieldNameMaximum, valueToCheckMaximum, isZeroAllowed)) error = true;


            return error;
        }
    }

}