using System.Net.Mail;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using AuthenticationException = MailKit.Security.AuthenticationException;
using IEmailSender = Volo.Abp.Emailing.IEmailSender;
using MailKitSmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace BeniceSoft.Abp.Extensions.Emailing;

public class ConfigurationSmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<ConfigurationSmtpEmailSender> logger) : IEmailSender, ITransientDependency
{
    private readonly SmtpOptions _options = options.Value;

    public Task SendAsync(
        string to,
        string? subject,
        string? body,
        bool isBodyHtml = true,
        AdditionalEmailSendingArgs? additionalEmailSendingArgs = null)
    {
        return SendAsync(_options.DefaultFromAddress, to, subject, body, isBodyHtml, additionalEmailSendingArgs);
    }

    public async Task SendAsync(
        string from,
        string to,
        string? subject,
        string? body,
        bool isBodyHtml = true,
        AdditionalEmailSendingArgs? additionalEmailSendingArgs = null)
    {
        ValidateOptions();

        var fromAddress = string.IsNullOrWhiteSpace(from) ? _options.DefaultFromAddress : from;
        var message = BuildMessage(fromAddress, to, subject, body, isBodyHtml);

        await SendMessageAsync(message);
        logger.LogDebug("Email sent to {To} with subject {Subject}", to, subject);
    }

    public Task QueueAsync(
        string to,
        string? subject,
        string? body,
        bool isBodyHtml = true,
        AdditionalEmailSendingArgs? additionalEmailSendingArgs = null)
    {
        return SendAsync(to, subject, body, isBodyHtml, additionalEmailSendingArgs);
    }

    public Task QueueAsync(
        string from,
        string to,
        string? subject,
        string? body,
        bool isBodyHtml = true,
        AdditionalEmailSendingArgs? additionalEmailSendingArgs = null)
    {
        return SendAsync(from, to, subject, body, isBodyHtml, additionalEmailSendingArgs);
    }

    public async Task SendAsync(MailMessage mail, bool normalize = true)
    {
        ValidateOptions();

        var message = MimeMessage.CreateFromMailMessage(mail);
        await SendMessageAsync(message);
        logger.LogDebug("Email sent to {To} with subject {Subject}", mail.To, mail.Subject);
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new AbpException("未配置 Smtp:Host");
        }

        if (string.IsNullOrWhiteSpace(_options.DefaultFromAddress))
        {
            throw new AbpException("未配置 Smtp:DefaultFromAddress");
        }

        if (!_options.UseDefaultCredentials && string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new AbpException("未配置 Smtp:Password");
        }

        if (!_options.UseDefaultCredentials && string.IsNullOrWhiteSpace(_options.UserName))
        {
            throw new AbpException("未配置 Smtp:UserName");
        }
    }

    private async Task SendMessageAsync(MimeMessage message)
    {
        try
        {
            using var client = await ConnectClientAsync();
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (AuthenticationException ex)
        {
            logger.LogWarning(ex, "SMTP authentication failed for host {Host}", _options.Host);
            throw CreateSmtpLoginException(ex);
        }
        catch (SmtpProtocolException ex) when (IsLoginFailureMessage(ex.Message))
        {
            logger.LogWarning(ex, "SMTP login rejected for host {Host}", _options.Host);
            throw CreateSmtpLoginException(ex);
        }
        catch (SmtpCommandException ex) when (IsLoginFailureMessage(ex.Message))
        {
            logger.LogWarning(ex, "SMTP login rejected for host {Host}", _options.Host);
            throw CreateSmtpLoginException(ex);
        }
    }

    private static bool IsLoginFailureMessage(string message)
    {
        return message.Contains("Login fail", StringComparison.OrdinalIgnoreCase)
               || message.Contains("authentication failed", StringComparison.OrdinalIgnoreCase);
    }

    private static AbpException CreateSmtpLoginException(Exception innerException)
    {
        return new AbpException(
            "SMTP 登录失败。请按以下步骤检查：" +
            "1) 在邮箱网页端中开启 POP3/SMTP 服务；" +
            "2) Smtp:Password 必须填 SMTP 授权码（16 位），不是密码；" +
            "3) Smtp:UserName 必须与生成授权码时使用的邮箱地址完全一致；" +
            "4) 若多次尝试失败，请等待 10–15 分钟后再试",
            innerException);
    }

    private static MimeMessage BuildMessage(
        string from,
        string to,
        string? subject,
        string? body,
        bool isBodyHtml)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject ?? string.Empty;

        var bodyBuilder = new BodyBuilder();
        if (isBodyHtml)
        {
            bodyBuilder.HtmlBody = body;
        }
        else
        {
            bodyBuilder.TextBody = body;
        }

        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }

    private async Task<MailKitSmtpClient> ConnectClientAsync()
    {
        var client = new MailKitSmtpClient();
        client.AuthenticationMechanisms.Remove("XOAUTH2");

        var secureSocketOptions = GetSecureSocketOptions();
        await client.ConnectAsync(_options.Host, _options.Port, secureSocketOptions);

        if (!_options.UseDefaultCredentials)
        {
            var userName = _options.UserName.Trim();
            var password = _options.Password.Trim();
            await client.AuthenticateAsync(userName, password);
        }

        return client;
    }

    private SecureSocketOptions GetSecureSocketOptions()
    {
        if (_options.Port == 465)
        {
            return SecureSocketOptions.SslOnConnect;
        }

        return _options.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
    }
}
