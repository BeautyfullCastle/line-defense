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
        private const float Dt = .05f;
        private const int BotEvery = 5;
        private const float MaxTime = 3600f;
        private const string DefaultSheet = "Assets/Game/Contents/Balance/BalanceSheet.bytes";

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
                    case "--sheet":
                        sheet = args[++i];
                        break;
                    case "--no-sheet":
                        sheet = null;
                        break;
                    case "--runs":
                        runs = int.Parse(args[++i], CultureInfo.InvariantCulture);
                        break;
                    case "--seed":
                        seed = int.Parse(args[++i], CultureInfo.InvariantCulture);
                        break;
                    case "--maxwave":
                        maxWave = int.Parse(args[++i], CultureInfo.InvariantCulture);
                        break;
                    case "--verbose":
                        verbose = true;
                        break;
                    case "--trace":
                        trace = verbose = true;
                        break;
                    case "--set":
                        sets.Add(args[++i]);
                        break;
                    default:
                        Console.Error.WriteLine("unknown argument " + args[i]);
                        return 1;
                }
            }

            var balance = BuildBalance(sheet, sets);
            if (balance == null)
            {
                return 1;
            }

            var reached = new List<int>();
            for (int runIndex = 0; runIndex < runs; runIndex++)
            {
                var game = new Game(balance.Clone(), seed + runIndex) { EmitEvents = false };
                var bot = new Bot(game);
                int lastLogged = 0;
                if (verbose)
                {
                    Console.WriteLine("--- run " + (runIndex + 1) + " (seed " + (seed + runIndex) + ")");
                }

                for (int step = 0; !game.Over && game.Wave <= maxWave && game.ElapsedTime <= MaxTime; step++)
                {
                    game.Step(Dt);
                    if (verbose && game.Phase == Phase.Reward && game.Wave != lastLogged)
                    {
                        lastLogged = game.Wave;
                        PrintWave(game);
                    }
                    if (step % BotEvery == 0)
                    {
                        bot.Tick();
                    }

                    if (trace && step % 200 == 0)
                    {
                        PrintTrace(game);
                    }
                }
                int survived = game.Phase == Phase.Countdown || game.Phase == Phase.Reward ? game.Wave : game.Wave - 1;
                reached.Add(survived);
                Console.WriteLine("run " + (runIndex + 1).ToString().PadLeft(2) + ": wave " + survived.ToString().PadLeft(2)
                    + (game.Over ? " (lost)" : " (alive)") + "  kills " + game.Kills + "  delivered " + game.Delivered + "  T " + game.ElapsedTime.ToString("0", CultureInfo.InvariantCulture));
            }

            reached.Sort();
            double mean = reached.Average();
            double median = reached.Count % 2 == 1 ? reached[reached.Count / 2] : (reached[reached.Count / 2 - 1] + reached[reached.Count / 2]) / 2.0;
            Console.WriteLine();
            Console.WriteLine("runs " + runs + "  wave reached: mean " + mean.ToString("0.0", CultureInfo.InvariantCulture)
                + "  median " + median.ToString("0.0", CultureInfo.InvariantCulture) + "  min " + reached[0] + "  max " + reached[reached.Count - 1]);
            foreach (var group in reached.GroupBy(x => x).OrderBy(x => x.Key))
            {
                Console.WriteLine("  wave " + group.Key.ToString().PadLeft(2) + " | " + new string('#', group.Count()) + " " + group.Count());
            }

            return 0;
        }

        // 10초마다 노선 구성과 요새 재고를 찍어 봇과 밸런스 문제를 추적함
        // 시트 값을 넣은 뒤 --set 값을 덮어씀. 실패하면 이유를 출력하고 null
        private static Balance BuildBalance(string sheet, List<string> sets)
        {
            var balance = new Balance();
            if (sheet == null)
            {
                Console.WriteLine("sheet: none (code defaults)");
            }
            else
            {
                if (!File.Exists(sheet))
                {
                    Console.Error.WriteLine("sheet not found: " + sheet);
                    return null;
                }
                if (!BalanceSheet.TryRead(File.ReadAllBytes(sheet), out var data, out string errorMessage))
                {
                    Console.Error.WriteLine("sheet error: " + errorMessage);
                    return null;
                }
                var applyResult = BalanceSheet.ApplyTo(balance, data);
                string unknown = applyResult.Unknown.Count > 0 ? ", unknown " + string.Join(", ", applyResult.Unknown) : string.Empty;
                Console.WriteLine("sheet " + sheet + ": " + data.Values.Count + " values, " + (data.Supply?.Count ?? 0) + " supply rows" + unknown);
                foreach (string warning in applyResult.Warnings)
                {
                    Console.WriteLine("  warning: " + warning);
                }
            }
            foreach (string setting in sets)
            {
                string[] keyAndValue = setting.Split('=');
                if (keyAndValue.Length == 2 && float.TryParse(keyAndValue[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && balance.TrySet(keyAndValue[0], value))
                {
                    continue;
                }

                Console.Error.WriteLine("bad --set " + setting + " (keys: " + string.Join(", ", Balance.Keys) + ")");
                return null;
            }
            return balance;
        }

        // 현재 폴더부터 위로 올라가며 저장소의 기본 시트를 찾음
        private static string FindDefaultSheet()
        {
            for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory != null; directory = directory.Parent)
            {
                string candidatePath = Path.Combine(directory.FullName, DefaultSheet);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }
            return null;
        }

        private static void PrintTrace(Game game)
        {
            string Short(NodeType t) => t switch
            {
                NodeType.Farm => "F",
                NodeType.Forge => "W",
                NodeType.Stable => "S",
                NodeType.Warehouse => "H",
                _ => "T"
            };
            var builder = new System.Text.StringBuilder();
            builder.Append("  t " + game.ElapsedTime.ToString("0", CultureInfo.InvariantCulture) + " " + game.Phase + " w" + game.Wave + " wall " + (int)game.Wall + " gold " + (int)game.Gold + " |");
            foreach (var line in game.Lines)
            {
                builder.Append(" [" + string.Concat(line.Nodes.Select(id => Short(game.NodeById(id).Type))) + " c" + line.Carts.Count + " " + string.Join(",", line.Carts.Select(cart => cart.Food + "/" + cart.Weapon)) + "]");
            }

            builder.Append(" |");
            foreach (var fort in game.Nodes.Where(node => node.Type == NodeType.Fort))
            {
                builder.Append(" T(" + fort.Food + "/" + fort.Weapon + " m" + fort.Morale.ToString("0.0", CultureInfo.InvariantCulture) + " L" + fort.AttackLevel + fort.SpeedLevel + ")");
            }

            builder.Append(" | prod");
            foreach (var productionNode in game.Nodes.Where(node => node.Type == NodeType.Farm || node.Type == NodeType.Forge))
            {
                builder.Append(" " + Short(productionNode.Type) + productionNode.Stock);
            }

            Console.WriteLine(builder.ToString());
        }

        private static void PrintWave(Game game)
        {
            var forts = game.Nodes.Where(node => node.Type == NodeType.Fort).ToList();
            int carts = game.Lines.Sum(line => line.Carts.Count);
            string F(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
            Console.WriteLine("  wave " + game.Wave.ToString().PadLeft(2)
                + "  wall " + ((int)Math.Ceiling(game.Wall)) + "/" + ((int)game.WallMax)
                + "  forts " + forts.Count
                + "  food " + (forts.Count > 0 ? F(forts.Average(fort => fort.Food)) : "-")
                + "  weapon " + (forts.Count > 0 ? F(forts.Average(fort => fort.Weapon)) : "-")
                + "  morale " + (forts.Count > 0 ? forts.Average(fort => fort.Morale).ToString("0.00", CultureInfo.InvariantCulture) : "-")
                + "  gold " + F(game.Gold)
                + "  lines " + game.Lines.Count
                + "  carts " + carts
                + "  routes " + game.Routes.Count);
        }
    }
}
