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
    internal sealed class XlsxWorkbook
    {
        private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        private readonly ZipArchive _archive;
        private readonly List<string> _shared;
        private readonly List<(string Name, string Path)> _sheets = new List<(string, string)>();

        public XlsxWorkbook(ZipArchive archive)
        {
            _archive = archive;
            _shared = ReadSharedStrings(archive);
            var workbookEntry = archive.GetEntry("xl/workbook.xml");
            var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
            if (workbookEntry == null || relationshipsEntry == null)
            {
                return;
            }

            var document = Load(workbookEntry);
            var relationships = Load(relationshipsEntry);
            foreach (XmlNode sheetNode in document.SelectNodes("//m:sheets/m:sheet", Names(document)))
            {
                if (sheetNode is XmlElement sheetElement)
                {
                    _sheets.Add((sheetElement.GetAttribute("name"), RelationshipTarget(relationships, sheetElement.GetAttribute("id", RelationshipNamespace))));
                }
            }
        }

        /// <summary>이름이 같은 시트의 행(열 문자 → 셀 문자열)입니다. 없으면 firstIfMissing일 때 첫 시트, 아니면 null입니다.</summary>
        public List<Dictionary<string, string>> Rows(string name, bool firstIfMissing)
        {
            string path = null;
            foreach (var sheet in _sheets)
            {
                if (sheet.Name == name)
                {
                    path = sheet.Path;
                    break;
                }
            }

            if (path == null && firstIfMissing && _sheets.Count > 0)
            {
                path = _sheets[0].Path;
            }

            return path == null ? null : ReadRows(path);
        }

        private List<Dictionary<string, string>> ReadRows(string path)
        {
            var entry = _archive.GetEntry(path);
            if (entry == null)
            {
                throw new InvalidDataException("시트 파일이 없습니다: " + path);
            }

            var document = Load(entry);
            var namespaces = Names(document);
            var rows = new List<Dictionary<string, string>>();
            foreach (XmlNode row in document.SelectNodes("//m:sheetData/m:row", namespaces))
            {
                var cells = new Dictionary<string, string>();
                foreach (XmlNode cellNode in row.SelectNodes("m:c", namespaces))
                {
                    if (!(cellNode is XmlElement cell))
                    {
                        continue;
                    }

                    string text = CellText(cell, namespaces);
                    if (!string.IsNullOrEmpty(text))
                    {
                        cells[Column(cell.GetAttribute("r"))] = text.Trim();
                    }
                }
                rows.Add(cells);
            }
            return rows;
        }

        // 공유 문자열, 인라인 문자열, 숫자, 수식 결과를 모두 문자열로 읽음
        private string CellText(XmlElement cell, XmlNamespaceManager namespaces)
        {
            string cellType = cell.GetAttribute("t");
            if (cellType == "inlineStr")
            {
                return TextOf(cell, namespaces);
            }

            string value = cell.SelectSingleNode("m:v", namespaces)?.InnerText;
            if (value == null || cellType != "s")
            {
                return value;
            }

            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) && i >= 0 && i < _shared.Count ? _shared[i] : null;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var list = new List<string>();
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return list;
            }

            var document = Load(entry);
            var namespaces = Names(document);
            foreach (XmlNode sharedItem in document.SelectNodes("//m:si", namespaces))
            {
                list.Add(TextOf(sharedItem, namespaces));
            }

            return list;
        }

        private static string RelationshipTarget(XmlDocument relationships, string id)
        {
            foreach (XmlNode childNode in relationships.DocumentElement.ChildNodes)
            {
                if (!(childNode is XmlElement relationship) || relationship.GetAttribute("Id") != id)
                {
                    continue;
                }

                string target = relationship.GetAttribute("Target").TrimStart('/');
                return target.StartsWith("xl/", StringComparison.Ordinal) ? target : "xl/" + target;
            }
            return null;
        }

        private static XmlDocument Load(ZipArchiveEntry entry)
        {
            var document = new XmlDocument { XmlResolver = null };
            using (var stream = entry.Open())
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                document.Load(reader);
            }

            return document;
        }

        private static XmlNamespaceManager Names(XmlDocument document)
        {
            var namespaces = new XmlNamespaceManager(document.NameTable);
            namespaces.AddNamespace("m", SpreadsheetNamespace);
            return namespaces;
        }

        // 서식이 섞인 문자열(r/t)도 이어 붙임. 발음 표기(rPh)는 제외
        private static string TextOf(XmlNode node, XmlNamespaceManager namespaces)
        {
            var builder = new StringBuilder();
            foreach (XmlNode textNode in node.SelectNodes(".//m:t[not(ancestor::m:rPh)]", namespaces))
            {
                builder.Append(textNode.InnerText);
            }

            return builder.ToString();
        }

        private static string Column(string cellReference)
        {
            int letterCount = 0;
            while (letterCount < cellReference.Length && char.IsLetter(cellReference[letterCount]))
            {
                letterCount++;
            }

            return cellReference.Substring(0, letterCount);
        }
    }
}
