using System;

namespace BeniceSoft.Core;

public class NoAuthorizationException : Exception
{
    public const string DefaultMessage = "\u672a\u6388\u6743\u6216\u767b\u5f55\u4fe1\u606f\u5df2\u8fc7\u671f";

    public NoAuthorizationException() : base(DefaultMessage)
    {
    }
}
