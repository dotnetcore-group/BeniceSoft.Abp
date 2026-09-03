using Shouldly;
using Xunit;

namespace BeniceSoft.Core.Tests.DeepClone;

public class DeepClonerTests
{
    [Fact]
    public void DeepClone_Null_Should_Return_Default()
    {
        DeepCloner.DeepClone<SimpleNode?>(null).ShouldBeNull();
        DeepCloner.DeepClone(0).ShouldBe(0);
        DeepCloner.DeepClone("hello").ShouldBe("hello");
    }

    [Fact]
    public void DeepClone_Simple_Object_Should_Be_Independent()
    {
        var source = new SimpleNode { Id = 1, Name = "root" };

        var copy = DeepCloner.DeepClone(source);

        copy.ShouldNotBeSameAs(source);
        copy.Id.ShouldBe(1);
        copy.Name.ShouldBe("root");

        copy.Name = "changed";
        source.Name.ShouldBe("root");
    }

    [Fact]
    public void DeepClone_Nested_Graph_Should_Clone_Children()
    {
        var source = new SimpleNode
        {
            Id = 1,
            Name = "parent",
            Child = new SimpleNode { Id = 2, Name = "child" }
        };

        var copy = DeepCloner.DeepClone(source);

        copy.ShouldNotBeSameAs(source);
        copy.Child.ShouldNotBeNull();
        copy.Child.ShouldNotBeSameAs(source.Child);
        copy.Child!.Id.ShouldBe(2);
        copy.Child.Name.ShouldBe("child");

        copy.Child.Name = "mutated";
        source.Child!.Name.ShouldBe("child");
    }

    [Fact]
    public void DeepClone_Circular_Reference_Should_Preserve_Identity()
    {
        var a = new SimpleNode { Id = 1, Name = "a" };
        var b = new SimpleNode { Id = 2, Name = "b", Child = a };
        a.Child = b;

        var copyA = DeepCloner.DeepClone(a);

        copyA.ShouldNotBeSameAs(a);
        copyA.Child.ShouldNotBeNull();
        copyA.Child.ShouldNotBeSameAs(b);
        copyA.Child!.Child.ShouldBeSameAs(copyA);
        copyA.Child.Id.ShouldBe(2);
    }

    [Fact]
    public void DeepClone_Class_Array_Should_Clone_Elements()
    {
        var source = new[]
        {
            new SimpleNode { Id = 1, Name = "n1" },
            new SimpleNode { Id = 2, Name = "n2" }
        };

        var copy = DeepCloner.DeepClone(source);

        copy.ShouldNotBeSameAs(source);
        copy.Length.ShouldBe(2);
        copy[0].ShouldNotBeSameAs(source[0]);
        copy[0].Id.ShouldBe(1);
        copy[1].Name.ShouldBe("n2");

        copy[0].Name = "x";
        source[0].Name.ShouldBe("n1");
    }

    [Fact]
    public void DeepClone_Struct_Array_Should_Copy_Values()
    {
        var source = new[]
        {
            new Point(1, 2),
            new Point(3, 4)
        };

        var copy = DeepCloner.DeepClone(source);

        copy.ShouldNotBeSameAs(source);
        copy.ShouldBe([new Point(1, 2), new Point(3, 4)]);

        copy[0] = new Point(9, 9);
        source[0].ShouldBe(new Point(1, 2));
    }

    [Fact]
    public void DeepClone_TwoDim_Array_Should_Clone()
    {
        var source = new[,]
        {
            { new SimpleNode { Id = 1, Name = "a" }, new SimpleNode { Id = 2, Name = "b" } },
            { new SimpleNode { Id = 3, Name = "c" }, new SimpleNode { Id = 4, Name = "d" } }
        };

        var copy = DeepCloner.DeepClone(source);

        copy.ShouldNotBeSameAs(source);
        copy[0, 0].ShouldNotBeSameAs(source[0, 0]);
        copy[1, 1].Id.ShouldBe(4);
        copy[1, 1].Name = "z";
        source[1, 1].Name.ShouldBe("d");
    }

    [Fact]
    public void DeepClone_List_And_Dictionary_Should_Clone()
    {
        var source = new Container
        {
            Items = [new SimpleNode { Id = 1, Name = "a" }, new SimpleNode { Id = 2, Name = "b" }],
            Map = new Dictionary<string, SimpleNode>
            {
                ["k1"] = new() { Id = 10, Name = "v1" }
            }
        };

        var copy = DeepCloner.DeepClone(source);

        copy.Items.ShouldNotBeSameAs(source.Items);
        copy.Items[0].ShouldNotBeSameAs(source.Items[0]);
        copy.Map.ShouldNotBeSameAs(source.Map);
        copy.Map["k1"].ShouldNotBeSameAs(source.Map["k1"]);
        copy.Map["k1"].Name.ShouldBe("v1");

        copy.Items[0].Name = "changed";
        source.Items[0].Name.ShouldBe("a");
    }

    [Fact]
    public void ShallowClone_Should_Share_Nested_References()
    {
        var source = new SimpleNode
        {
            Id = 1,
            Name = "parent",
            Child = new SimpleNode { Id = 2, Name = "child" }
        };

        var shallow = DeepCloner.ShallowClone(source);

        shallow.ShouldNotBeSameAs(source);
        shallow.Child.ShouldBeSameAs(source.Child);

        var deep = DeepCloner.DeepClone(source);
        deep.Child.ShouldNotBeSameAs(source.Child);
    }

    [Fact]
    public void DeepClone_To_Existing_Instance_Should_Fill_Target()
    {
        var from = new SimpleNode
        {
            Id = 7,
            Name = "from",
            Child = new SimpleNode { Id = 8, Name = "from-child" }
        };
        var to = new SimpleNode { Id = 0, Name = "to" };

        var result = DeepCloner.DeepClone(from, to);

        result.ShouldBeSameAs(to);
        to.Id.ShouldBe(7);
        to.Name.ShouldBe("from");
        to.Child.ShouldNotBeNull();
        to.Child.ShouldNotBeSameAs(from.Child);
        to.Child!.Name.ShouldBe("from-child");
    }

    [Fact]
    public void DeepClone_Struct_With_Reference_Field_Should_Clone_Inner()
    {
        var source = new NodeBox
        {
            Tag = 1,
            Node = new SimpleNode { Id = 5, Name = "boxed" }
        };

        var copy = DeepCloner.DeepClone(source);

        copy.Tag.ShouldBe(1);
        copy.Node.ShouldNotBeSameAs(source.Node);
        copy.Node.Name.ShouldBe("boxed");
        copy.Node.Name = "x";
        source.Node.Name.ShouldBe("boxed");
    }

    private sealed class SimpleNode
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public SimpleNode? Child { get; set; }
    }

    private sealed class Container
    {
        public List<SimpleNode> Items { get; set; } = [];
        public Dictionary<string, SimpleNode> Map { get; set; } = new();
    }

    private readonly struct Point(int x, int y) : IEquatable<Point>
    {
        public int X { get; } = x;
        public int Y { get; } = y;

        public bool Equals(Point other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is Point other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
    }

    private struct NodeBox
    {
        public int Tag;
        public SimpleNode Node;
    }
}
