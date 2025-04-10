using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modules.Utilities
{
    [CreateAssetMenu(fileName = "BuildSettingInfo", menuName = "[Draft Utility]/Create BuildSettingInfo", order = 0)]
    public class UnityBuildSettingInfo : ScriptableObject
    {
        public bool m_EnableSoundNotify;
        public bool m_EnableCopyFolder;
        public List<string> m_CopyFolderNameList;
        public bool m_EnableSendMessage;

        public string m_APISend =
            "https://api.telegram.org/bot1671713978:AAGGuzmbA2IQlZlQz66Z9yNWtckivBZZuuw/sendMessage?chat_id=1575164820&text=";
    }
}