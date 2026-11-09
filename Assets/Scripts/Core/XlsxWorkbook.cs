using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace LineDefense.Core
{
    /// <summary>xlsx 안의 통합 문서입니다. 시트 이름으로 행을 읽습니다. 셀 서식과 수식은 읽지 않고 저장된 값만 읽습니다.</summary>
    sealed class XlsxWorkbook
    {
        const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        readonly ZipArchive _zip;
        readonly List<string> _shared;
        readonly List<(string Name, string Path)> _sheets = new List<(string, string)>();

        public XlsxWorkbook(ZipArchive zip)
        {
            _zip = zip;
            _shared = ReadSharedStrings(zip);
            var wbEntry = zip.GetEntry("xl/workbook.xml");
            var relEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
            if (wbEntry == null || relEntry == null) return;
            var wb = Load(wbEntry);
            var rels = Load(relEntry);
            foreach (XmlNode n in wb.SelectNodes("//m:sheets/m:sheet", Names(wb)))
                if (n is XmlElement s) _sheets.Add((s.GetAttribute("name"), RelTarget(rels, s.GetAttribute("id", RelNs))));
        }

        /// <summary>이름이 같은 시트의 행(열 문자 → 셀 문자열)입니다. 없으면 firstIfMissing일 때 첫 시트, 아니면 null입니다.</summary>
        public List<Dictionary<string, string>> Rows(string name, bool firstIfMissing)
        {
            string path = null;
            foreach (var s in _sheets) if (s.Name == name) { path = s.Path; break; }
            if (path == null && firstIfMissing && _sheets.Count > 0) path = _sheets[0].Path;
            return path == null ? null : ReadRows(path);
        }

        List<Dictionary<string, string>> ReadRows(string path)
        {
            var e = _zip.GetEntry(path);
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
                    string v = CellText(c, nm);
                    if (!string.IsNullOrEmpty(v)) cells[Column(c.GetAttribute("r"))] = v.Trim();
                }
                rows.Add(cells);
            }
            return rows;
        }

        // 공유 문자열, 인라인 문자열, 숫자, 수식 결과를 모두 문자열로 읽음
        string CellText(XmlElement c, XmlNamespaceManager nm)
        {
            string t = c.GetAttribute("t");
            if (t == "inlineStr") return TextOf(c, nm);
            string v = c.SelectSingleNode("m:v", nm)?.InnerText;
            if (v == null || t != "s") return v;
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) && i >= 0 && i < _shared.Count ? _shared[i] : null;
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
