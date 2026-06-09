using System.Net;
using System.Net.Mail;

namespace WizAccountant.Api;

/// <summary>
/// MC2 — Real SMTP email service. Replaces stub notification log for production alerts.
/// Configure via appsettings:
///   Email:SmtpHost, Email:SmtpPort (587), Email:SmtpUser, Email:SmtpPassword,
///   Email:FromAddress, Email:FromName, Email:Enabled (true/false)
/// </summary>
public sealed class SmtpEmailService(
    IConfiguration config,
    ILogger<SmtpEmailService> logger)
{
    public bool IsEnabled => config.GetValue<bool>("Email:Enabled");

    /// <summary>Send a plain-text + HTML email. Logs a warning if SMTP is not configured.</summary>
    public async Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            logger.LogDebug("[Email] SMTP disabled — skipping send to {To}: {Subject}", toAddress, subject);
            return;
        }

        var host     = config["Email:SmtpHost"] ?? throw new InvalidOperationException("Email:SmtpHost not configured.");
        var port     = config.GetValue<int>("Email:SmtpPort", 587);
        var user     = config["Email:SmtpUser"] ?? "";
        var password = config["Email:SmtpPassword"] ?? "";
        var from     = config["Email:FromAddress"] ?? user;
        var fromName = config["Email:FromName"] ?? "WizAccountant";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = port != 25,
            Credentials = string.IsNullOrWhiteSpace(user)
                ? null
                : new NetworkCredential(user, password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        using var msg = new MailMessage
        {
            From = new MailAddress(from, fromName),
            Subject = subject,
            IsBodyHtml = true,
            Body = htmlBody,
        };
        msg.To.Add(toAddress);

        ct.ThrowIfCancellationRequested();
        await client.SendMailAsync(msg, ct);
        logger.LogInformation("[Email] Sent '{Subject}' → {To}", subject, toAddress);
    }

    // ── Convenience templates ─────────────────────────────────────────────────

    public Task SendApprovalRequiredAsync(string toAddress, string proposalTitle, string actUrl, CancellationToken ct = default)
        => SendAsync(toAddress, $"[WizAccountant] Approval required: {proposalTitle}",
            $"""
            <h2>Approval Required</h2>
            <p>A new posting proposal is waiting for your approval:</p>
            <blockquote><strong>{System.Web.HttpUtility.HtmlEncode(proposalTitle)}</strong></blockquote>
            <p><a href="{actUrl}">Review and approve in WizAccountant Act →</a></p>
            <p style="color:#888;font-size:12px">WizAccountant — Sage 200 Evolution bridge</p>
            """, ct);

    public Task SendJobFailedAsync(string toAddress, Guid jobId, string operation, CancellationToken ct = default)
        => SendAsync(toAddress, "[WizAccountant] Connector job failed",
            $"""
            <h2>Job Failed</h2>
            <p>A connector job did not complete successfully:</p>
            <ul>
              <li><strong>Job ID:</strong> {jobId}</li>
              <li><strong>Operation:</strong> {System.Web.HttpUtility.HtmlEncode(operation)}</li>
            </ul>
            <p>Check the <a href="/audit">Audit log</a> for details.</p>
            """, ct);
}
