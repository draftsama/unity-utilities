using System.Collections.Generic;
using Modules.Utilities;
using UnityEngine;
using UnityEngine.UI;

// Exercises IHUDIndicatorViewHandler: writes the bound indicator's name and bind count into the label.
public class HUDTestLabelBinder : MonoBehaviour, IHUDIndicatorViewHandler
{
    public string prefix = "";

    static readonly Dictionary<string, int> s_BindCounts = new Dictionary<string, int>();

    public static int BindCount(string indicatorName)
    {
        return s_BindCounts.TryGetValue(indicatorName, out int count) ? count : 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_BindCounts.Clear();
    }

    public void OnBind(HUDIndicator indicator, HUDIndicatorView view)
    {
        int count = BindCount(indicator.name) + 1;
        s_BindCounts[indicator.name] = count;
        GetComponentInChildren<Text>(true).text = prefix + indicator.name + " #" + count;
    }

    public void OnUnbind(HUDIndicator indicator, HUDIndicatorView view)
    {
        GetComponentInChildren<Text>(true).text = "";
    }
}
