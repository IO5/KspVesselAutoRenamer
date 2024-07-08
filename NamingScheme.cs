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

    public abstract class VariableWidthScheme : NamingScheme
    {
        public short width = 0;

        public VariableWidthScheme Clone()
        {
            return this.MemberwiseClone() as VariableWidthScheme;
        }
    }

    public enum Case { Lower, Upper };

    namespace Scheme
    {
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

            public override string GetNthName(short ord) => String.Format("{0:" + (letterCase == Case.Upper ? 'X' : 'x' ) + width.ToString() + "}", ord);
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

        public class LatinAlphabet : NamingScheme
        {
            public Case letterCase;

            public override bool TryConvertToNumber(string str, out short result)
            {
                result = 0;

                if (str.Length == 1)
                {
                    char c = char.ToUpper(str[0]);
                    if (c >= 'A' && c <= 'Z')
                    {
                        result = (short)(c - 'A' + 1);
                        return true;
                    }
                }

                return false;
            }

            public override string GetNthName(short ord)
            {
                if (ord <= 0 || ord > ('Z' - 'A' + 1))
                    return "";

                char c = letterCase == Case.Upper ? 'A' : 'a';
                c += (char)(ord - 1);

                return c.ToString();
            }
        }

        public class GreekAlphabet : NamingScheme
        {
            public Case letterCase;

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

            public override bool TryConvertToNumber(string str, out short result)
            {
                result = 0;

                if (str.Length != 1)
                    return false;

                char c = str[0];
                var idx = alphabet.FindIndex(l => c == l.Item1 || c == l.Item2);
                if (idx == -1)
                    return false;

                result = (short)(idx + 1);
                return true;
            }

            public override string GetNthName(short ord)
            {
                if (ord <= 0 || ord > alphabet.Count)
                    return "";

                var (upper, lower) = alphabet[ord - 1];
                char c = letterCase == Case.Upper ? upper : lower;
                return c.ToString();
            }
        }

        public class GreekAlphabetAsWords : NamingScheme
        {
            public Case letterCase;

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

            public override bool TryConvertToNumber(string str, out short result)
            {
                result = 0;

                str = str.ToLower();
                var idx = alphabet.FindIndex(l => str == l);
                if (idx == -1)
                    return false;

                result = (short)(idx + 1);
                return true;
            }

            public override string GetNthName(short ord)
            {
                if (ord <= 0 || ord > alphabet.Count)
                    return "";

                var result = alphabet[ord - 1];
                if (letterCase == Case.Upper)
                    result = char.ToUpper(result[0]) + result.Substring(1);

                return result;
            }
        }
    }
}
