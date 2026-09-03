namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public class ShardingException : Exception
{
    public ShardingException(string message) : base(message)
    {
    }

    public ShardingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class ShardingInvalidOperationException : ShardingException
{
    public ShardingInvalidOperationException(string message) : base(message)
    {
    }

    public ShardingInvalidOperationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class ShardingNotFoundException : ShardingException
{
    public ShardingNotFoundException(string message) : base(message)
    {
    }

    public ShardingNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class ShardingNotImplementedException : ShardingException
{
    public ShardingNotImplementedException() : base(string.Empty)
    {
    }

    public ShardingNotImplementedException(string message) : base(message)
    {
    }

    public ShardingNotImplementedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class ShardingNotSupportException : ShardingException
{
    public ShardingNotSupportException(string message) : base(message)
    {
    }

    public ShardingNotSupportException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class ShardingNotMatchException : ShardingException
{
    public ShardingNotMatchException(string message) : base(message)
    {
    }

    public ShardingNotMatchException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class ShardingAccessException : ShardingException
{
    public ShardingAccessException(string message) : base(message)
    {
    }

    public ShardingAccessException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
