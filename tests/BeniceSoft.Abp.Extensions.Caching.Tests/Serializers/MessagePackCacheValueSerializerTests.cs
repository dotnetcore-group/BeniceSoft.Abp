using BeniceSoft.Abp.Extensions.Caching.MessagePack;
using Shouldly;
using Xunit;

namespace BeniceSoft.Abp.Extensions.Caching.Tests.Serializers;

/// <summary>
/// MessagePack 序列化器测试
/// </summary>
public class MessagePackCacheValueSerializerTests
{
    private readonly MessagePackCacheValueSerializer _serializer = new();

    [Fact]
    public void Name_ShouldBe_MessagePack()
    {
        _serializer.Name.ShouldBe("MessagePack");
    }

    [Fact]
    public void Serialize_And_Deserialize_String()
    {
        var original = "Hello, World!";
        var bytes = _serializer.Serialize(original);
        var result = _serializer.Deserialize<string>(bytes);
        result.ShouldBe(original);
    }

    [Fact]
    public void Serialize_And_Deserialize_Int()
    {
        var original = 12345;
        var bytes = _serializer.Serialize(original);
        var result = _serializer.Deserialize<int>(bytes);
        result.ShouldBe(original);
    }

    [Fact]
    public void Serialize_And_Deserialize_ComplexObject()
    {
        var original = new TestDto
        {
            Id = 1,
            Name = "Test",
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Tags = new List<string> { "tag1", "tag2" }
        };

        var bytes = _serializer.Serialize(original);
        var result = _serializer.Deserialize<TestDto>(bytes);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(original.Id);
        result.Name.ShouldBe(original.Name);
        result.CreatedAt.ShouldBe(original.CreatedAt);
        result.Tags.ShouldBe(original.Tags);
    }

    [Fact]
    public void Serialize_And_Deserialize_List()
    {
        var original = new List<int> { 1, 2, 3, 4, 5 };
        var bytes = _serializer.Serialize(original);
        var result = _serializer.Deserialize<List<int>>(bytes);
        result.ShouldBe(original);
    }

    [Fact]
    public void Serialize_And_Deserialize_Dictionary()
    {
        var original = new Dictionary<string, int>
        {
            { "one", 1 },
            { "two", 2 },
            { "three", 3 }
        };

        var bytes = _serializer.Serialize(original);
        var result = _serializer.Deserialize<Dictionary<string, int>>(bytes);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result["one"].ShouldBe(1);
        result["two"].ShouldBe(2);
        result["three"].ShouldBe(3);
    }

    [Fact]
    public void Deserialize_WithType_ShouldWork()
    {
        var original = new TestDto { Id = 100, Name = "TypeTest" };
        var bytes = _serializer.Serialize(original);
        var result = _serializer.Deserialize(bytes, typeof(TestDto)) as TestDto;

        result.ShouldNotBeNull();
        result.Id.ShouldBe(100);
        result.Name.ShouldBe("TypeTest");
    }

    [Fact]
    public void Serialize_Null_ShouldWork()
    {
        var bytes = _serializer.Serialize<string?>(null);
        var result = _serializer.Deserialize<string?>(bytes);
        result.ShouldBeNull();
    }

    [Fact]
    public void Serialize_NestedObject_ShouldWork()
    {
        var original = new ParentDto
        {
            Id = 1,
            Child = new TestDto { Id = 2, Name = "Child" }
        };

        var bytes = _serializer.Serialize(original);
        var result = _serializer.Deserialize<ParentDto>(bytes);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(1);
        result.Child.ShouldNotBeNull();
        result.Child.Id.ShouldBe(2);
        result.Child.Name.ShouldBe("Child");
    }

    [Fact]
    public void Serialize_EmptyList_ShouldWork()
    {
        var original = new List<string>();
        var bytes = _serializer.Serialize(original);
        var result = _serializer.Deserialize<List<string>>(bytes);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }
}

