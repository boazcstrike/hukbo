using System;

namespace Sandata.Core.Maps;

/// <summary>
/// The single hard-error type <c>MapTokenizer</c> throws for every
/// malformed `.hkmap` input (design section 12). A malformed line is never
/// skipped and a field is never defaulted — the tokenizer either returns a
/// fully validated record or it throws this. <see cref="LineNumber"/> is
/// the 1-based line number of the original source text; <see cref="Rule"/>
/// is one of the stable identifiers in <see cref="Rules"/>, naming exactly
/// which rule was broken.
/// </summary>
public sealed class MapLoadException : Exception
{
    public MapLoadException(int lineNumber, string rule, string message)
        : base(message)
    {
        LineNumber = lineNumber;
        Rule = rule;
    }

    /// <summary>The 1-based line number of the original source text that failed.</summary>
    public int LineNumber { get; }

    /// <summary>The stable rule identifier that was broken. One of <see cref="Rules"/>.</summary>
    public string Rule { get; }

    /// <summary>
    /// Stable, machine-checkable identifiers for every rejection rule
    /// <c>MapTokenizer</c> enforces. A test asserts against these
    /// constants rather than against exception message text.
    /// </summary>
    public static class Rules
    {
        /// <summary>An integer token began with '-'. The format cannot express a negative number.</summary>
        public const string NegativeSign = "negative-sign";

        /// <summary>An integer token contained '.'. The format has no syntax for a fraction.</summary>
        public const string DecimalPoint = "decimal-point";

        /// <summary>An integer token contained ','. The format has no syntax for a group separator.</summary>
        public const string GroupSeparator = "group-separator";

        /// <summary>An integer token began with a whitespace character other than the single-space separator.</summary>
        public const string LeadingWhitespace = "leading-whitespace";

        /// <summary>An integer token ended with a whitespace character other than the single-space separator.</summary>
        public const string TrailingWhitespace = "trailing-whitespace";

        /// <summary>A token between two separators (or at a line boundary) was empty.</summary>
        public const string EmptyToken = "empty-token";

        /// <summary>The first token of a line did not name one of the nine record kinds.</summary>
        public const string UnknownRecordKind = "unknown-record-kind";

        /// <summary>A record had more or fewer tokens than its kind requires.</summary>
        public const string WrongTokenCount = "wrong-token-count";

        /// <summary>An integer token was not parseable as an integer for a reason other than the rules above.</summary>
        public const string NonIntegerToken = "non-integer-token";

        /// <summary>A field parsed as an integer but its value fell outside the range design section 12 permits.</summary>
        public const string OutOfRangeField = "out-of-range-field";

        /// <summary>Either the first record was not <c>HKMAP</c>, or <c>HKMAP</c> did not appear on line 1.</summary>
        public const string HkmapNotLineOne = "hkmap-not-line-1";

        /// <summary><c>HKMAP</c>'s version field was not 1, the only version this loader accepts.</summary>
        public const string WrongVersion = "wrong-version";

        /// <summary>A record appeared after <c>END</c>, which must be the last record in the file.</summary>
        public const string EndNotLast = "end-not-last";

        /// <summary><c>GRID</c>'s <c>cellWu</c> was not a power of two.</summary>
        public const string GridCellSizeNotPowerOfTwo = "grid-cell-size-not-power-of-two";

        /// <summary><c>GRID</c>'s width or height spans more than 512 cells.</summary>
        public const string GridDimensionOver512 = "grid-dimension-over-512";

        /// <summary><c>NAME</c>'s id did not match <c>[a-z0-9-]{1,32}</c>.</summary>
        public const string InvalidNameId = "invalid-name-id";

        /// <summary>A header record appeared out of the fixed order HKMAP, NAME, GRID, or a header kind repeated in the body.</summary>
        public const string HeaderOutOfOrder = "header-out-of-order";

        /// <summary>The file ended without an <c>END</c> record.</summary>
        public const string MissingEndRecord = "missing-end-record";
    }
}
