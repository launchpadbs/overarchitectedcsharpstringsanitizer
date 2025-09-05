namespace FlashAssessment.Domain.Common;

public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
}

public sealed class DuplicateException : Exception
{
    public DuplicateException(string message) : base(message) { }
}


