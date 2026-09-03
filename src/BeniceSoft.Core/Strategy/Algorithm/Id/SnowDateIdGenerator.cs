namespace BeniceSoft.Core.Strategy;

public class SnowDateIdGenerator : IIdGenerator
{
    #region Members
    private readonly long _machineId;
    private readonly byte _machineIdBits = 4; // 解决不同副本间1秒内生成ID的冲突问题
    private readonly byte _sequenceBits;// 解决的是同一个副本内1秒内生成ID的冲突问题
    private readonly long _maxSequence;
    private readonly object _locker = new();

    private readonly int _digit;
    private readonly string _dateFormat;

    private long _sequence;
    private long _lastTimestamp;
    private DateOnly _lastDate;

    #endregion

    #region Constructors

    public SnowDateIdGenerator(byte machineId = 0, byte sequenceBits = 10, int digit = 0, string dateFormat = "yyMMdd")
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sequenceBits, 20);

        _sequenceBits = sequenceBits;
        _maxSequence = SnowIdGenerator.GetMaxOfBits(_sequenceBits);

        if (machineId > 0)
        {
            var maxMachineId = SnowIdGenerator.GetMaxOfBits(machineId);

            ArgumentOutOfRangeException.ThrowIfGreaterThan(machineId, maxMachineId);

            _machineId = machineId;
        }
        else
        {
            _machineId = GenerateMachineId();
        }

        _lastDate = DateTimeOffset.Now.ToDateOnly();
        _digit = digit;
        _dateFormat = dateFormat;
    }
    #endregion

    #region Methods

    /// <summary>
    /// 生成机器码(最大副本数16个)
    /// </summary>
    /// <returns></returns>
    private static byte GenerateMachineId()
    {
        var processId = Environment.ProcessId;
        var machineName = Environment.MachineName;
        var timestamp = DateTime.UtcNow.Ticks;

        var hash = HashCode.Combine(processId, machineName, timestamp);
        return (byte)(Math.Abs(hash) % 16);
    }

    private long GetTimestampNow(DateTimeOffset offset)
    {
        var offsetTicks = (offset - _lastDate.ToDateTimeOffset()).Ticks;
        return offsetTicks / TimeSpan.TicksPerSecond;
    }

    private long GetNextTimestamp(out DateTimeOffset offset)
    {
        offset = DateTimeOffset.Now;
        var date = offset.ToDateOnly();
        if (date > _lastDate)
        {
            _sequence = 0;
            _lastTimestamp = 0;
            _lastDate = date;
        }

        var timestamp = GetTimestampNow(offset);
        if (_lastTimestamp - timestamp > 10)
        {
            throw new InvalidProgramException("exceeding the clock callback tolerance limit");
        }

        while (timestamp < _lastTimestamp)
        {
            Thread.Sleep(1);
            offset = DateTimeOffset.Now;
            timestamp = GetTimestampNow(offset);
        }

        while (timestamp == _lastTimestamp)
        {
            if (_sequence < _maxSequence)
            {
                _sequence++;
                return timestamp;
            }

            Thread.Yield(); // 降低CPU消耗
            offset = DateTimeOffset.Now;
            timestamp = GetTimestampNow(offset);
        }

        _sequence = 0;

        return timestamp;
    }

    /// <summary>
    /// 生成新的ID
    /// </summary>
    /// <returns>ID</returns>
    public long NewSequenceId(out DateTimeOffset offset)
    {
        lock (_locker)
        {
            _lastTimestamp = GetNextTimestamp(out offset);
            var timestampShift = _machineIdBits + _sequenceBits;
            int machineIdShift = _sequenceBits;
            return (_lastTimestamp << timestampShift) | (_machineId << machineIdShift) | _sequence;
        }
    }

    public long NewSequenceId()
    {
        var id = NewSequenceId(out var date);
        var idStr = $"{date.ToString(_dateFormat)}{id.ToString("D" + _digit)}";
        return idStr.ToInt64();
    }
    #endregion
}
