using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Arkh.Util
{
    /// <summary>
    /// A small JSON reader and writer, sufficient for talking to chat-completion APIs.
    ///
    /// Hand-rolled on purpose. RimWorld ships no Newtonsoft, and the BCL alternative
    /// (DataContractJsonSerializer) wants attributed types, which fits badly when every provider
    /// returns a differently-shaped envelope. More importantly, this mod has already been bitten
    /// once by a BCL method that existed at compile time and not at runtime — keeping the surface
    /// we depend on small is worth two hundred lines.
    ///
    /// It is also fully exercisable by the headless harness, which matters more here than
    /// elsewhere: a parser bug would otherwise surface as "the model said nothing".
    /// </summary>
    public static class Json
    {
        // --- Writing --------------------------------------------------------------------------

        /// <summary>
        /// Builds a JSON object by appending members in order. Deliberately minimal: request
        /// bodies are small, flat and known, so a fluent writer beats a serializer.
        /// </summary>
        public sealed class Writer
        {
            private readonly StringBuilder _sb = new StringBuilder();
            private bool _first = true;

            public Writer() => _sb.Append('{');

            public Writer Str(string key, string value)
            {
                if (value == null) return this;
                Comma();
                AppendString(_sb, key);
                _sb.Append(':');
                AppendString(_sb, value);
                return this;
            }

            public Writer Num(string key, double value)
            {
                Comma();
                AppendString(_sb, key);
                _sb.Append(':');
                _sb.Append(value.ToString("R", CultureInfo.InvariantCulture));
                return this;
            }

            public Writer Bool(string key, bool value)
            {
                Comma();
                AppendString(_sb, key);
                _sb.Append(':').Append(value ? "true" : "false");
                return this;
            }

            /// <summary>Appends an already-formed JSON fragment (array or object) under a key.</summary>
            public Writer Raw(string key, string json)
            {
                Comma();
                AppendString(_sb, key);
                _sb.Append(':').Append(json);
                return this;
            }

            public string Done() => _sb.Append('}').ToString();

            private void Comma()
            {
                if (_first) _first = false;
                else _sb.Append(',');
            }
        }

        /// <summary>Escapes a string and wraps it in quotes.</summary>
        public static string Quote(string value)
        {
            var sb = new StringBuilder();
            AppendString(sb, value);
            return sb.ToString();
        }

        private static void AppendString(StringBuilder sb, string value)
        {
            sb.Append('"');
            if (value != null)
            {
                foreach (char c in value)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        default:
                            // Control characters must be escaped; everything else, including any
                            // non-ASCII, goes through as UTF-8. Escaping those would be legal but
                            // would triple the size of a Chinese prompt for no benefit.
                            if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else sb.Append(c);
                            break;
                    }
                }
            }
            sb.Append('"');
        }

        // --- Reading --------------------------------------------------------------------------

        /// <summary>
        /// A parsed JSON value. Accessors never throw and never return null for a missing path —
        /// they return an absent value that keeps answering, so reading a deeply nested field from
        /// an unexpected envelope gives an empty string rather than a NullReferenceException three
        /// frames away from the cause.
        /// </summary>
        public sealed class Value
        {
            private static readonly Value Absent = new Value();

            private Dictionary<string, Value> _object;
            private List<Value> _array;
            private string _string;
            private double _number;
            private bool _bool;
            private Kind _kind = Kind.Absent;

            private enum Kind { Absent, Null, Object, Array, String, Number, Bool }

            public bool Exists => _kind != Kind.Absent && _kind != Kind.Null;
            public bool IsArray => _kind == Kind.Array;
            public int Count => _array?.Count ?? 0;

            public Value this[string key]
            {
                get
                {
                    if (_object != null && key != null && _object.TryGetValue(key, out var v)) return v;
                    return Absent;
                }
            }

            public Value this[int index]
            {
                get
                {
                    if (_array != null && index >= 0 && index < _array.Count) return _array[index];
                    return Absent;
                }
            }

            public string AsString(string fallback = "")
            {
                switch (_kind)
                {
                    case Kind.String: return _string;
                    case Kind.Number: return _number.ToString("R", CultureInfo.InvariantCulture);
                    case Kind.Bool: return _bool ? "true" : "false";
                    default: return fallback;
                }
            }

            public int AsInt(int fallback = 0)
            {
                if (_kind == Kind.Number) return (int)_number;
                if (_kind == Kind.String && double.TryParse(_string, NumberStyles.Any, CultureInfo.InvariantCulture, out double d)) return (int)d;
                return fallback;
            }

            public bool AsBool(bool fallback = false) => _kind == Kind.Bool ? _bool : fallback;

            internal static Value Obj(Dictionary<string, Value> members) => new Value { _kind = Kind.Object, _object = members };
            internal static Value Arr(List<Value> items) => new Value { _kind = Kind.Array, _array = items };
            internal static Value Text(string s) => new Value { _kind = Kind.String, _string = s };
            internal static Value Number(double d) => new Value { _kind = Kind.Number, _number = d };
            internal static Value Boolean(bool b) => new Value { _kind = Kind.Bool, _bool = b };
            internal static Value Nothing() => new Value { _kind = Kind.Null };
        }

        /// <summary>
        /// Parses JSON. Returns an absent value rather than throwing on malformed input — a
        /// provider returning an HTML error page is an ordinary Tuesday, not an exceptional event.
        /// </summary>
        public static Value Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return Value.Nothing();
            try
            {
                int i = 0;
                var v = ParseValue(text, ref i);
                return v;
            }
            catch
            {
                return Value.Nothing();
            }
        }

        private static Value ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end");

            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return Value.Text(ParseString(s, ref i));
            if (Match(s, ref i, "true")) return Value.Boolean(true);
            if (Match(s, ref i, "false")) return Value.Boolean(false);
            if (Match(s, ref i, "null")) return Value.Nothing();
            return Value.Number(ParseNumber(s, ref i));
        }

        private static Value ParseObject(string s, ref int i)
        {
            var members = new Dictionary<string, Value>();
            i++; // {
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return Value.Obj(members); }

            while (true)
            {
                SkipWhitespace(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                Expect(s, ref i, ':');
                members[key] = ParseValue(s, ref i);
                SkipWhitespace(s, ref i);

                if (i >= s.Length) throw new FormatException("unterminated object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return Value.Obj(members); }
                throw new FormatException("expected , or }");
            }
        }

        private static Value ParseArray(string s, ref int i)
        {
            var items = new List<Value>();
            i++; // [
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return Value.Arr(items); }

            while (true)
            {
                items.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);

                if (i >= s.Length) throw new FormatException("unterminated array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return Value.Arr(items); }
                throw new FormatException("expected , or ]");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            Expect(s, ref i, '"');
            var sb = new StringBuilder();

            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();

                if (c != '\\') { sb.Append(c); continue; }

                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("truncated \\u");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw new FormatException("bad escape");
                }
            }

            throw new FormatException("unterminated string");
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && ("+-.eE".IndexOf(s[i]) >= 0 || (s[i] >= '0' && s[i] <= '9'))) i++;
            if (i == start) throw new FormatException("expected a value");
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        private static void Expect(string s, ref int i, char c)
        {
            if (i >= s.Length || s[i] != c) throw new FormatException("expected " + c);
            i++;
        }

        private static bool Match(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length) return false;
            if (string.CompareOrdinal(s, i, literal, 0, literal.Length) != 0) return false;
            i += literal.Length;
            return true;
        }
    }
}
