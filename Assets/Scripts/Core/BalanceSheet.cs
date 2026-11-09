using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace LineDefense.Core
{
    /// <summary>
    /// 기획 밸런스 시트(xlsx)의 "밸런스" 시트에서 코드 상수와 값을 읽습니다.
    /// 머리글 행의 "코드 상수", "값" 열을 찾아 읽으므로 열 순서가 바뀌어도 됩니다.
    /// </summary>
    public static class BalanceSheet
    {
        /// <summary>값을 읽는 시트 이름입니다. 없으면 첫 시트를 읽습니다.</summary>
        public const string SheetName = "밸런스";

        const string KeyHeader = "코드 상수";
        const string ValueHeader = "값";
        const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        static readonly Regex KeyPattern = new Regex("^[A-Z][A-Z0-9_]*$");

        /// <summary>xlsx 바이트에서 상수 이름과 값을 읽습니다. 실패하면 false와 이유를 반환합니다.</summary>
        public static bool TryRead(byte[] xlsx, out Dictionary<string, float> values, out string error)
        {
            values = null;
            error = null;
            if (xlsx == null || xlsx.Length == 0) { error = "빈 파일입니다."; return false; }
            try
            {
                using (var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read))
                {
                    string path = FindSheetPath(zip);
                    if (path == null) { error = "시트를 찾지 못했습니다."; return false; }
                    values = Extract(ReadRows(zip, path, ReadSharedStrings(zip)), out error);
                    return values != null;
                }
            }
            catch (Exception e) when (e is InvalidDataException || e is XmlException || e is IOException)
            {
                error = "xlsx 파일을 읽지 못했습니다: " + e.Message;
                return false;
            }
        }

        static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var list = new List<string>();
            var e = zip.GetEntry("xl/sharedStrings.xml");
            if (e == null) return list;
            var doc = Load(e);
            var nm = Names(doc);
            foreach (XmlNode si in doc.SelectNodes("//m:si", nm)) list.Add(TextOf(si, nm));
            return list;
        }

        // 이름이 SheetName인 시트(없으면 첫 시트)의 zip 안 경로
        static string FindSheetPath(ZipArchive zip)
        {
            var wbEntry = zip.GetEntry("xl/workbook.xml");
            var relEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
            if (wbEntry == null || relEntry == null) return null;
            var wb = Load(wbEntry);
            XmlElement pick = null;
            foreach (XmlNode n in wb.SelectNodes("//m:sheets/m:sheet", Names(wb)))
            {
                if (!(n is XmlElement s)) continue;
                if (pick == null) pick = s;
                if (s.GetAttribute("name") == SheetName) { pick = s; break; }
            }
            return pick == null ? null : RelTarget(Load(relEntry), pick.GetAttribute("id", RelNs));
        }

        static string RelTarget(XmlDocument rels, string id)
        {
            foreach (XmlNode n in rels.DocumentElement.ChildNodes)
            {
                if (!(n is XmlElement rel) || rel.GetAttribute("Id") != id) continue;
                string target = rel.GetAttribute("Target").TrimStart('/');
                return target.StartsWith("xl/", StringComparison.Ordinal) ? target : "xl/" + target;
            }
            return null;
        }

        // 행마다 열 문자 -> 셀 문자열
        static List<Dictionary<string, string>> ReadRows(ZipArchive zip, string path, List<string> shared)
        {
            var e = zip.GetEntry(path);
            if (e == null) throw new InvalidDataException("시트 파일이 없습니다: " + path);
            var doc = Load(e);
            var nm = Names(doc);
            var rows = new List<Dictionary<string, string>>();
            foreach (XmlNode row in doc.SelectNodes("//m:sheetData/m:row", nm))
            {
                var cells = new Dictionary<string, string>();
                foreach (XmlNode n in row.SelectNodes("m:c", nm))
                {
                    if (!(n is XmlElement c)) continue;
                    string v = CellText(c, nm, shared);
                    if (!string.IsNullOrEmpty(v)) cells[Column(c.GetAttribute("r"))] = v.Trim();
                }
                rows.Add(cells);
            }
            return rows;
        }

        // 공유 문자열, 인라인 문자열, 숫자, 수식 결과를 모두 문자열로 읽음
        static string CellText(XmlElement c, XmlNamespaceManager nm, List<string> shared)
        {
            string t = c.GetAttribute("t");
            if (t == "inlineStr") return TextOf(c, nm);
            string v = c.SelectSingleNode("m:v", nm)?.InnerText;
            if (v == null || t != "s") return v;
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) && i >= 0 && i < shared.Count ? shared[i] : null;
        }

        static Dictionary<string, float> Extract(List<Dictionary<string, string>> rows, out string error)
        {
            error = null;
            if (!FindColumns(rows, out string keyCol, out string valCol, out int start))
            {
                error = "머리글에서 \"" + KeyHeader + "\"와 \"" + ValueHeader + "\" 열을 찾지 못했습니다.";
                return null;
            }
            var values = new Dictionary<string, float>();
            for (int i = start; i < rows.Count; i++)
            {
                // 계산값 행은 코드 상수 칸이 비어 있어 건너뜀
                if (!rows[i].TryGetValue(keyCol, out string key) || !KeyPattern.IsMatch(key)) continue;
                if (rows[i].TryGetValue(valCol, out string raw) && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                    values[key] = f;
            }
            if (values.Count == 0) { error = "읽은 값이 없습니다."; return null; }
            return values;
        }

        // "코드 상수"로 시작하는 칸과 "값" 칸이 함께 있는 첫 행을 머리글로 봄
        static bool FindColumns(List<Dictionary<string, string>> rows, out string keyCol, out string valCol, out int start)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                keyCol = valCol = null;
                foreach (var kv in rows[i])
                {
                    if (kv.Value.StartsWith(KeyHeader, StringComparison.Ordinal)) keyCol = kv.Key;
                    else if (kv.Value == ValueHeader) valCol = kv.Key;
                }
                start = i + 1;
                if (keyCol != null && valCol != null) return true;
            }
            keyCol = valCol = null;
            start = 0;
            return false;
        }

        static XmlDocument Load(ZipArchiveEntry e)
        {
            var doc = new XmlDocument { XmlResolver = null };
            using (var s = e.Open())
            using (var r = XmlReader.Create(s, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                doc.Load(r);
            return doc;
        }

        static XmlNamespaceManager Names(XmlDocument doc)
        {
            var nm = new XmlNamespaceManager(doc.NameTable);
            nm.AddNamespace("m", Ns);
            return nm;
        }

        // 서식이 섞인 문자열(r/t)도 이어 붙임. 발음 표기(rPh)는 제외
        static string TextOf(XmlNode node, XmlNamespaceManager nm)
        {
            var sb = new StringBuilder();
            foreach (XmlNode t in node.SelectNodes(".//m:t[not(ancestor::m:rPh)]", nm)) sb.Append(t.InnerText);
            return sb.ToString();
        }

        static string Column(string cellRef)
        {
            int n = 0;
            while (n < cellRef.Length && char.IsLetter(cellRef[n])) n++;
            return cellRef.Substring(0, n);
        }
    }
}
