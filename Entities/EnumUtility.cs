using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Modules.Utilities
{
    public class EnumUtility
    {

        public static List<string> EnumToNameList<T>() where T : struct
        {
            Type t = typeof(T);
            return !t.IsEnum ? null : Enum.GetValues(t).Cast<Enum>().Select(x => x.ToString()).ToList();
        }
    }
}
