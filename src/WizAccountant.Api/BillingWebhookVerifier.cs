using System.Security.Cryptography;
using System.Text;

namespace WizAccountant.Api;

/// <summary>
/// Phase 4 Block 4 (Task #19) — verifies billing provider webhook signatures.
///
/// Stripe: uses HMAC-SHA256 over "timestamp.payload" with the Stripe-Signature header.
/// Paddle: uses HMAC-SHA256 over the raw body with the Paddle-Signature header.
///
/// Configure keys in appsettings:
/// "Billing": {
///   "StripeWebhookSecret": "whsec_...",
///   "PaddleWebhookSecret": "your-paddle-secret"
/// }
/// </summary>
public static class BillingWebhookVerifier
{
    /// <summary>
    /// Verifies a Stripe webhook signature.
    /// Header format: "t=TIMESTAMP,v1=SIG1[,v1=SIG2]"
    /// Returns true when the signature is valid and timestamp is within tolerance.
    /// </summary>
    public static bool VerifyStripe(
        string rawBody,
        string stripeSignatureHeader,
        string webhookSecret,
        int toleranceSeconds = 300)
    {
        if (string.IsNullOrWhiteSpace(rawBody) ||
            string.IsNullOrWhiteSpace(stripeSignatureHeader) ||
            string.IsNullOrWhiteSpace(webhookSecret))
            return false;

        long timestamp = 0;
        var signatures = new List<string>();

        foreach (var part in stripeSignatureHeader.Split(','))
        {
            var eq = part.IndexOf('=');
            if (eq < 0) continue;
            var key = part[..eq].Trim();
            var val = part[(eq + 1)..].Trim();
            if (key == "t") long.TryParse(val, out timestamp);
            if (key == "v1") signatures.Add(val);
        }

        if (timestamp == 0 || signatures.Count == 0)
            return false;

        // Tolerance check
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - timestamp) > toleranceSeconds)
            return false;

        // Compute expected signature: HMAC-SHA256( "timestamp.rawBody", secret )
        var signedPayload = $"{timestamp}.{rawBody}";
        var expectedSig = ComputeHmac256(signedPayload, webhookSecret);

        return signatures.Any(sig =>
            string.Equals(sig, expectedSig, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies a Paddle webhook signature.
    /// Paddle Classic: "ts=TIMESTAMP;h1=SIG" — HMAC-SHA256 over "ts:rawBody".
    /// </summary>
    public static bool VerifyPaddle(
        string rawBody,
        string paddleSignatureHeader,
        string webhookSecret,
        int toleranceSeconds = 300)
    {
        if (string.IsNullOrWhiteSpace(rawBody) ||
            string.IsNullOrWhiteSpace(paddleSignatureHeader) ||
            string.IsNullOrWhiteSpace(webhookSecret))
            return false;

        long timestamp = 0;
        string? providedSig = null;

        foreach (var part in paddleSignatureHeader.Split(';'))
        {
            var eq = part.IndexOf('=');
            if (eq < 0) continue;
            var key = part[..eq].Trim();
            var val = part[(eq + 1)..].Trim();
            if (key == "ts") long.TryParse(val, out timestamp);
            if (key == "h1") providedSig = val;
        }

        if (timestamp == 0 || providedSig is null) return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - timestamp) > toleranceSeconds) return false;

        var signedPayload = $"{timestamp}:{rawBody}";
        var expectedSig = ComputeHmac256(signedPayload, webhookSecret);
        return string.Equals(providedSig, expectedSig, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeHmac256(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
