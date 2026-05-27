using WizAccountant.Contracts;

namespace WizAccountant.Api.Act;

public static class WorkflowTemplates
{
    public static IReadOnlyList<WorkflowTemplateDto> All { get; } =
    [
        new()
        {
            Id = "month-end",
            Name = "Month-end checklist",
            Steps =
            [
                "Review TB summary in Insight dashboard",
                "Propose adjusting journals (Preparer)",
                "Approve and post journals (Approver)",
                "Export write audit trail"
            ]
        },
        new()
        {
            Id = "payment-run",
            Name = "Payment run proposal",
            Steps =
            [
                "List supplier open items",
                "AI draft AP payments",
                "Preparer submits proposals",
                "Approver reviews and approves each payment"
            ]
        }
    ];
}
