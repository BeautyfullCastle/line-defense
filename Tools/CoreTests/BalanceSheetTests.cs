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
        private static readonly string[] CodeOnlyKeys = { "WH_RESERVE", "WH_MIN_TRANSFER" };

        private static string RepoFile(string relativePath)
        {
            for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory != null; directory = directory.Parent)
            {
                string candidatePath = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }
            throw new FileNotFoundException(relativePath);
        }

        [Fact]
        public void DefaultSheet_HasEveryBalanceKey_AndNoUnknownKey()
        {
            byte[] bytes = File.ReadAllBytes(RepoFile("Assets/Game/Contents/Balance/BalanceSheet.bytes"));
            Assert.True(BalanceSheet.TryRead(bytes, out var data, out string errorMessage), errorMessage);

            var applyResult = BalanceSheet.ApplyTo(new Balance(), data);
            Assert.Empty(applyResult.Unknown);
            Assert.Empty(applyResult.Warnings);
            Assert.Equal(CodeOnlyKeys.OrderBy(k => k), applyResult.Missing.OrderBy(k => k));
        }

        [Fact]
        public void DefaultSheet_HasEverySupplyId()
        {
            byte[] bytes = File.ReadAllBytes(RepoFile("Assets/Game/Contents/Balance/BalanceSheet.bytes"));
            Assert.True(BalanceSheet.TryRead(bytes, out var data, out string errorMessage), errorMessage);
            Assert.NotNull(data.Supply);
            foreach (var supplyDef in Supply.Build().Values)
            {
                Assert.True(data.Supply.ContainsKey(supplyDef.Id), supplyDef.Id);
            }

            Assert.True(data.Supply.ContainsKey(Supply.GoldGrowId));
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

            Assert.True(BalanceSheet.TryRead(x, out var data, out string errorMessage), errorMessage);
            Assert.Equal(3, data.Values.Count);
            Assert.Equal(200f, data.Values["RANGE"]);
            Assert.Null(data.Supply); // 보급 선택지 시트가 없으면 코드 기본 표

            var balance = new Balance();
            var applyResult = BalanceSheet.ApplyTo(balance, data);
            Assert.Equal(200f, balance.RANGE);
            Assert.Equal(6f, balance.FORT_CAP); // 정수 상수는 반올림
            Assert.Equal(new[] { "NOT_IN_CODE" }, applyResult.Unknown);
            Assert.Contains(applyResult.Changed, change => change.Key == "RANGE" && change.From == 165f && change.To == 200f);
            Assert.Contains("FORT_ATK", applyResult.Missing);
        }

        private const string BalanceRows =
            "<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>코드 상수</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>값</t></is></c></row>" +
            "<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>RANGE</t></is></c><c r=\"B2\"><v>165</v></c></row>";

        private static string SupplyRow(int rowNumber, string id, string valueText, string weight, string max, string first)
        {
            string C(string column, string cellValue) => cellValue == null ? "" : $"<c r=\"{column}{rowNumber}\"><v>{cellValue}</v></c>";
            return $"<row r=\"{rowNumber}\"><c r=\"A{rowNumber}\" t=\"inlineStr\"><is><t>{id}</t></is></c>{C("B", valueText)}{C("C", weight)}{C("D", max)}{C("E", first)}</row>";
        }

        private static byte[] SupplyXlsx(string rows) => Xlsx(new[]
        {
            (BalanceSheet.SheetName, BalanceRows),
            (BalanceSheet.SupplySheetName,
                "<row r=\"4\"><c r=\"A4\" t=\"inlineStr\"><is><t>ID</t></is></c><c r=\"B4\" t=\"inlineStr\"><is><t>값</t></is></c>" +
                "<c r=\"C4\" t=\"inlineStr\"><is><t>등장 가중치</t></is></c><c r=\"D4\" t=\"inlineStr\"><is><t>최대 선택 횟수</t></is></c>" +
                "<c r=\"E4\" t=\"inlineStr\"><is><t>첫 등장 웨이브</t></is></c></row>" +
                "<row r=\"5\"><c r=\"A5\" t=\"inlineStr\"><is><t>1. 건설 권한</t></is></c></row>" + rows),
        }, new string[0]);

        [Fact]
        public void Supply_AppliesRows_AndBlankMaxMeansUnlimited()
        {
            byte[] x = SupplyXlsx(
                SupplyRow(6, "SUP_GOLD", "40", "50", null, "3") +
                SupplyRow(7, "CART_SPD_UP", "0.9", "10", "6", "1") +
                SupplyRow(8, Supply.GoldGrowId, "7", null, null, null) +
                SupplyRow(9, "SUP_UNKNOWN", "1", "1", null, "1"));
            Assert.True(BalanceSheet.TryRead(x, out var data, out string errorMessage), errorMessage);

            var balance = new Balance();
            var applyResult = BalanceSheet.ApplyTo(balance, data);
            var game = new Game(balance, 1);
            var gold = game.Supplies["gold"];
            Assert.Equal(40f, gold.Value);
            Assert.Equal(50f, gold.Weight);
            Assert.Equal(0, gold.MaxPicks);
            Assert.Equal(3, gold.FirstWave);
            var cartSpeedSupply = game.Supplies["cartSpd"];
            Assert.Null(cartSpeedSupply.Value); // 값은 밸런스 시트 CART_SPD_UP을 씀
            Assert.Equal(6, cartSpeedSupply.MaxPicks);
            Assert.Equal(7f, game.GoldSupplyGrowth);
            Assert.Equal(new[] { "SUP_UNKNOWN" }, applyResult.Unknown);
            Assert.Contains(applyResult.Changed, change => change.Key == "SUP_GOLD 등장 가중치" && change.From == 20f && change.To == 50f);
            Assert.Contains(applyResult.Changed, change => change.Key == Supply.GoldGrowId && change.To == 7f);
        }

        [Fact]
        public void Supply_WarnsOnDuplicateId_AndKeepsFirstRow()
        {
            byte[] x = SupplyXlsx(SupplyRow(6, "SUP_BUILD_FORT", "1", "25", null, "1") + SupplyRow(7, "SUP_BUILD_FORT", "1", "99", null, "1"));
            Assert.True(BalanceSheet.TryRead(x, out var data, out _));
            Assert.Equal(25f, data.Supply["SUP_BUILD_FORT"].Weight);
            Assert.Contains(data.Warnings, warning => warning.Contains("SUP_BUILD_FORT"));
        }

        [Fact]
        public void Supply_WarnsWhenHeaderIsMissing_AndUsesCodeTable()
        {
            byte[] x = Xlsx(new[] { (BalanceSheet.SheetName, BalanceRows), (BalanceSheet.SupplySheetName, "<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>") }, new string[0]);
            Assert.True(BalanceSheet.TryRead(x, out var data, out _));
            Assert.Null(data.Supply);
            Assert.Single(data.Warnings);
        }

        [Fact]
        public void Read_RejectsBrokenFile_WithoutThrowing()
        {
            Assert.False(BalanceSheet.TryRead(Encoding.UTF8.GetBytes("not a zip"), out _, out string errorMessage));
            Assert.False(string.IsNullOrEmpty(errorMessage));
            Assert.False(BalanceSheet.TryRead(new byte[0], out _, out _));
        }

        [Fact]
        public void Read_FailsWhenHeaderIsMissing()
        {
            byte[] x = Xlsx(new[] { (BalanceSheet.SheetName, "<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>") }, new string[0]);
            Assert.False(BalanceSheet.TryRead(x, out _, out string errorMessage));
            Assert.Contains("코드 상수", errorMessage);
        }

        // 시트 이름과 sheetData 안쪽 XML로 최소한의 xlsx를 만듦
        private static byte[] Xlsx(IList<(string Name, string Rows)> sheets, string[] shared)
        {
            const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            var memoryStream = new MemoryStream();
            using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                void Put(string path, string xmlText)
                {
                    using var writer = new StreamWriter(zipArchive.CreateEntry(path).Open(), new UTF8Encoding(false));
                    writer.Write(xmlText);
                }
                var workbookXml = new StringBuilder($"<workbook xmlns=\"{SpreadsheetNamespace}\" xmlns:r=\"{RelationshipNamespace}\"><sheets>");
                var rels = new StringBuilder("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
                for (int i = 0; i < sheets.Count; i++)
                {
                    workbookXml.Append($"<sheet name=\"{sheets[i].Name}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
                    rels.Append($"<Relationship Id=\"rId{i + 1}\" Type=\"ws\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
                    Put($"xl/worksheets/sheet{i + 1}.xml", $"<worksheet xmlns=\"{SpreadsheetNamespace}\"><sheetData>{sheets[i].Rows}</sheetData></worksheet>");
                }
                Put("xl/workbook.xml", workbookXml.Append("</sheets></workbook>").ToString());
                Put("xl/_rels/workbook.xml.rels", rels.Append("</Relationships>").ToString());
                var sharedStringsXml = new StringBuilder($"<sst xmlns=\"{SpreadsheetNamespace}\">");
                foreach (string sharedString in shared)
                {
                    sharedStringsXml.Append($"<si><t>{sharedString}</t></si>");
                }

                Put("xl/sharedStrings.xml", sharedStringsXml.Append("</sst>").ToString());
            }
            return memoryStream.ToArray();
        }
    }
}
