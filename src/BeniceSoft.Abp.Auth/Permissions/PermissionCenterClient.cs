using System.Net.Http.Json;
using BeniceSoft.Abp.Auth.Core.Models;
using BeniceSoft.Abp.Core;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Auth.Permissions;

public class PermissionCenterClient : IPermissionCenterClient, ITransientDependency
{
    public const string PermissionCenterHttpClientName = "Wecharmer.PermissionCenter";

    private readonly BeniceSoftAuthOptions _authOptions;
    private readonly IHttpClientFactory _httpClientFactory;

    public PermissionCenterClient(BeniceSoftAuthOptions options, IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _authOptions = options;
    }

    public async Task<List<RowPermission>?> GetUserRowPermissions(long userId, string accessToken)
    {
        using var httpClient = CreateHttpClient();
        if (!string.IsNullOrWhiteSpace(accessToken) && !httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            httpClient.DefaultRequestHeaders.Add("Authorization", accessToken);
        }

        httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");

        httpClient.Timeout = TimeSpan.FromSeconds(30);
        var url = BuildRequestUri($"api/permission/permissions/row-permission-by-user-id/{userId}");
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"获取行权限错误 {response.StatusCode}:{response.ReasonPhrase}");
        }

        var result = await response.Content.ReadFromJsonAsync<GetRowDataAuthResponse>();
        if (result?.Code != 200)
        {
            throw new HttpRequestException($"获取行数据权限接口异常 {result?.Code}:{result?.Message}");
        }

        return result.Data.Select(c => new RowPermission()
        {
            TableName = c.TableName,
            ConditionGroups = c.ConditionGroups.Select(d => new RowPermissionConditionGroup()
            {
                LogicalOperator = d.GroupOperator,
                Conditions = d.Conditions.Select(x => new RowPermissionCondition()
                {
                    IsDataSuperAdmin = x.IsDataSuperAdmin,
                    ColumnName = x.ColumnName,
                    Operator = x.Operator.ToString(),
                    Values = x.Values,
                    LogicalOperator = x.LogicalOperator,
                }).ToList()
            }).ToList()
        }).ToList();
    }

    public async Task<List<FieldPermission>?> GetUserFieldPermissions(long userId, string accessToken)
    {
        using var httpClient = CreateHttpClient();
        if (!httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            httpClient.DefaultRequestHeaders.Add("Authorization", accessToken);
        }

        httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");

        httpClient.Timeout = TimeSpan.FromSeconds(30);
        var url = BuildRequestUri($"api/permission/permissions/field-permission-by-user-id/{userId}");
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"获取字段数据权限接口异常,错误码{response.StatusCode}:{response.ReasonPhrase}");
        }

        var result = await response.Content.ReadFromJsonAsync<GetFieldDataAuthResponse>();
        if (result?.Code != 200)
        {
            throw new HttpRequestException($"字段权限数据Json转换失败");
        }

        return [.. result.Data.Select(c => new FieldPermission()
        {
            TableName = c.TableName,
            FieldName = c.FieldName,
            FieldAuthLevel = c.FieldAuthLevel,
            IsDisplay = c.IsDisplay,
        })];
    }

    public async Task<List<string>?> GetUserFunctionPermissions(long userId, string accessToken)
    {
        using var httpClient = CreateHttpClient();
        if (!string.IsNullOrWhiteSpace(accessToken) && !httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            httpClient.DefaultRequestHeaders.Add("Authorization", accessToken);
        }

        httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var url = BuildRequestUri($"api/permission/permissions/function-permission-codes-by-user-id/{userId}");
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"获取方法权限错误 {response.StatusCode}:{response.ReasonPhrase}");
        }

        var result = await response.Content.ReadFromJsonAsync<GetFunctionPermissionResponse>();
        if (result?.Code != 200)
        {
            throw new HttpRequestException($"获取方法权限接口异常 {result?.Code}:{result?.Message}");
        }

        return result.Data ?? [];
    }

    private HttpClient CreateHttpClient()
    {
        return _httpClientFactory.CreateClient(PermissionCenterHttpClientName);
    }

    private string BuildRequestUri(string uri)
    {
        Check.NotNullOrWhiteSpace(_authOptions.PermissionCenterUrl, nameof(_authOptions.PermissionCenterUrl));

        return _authOptions.PermissionCenterUrl.EnsureEndsWith('/') + uri;
    }

    class GetRowDataAuthResponse : BaseResponse
    {
        public List<AmRoleAuthRowDataDto> Data { get; set; } = [];
    }

    class AmRoleAuthRowDataDto
    {
        /// <summary>
        /// 角色Id
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 数据菜单Id
        /// </summary>
        public string DataMenuId { get; set; } = string.Empty;

        /// <summary>
        /// 表名
        /// </summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// 条件组
        /// </summary>
        public List<DataConditionGroup> ConditionGroups { get; set; } = [];
    }

    class DataConditionGroup
    {
        /// <summary>
        /// 组与组之间的逻辑操作符
        /// and or
        /// </summary>
        public string GroupOperator { get; set; } = string.Empty;

        /// <summary>
        /// 条件组
        /// </summary>
        public List<DataCondition> Conditions { get; set; } = [];
    }

    class DataCondition
    {
        public bool IsDataSuperAdmin { get; set; }

        /// <summary>
        /// 条件与条件之间的逻辑操作符
        /// and or
        /// </summary>
        public string LogicalOperator { get; set; } = string.Empty;

        /// <summary>
        /// 列名
        /// </summary>
        public string ColumnName { get; set; } = string.Empty;

        /// <summary>
        /// 列名与值之间的操作符
        /// </summary>
        public int Operator { get; set; }

        /// <summary>
        /// 值
        /// </summary>
        public List<string> Values { get; set; } = [];
    }

    class GetFieldDataAuthResponse : BaseResponse
    {
        public List<AmRoleAuthFieldDataDto> Data { get; set; } = [];
    }

    class GetFunctionPermissionResponse : BaseResponse
    {
        public List<string> Data { get; set; } = [];
    }

    class AmRoleAuthFieldDataDto
    {
        /// <summary>
        /// 表名
        /// </summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// 字段名
        /// </summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// 字段权限等级
        /// 1：无权限
        /// 2：只读
        /// 4：读写
        /// </summary>
        public int FieldAuthLevel { get; set; }

        /// <summary>
        /// 当前字段是否显示
        /// </summary>
        public bool IsDisplay { get; set; }
    }
}