namespace BeniceSoft.Abp.EventBus.Dtm;

public static class InboxBarrierProperties
{
    public static string Reason { get; set; } = "dtm_inbox";

    public static string TransType { get; set; } = "dtm_inbox";

    public static string BranchId { get; set; } = "00";

    public static string Op { get; set; } = "dtm_inbox";

    public static string BarrierId { get; set; } = "01";
}
