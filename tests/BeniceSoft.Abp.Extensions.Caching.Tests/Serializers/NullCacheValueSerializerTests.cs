using Microsoft.Extensions.Logging;
using Moq;
using BeniceSoft.Abp.Extensions.Caching.Internals;
using Shouldly;
using Xunit;

namespace BeniceSoft.Abp.Extensions.Caching.Tests.Serializers;

/// <summary>
/// NullCacheValueSerializer 测试
/// </summary>
public class NullCacheValueSerializerTests
{
    [Fact]
    public void Name_ShouldBe_Null()
    {
        var logger = new Mock<ILogger<NullCacheValueSerializer>>();
        var serializer = new NullCacheValueSerializer(logger.Object);
        
        serializer.Name.ShouldBe("Null");
    }

    [Fact]
    public void Serialize_ShouldReturn_EmptyArray()
    {
        var logger = new Mock<ILogger<NullCacheValueSerializer>>();
        var serializer = new NullCacheValueSerializer(logger.Object);

        var result = serializer.Serialize("test");

        result.ShouldNotBeNull();
        result.Length.ShouldBe(0);
    }

    [Fact]
    public void Deserialize_Generic_ShouldReturn_Default()
    {
        var logger = new Mock<ILogger<NullCacheValueSerializer>>();
        var serializer = new NullCacheValueSerializer(logger.Object);

        var stringResult = serializer.Deserialize<string>(new byte[] { 1, 2, 3 });
        var intResult = serializer.Deserialize<int>(new byte[] { 1, 2, 3 });

        stringResult.ShouldBeNull();
        intResult.ShouldBe(0);
    }

    [Fact]
    public void Deserialize_WithType_ShouldReturn_Null()
    {
        var logger = new Mock<ILogger<NullCacheValueSerializer>>();
        var serializer = new NullCacheValueSerializer(logger.Object);

        var result = serializer.Deserialize(new byte[] { 1, 2, 3 }, typeof(TestDto));

        result.ShouldBeNull();
    }

    [Fact]
    public void Should_Log_Warning_Only_Once()
    {
        var logger = new Mock<ILogger<NullCacheValueSerializer>>();
        var serializer = new NullCacheValueSerializer(logger.Object);

        // 多次调用
        serializer.Serialize("test1");
        serializer.Serialize("test2");
        serializer.Serialize("test3");
        serializer.Deserialize<string>(new byte[0]);
        serializer.Deserialize(new byte[0], typeof(string));

        // 验证只记录了一次警告
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}

