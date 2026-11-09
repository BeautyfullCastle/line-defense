using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;

namespace LineDefense.Core
{
    /// <summary>밸런스 시트 파일에서 읽은 값입니다.</summary>
    public sealed class BalanceSheetData
    {
        /// <summary>"밸런스" 시트의 코드 상수 → 값입니다.</summary>
        public Dictionary<string, float> Values;

        /// <summary>"보급 선택지" 시트의 ID → 행입니다. 시트가 없거나 읽지 못하면 null입니다.</summary>
        public Dictionary<string, SupplyRow> Supply;

        /// <summary>읽기는 됐지만 확인이 필요한 내용입니다(같은 ID가 두 번 있음 등).</summary>
        public readonly List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// 기획 밸런스 시트(xlsx)를 읽습니다. "밸런스" 시트에서 코드 상수와 값을,
    /// "보급 선택지" 시트에서 ID, 값, 등장 가중치, 최대 선택 횟수, 첫 등장 웨이브를 읽습니다.
    /// 머리글 이름으로 열을 찾으므로 열 순서가 바뀌어도 됩니다.
    /// </summary>
    public static class BalanceSheet
    {
        /// <summary>밸런스 값을 읽는 시트 이름입니다. 없으면 첫 시트를 읽습니다.</summary>
        public const string SheetName = "밸런스";

        /// <summary>보급 선택지를 읽는 시트 이름입니다. 없으면 보급 선택지는 코드 기본 표를 씁니다.</summary>
        public const string SupplySheetName = "보급 선택지";

        const string KeyHeader = "코드 상수", ValueHeader = "값";
        const string IdHeader = "ID", WeightHeader = "등장 가중치", MaxHeader = "최대 선택 횟수", FirstHeader = "첫 등장 웨이브";

        static readonly Regex KeyPattern = new Regex("^[A-Z][A-Z0-9_]*$");

        /// <summary>xlsx 바이트를 읽습니다. 밸런스 값을 못 읽으면 false와 이유를 반환합니다.</summary>
        public static bool TryRead(byte[] xlsx, out BalanceSheetData data, out string error)
        {
            data = null;
            error = null;
            if (xlsx == null || xlsx.Length == 0) { error = "빈 파일입니다."; return false; }
            try
            {
                using (var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read))
                {
                    var book = new XlsxWorkbook(zip);
                    var rows = book.Rows(SheetName, true);
                    if (rows == null) { error = "시트를 찾지 못했습니다."; return false; }
                    var d = new BalanceSheetData { Values = ExtractValues(rows, out error) };
                    if (d.Values == null) return false;
                    var supplyRows = book.Rows(SupplySheetName, false);
                    if (supplyRows != null) d.Supply = ExtractSupply(supplyRows, d.Warnings);
                    data = d;
                    return true;
                }
            }
            catch (Exception e) when (e is InvalidDataException || e is XmlException || e is IOException)
            {
                error = "xlsx 파일을 읽지 못했습니다: " + e.Message;
                return false;
            }
        }

        /// <summary>
        /// 읽은 값을 balance에 덮어씁니다. 보급 선택지 행은 balance가 가진 행 위에 덮어씁니다.
        /// 결과에는 밸런스 값과 보급 선택지의 바뀐 값, 모르는 상수·ID, 경고가 들어갑니다.
        /// </summary>
        public static BalanceApplyResult ApplyTo(Balance balance, BalanceSheetData data)
        {
            var before = Supply.Build(balance.SupplyRows, out float growBefore);
            var res = balance.Apply(data.Values);
            if (data.Supply != null)
            {
                var merged = new Dictionary<string, SupplyRow>();
                if (balance.SupplyRows != null) foreach (var kv in balance.SupplyRows) merged[kv.Key] = kv.Value;
                foreach (var kv in data.Supply) merged[kv.Key] = kv.Value;
                balance.SupplyRows = merged;
            }
            var after = Supply.Build(balance.SupplyRows, out float growAfter, res.Unknown);
            Supply.Diff(before, after, res.Changed);
            if (growBefore != growAfter) res.Changed.Add((Supply.GoldGrowId, growBefore, growAfter));
            res.Warnings.AddRange(data.Warnings);
            return res;
        }

        static Dictionary<string, float> ExtractValues(List<Dictionary<string, string>> rows, out string error)
        {
            error = null;
            var cols = FindColumns(rows, out int start, KeyHeader, ValueHeader);
            if (cols == null) { error = Missing(SheetName, KeyHeader, ValueHeader); return null; }
            var values = new Dictionary<string, float>();
            for (int i = start; i < rows.Count; i++)
            {
                // 계산값 행은 코드 상수 칸이 비어 있어 건너뜀
                if (!TryKey(rows[i], cols[KeyHeader], out string key)) continue;
                float? v = Num(rows[i], cols[ValueHeader]);
                if (v != null) values[key] = v.Value;
            }
            if (values.Count == 0) { error = "읽은 값이 없습니다."; return null; }
            return values;
        }

        static Dictionary<string, SupplyRow> ExtractSupply(List<Dictionary<string, string>> rows, List<string> warnings)
        {
            var cols = FindColumns(rows, out int start, IdHeader, ValueHeader, WeightHeader, MaxHeader, FirstHeader);
            if (cols == null)
            {
                warnings.Add(Missing(SupplySheetName, IdHeader, ValueHeader, WeightHeader, MaxHeader, FirstHeader) + " 보급 선택지는 기본 표를 씁니다.");
                return null;
            }
            var supply = new Dictionary<string, SupplyRow>();
            for (int i = start; i < rows.Count; i++)
            {
                // 구분 제목 행과 아래쪽 계산 표는 ID 칸이 비어 있거나 ID 모양이 아니라 건너뜀
                if (!TryKey(rows[i], cols[IdHeader], out string id)) continue;
                if (supply.ContainsKey(id)) { warnings.Add(SupplySheetName + " 시트에 ID " + id + "가 두 번 있습니다. 첫 행만 씁니다."); continue; }
                var r = rows[i];
                supply[id] = new SupplyRow
                {
                    Val = Num(r, cols[ValueHeader]), Weight = Num(r, cols[WeightHeader]),
                    Max = Int(Num(r, cols[MaxHeader])), FirstWave = Int(Num(r, cols[FirstHeader])),
                };
            }
            return supply;
        }

        // 머리글 이름이 모두 있는 첫 행을 찾아 머리글 → 열 문자를 돌려줌. "코드 상수 (프로토 연동)"처럼 뒤에 붙은 설명은 무시
        static Dictionary<string, string> FindColumns(List<Dictionary<string, string>> rows, out int start, params string[] headers)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var found = new Dictionary<string, string>();
                foreach (var cell in rows[i])
                {
                    string h = Array.Find(headers, x => cell.Value == x || cell.Value.StartsWith(x + " ", StringComparison.Ordinal));
                    if (h != null && !found.ContainsKey(h)) found[h] = cell.Key;
                }
                start = i + 1;
                if (found.Count == headers.Length) return found;
            }
            start = 0;
            return null;
        }

        static string Missing(string sheet, params string[] headers) =>
            sheet + " 시트 머리글에서 " + string.Join(", ", headers) + " 열을 찾지 못했습니다.";

        static bool TryKey(Dictionary<string, string> row, string col, out string key) =>
            row.TryGetValue(col, out key) && KeyPattern.IsMatch(key);

        static float? Num(Dictionary<string, string> row, string col) =>
            row.TryGetValue(col, out string raw) && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : (float?)null;

        static int? Int(float? v) => v == null ? (int?)null : Game.I(v.Value);
    }
}
