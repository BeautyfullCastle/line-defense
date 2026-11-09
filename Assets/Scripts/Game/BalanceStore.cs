using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>
    /// 게임에 쓸 밸런스 값을 정합니다. 기본 시트(Resources/BalanceSheet.bytes)를 먼저 적용하고,
    /// 치트 패널에서 불러온 시트가 있으면 그 값을 덮어씁니다. 불러온 값은 PlayerPrefs에 남아 새로고침해도 유지됩니다.
    /// </summary>
    public static class BalanceStore
    {
        const string DefaultResource = "BalanceSheet";
        const string PrefValues = "balance.sheet.values";
        const string PrefName = "balance.sheet.name";

        /// <summary>지금 쓰는 값의 출처입니다. 기본 시트면 null입니다.</summary>
        public static string ImportedName { get; private set; }

        /// <summary>기본 시트에 불러온 시트를 덮어쓴 밸런스를 만듭니다.</summary>
        public static Balance Load(out string warning)
        {
            var b = LoadDefault(out warning);
            ImportedName = null;
            string saved = PlayerPrefs.GetString(PrefValues, string.Empty);
            if (saved.Length == 0) return b;
            b.Apply(Decode(saved));
            ImportedName = PlayerPrefs.GetString(PrefName, "불러온 시트");
            return b;
        }

        /// <summary>
        /// xlsx를 읽어 기본 시트 위에 적용하고 저장합니다. diff는 기본 시트 대비 바뀐 값입니다.
        /// 읽지 못하면 false와 이유를 반환하고 저장된 값은 그대로 둡니다.
        /// </summary>
        public static bool TryImport(byte[] xlsx, string name, out Balance balance, out BalanceApplyResult diff, out string error)
        {
            balance = null;
            diff = null;
            if (!BalanceSheet.TryRead(xlsx, out var values, out error)) return false;
            balance = LoadDefault(out _);
            diff = balance.Apply(values);
            PlayerPrefs.SetString(PrefValues, Encode(values));
            PlayerPrefs.SetString(PrefName, name);
            PlayerPrefs.Save();
            ImportedName = name;
            return true;
        }

        /// <summary>불러온 시트를 지우고 기본 시트 값으로 돌아갑니다.</summary>
        public static Balance Reset(out string warning)
        {
            PlayerPrefs.DeleteKey(PrefValues);
            PlayerPrefs.DeleteKey(PrefName);
            PlayerPrefs.Save();
            ImportedName = null;
            return LoadDefault(out warning);
        }

        static Balance LoadDefault(out string warning)
        {
            warning = null;
            var b = new Balance();
            var asset = Resources.Load<TextAsset>(DefaultResource);
            if (asset == null) { warning = "기본 밸런스 시트가 없어 코드 기본값을 씁니다."; return b; }
            if (BalanceSheet.TryRead(asset.bytes, out var values, out string err)) b.Apply(values);
            else warning = "기본 밸런스 시트를 읽지 못해 코드 기본값을 씁니다. " + err;
            Resources.UnloadAsset(asset);
            if (warning != null) Debug.LogWarning(warning);
            return b;
        }

        static string Encode(Dictionary<string, float> values)
        {
            var sb = new StringBuilder();
            foreach (var kv in values) sb.Append(kv.Key).Append('=').Append(kv.Value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            return sb.ToString();
        }

        static Dictionary<string, float> Decode(string s)
        {
            var d = new Dictionary<string, float>();
            foreach (string line in s.Split('\n'))
            {
                int i = line.IndexOf('=');
                if (i > 0 && float.TryParse(line.Substring(i + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                    d[line.Substring(0, i)] = v;
            }
            return d;
        }
    }
}
