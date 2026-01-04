using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace VesselAutoRenamer
{
    public abstract class NamingScheme
    {
        public abstract bool TryConvertToNumber(string str, out short result);
        public abstract string GetNthName(short ord);
    }

    public enum Case { Lower, Upper };

    namespace Scheme
    {
        public abstract class VariableWidthScheme : NamingScheme
        {
            public short width = 0;

            public VariableWidthScheme Clone()
            {
                return this.MemberwiseClone() as VariableWidthScheme;
            }
        }

        public abstract class Alphabet : NamingScheme
        {
            public Case letterCase;

            protected abstract short AlphabetLength { get; }

            protected abstract short LetterToNumber(char letter);

            protected abstract char NumberToLetter(short num);

            public override bool TryConvertToNumber(string str, out short result)
            {
                result = 0;

                foreach (char c in str)
                {
                    var idx = LetterToNumber(c);
                    if (idx == -1)
                        return false;

                    result = (short)(result * AlphabetLength + idx + 1);
                }

                return true;
            }

            public override string GetNthName(short ord)
            {
                if (ord <= 0)
                    return "";

                string result = "";

                do
                {
                    var reminder = (short)((ord - 1) % AlphabetLength);
                    result = NumberToLetter((short)(reminder)) + result;
                    ord = (short)((ord - reminder) / AlphabetLength);
                }
                while (ord > 0);

                return result;
            }
        }

        public abstract class WordList : NamingScheme
        {
            public Case letterCase;

            protected abstract List<string> words { get; }

            public override bool TryConvertToNumber(string str, out short result)
            {
                result = 0;

                str = str.ToLower();
                var idx = words.FindIndex(l => str == l);
                if (idx == -1)
                    return false;

                result = (short)(idx + 1);
                return true;
            }

            public override string GetNthName(short ord)
            {
                if (ord <= 0 || ord > words.Count)
                    return "";

                var result = words[ord - 1];
                if (letterCase == Case.Upper)
                    result = char.ToUpper(result[0]) + result.Substring(1);

                return result;
            }
        }

        public class Decimal : VariableWidthScheme
        {
            public override bool TryConvertToNumber(string str, out short result) => Int16.TryParse(str, out result);

            public override string GetNthName(short ord) => String.Format("{0:D" + width.ToString() + "}", ord);
        }

        public class Hex : VariableWidthScheme
        {
            public Case letterCase;

            public override bool TryConvertToNumber(string str, out short result)
            {
                return Int16.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
            }

            public override string GetNthName(short ord) => String.Format("{0:" + (letterCase == Case.Upper ? 'X' : 'x') + width.ToString() + "}", ord);
        }

        public class Binary : NamingScheme
        {
            public bool padded;

            public override bool TryConvertToNumber(string str, out short result)
            {
                try
                {
                    // Convert the binary string to an int using base 2
                    result = Convert.ToInt16(str, 2);
                    return true;
                }
                catch
                {
                    result = 0;
                    return false;
                }
            }

            public override string GetNthName(short ord)
            {
                string result = "0";
                result = Convert.ToString(ord, toBase: 2);
                if (padded)
                {
                    result = result.PadLeft(8, '0');
                }
                return result;
            }
        }

        public class Roman : NamingScheme
        {
            private static Dictionary<char, short> FromRoman = new Dictionary<char, short>()
            {
                {'I', 1},
                {'V', 5},
                {'X', 10},
                {'L', 50},
                {'C', 100},
                {'D', 500},
                {'M', 1000},
            };

            private static List<(short, string)> ToRoman = new List<(short, string)>
            {
                (1000, "M" ),
                (900,  "CM"),
                (500,  "D" ),
                (400,  "CD"),
                (100,  "C" ),
                (90,   "XC"),
                (50 ,  "L" ),
                (40 ,  "XL"),
                (10,   "X" ),
                (9,    "IX"),
                (5,    "V" ),
                (4,    "IV"),
                (1,    "I" ),
            };

            public Case letterCase;

            public override bool TryConvertToNumber(string str, out short result)
            {
                result = 0;
                str = str.ToUpper();

                if (!str.All(c => FromRoman.Keys.Contains(c)))
                    return false;

                for (int i = 0; i < str.Length; ++i)
                {
                    if (i + 1 < str.Length && FromRoman[str[i]] < FromRoman[str[i + 1]])
                    {
                        result -= FromRoman[str[i]];
                    }
                    else
                    {
                        result += FromRoman[str[i]];
                    }
                }
                return true;
            }

            public override string GetNthName(short ord)
            {
                if (ord <= 0 || ord >= 4000)
                    return "";

                var result = new StringBuilder();

                foreach (var (num, str) in ToRoman)
                {
                    while (ord >= num)
                    {
                        result.Append(letterCase == Case.Lower ? str.ToLower() : str);
                        ord -= num;
                    }
                }

                return result.ToString();
            }
        }


        public class LatinAlphabet : Alphabet
        {
            protected override short AlphabetLength { get { return 'Z' - 'A' + 1; } }

            protected override short LetterToNumber(char letter) // zero based
            {
                return (short)(char.ToLower(letter) - 'a');
            }

            protected override char NumberToLetter(short num) // zero based
            {
                char c = letterCase == Case.Upper ? 'A' : 'a';
                c += (char)num;
                return c;
            }
        }

        public class GreekAlphabet : Alphabet
        {
            protected override short AlphabetLength { get { return (short)alphabet.Count; } }

            private static List<(char, char)> alphabet = new List<(char, char)>
            {
                ('Α', 'α'),
                ('Β', 'β'),
                ('Γ', 'γ'),
                ('Δ', 'δ'),
                ('Ε', 'ε'),
                ('Ζ', 'ζ'),
                ('Η', 'η'),
                ('Θ', 'θ'),
                ('Ι', 'ι'),
                ('Κ', 'κ'),
                ('Λ', 'λ'),
                ('Μ', 'μ'),
                ('Ν', 'ν'),
                ('Ξ', 'ξ'),
                ('Ο', 'ο'),
                ('Π', 'π'),
                ('Ρ', 'ρ'),
                ('Σ', 'σ'),
                ('Τ', 'τ'),
                ('Υ', 'υ'),
                ('Φ', 'φ'),
                ('Χ', 'χ'),
                ('Ψ', 'ψ'),
                ('Ω', 'ω'),
            };

            protected override short LetterToNumber(char letter)
            {
                return (short)alphabet.FindIndex(l => letter == l.Item1 || letter == l.Item2);
            }

            protected override char NumberToLetter(short num)
            {
                var (upper, lower) = alphabet[num];
                return letterCase == Case.Upper ? upper : lower;
            }
        }

        public class RussianAlphabet : Alphabet
        {
            protected override short AlphabetLength { get { return (short)alphabet.Count; } }

            private static List<(char, char)> alphabet = new List<(char, char)>
            {
                ('А', 'а'),
                ('Б', 'б'),
                ('В', 'в'),
                ('Г', 'г'),
                ('Д', 'д'),
                ('Е', 'е'),
                ('Ж', 'ж'),
                ('З', 'з'),
                ('И', 'и'),
                ('К', 'к'),
                ('Л', 'л'),
                ('М', 'м'),
                ('Н', 'н'),
                ('О', 'о'),
                ('П', 'п'),
                ('Р', 'р'),
                ('С', 'с'),
                ('Т', 'т'),
                ('У', 'у'),
                ('Ф', 'ф'),
                ('Х', 'х'),
                ('Ц', 'ц'),
                ('Ч', 'ч'),
                ('Ш', 'ш'),
                ('Щ', 'щ'),
                ('Э', 'э'),
                ('Ю', 'ю'),
                ('Я', 'я'),
            };

            protected override short LetterToNumber(char letter)
            {
                return (short)alphabet.FindIndex(l => letter == l.Item1 || letter == l.Item2);
            }

            protected override char NumberToLetter(short num)
            {
                var (upper, lower) = alphabet[num];
                return letterCase == Case.Upper ? upper : lower;
            }
        }

        public class GreekAlphabetAsWords : WordList
        {
            private static List<string> alphabet = new List<string>
            {
                "alpha",
                "beta",
                "gamma",
                "delta",
                "epsilon",
                "zeta",
                "eta",
                "theta",
                "iota",
                "kappa",
                "lambda",
                "mu",
                "nu",
                "xi",
                "omicron",
                "pi",
                "rho",
                "sigma",
                "tau",
                "upsilon",
                "phi",
                "chi",
                "psi",
                "omega",
            };

            protected override List<string> words { get { return alphabet; } }
        }

        public class NatoPhoneticAlphabet : WordList
        {
            private static List<string> alphabet = new List<string>
            {
                "alpha",
                "bravo",
                "charlie",
                "delta",
                "echo",
                "foxtrot",
                "golf",
                "hotel",
                "india",
                "juliett",
                "kilo",
                "lima",
                "mike",
                "november",
                "oscar",
                "papa",
                "quebec",
                "romeo",
                "sierra",
                "tango",
                "uniform",
                "victor",
                "whiskey",
                "xray",
                "yankee",
                "zulu",
            };

            protected override List<string> words { get { return alphabet; } }
        }

        public class LegacyIcaoPhoneticAlphabet : WordList
        {
            private static List<string> alphabet = new List<string>
            {
                "able",
                "baker",
                "charlie",
                "dog",
                "easy",
                "fox",
                "george",
                "how",
                "item",
                "jig",
                "king",
                "love",
                "mike",
                "nan",
                "oboe",
                "peter",
                "queen",
                "roger",
                "sugar",
                "tare",
                "uncle",
                "victor",
                "william",
                "xray",
                "yoke",
                "zebra",
            };

            protected override List<string> words { get { return alphabet; } }
        }
    }
}
