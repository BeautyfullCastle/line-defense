using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LineDefense.Core;

namespace SimRunner
{
    // dotnet run -c Release --project Tools/SimRunner -- [--runs N] [--seed S] [--maxwave W] [--sheet FILE.xlsx | --no-sheet] [--set KEY=VAL ...] [--verbose]
    // 기본으로 게임과 같은 Assets/Game/Contents/Balance/BalanceSheet.bytes를 읽고, --set은 시트 값 위에 덮어씀
    public static class Program
    {
        const float Dt = .05f;
        const int BotEvery = 5;
        const float MaxTime = 3600f;
        const string DefaultSheet = "Assets/Game/Contents/Balance/BalanceSheet.bytes";

        public static int Main(string[] args)
        {
            int runs = 10, seed = 1, maxWave = 40;
            bool verbose = false, trace = false;
            string sheet = FindDefaultSheet();
            var sets = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--sheet": sheet = args[++i]; break;
                    case "--no-sheet": sheet = null; break;
                    case "--runs": runs = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--seed": seed = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--maxwave": maxWave = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--verbose": verbose = true; break;
                    case "--trace": trace = verbose = true; break;
                    case "--set": sets.Add(args[++i]); break;
                    default:
                        Console.Error.WriteLine("unknown argument " + args[i]);
                        return 1;
                }
            }

            var balance = BuildBalance(sheet, sets);
            if (balance == null) return 1;

            var reached = new List<int>();
            for (int r = 0; r < runs; r++)
            {
                var g = new Game(balance.Clone(), seed + r) { EmitEvents = false };
                var bot = new Bot(g);
                int lastLogged = 0;
                if (verbose) Console.WriteLine("--- run " + (r + 1) + " (seed " + (seed + r) + ")");
                for (int step = 0; !g.Over && g.Wave <= maxWave && g.T <= MaxTime; step++)
                {
                    g.Step(Dt);
                    if (verbose && g.Phase == Phase.Reward && g.Wave != lastLogged)
                    {
                        lastLogged = g.Wave;
                        PrintWave(g);
                    }
                    if (step % BotEvery == 0) bot.Tick();
                    if (trace && step % 200 == 0) PrintTrace(g);
                }
                int survived = g.Phase == Phase.Countdown || g.Phase == Phase.Reward ? g.Wave : g.Wave - 1;
                reached.Add(survived);
                Console.WriteLine("run " + (r + 1).ToString().PadLeft(2) + ": wave " + survived.ToString().PadLeft(2)
                    + (g.Over ? " (lost)" : " (alive)") + "  kills " + g.Kills + "  delivered " + g.Delivered + "  T " + g.T.ToString("0", CultureInfo.InvariantCulture));
            }

            reached.Sort();
            double mean = reached.Average();
            double median = reached.Count % 2 == 1 ? reached[reached.Count / 2] : (reached[reached.Count / 2 - 1] + reached[reached.Count / 2]) / 2.0;
            Console.WriteLine();
            Console.WriteLine("runs " + runs + "  wave reached: mean " + mean.ToString("0.0", CultureInfo.InvariantCulture)
                + "  median " + median.ToString("0.0", CultureInfo.InvariantCulture) + "  min " + reached[0] + "  max " + reached[reached.Count - 1]);
            foreach (var grp in reached.GroupBy(x => x).OrderBy(x => x.Key))
                Console.WriteLine("  wave " + grp.Key.ToString().PadLeft(2) + " | " + new string('#', grp.Count()) + " " + grp.Count());
            return 0;
        }

        // 10초마다 노선 구성과 요새 재고를 찍어 봇과 밸런스 문제를 추적함
        // 시트 값을 넣은 뒤 --set 값을 덮어씀. 실패하면 이유를 출력하고 null
        static Balance BuildBalance(string sheet, List<string> sets)
        {
            var balance = new Balance();
            if (sheet == null) Console.WriteLine("sheet: none (code defaults)");
            else
            {
                if (!File.Exists(sheet)) { Console.Error.WriteLine("sheet not found: " + sheet); return null; }
                if (!BalanceSheet.TryRead(File.ReadAllBytes(sheet), out var data, out string err)) { Console.Error.WriteLine("sheet error: " + err); return null; }
                var res = BalanceSheet.ApplyTo(balance, data);
                string unknown = res.Unknown.Count > 0 ? ", unknown " + string.Join(", ", res.Unknown) : string.Empty;
                Console.WriteLine("sheet " + sheet + ": " + data.Values.Count + " values, " + (data.Supply?.Count ?? 0) + " supply rows" + unknown);
                foreach (string w in res.Warnings) Console.WriteLine("  warning: " + w);
            }
            foreach (string s in sets)
            {
                string[] kv = s.Split('=');
                if (kv.Length == 2 && float.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && balance.TrySet(kv[0], v)) continue;
                Console.Error.WriteLine("bad --set " + s + " (keys: " + string.Join(", ", Balance.Keys) + ")");
                return null;
            }
            return balance;
        }

        // 현재 폴더부터 위로 올라가며 저장소의 기본 시트를 찾음
        static string FindDefaultSheet()
        {
            for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
            {
                string p = Path.Combine(d.FullName, DefaultSheet);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        static void PrintTrace(Game g)
        {
            string Short(NodeType t) => t switch { NodeType.Farm => "F", NodeType.Forge => "W", NodeType.Stable => "S", NodeType.Warehouse => "H", _ => "T" };
            var sb = new System.Text.StringBuilder();
            sb.Append("  t " + g.T.ToString("0", CultureInfo.InvariantCulture) + " " + g.Phase + " w" + g.Wave + " wall " + (int)g.Wall + " gold " + (int)g.Gold + " |");
            foreach (var l in g.Lines)
                sb.Append(" [" + string.Concat(l.Nodes.Select(id => Short(g.NodeById(id).Type))) + " c" + l.Carts.Count + " " + string.Join(",", l.Carts.Select(c => c.Food + "/" + c.Weapon)) + "]");
            sb.Append(" |");
            foreach (var f in g.Nodes.Where(n => n.Type == NodeType.Fort))
                sb.Append(" T(" + f.Food + "/" + f.Weapon + " m" + f.Morale.ToString("0.0", CultureInfo.InvariantCulture) + " L" + f.AtkLv + f.SpdLv + ")");
            sb.Append(" | prod");
            foreach (var p in g.Nodes.Where(n => n.Type == NodeType.Farm || n.Type == NodeType.Forge))
                sb.Append(" " + Short(p.Type) + p.Stock);
            Console.WriteLine(sb.ToString());
        }

        static void PrintWave(Game g)
        {
            var forts = g.Nodes.Where(n => n.Type == NodeType.Fort).ToList();
            int carts = g.Lines.Sum(l => l.Carts.Count);
            string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
            Console.WriteLine("  wave " + g.Wave.ToString().PadLeft(2)
                + "  wall " + ((int)Math.Ceiling(g.Wall)) + "/" + ((int)g.WallMax)
                + "  forts " + forts.Count
                + "  food " + (forts.Count > 0 ? F(forts.Average(f => f.Food)) : "-")
                + "  weapon " + (forts.Count > 0 ? F(forts.Average(f => f.Weapon)) : "-")
                + "  morale " + (forts.Count > 0 ? forts.Average(f => f.Morale).ToString("0.00", CultureInfo.InvariantCulture) : "-")
                + "  gold " + F(g.Gold)
                + "  lines " + g.Lines.Count
                + "  carts " + carts
                + "  routes " + g.Routes.Count);
        }
    }
}
