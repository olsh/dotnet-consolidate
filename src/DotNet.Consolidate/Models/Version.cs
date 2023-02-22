using System;

namespace DotNet.Consolidate.Models;

public class Version : IEquatable<Version>
{
    public Version(string value)
    {
        OriginalValue = value ?? throw new ArgumentNullException(nameof(value));
        NormalizedValue = TrimInsignificantSymbols(value);
    }

    public string OriginalValue { get; }

    public string NormalizedValue { get; }

    public static bool operator ==(Version? left, Version? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Version? left, Version? right)
    {
        return !Equals(left, right);
    }

    public override string ToString()
    {
        return OriginalValue;
    }

    public bool Equals(Version? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return NormalizedValue == other.NormalizedValue;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((Version)obj);
    }

    public override int GetHashCode()
    {
        return NormalizedValue.GetHashCode();
    }

    private static string TrimInsignificantSymbols(string value)
    {
        return value.Trim('0', '.');
    }
}
