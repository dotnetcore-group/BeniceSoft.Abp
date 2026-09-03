using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;
using BeniceSoft.Core;

namespace BeniceSoft.Http.FluentClient;

public interface IHttpSerializer
{
    HttpContent Build<T>(T body);

    Task<T?> ReadAsync<T>(HttpContent content, CancellationToken cancellationToken = default);
}

public class JsonHttpSerializer : IHttpSerializer
{
    private readonly JsonSerializerOptions _options;

    public JsonHttpSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? JsonUtils.Options;
    }

    public HttpContent Build<T>(T body)
    {
        return JsonContent.Create(body, options: _options);
    }

    public Task<T?> ReadAsync<T>(HttpContent content, CancellationToken cancellationToken = default)
    {
        return content.ReadFromJsonAsync<T>(_options, cancellationToken);
    }
}

public class XmlHttpSerializer(XmlWriterSettings? writerSettings = null, XmlReaderSettings? readerSettings = null) : IHttpSerializer
{
    public HttpContent Build<T>(T body)
    {
        var xml = body.XmlSerialize(writerSettings);
        return new StringContent(xml, Encoding.UTF8, new MediaTypeHeaderValue(MimeTypes.Application.Xml, "utf-8"));
    }

    public async Task<T?> ReadAsync<T>(HttpContent content, CancellationToken cancellationToken = default)
    {
        var xml = await content.ReadAsStringAsync(cancellationToken);
        return xml.XmlDeserialize<T>(readerSettings);
    }
}