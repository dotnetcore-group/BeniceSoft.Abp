using System.Diagnostics;

namespace BeniceSoft.Core.Strategy;

public class SnowIdGenerator : IIdGenerator
{
    #region Members
    private readonly long _offsetTicks;
    private readonly Stopwatch _stopwatch;

    private readonly long _machineId;
    private readonly byte _machineIdBits = 8;
    private readonly byte _sequenceBits;
    private readonly long _maxSequence;
    private readonly object _locker = new();

    private long _sequence;
    private long _lastTimestamp;
    #endregion

    #region Constructors
    /// <summary>
    /// the constructor of <see cref="SnowIdGenerator"/>.
    /// </summary>
    /// <param name="machineId">当前机器码</param>
    /// <param name="sequenceBits">
    /// 序列号位数（0-20之间）
    /// 注意：
    /// 1. 并发量越大，此值也要越大，例如：10 可以 1 秒内生成 2^10=1024 个 ID。
    /// 2. 每台机器此参数务必相同。
    /// </param>
    public SnowIdGenerator(byte machineId = 0, byte sequenceBits = 10)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sequenceBits, 20);

        _sequenceBits = sequenceBits;
        _maxSequence = GetMaxOfBits(_sequenceBits);

        if (machineId > 0)
        {
            var maxMachineId = GetMaxOfBits(machineId);

            ArgumentOutOfRangeException.ThrowIfGreaterThan(machineId, maxMachineId);

            _machineId = machineId;
        }

        _offsetTicks = DateTimeOffset.UtcNow.Ticks - new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks;
        _stopwatch = Stopwatch.StartNew();
    }
    #endregion

    #region Methods
    /// <summary>
    /// 获取指定长度二进制的最大整型数。例如：5 返回 000..011111。
    /// </summary>
    /// <param name="bits"></param>
    /// <returns></returns>
    internal static long GetMaxOfBits(byte bits)
    {
        return (1L << bits) - 1; // 或 -1 ^ -1 << bits
    }

    /// <summary>
    /// 10000000 = TimeSpan.FromSeconds(1).Ticks
    /// </summary>
    /// <returns></returns>
    private long GetTimestampNow()
    {
        return (_offsetTicks + _stopwatch.Elapsed.Ticks) / TimeSpan.TicksPerSecond;
    }

    private long GetNextTimestamp()
    {
        var timestamp = GetTimestampNow();
        if (_lastTimestamp - timestamp > 10)
        {
            throw new InvalidProgramException("exceeding the clock callback tolerance limit");
        }

        while (timestamp < _lastTimestamp)
        {
            Thread.Sleep(1);
            timestamp = GetTimestampNow();
        }

        while (timestamp == _lastTimestamp)
        {
            if (_sequence < _maxSequence)
            {
                _sequence++;
                return timestamp;
            }

            Thread.Yield(); // 降低CPU消耗
            timestamp = GetTimestampNow();
        }

        _sequence = 0;

        return timestamp;
    }

    /// <summary>
    /// 生成新的ID
    /// </summary>
    /// <returns>ID</returns>
    public long NewSequenceId()
    {
        lock (_locker)
        {
            _lastTimestamp = GetNextTimestamp();

            var timestampShift = _machineIdBits + _sequenceBits;
            int machineIdShift = _sequenceBits;
            return (_lastTimestamp << timestampShift) | (_machineId << machineIdShift) | _sequence;
        }
    }
    #endregion
}
