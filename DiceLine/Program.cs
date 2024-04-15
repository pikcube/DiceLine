using System.Diagnostics.CodeAnalysis;
using System.Text;
// ReSharper disable MemberCanBePrivate.Global

namespace DiceLine;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Missing dice");
            return;
        }
        
        foreach (DieResult result in args.SelectMany(DieRoll.Generate).Select(z => new DieResult(z)))
        {
            ConsoleColor color = Console.ForegroundColor;
            if (result.IsCrit)
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }

            if (result.IsCritFail)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            Console.WriteLine(result.ToString());
            Console.ForegroundColor = color;
        }
    }
}

public class DieResult
{
    private static readonly Random Random = new();

    public List<int[]> Results { get; } = [];

    public List<int[]> Dropped { get; } = [];

    public int Total { get; }

    public List<int> Modifier { get; }

    public bool IsCrit { get; set; } = true;

    public bool IsCritFail { get; set; } = true;

    public override string ToString()
    {
        return $"Result: {Total} {Details()}{Drop()}";

        string Details()
        {
            string[] strings = [
                ..Results.Select(result => $"{(result.Any(z => z < 0) ? "-" : "")}[{string.Join('+', result.Select(Math.Abs))}]"),
                ..Modifier.Count != 0 ? Modifier.Select(z => z.ToString()) : ["0"]
            ];

            return string.Join('+', strings).Replace("+-", "-");
        }

        string Drop()
        {
            int count = Dropped.Count(z => z.Length != 0);
            return count switch
            {
                0 => string.Empty,
                -1 or 1 => $" Dropped ({string.Join(',', Dropped.Single(z => z.Length > 0).Select(Math.Abs))})",
                _ => $" Dropped ({string.Join(", ", Dropped.Select(z => $"[{string.Join(',', z.Select(Math.Abs))}]"))})"
            };
        }
    }

    public DieResult(DieRoll rollInstructions)
    {
        Modifier = [.. rollInstructions.Modifiers];
        foreach (DieRoll.DieSet set in rollInstructions.DieSets)
        {
            int numberOfDice = set.NumberOfDice;
            bool isNeg = false;
            if (numberOfDice < 0)
            {
                numberOfDice = -numberOfDice;
                isNeg = true;
            }

            int[] localRaw = [.. Roll(Enumerable.Repeat(set.DieSize, numberOfDice))];

            IEnumerable<int> Roll(IEnumerable<int> input)
            {
                int x = 150000;
                using IEnumerator<int> enumerator = input.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    --x;
                    if (x < 0)
                    {
                        break;
                    }
                    int current = enumerator.Current;
                    int result;
                    do
                    {
                        do { result = Random.Next(current) + 1; } while (set.Reroll.Any(z => z == result));

                        if (result <= set.Minimum)
                        {
                            yield return set.Minimum;
                        }
                        else
                        {
                            yield return result;
                        }
                    } while (set.IsExploding && current == result);

                }
            }

            int[] localResults;
            int[] localDrop;

            if (set.DropNumber == 0)
            {
                localResults = [..localRaw];
                localDrop = [];
            }
            else
            {
                int[] sortIndexes = [..localRaw
                    .Select((z, i) => new KeyValuePair<int, int>(z, i))
                    .OrderBy(z => z.Key)
                    .Select(z => z.Value)
                ];
                if (set.DropNumber > 0)
                {
                    localDrop = [.. sortIndexes[..set.DropNumber].OrderBy(z => z).Select(z => localRaw[z])];
                    localResults = [.. sortIndexes[set.DropNumber..].OrderBy(z => z).Select(z => localRaw[z])];
                }
                else
                {
                    localDrop = [.. sortIndexes[-set.DropNumber..].OrderBy(z => z).Select(z => localRaw[z])];
                    localResults = [.. sortIndexes[..-set.DropNumber].OrderBy(z => z).Select(z => localRaw[z])];
                }
            }


            if (localResults.Any(z => z > 1))
            {
                IsCritFail = false;
            }

            if (localResults.Any(z => z < set.DieSize))
            {
                IsCrit = false;
            }

            if (isNeg)
            {
                localResults = [.. localResults.Select(z => -z)];
                localDrop = [..localDrop.Select(z => -z)];
            }

            Results.Add(localResults);
            Dropped.Add(localDrop);
        }

        if (IsCrit && IsCritFail)
        {
            IsCrit = false;
            IsCritFail = false;
        }

        Total = Results.SelectMany(z => z).Sum() + Modifier.Sum();

    }

}

public class DieRoll
{
    public static IEnumerable<DieRoll> Generate(string arg)
    {
        arg = arg.ToLower();
        if (arg.Contains('x'))
        {
            string[] splitArg = arg.Split('x', 2);
            int num = int.Parse(splitArg[1]);
            for (int n = 0; n < num; ++n)
            {
                yield return new DieRoll(splitArg[0]);
            }
        }
        else
        {
            yield return new DieRoll(arg);
        }
    }

    public required List<DieSet> DieSets { get; init; }= [];
    public required List<int> Modifiers { get; init; } = [];

    public class DieSet
    {
        public required int NumberOfDice { get; init; }
        public required int DieSize { get; init; }
        public required int DropNumber { get; init; }
        public required bool IsExploding { get; init; }
        public required int[] Reroll { get; init; }
        public required int Minimum { get; init; }
    }

    
    // ReSharper disable once UnusedMember.Global
    public DieRoll()
    {
    }

    [SetsRequiredMembers]
    public DieRoll(string dieString)
    {
        char[] dieChars = dieString.ToCharArray();
        StringBuilder sb = new();
        char currentToken = '+';
        foreach (char c in dieChars)
        {
            if (c is not ('+' or '-'))
            {
                sb.Append(c);
                continue;
            }

            if (sb.Length == 0)
            {
                currentToken = c;
                continue;
            }

            if (sb[^1] == 'd')
            {
                sb.Append(c);
                continue;
            }

            Read(sb.ToString());
            sb.Clear();
            currentToken = c;
        }

        Read(sb.ToString());
        sb.Clear();

        void Read(string instructions)
        {
            try
            {
                instructions = instructions.ToLower();

                if (instructions.StartsWith('d'))
                {
                    instructions = $"1{instructions}";
                }

                bool isExploding = instructions.Contains('e');

                if (isExploding)
                {
                    instructions = instructions.Replace("e", "");
                }

                List<int> reroll = [];
                int min = 0;

                while (instructions.Contains('m'))
                {
                    StringBuilder rerollBuilder = new();
                    for (int n = instructions.IndexOf('m') + 1; n < instructions.Length; ++n)
                    {
                        char c = instructions[n];
                        if (instructions[n] is 'r' or 'd' or 'm')
                        {
                            break;
                        }

                        rerollBuilder.Append(c);
                    }

                    string s = rerollBuilder.ToString();
                    min = int.Parse(s);
                    instructions = instructions.Replace($"m{s}", "");
                }

                while (instructions.Contains('r'))
                {
                    StringBuilder rerollBuilder = new();
                    for (int n = instructions.IndexOf('r') + 1; n < instructions.Length; ++n)
                    {
                        char c = instructions[n];
                        if (instructions[n] is 'r' or 'd')
                        {
                            break;
                        }

                        rerollBuilder.Append(c);
                    }

                    string s = rerollBuilder.ToString();
                    reroll.Add(int.Parse(s));
                    instructions = instructions.Replace($"r{s}", "");
                }


                int[] tokenStrings = [.. instructions.Split('d').Select(int.Parse)];
                switch (tokenStrings.Length)
                {
                    case 1:
                        Modifiers.Add(currentToken == '+' ? tokenStrings[0] : -tokenStrings[0]);
                        return;
                    case 2:
                        DieSets.Add(new DieSet
                        {
                            NumberOfDice = currentToken == '+' ? tokenStrings[0] : -tokenStrings[0],
                            DieSize = tokenStrings[1],
                            DropNumber = 0,
                            IsExploding = isExploding,
                            Reroll = [.. reroll],
                            Minimum = min,
                        });
                        return;
                    case 3:
                        DieSets.Add(new DieSet
                        {
                            NumberOfDice = currentToken == '+' ? tokenStrings[0] : -tokenStrings[0],
                            DieSize = tokenStrings[1],
                            DropNumber = tokenStrings[2],
                            IsExploding = isExploding,
                            Reroll = [.. reroll],
                            Minimum = min,
                        });
                        return;
                    default:
                        throw new FormatException();
                }
            }
            catch (FormatException)
            {
                throw new FormatException($"Instruction [{instructions}] was not formatted correctly");
            }
        }
    }
}