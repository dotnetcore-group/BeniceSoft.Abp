using BeniceSoft.Abp.Extensions.Caching.Abstractions.Interfaces;
using BeniceSoft.Abp.Extensions.Caching.MessagePack;
using BeniceSoft.Abp.Extensions.Caching.SystemTextJson;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace BeniceSoft.Abp.Extensions.Caching.Tests.Serializers;

/// <summary>
/// 序列化器对比测试
/// </summary>
public class SerializerComparisonTests
{
    private readonly ITestOutputHelper _output;
    private readonly MessagePackCacheValueSerializer _messagePackSerializer = new();
    private readonly SystemTextJsonCacheValueSerializer _jsonSerializer = new();

    public SerializerComparisonTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void MessagePack_ShouldBe_Smaller_Than_Json()
    {
        var data = new TestDto
        {
            Id = 12345,
            Name = "This is a test string with some content",
            CreatedAt = DateTime.UtcNow,
            Tags = new List<string> { "tag1", "tag2", "tag3", "tag4", "tag5" }
        };

        var messagePackBytes = _messagePackSerializer.Serialize(data);
        var jsonBytes = _jsonSerializer.Serialize(data);

        _output.WriteLine($"MessagePack size: {messagePackBytes.Length} bytes");
        _output.WriteLine($"SystemTextJson size: {jsonBytes.Length} bytes");
        _output.WriteLine($"Ratio: {(double)messagePackBytes.Length / jsonBytes.Length:P2}");

        // MessagePack 通常比 JSON 小
        messagePackBytes.Length.ShouldBeLessThan(jsonBytes.Length);
    }

    [Fact]
    public void Both_Serializers_Should_Produce_Same_Result()
    {
        var original = new TestDto
        {
            Id = 999,
            Name = "Comparison Test",
            CreatedAt = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc),
            Tags = new List<string> { "a", "b", "c" }
        };

        // MessagePack 序列化和反序列化
        var mpBytes = _messagePackSerializer.Serialize(original);
        var mpResult = _messagePackSerializer.Deserialize<TestDto>(mpBytes);

        // JSON 序列化和反序列化
        var jsonBytes = _jsonSerializer.Serialize(original);
        var jsonResult = _jsonSerializer.Deserialize<TestDto>(jsonBytes);

        // 两者结果应该相同
        mpResult.ShouldNotBeNull();
        jsonResult.ShouldNotBeNull();

        mpResult.Id.ShouldBe(jsonResult.Id);
        mpResult.Name.ShouldBe(jsonResult.Name);
        mpResult.CreatedAt.ShouldBe(jsonResult.CreatedAt);
        mpResult.Tags.ShouldBe(jsonResult.Tags);
    }

    [Fact]
    public void Serializers_Should_Handle_Large_Data()
    {
        var largeList = Enumerable.Range(1, 10000)
            .Select(i => new TestDto
            {
                Id = i,
                Name = $"Item {i}",
                CreatedAt = DateTime.UtcNow.AddDays(-i),
                Tags = new List<string> { $"tag{i}" }
            })
            .ToList();

        // MessagePack
        var mpBytes = _messagePackSerializer.Serialize(largeList);
        var mpResult = _messagePackSerializer.Deserialize<List<TestDto>>(mpBytes);

        // JSON
        var jsonBytes = _jsonSerializer.Serialize(largeList);
        var jsonResult = _jsonSerializer.Deserialize<List<TestDto>>(jsonBytes);

        _output.WriteLine($"Large data (10000 items):");
        _output.WriteLine($"  MessagePack: {mpBytes.Length:N0} bytes");
        _output.WriteLine($"  SystemTextJson: {jsonBytes.Length:N0} bytes");

        mpResult.ShouldNotBeNull();
        mpResult.Count.ShouldBe(10000);

        jsonResult.ShouldNotBeNull();
        jsonResult.Count.ShouldBe(10000);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void Serializers_Should_Handle_Various_Sizes(int count)
    {
        var data = Enumerable.Range(1, count).ToList();

        var mpBytes = _messagePackSerializer.Serialize(data);
        var jsonBytes = _jsonSerializer.Serialize(data);

        var mpResult = _messagePackSerializer.Deserialize<List<int>>(mpBytes);
        var jsonResult = _jsonSerializer.Deserialize<List<int>>(jsonBytes);

        mpResult.ShouldBe(data);
        jsonResult.ShouldBe(data);

        _output.WriteLine($"Count: {count}, MessagePack: {mpBytes.Length} bytes, JSON: {jsonBytes.Length} bytes");
    }
}

