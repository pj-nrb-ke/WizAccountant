using System.Text.Json;
using System.Text.RegularExpressions;
using WizAccountant.Contracts;

namespace WizAccountant.Api.Act;

public sealed class ActDraftService
{
    public AiDraftResponse CreateDraft(AiDraftRequest request)
    {
        var m = request.Message.ToLowerInvariant();

        if (m.Contains("journal") || m.Contains("gl"))
        {
            var amount = ExtractAmount(m) ?? 100m;
            var payload = new
            {
                reference = $"WIZ-{DateTime.UtcNow:yyyyMMdd-HHmm}",
                description = request.Message.Length > 80 ? request.Message[..80] : request.Message,
                lines = new[]
                {
                    new { account = "1000", debit = amount, credit = 0m },
                    new { account = "2000", debit = 0m, credit = amount }
                }
            };
            return new AiDraftResponse
            {
                ProposalType = "gl.journal",
                Title = "Proposed GL journal",
                PayloadJson = JsonSerializer.Serialize(payload)
            };
        }

        if (m.Contains("supplier") || m.Contains("payment") && m.Contains("supplier"))
        {
            var amount = ExtractAmount(m) ?? 50m;
            var payload = new
            {
                account = ExtractCode(m) ?? "SUPP001",
                reference = $"AP-{DateTime.UtcNow:yyyyMMdd}",
                description = "Proposed supplier payment",
                amount
            };
            return new AiDraftResponse
            {
                ProposalType = "ap.transaction",
                Title = "Proposed AP payment",
                PayloadJson = JsonSerializer.Serialize(payload)
            };
        }

        if (m.Contains("customer") || m.Contains("invoice") || m.Contains("receipt"))
        {
            var amount = ExtractAmount(m) ?? 75m;
            var payload = new
            {
                account = ExtractCode(m) ?? "CASH",
                reference = $"AR-{DateTime.UtcNow:yyyyMMdd}",
                description = "Proposed AR transaction",
                amount
            };
            return new AiDraftResponse
            {
                ProposalType = "ar.transaction",
                Title = "Proposed AR transaction",
                PayloadJson = JsonSerializer.Serialize(payload)
            };
        }

        if (m.Contains("alloc"))
        {
            var payload = new
            {
                account = ExtractCode(m) ?? "CASH",
                reference = "ALLOC-001",
                entries = new[] { new { amount = ExtractAmount(m) ?? 25m, reference = "INV-001" } }
            };
            return new AiDraftResponse
            {
                ProposalType = "ar.allocation",
                Title = "Proposed AR allocation",
                PayloadJson = JsonSerializer.Serialize(payload)
            };
        }

        return new AiDraftResponse
        {
            ProposalType = "gl.journal",
            Title = "Draft template",
            PayloadJson = """{"reference":"WIZ-DRAFT","description":"Edit before submit","lines":[{"account":"1000","debit":1,"credit":0},{"account":"2000","debit":0,"credit":1}]}""",
            Notice = "Could not infer intent — edit the draft JSON before submitting for approval."
        };
    }

    private static decimal? ExtractAmount(string text)
    {
        var match = Regex.Match(text, @"(\d+(?:\.\d{2})?)");
        return match.Success && decimal.TryParse(match.Groups[1].Value, out var v) ? v : null;
    }

    private static string? ExtractCode(string text)
    {
        var match = Regex.Match(text, @"\b([A-Z0-9]{2,12})\b");
        return match.Success ? match.Groups[1].Value : null;
    }
}
