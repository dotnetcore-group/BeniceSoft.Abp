using System.Collections;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace BeniceSoft.Core;

public static class JsonUtils
{
    static JsonUtils()
    {
        DefaultOptions.AddSupportConverter();
        Options = DefaultOptions.Copy();
        Options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        Options.SetDateTimeFormat();
    }

    /// <summary>
    /// 常用序列化配置，使数据易读，可做修改
    /// </summary>
    public static JsonSerializerOptions Options { get; private set; }

    /// <summary>
    /// 高精度序列化配置，保证数据正确性（不建议修改）
    /// </summary>
    public static JsonSerializerOptions DefaultOptions { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,// 首字母小写
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public static string Serialize<T>(T value, JsonSerializerOptions? options = null)
    {
        options ??= Options;
        return JsonSerializer.Serialize(value, options);
    }

    public static string Serialize(object? value, Type type, JsonSerializerOptions? options = null)
    {
        options ??= Options;
        return JsonSerializer.Serialize(value, type, options);
    }

    public static byte[] SerializeBytes<T>(T value, JsonSerializerOptions? options = null)
    {
        var json = Serialize(value, options);
        return Encoding.UTF8.GetBytes(json);
    }

    public static byte[] SerializeBytes(object? value, Type type, JsonSerializerOptions? options = null)
    {
        var json = Serialize(value, type, options);
        return Encoding.UTF8.GetBytes(json);
    }


    public static T? Deserialize<T>(string? json, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        options ??= Options;
        return JsonSerializer.Deserialize<T>(json, options);
    }

    public static object? Deserialize(string? json, Type type, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        options ??= Options;
        return JsonSerializer.Deserialize(json, type, options);
    }

    public static T? DeserializeBytes<T>(byte[]? bytes, JsonSerializerOptions? options = null)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return default;
        }

        var json = Encoding.UTF8.GetString(bytes);
        return Deserialize<T>(json, options);
    }

    public static object? DeserializeBytes(byte[]? bytes, Type type, JsonSerializerOptions? options = null)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return default;
        }

        var json = Encoding.UTF8.GetString(bytes);
        return Deserialize(json, type, options);
    }

    #region Converts
    private sealed class DateTimeConverter(string? format = null, DateTimeStyles styles = DateTimeStyles.None) : JsonConverter<DateTime>
    {
        public string? Format { get; set; } = format;

        public DateTimeStyles Styles { get; set; } = styles;

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (!reader.TryGetDateTime(out var date))
            {
                date = DateTime.ParseExact(reader.GetString()!, Format!, null, Styles);
            }

            return date;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            if ((Styles & DateTimeStyles.AssumeUniversal) == DateTimeStyles.AssumeUniversal || (Styles & DateTimeStyles.AdjustToUniversal) == DateTimeStyles.AdjustToUniversal)
            {
                value = value.ToUniversalTime();
            }

            if (string.IsNullOrEmpty(Format))
            {
                writer.WriteStringValue(value);
            }
            else
            {
                writer.WriteStringValue(value.ToString(Format));
            }
        }
    }

    private sealed class DateTimeOffsetConverter(string? format = null, DateTimeStyles styles = DateTimeStyles.None) : JsonConverter<DateTimeOffset>
    {
        public string? Format { get; set; } = format;

        public DateTimeStyles Styles { get; set; } = styles;

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (!reader.TryGetDateTimeOffset(out var date))
            {
                date = DateTimeOffset.ParseExact(reader.GetString()!, Format!, null, Styles);
            }

            return date;
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            if ((Styles & DateTimeStyles.AssumeUniversal) == DateTimeStyles.AssumeUniversal || (Styles & DateTimeStyles.AdjustToUniversal) == DateTimeStyles.AdjustToUniversal)
            {
                value = value.ToUniversalTime();
            }

            if (string.IsNullOrEmpty(Format))
            {
                writer.WriteStringValue(value);
            }
            else
            {
                writer.WriteStringValue(value.ToString(Format));
            }
        }
    }

    private sealed class DataTableConverter : JsonConverter<DataTable?>
    {
        public override DataTable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            var dt = new DataTable();
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                dt.TableName = reader.GetString();
                if (!reader.Read() || reader.TokenType == JsonTokenType.Null)
                {
                    return dt;
                }
            }

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException($"Unexpected JSON token when reading DataTable. Expected StartArray, got {reader.TokenType}.");
            }

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                CreateRow(ref reader, dt, options);
            }

            return dt;
        }

        private static void CreateRow(ref Utf8JsonReader reader, DataTable dt, JsonSerializerOptions options)
        {
            var dr = dt.NewRow();
            reader.Read();
            dr.BeginEdit();

            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                var columnName = reader.GetString()!;
                reader.Read();

                var column = dt.Columns[columnName];
                if (column == null)
                {
                    var columnType = GetColumnType(ref reader);
                    column = new DataColumn(columnName, columnType);
                    dt.Columns.Add(column);
                }

                if (column.DataType == typeof(DataTable))
                {
                    if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        reader.Read();
                    }

                    var nestedDt = new DataTable();

                    while (reader.TokenType != JsonTokenType.EndArray)
                    {
                        CreateRow(ref reader, nestedDt, options);
                        reader.Read();
                    }

                    dr[columnName] = nestedDt;
                }
                else if (column.DataType.IsArray)
                {
                    if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        reader.Read();
                    }

                    var o = new List<object?>();
                    while (reader.TokenType != JsonTokenType.EndArray)
                    {
                        switch (reader.TokenType)
                        {
                            case JsonTokenType.True:
                                o.Add(true);
                                break;
                            case JsonTokenType.False:
                                o.Add(false);
                                break;
                            case JsonTokenType.Number:
                                o.Add(reader.GetDecimal());
                                break;
                            default:
                                o.Add(reader.GetString());
                                break;
                        }

                        reader.Read();
                    }

                    var elementType = column.DataType.GetElementType()!;
                    var destinationArray = Array.CreateInstance(elementType, o.Count);
                    ((IList)o).CopyTo(destinationArray, 0);
                    dr[columnName] = destinationArray;
                }
                else
                {
                    var columnValue = JsonSerializer.Deserialize(ref reader, column.DataType, options) ?? DBNull.Value;
                    dr[columnName] = columnValue;
                }

                reader.Read();
            }

            dr.EndEdit();
            dt.Rows.Add(dr);
        }

        private static Type GetColumnType(ref Utf8JsonReader reader)
        {
            var tokenType = reader.TokenType;
            switch (tokenType)
            {
                case JsonTokenType.True:
                case JsonTokenType.False:
                    return typeof(bool);
                case JsonTokenType.String:
                case JsonTokenType.Null:
                case JsonTokenType.None:
                case JsonTokenType.EndArray:
                    return typeof(string);
                case JsonTokenType.Number:
                    return typeof(decimal);
                case JsonTokenType.StartArray:
                    {
                        if (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
                        {
                            return typeof(DataTable);
                        }

                        return GetColumnType(ref reader).MakeArrayType();
                    }

                default:
                    throw new JsonException($"Unexpected JSON token when reading DataTable: {tokenType}");
            }
        }

        public override void Write(Utf8JsonWriter writer, DataTable? value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            if (value is not null)
            {
                foreach (DataRow row in value.Rows)
                {
                    writer.WriteStartObject();
                    foreach (DataColumn column in value.Columns)
                    {
                        object? columnValue = row[column];
                        if ((columnValue == null || columnValue == DBNull.Value) && options.DefaultIgnoreCondition != JsonIgnoreCondition.Never)
                        {
                            continue;
                        }

                        if (columnValue == DBNull.Value)
                        {
                            columnValue = null;
                        }

                        var columnName = options.PropertyNamingPolicy?.ConvertName(column.ColumnName) ?? column.ColumnName;

                        writer.WritePropertyName(columnName);
                        JsonSerializer.Serialize(writer, columnValue, column.DataType, options);
                    }

                    writer.WriteEndObject();
                }
            }

            writer.WriteEndArray();
        }
    }

    private sealed class DataSetConverter : JsonConverter<DataSet?>
    {
        public override DataSet? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            var ds = new DataSet();
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var tableName = reader.GetString();
                var dt = JsonSerializer.Deserialize<DataTable>(ref reader, options);
                if (dt != null)
                {
                    dt.TableName = tableName;
                    ds.Tables.Add(dt);
                }
            }

            return ds;
        }

        public override void Write(Utf8JsonWriter writer, DataSet? value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            if (value is not null)
            {
                foreach (DataTable table in value.Tables)
                {
                    writer.WritePropertyName(table.TableName);
                    JsonSerializer.Serialize(writer, table, typeof(DataTable), options);
                }
            }

            writer.WriteEndObject();
        }
    }
    #endregion

    #region Extensions
    public static JsonSerializerOptions Copy(this JsonSerializerOptions aim)
    {
        ArgumentNullException.ThrowIfNull(aim);

        var result = new JsonSerializerOptions(aim);
        return result;
    }

    public static void CopyFrom(this JsonSerializerOptions aim, JsonSerializerOptions? source = null)
    {
        ArgumentNullException.ThrowIfNull(aim);

        source ??= Options;
        aim.AllowTrailingCommas = source.AllowTrailingCommas;
        source.Converters.ForEach(aim.Converters.Add);
        aim.DefaultBufferSize = source.DefaultBufferSize;
        aim.DefaultIgnoreCondition = source.DefaultIgnoreCondition;
        aim.DictionaryKeyPolicy = source.DictionaryKeyPolicy;
        aim.Encoder = source.Encoder;
        //aim.IgnoreNullValues = source.IgnoreNullValues;
        aim.IgnoreReadOnlyFields = source.IgnoreReadOnlyFields;
        aim.IgnoreReadOnlyProperties = source.IgnoreReadOnlyProperties;
        aim.IncludeFields = source.IncludeFields;
        aim.MaxDepth = source.MaxDepth;
        aim.NumberHandling = source.NumberHandling;
        aim.PreferredObjectCreationHandling = source.PreferredObjectCreationHandling;
        aim.PropertyNameCaseInsensitive = source.PropertyNameCaseInsensitive;
        aim.PropertyNamingPolicy = source.PropertyNamingPolicy;
        aim.ReadCommentHandling = source.ReadCommentHandling;
        aim.ReferenceHandler = source.ReferenceHandler;
        aim.TypeInfoResolver = source.TypeInfoResolver;
        //source.TypeInfoResolverChain.ForEach(aim.TypeInfoResolverChain.Add);
        aim.UnknownTypeHandling = source.UnknownTypeHandling;
        aim.UnmappedMemberHandling = source.UnmappedMemberHandling;
        aim.WriteIndented = source.WriteIndented;
    }

    public static JsonSerializerOptions AddConverter<T>(this JsonSerializerOptions aim, Func<T> addConverterFactory, Action<T> updateConverterFactory)
        where T : JsonConverter
    {
        ArgumentNullException.ThrowIfNull(addConverterFactory);

        var converter = aim.Converters.OfType<T>().FirstOrDefault();
        if (converter != null)
        {
            updateConverterFactory?.Invoke(converter);
        }
        else
        {
            aim.Converters.Add(addConverterFactory());
        }

        return aim;
    }

    public static JsonSerializerOptions RemoveDateTimeFormat(this JsonSerializerOptions aim)
    {
        var dateConverters = aim.Converters.OfType<DateTimeConverter>();
        dateConverters.ForEach(t => aim.Converters.Remove(t));
        var offsetConverters = aim.Converters.OfType<DateTimeOffsetConverter>();
        offsetConverters.ForEach(t => aim.Converters.Remove(t));
        return aim;
    }

    public static JsonSerializerOptions SetDateTimeFormat(this JsonSerializerOptions aim, string dateTimeFormat = "yyyy-MM-dd HH:mm:ss", DateTimeStyles styles = DateTimeStyles.None)
    {
        aim.AddConverter<DateTimeConverter>(() => new DateTimeConverter(dateTimeFormat, styles), c =>
        {
            c.Format = dateTimeFormat;
            c.Styles = styles;
        });

        aim.AddConverter<DateTimeOffsetConverter>(() => new DateTimeOffsetConverter(dateTimeFormat, styles), c =>
        {
            c.Format = dateTimeFormat;
            c.Styles = styles;
        });
        return aim;
    }

    public static JsonSerializerOptions AddSupportConverter(this JsonSerializerOptions aim)
    {
        aim.Converters.Add(new DataTableConverter());
        aim.Converters.Add(new DataSetConverter());
        return aim;
    }

    public static object? GetObject(this JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array or JsonValueKind.Object => element,
            _ => null,
        };
    }

    public static object? GetObject(this JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.GetObject();
    }
    #endregion

    #region JsonParse
    public static IDictionary<string, string?> ParseConfigurationPath(Stream input)
    {
        return new JsonConfigurationFileParser().ParseStream(input);
    }

    public static IDictionary<string, string?> ParseConfigurationPath(string input)
    {
        return new JsonConfigurationFileParser().ParseString(input);
    }

    private sealed class JsonConfigurationFileParser
    {
        private const string KeyDelimiter = ":";

        private readonly Dictionary<string, string?> _data = new(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<string> _paths = new();

        public IDictionary<string, string?> ParseStream(Stream input)
        {
            using var reader = new StreamReader(input);
            return ParseString(reader.ReadToEnd());
        }

        public IDictionary<string, string?> ParseString(string input)
        {
            var options = new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };

            using var doc = JsonDocument.Parse(input, options);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("invalid json format");
            }

            VisitObjectElement(doc.RootElement);

            return _data;
        }

        private void VisitObjectElement(JsonElement element)
        {
            var isEmpty = true;

            foreach (var property in element.EnumerateObject())
            {
                isEmpty = false;
                EnterContext(property.Name);
                VisitValue(property.Value);
                ExitContext();
            }

            SetNullIfElementIsEmpty(isEmpty);
        }

        private void VisitArrayElement(JsonElement element)
        {
            var index = 0;

            foreach (var arrayElement in element.EnumerateArray())
            {
                EnterContext(index.ToString());
                VisitValue(arrayElement);
                ExitContext();
                index++;
            }

            SetNullIfElementIsEmpty(isEmpty: index == 0);
        }

        private void SetNullIfElementIsEmpty(bool isEmpty)
        {
            if (isEmpty && _paths.Count > 0)
            {
                _data[_paths.Peek()] = null;
            }
        }

        private void VisitValue(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    VisitObjectElement(value);
                    break;

                case JsonValueKind.Array:
                    VisitArrayElement(value);
                    break;

                case JsonValueKind.Number:
                case JsonValueKind.String:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    {
                        var key = _paths.Peek();
                        if (_data.ContainsKey(key))
                        {
                            throw new FormatException($"a duplicate key '{key}' was found.");
                        }

                        _data[key] = value.ToString();
                        break;
                    }

                default:
                    throw new FormatException($"unsupported JSON token '{value.ValueKind}'");
            }
        }

        private void EnterContext(string context)
        {
            _paths.Push(_paths.Count > 0 ? _paths.Peek() + KeyDelimiter + context : context);
        }

        private void ExitContext()
        {
            _paths.Pop();
        }
    }
    #endregion
}
