using System.Net;
using BeniceSoft.Http.FluentClient;
using Shouldly;
using Xunit;

namespace BeniceSoft.Core.Tests.Net.Http;

public class FluentClientTests : IDisposable
{
    private readonly MockHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;
    private readonly IFluentClient _client;

    public FluentClientTests()
    {
        _handler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost") };
        _client = new FluentClient(_httpClient, "http://localhost/api", manageBaseClient: false);
    }

    [Fact]
    public void Constructor_SetsBaseUrl()
    {
        _client.BaseUrl.ShouldBe(new Uri("http://localhost/api"));
    }

    [Fact]
    public void Constructor_SetsBaseClient()
    {
        _client.BaseClient.ShouldBe(_httpClient);
    }

    [Fact]
    public void Filters_IsEmptyByDefault()
    {
        _client.Filters.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_SendsGetRequest()
    {
        _handler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":1}")
        });

        var response = await _client.Get("users").AsResponse();

        response.IsSuccessStatusCode.ShouldBeTrue();
        _handler.LastRequest!.Method.ShouldBe(HttpMethod.Get);
        _handler.LastRequest.RequestUri!.AbsolutePath.ShouldBe("/api/users");
    }

    [Fact]
    public async Task Post_SendsPostRequestWithBody()
    {
        _handler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });

        var response = await _client.Post(new { Name = "test" }, "users").AsResponse();

        response.IsSuccessStatusCode.ShouldBeTrue();
        _handler.LastRequest!.Method.ShouldBe(HttpMethod.Post);
        _handler.LastRequest.Content.ShouldNotBeNull();
    }

    [Fact]
    public async Task Put_SendsPutRequestWithBody()
    {
        _handler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });

        await _client.Put(new { Name = "updated" }, "users/1").AsResponse();

        _handler.LastRequest!.Method.ShouldBe(HttpMethod.Put);
    }

    [Fact]
    public async Task Delete_SendsDeleteRequest()
    {
        _handler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });

        await _client.Delete("users/1").AsResponse();

        _handler.LastRequest!.Method.ShouldBe(HttpMethod.Delete);
    }

    [Fact]
    public async Task WithArgument_AppendsQueryString()
    {
        _handler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });

        await _client.Get("users").WithArgument("page", 1).WithArgument("size", 10).AsResponse();

        var uri = _handler.LastRequest!.RequestUri!;
        uri.Query.ShouldContain("page=1");
        uri.Query.ShouldContain("size=10");
    }

    [Fact]
    public async Task WithArguments_FromObject_AppendsQueryString()
    {
        _handler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });

        await _client.Get("users").WithArguments(new { page = 1, size = 10 }).AsResponse();

        var uri = _handler.LastRequest!.RequestUri!;
        uri.Query.ShouldContain("page=1");
        uri.Query.ShouldContain("size=10");
    }

    [Fact]
    public async Task WithHeader_AddsHeader()
    {
        _handler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });

        await _client.Get("users").WithHeader("X-Custom", "test-value").AsResponse();

        _handler.LastRequest!.Headers.GetValues("X-Custom").ShouldContain("test-value");
    }

    [Fact]
    public async Task AsString_ReturnsResponseBody()
    {
        _handler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("hello world")
        });

        var result = await _client.Get("test").AsString();

        result.ShouldBe("hello world");
    }

    [Fact]
    public async Task As_DeserializesJson()
    {
        _handler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":42,\"name\":\"test\"}", System.Text.Encoding.UTF8, "application/json")
        });

        var result = await _client.Get("test").As<TestModel>();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(42);
        result.Name.ShouldBe("test");
    }

    [Fact]
    public void AddDefault_AppliesDefaultToAllRequests()
    {
        _client.AddDefault(req => req.WithHeader("X-Default", "yes"));

        _handler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });

        var request = _client.Get("test");
        request.Message.Headers.GetValues("X-Default").ShouldContain("yes");
    }

    [Fact]
    public async Task Filter_OnRequest_IsCalled()
    {
        var filterCalled = false;
        _client.Filters.Add(new TestFilter(
            onRequest: _ => filterCalled = true));

        _handler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });

        await _client.Get("test").AsResponse();

        filterCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task Filter_OnResponse_IsCalled()
    {
        var filterCalled = false;
        _client.Filters.Add(new TestFilter(
            onResponse: _ => filterCalled = true));

        _handler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });

        await _client.Get("test").AsResponse();

        filterCalled.ShouldBeTrue();
    }

    [Fact]
    public void SetTimeout_ChangesTimeout()
    {
        _client.SetTimeout(TimeSpan.FromSeconds(30));
        _client.BaseClient.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task WithBody_FormUrlEncoded()
    {
        _handler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });

        await _client.Send(HttpMethod.Post, "test")
            .WithBody(b => b.FormUrlEncoded(new { username = "admin", password = "123" }))
            .AsResponse();

        _handler.LastRequest!.Content.ShouldBeOfType<FormUrlEncodedContent>();
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private sealed class TestModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
