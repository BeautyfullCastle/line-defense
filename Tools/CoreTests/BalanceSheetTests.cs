using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using LineDefense.Core;
using Xunit;

namespace CoreTests
{
    public class BalanceSheetTests
    {
        // 시트에 없어도 되는 상수. 창고 순서도 구현에만 쓰는 값
        static readonly string[] CodeOnlyKeys = { "WH_RESERVE", "WH_MIN_TRANSFER" };

        static string RepoFile(string rel)
        {
            for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
            {
                string p = Path.Combine(d.FullName, rel);
                if (File.Exists(p)) return p;
            }
            throw new FileNotFoundException(rel);
        }

        [Fact]
        public void DefaultSheet_HasEveryBalanceKey_AndNoUnknownKey()
        {
            byte[] bytes = File.ReadAllBytes(RepoFile("Assets/Resources/BalanceSheet.bytes"));
            Assert.True(BalanceSheet.TryRead(bytes, out var values, out string err), err);

            var res = new Balance().Apply(values);
            Assert.Empty(res.Unknown);
            Assert.Equal(CodeOnlyKeys.OrderBy(k => k), res.Missing.OrderBy(k => k));
        }

        [Fact]
        public void Read_FindsColumnsByHeader_AndSkipsRowsWithoutKey()
        {
            // 열 순서를 바꾸고 공유 문자열과 인라인 문자열을 섞은 시트
            byte[] x = Xlsx(new[]
            {
                ("Other", "<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>다른 시트</t></is></c></row>"),
                (BalanceSheet.SheetName,
                    "<row r=\"3\"><c r=\"B3\" t=\"s\"><v>0</v></c><c r=\"D3\" t=\"s\"><v>1</v></c></row>" +
                    "<row r=\"4\"><c r=\"A4\" t=\"s\"><v>2</v></c></row>" +
                    "<row r=\"5\"><c r=\"B5\" t=\"inlineStr\"><is><t>RANGE</t></is></c><c r=\"D5\"><v>200</v></c></row>" +
                    "<row r=\"6\"><c r=\"D6\"><v>999</v></c></row>" +
                    "<row r=\"7\"><c r=\"B7\" t=\"inlineStr\"><is><t>FORT_CAP</t></is></c><c r=\"D7\"><f>2*3</f><v>6.4</v></c></row>" +
                    "<row r=\"8\"><c r=\"B8\" t=\"inlineStr\"><is><t>NOT_IN_CODE</t></is></c><c r=\"D8\"><v>1</v></c></row>"),
            }, new[] { "코드 상수 (프로토 연동)", "값", "1. 구분" });

            Assert.True(BalanceSheet.TryRead(x, out var values, out string err), err);
            Assert.Equal(3, values.Count);
            Assert.Equal(200f, values["RANGE"]);

            var b = new Balance();
            var res = b.Apply(values);
            Assert.Equal(200f, b.RANGE);
            Assert.Equal(6f, b.FORT_CAP); // 정수 상수는 반올림
            Assert.Equal(new[] { "NOT_IN_CODE" }, res.Unknown);
            Assert.Contains(res.Changed, c => c.Key == "RANGE" && c.From == 165f && c.To == 200f);
            Assert.Contains("FORT_ATK", res.Missing);
        }

        [Fact]
        public void Read_RejectsBrokenFile_WithoutThrowing()
        {
            Assert.False(BalanceSheet.TryRead(Encoding.UTF8.GetBytes("not a zip"), out _, out string err));
            Assert.False(string.IsNullOrEmpty(err));
            Assert.False(BalanceSheet.TryRead(new byte[0], out _, out _));
        }

        [Fact]
        public void Read_FailsWhenHeaderIsMissing()
        {
            byte[] x = Xlsx(new[] { (BalanceSheet.SheetName, "<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>") }, new string[0]);
            Assert.False(BalanceSheet.TryRead(x, out _, out string err));
            Assert.Contains("코드 상수", err);
        }

        // 시트 이름과 sheetData 안쪽 XML로 최소한의 xlsx를 만듦
        static byte[] Xlsx(IList<(string Name, string Rows)> sheets, string[] shared)
        {
            const string M = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                void Put(string path, string xml)
                {
                    using var w = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
                    w.Write(xml);
                }
                var wb = new StringBuilder($"<workbook xmlns=\"{M}\" xmlns:r=\"{R}\"><sheets>");
                var rels = new StringBuilder("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
                for (int i = 0; i < sheets.Count; i++)
                {
                    wb.Append($"<sheet name=\"{sheets[i].Name}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
                    rels.Append($"<Relationship Id=\"rId{i + 1}\" Type=\"ws\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
                    Put($"xl/worksheets/sheet{i + 1}.xml", $"<worksheet xmlns=\"{M}\"><sheetData>{sheets[i].Rows}</sheetData></worksheet>");
                }
                Put("xl/workbook.xml", wb.Append("</sheets></workbook>").ToString());
                Put("xl/_rels/workbook.xml.rels", rels.Append("</Relationships>").ToString());
                var ss = new StringBuilder($"<sst xmlns=\"{M}\">");
                foreach (string s in shared) ss.Append($"<si><t>{s}</t></si>");
                Put("xl/sharedStrings.xml", ss.Append("</sst>").ToString());
            }
            return ms.ToArray();
        }
    }
}
