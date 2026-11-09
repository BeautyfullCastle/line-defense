using System;
using H.ContentsLoader;
using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>
    /// 게임에 쓸 밸런스 값을 정합니다. 기본 시트(Contents의 Balance/BalanceSheet.bytes)를 먼저 적용하고,
    /// 치트 패널에서 불러온 시트가 있으면 그 값을 덮어씁니다. 불러온 xlsx는 PlayerPrefs에 남아 새로고침해도 유지됩니다.
    /// </summary>
    public static class BalanceStore
    {
        private const string DefaultSheet = "Balance/BalanceSheet";
        private const string PrefSheet = "balance.sheet.xlsx";
        private const string PrefName = "balance.sheet.name";

        /// <summary>지금 쓰는 값의 출처입니다. 기본 시트면 null입니다.</summary>
        public static string ImportedName
        {
            get; private set;
        }

        /// <summary>기본 시트에 불러온 시트를 덮어쓴 밸런스를 만듭니다.</summary>
        public static Balance Load(out string warning)
        {
            var balance = LoadDefault(out warning);
            ImportedName = null;
            string saved = PlayerPrefs.GetString(PrefSheet, string.Empty);
            if (saved.Length == 0)
            {
                return balance;
            }

            if (!TryDecode(saved, out var data))
            {
                warning = "저장해 둔 밸런스 시트를 읽지 못해 기본 시트를 씁니다.";
                return balance;
            }
            BalanceSheet.ApplyTo(balance, data);
            ImportedName = PlayerPrefs.GetString(PrefName, "불러온 시트");
            return balance;
        }

        /// <summary>
        /// xlsx를 읽어 기본 시트 위에 적용하고 저장합니다. diff는 기본 시트 대비 바뀐 값입니다.
        /// 읽지 못하면 false와 이유를 반환하고 저장된 시트는 그대로 둡니다.
        /// </summary>
        public static bool TryImport(byte[] xlsx, string name, out Balance balance, out BalanceApplyResult diff, out string error)
        {
            balance = null;
            diff = null;
            if (!BalanceSheet.TryRead(xlsx, out var data, out error))
            {
                return false;
            }

            balance = LoadDefault(out _);
            diff = BalanceSheet.ApplyTo(balance, data);
            PlayerPrefs.SetString(PrefSheet, Convert.ToBase64String(xlsx));
            PlayerPrefs.SetString(PrefName, name);
            PlayerPrefs.Save();
            ImportedName = name;
            return true;
        }

        /// <summary>불러온 시트를 지우고 기본 시트 값으로 돌아갑니다.</summary>
        public static Balance Reset(out string warning)
        {
            PlayerPrefs.DeleteKey(PrefSheet);
            PlayerPrefs.DeleteKey(PrefName);
            PlayerPrefs.Save();
            ImportedName = null;
            return LoadDefault(out warning);
        }

        private static Balance LoadDefault(out string warning)
        {
            warning = null;
            var balance = new Balance();
            var asset = Content.Load<TextAsset>(DefaultSheet);
            if (asset == null)
            {
                warning = "기본 밸런스 시트가 없어 코드 기본값을 씁니다.";
                return balance;
            }
            if (BalanceSheet.TryRead(asset.bytes, out var data, out string errorMessage))
            {
                var result = BalanceSheet.ApplyTo(balance, data);
                foreach (string sheetWarning in result.Warnings)
                {
                    Debug.LogWarning("기본 밸런스 시트: " + sheetWarning);
                }
            }
            else
            {
                warning = "기본 밸런스 시트를 읽지 못해 코드 기본값을 씁니다. " + errorMessage;
            }

            if (warning != null)
            {
                Debug.LogWarning(warning);
            }

            return balance;
        }

        private static bool TryDecode(string base64, out BalanceSheetData data)
        {
            data = null;
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64);
            }
            catch (FormatException) { return false; }
            return BalanceSheet.TryRead(bytes, out data, out _);
        }
    }
}
