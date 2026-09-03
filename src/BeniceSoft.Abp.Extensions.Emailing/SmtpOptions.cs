namespace BeniceSoft.Abp.Extensions.Emailing;

/// <summary>
/// SMTP 配置
/// </summary>
public class SmtpOptions
{
    /// <summary>
    /// 邮件服务器地址(smtp.qq.com)
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// 端口号(465/587)
    /// </summary>
    public int Port { get; set; } = 465;

    /// <summary>
    /// 邮箱地址(发送邮件的邮箱地址)
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// SMTP 密码或邮箱授权码
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用 SSL 加密连接(默认 true，465 port 默认强制开启)
    /// </summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>
    /// 是否启用默认凭据(如果启用，则使用 UserName 和 Password 进行身份验证 默认 false)
    /// </summary>
    public bool UseDefaultCredentials { get; set; } = false;

    /// <summary>
    /// 默认发件人地址(如果不设置，则使用 UserName 作为发件人地址)
    /// </summary>
    public string DefaultFromAddress { get; set; } = string.Empty;

    /// <summary>
    /// 默认发件人显示名称(如果不设置，则使用 DefaultFromAddress 作为发件人显示名称)
    /// </summary>
    public string DefaultFromDisplayName { get; set; } = string.Empty;
}
