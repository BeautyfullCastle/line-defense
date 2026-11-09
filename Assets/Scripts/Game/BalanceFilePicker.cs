using System;
using System.IO;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace LineDefense.View
{
    /// <summary>밸런스 xlsx 파일을 고르는 창을 엽니다. 웹 빌드는 브라우저 창, 에디터는 에디터 창을 씁니다.</summary>
    public static class BalanceFilePicker
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void BalancePickFile(string objectName, string methodName);
#endif

        /// <summary>
        /// 파일 창을 엽니다. 웹 빌드는 고른 파일을 target 오브젝트의 method로 "이름|base64" 문자열로 보냅니다.
        /// 에디터는 바로 onPicked를 부릅니다. 지원하지 않는 플랫폼이면 false입니다.
        /// </summary>
        public static bool Open(GameObject target, string method, Action<string, byte[]> onPicked)
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("밸런스 시트 불러오기", string.Empty, "xlsx");
            if (!string.IsNullOrEmpty(path)) onPicked(Path.GetFileName(path), File.ReadAllBytes(path));
            return true;
#elif UNITY_WEBGL
            BalancePickFile(target.name, method);
            return true;
#else
            return false;
#endif
        }

        /// <summary>웹 빌드가 보낸 "이름|base64" 문자열을 풉니다.</summary>
        public static bool TryDecode(string payload, out string name, out byte[] bytes)
        {
            name = null;
            bytes = null;
            int i = payload == null ? -1 : payload.LastIndexOf('|');
            if (i < 0) return false;
            name = payload.Substring(0, i);
            try { bytes = Convert.FromBase64String(payload.Substring(i + 1)); }
            catch (FormatException) { return false; }
            return true;
        }
    }
}
