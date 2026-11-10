namespace BeniceSoft.Abp.Sample.Domain;

public enum TransactionSampleState
{
    Initial = 0,
    Prepared = 1,
    Confirmed = 2,
    Cancelled = 3,
    Applied = 4,
    Compensated = 5
}

