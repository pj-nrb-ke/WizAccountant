namespace WizAccountant.Api.Insight;

/// <summary>Detect AR payment discipline queries vs outstanding balance ranking (SAGE-TRAIN-008).</summary>
internal static class CustomerPaymentBehaviorHelper
{
    public static bool IsPaymentBehaviorQuery(string messageLower)
    {
        if (string.IsNullOrWhiteSpace(messageLower))
            return false;

        var m = messageLower;
        if (m.Contains("supplier") && !m.Contains("customer"))
            return false;

        if (IsOutstandingRankingQuery(m))
            return false;

        if (m.Contains("paying promptly") || m.Contains("prompt payer") || m.Contains("prompt paying") ||
            m.Contains("good payer") || m.Contains("best paying") || m.Contains("best payer") ||
            m.Contains("pays on time") || m.Contains("pay on time") || m.Contains("pay promptly") ||
            m.Contains("clearing within") || m.Contains("within credit terms") || m.Contains("within terms") ||
            m.Contains("within their terms") || m.Contains("within respective terms") ||
            m.Contains("payment discipline") || m.Contains("collection discipline") ||
            m.Contains("payment behaviour") || m.Contains("payment behavior") ||
            m.Contains("analyze customer payment") ||
            m.Contains("who pays") && (m.Contains("fastest") || m.Contains("prompt") || m.Contains("on time")) ||
            m.Contains("customers who pay early") || m.Contains("customers who pay within"))
            return true;

        if (m.Contains("late payer") || m.Contains("slow payer") || m.Contains("chronic overdue") ||
            m.Contains("pay late") || m.Contains("paying late") || m.Contains("slowest paying") ||
            m.Contains("slow paying customer") || m.Contains("customers who pay late"))
            return true;

        if ((m.Contains("payment") || m.Contains("paying")) && m.Contains("customer") &&
            (m.Contains("behav") || m.Contains("discipline") || m.Contains("prompt") || m.Contains("late")))
            return true;

        return (m.Contains("clearing") || m.Contains("cleared")) &&
               (m.Contains("terms") || m.Contains("credit term")) &&
               m.Contains("customer");
    }

    public static bool IsPromptPayerQuery(string m) =>
        IsPaymentBehaviorQuery(m) &&
        !IsLatePayerQuery(m) &&
        !m.Contains("summary") && !m.Contains("overview") &&
        (m.Contains("prompt") || m.Contains("on time") || m.Contains("good payer") ||
         m.Contains("best pay") || m.Contains("within term") || m.Contains("clearing") ||
         m.Contains("payment discipline") || m.Contains("fastest") || m.Contains("early"));

    public static bool IsLatePayerQuery(string m) =>
        m.Contains("late payer") || m.Contains("slow payer") || m.Contains("chronic overdue") ||
        m.Contains("pay late") || m.Contains("paying late") || m.Contains("slowest paying") ||
        m.Contains("slow paying");

    public static bool IsPaymentDetailQuery(string m) =>
        IsPaymentBehaviorQuery(m) &&
        (m.Contains("payment behaviour for") || m.Contains("payment behavior for") ||
         m.Contains("payment discipline for") || m.Contains("behaviour for customer") ||
         m.Contains("behavior for customer"));

    public static bool IsPaymentSummaryQuery(string m) =>
        (m.Contains("summary") || m.Contains("overview") ||
         (m.Contains("analyze") && (m.Contains("payment") || m.Contains("paying")))) &&
        IsPaymentBehaviorQuery(m);

    private static bool IsOutstandingRankingQuery(string m) =>
        m.Contains("who owes") || m.Contains("highest unpaid") || m.Contains("most outstanding") ||
        m.Contains("largest outstanding") || m.Contains("biggest outstanding") ||
        (m.Contains("highest") && m.Contains("outstanding") && !m.Contains("prompt") && !m.Contains("within term")) ||
        (m.Contains("top") && m.Contains("outstanding") && !m.Contains("payment")) ||
        (m.Contains("unpaid") && m.Contains("balance") && !m.Contains("payment behav") && !m.Contains("paying promptly"));
}
