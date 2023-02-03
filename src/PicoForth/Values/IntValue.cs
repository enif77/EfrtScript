/* Copyright (C) Premysl Fara and Contributors */

namespace PicoForth.Values;

using System.Globalization;


internal class IntValue : IValue
{
    public int Int { get; }
    public long Long => (long)Int;
    public string String => Int.ToString(CultureInfo.InvariantCulture);


    public IntValue(int value)
    {
        Int = value;
    }
}