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

/// <summary>
/// An object holding a generated die result.
/// </summary>
/// <remarks>
/// Dice are rolled upon constructing the object. Generating multiple object instances from the same DieRoll will yield different results.
/// </remarks>
public class DieResult
{
    private static readonly Random Random = new();

    /// <summary>
    /// A list of arrays containing individual die rolls.
    /// </summary>
    public IReadOnlyCollection<IReadOnlyCollection<int>> Results { get; }

    /// <summary>
    /// A list of arrays containing all die rolls that were ignored.
    /// </summary>
    public IReadOnlyCollection<IReadOnlyCollection<int>> Dropped { get; }

    /// <summary>
    /// The total result.
    /// </summary>
    public int Total { get; }

    /// <summary>
    /// A list of static modifiers that are added to the die roll.
    /// </summary>
    public IReadOnlyCollection<int> Modifier { get; }

    /// <summary>
    /// True if every die is the maximum value, false otherwise.
    /// </summary>
    /// <remarks>
    /// If the maximum die size is equal to 1, then this is always false.
    /// </remarks>
    public bool IsCrit { get; } = true;

    /// <summary>
    /// True if every die is a 1, false otherwise.
    /// </summary>
    /// <remarks>
    /// If the maximum die size is equal to 1, then this is always false.
    /// </remarks>
    public bool IsCritFail { get; } = true;

    /// <summary>
    /// Generates a string summary of the object.
    /// </summary>
    /// <returns></returns>
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
            int count = Dropped.Count(z => z.Count != 0);
            return count switch
            {
                0 => string.Empty,
                -1 or 1 => $" Dropped ({string.Join(',', Dropped.Single(z => z.Count > 0).Select(Math.Abs))})",
                _ => $" Dropped ({string.Join(", ", Dropped.Select(z => $"[{string.Join(',', z.Select(Math.Abs))}]"))})"
            };
        }
    }

    /// <summary>
    /// Constructs a die result, rolling all dice and generating the results. Resulting properties are read-only.
    /// </summary>
    /// <param name="rollInstructions">A set of instructions about how to roll the dice.</param>
    public DieResult(DieRoll rollInstructions)
    {
        Modifier = [.. rollInstructions.Modifiers];
        List<int[]> results = [];
        List<int[]> dropped = [];
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

            results.Add(localResults);
            dropped.Add(localDrop);
        }

        if (IsCrit && IsCritFail)
        {
            IsCrit = false;
            IsCritFail = false;
        }

        Results = [..results];
        Dropped = [..dropped];
        Total = Results.SelectMany(z => z).Sum() + Modifier.Sum();

    }

}

/// <summary>
/// A class containing a roll that may be performed by calling Roll.
/// </summary>
/// <remarks>
/// The dice aren't rolled unless you call the roll method or construct a new die result.
/// </remarks>
public class DieRoll
{
    /// <summary>
    /// Converts a string into a collection of die rolls.
    /// </summary>
    /// <param name="arg">
    /// A single string with no white space containing instructions about how to roll a die. Each instruction is a token separated by a + or -.<br/>
    /// A modifier token is an <c>int</c> and is added or subtracted to the running total.<br/>
    /// A <c>DieSet</c> token is formatted as {NumberOfDice = 1}d{DieSize} to set the properties of DieSet (default values shown)<br/>
    /// Optionally, a token may be suffixed with any of the following to modify the following from their default values<br/>
    /// "d{DropNumber = 0}", "m{Minimum = 0}", "r{Reroll}"<br/>
    /// If "e" is provided, the maximum die will explode and roll again.
    /// </param>
    /// <returns></returns>
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

    /// <summary>
    /// Generate a <c>DieResult</c> with rolled dice.
    /// </summary>
    /// <returns>
    /// Returns a new <c>DieResult</c> with freshly rolled dice.
    /// </returns>
    public DieResult Roll()
    {
        return new DieResult(this);
    }

    /// <summary>
    /// A list of <c>DieSet</c> containing instructions about how to roll the given dice.
    /// </summary>
    public required List<DieSet> DieSets { get; init; }= [];
    /// <summary>
    /// The list of static modifiers to add to the die roll
    /// </summary>
    public required List<int> Modifiers { get; init; } = [];

    /// <summary>
    /// Instructions about how to roll a set of dice. 
    /// </summary>
    public class DieSet
    {
        /// <summary>
        /// The maximum value of a die. Must be greater than 0.
        /// </summary>
        public required int DieSize { get; init; }
        /// <summary>
        /// The number of dice to roll, summing the result.
        /// </summary>
        /// <remarks>
        /// If this value is negative, the resulting dice will be subtracted from the total
        /// </remarks>
        public int NumberOfDice { get; init; } = 1;
        /// <summary>
        /// AFter rolling, set this many dice aside and don't add them to the total.
        /// </summary>
        /// <remarks>
        /// A positive value will remove the lowest rolls. A negative value will remove the highest rolls.
        /// </remarks>
        public int DropNumber { get; init; } = 0;
        /// <summary>
        /// True if rolling the maximum result will result in an additional die being rolled, false otherwise.
        /// </summary>
        public bool IsExploding { get; init; } = false;
        /// <summary>
        /// A list of integer values to disallow as results, forcing a reroll.
        /// </summary>
        /// <remarks>
        /// If this list contains no values, all rolls are kept, otherwise any rolls matching these values will be regenerated.
        /// </remarks>
        public int[] Reroll { get; init; } = [];
        /// <summary>
        /// Any die rolls below this value will be changed to this value.
        /// </summary>
        public int Minimum { get; init; } = 0;
    }

    
    // ReSharper disable once UnusedMember.Global
    /// <summary>
    /// Public contructor, requires initization of fields from object initializer.
    /// </summary>
    public DieRoll()
    {
    }

    [SetsRequiredMembers]
    private DieRoll(string dieString)
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