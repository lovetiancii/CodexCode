namespace Tianci.OA.Domain.Audit;

public sealed class OperationLog
{
    public long Id { get; set; }
    public string? TraceId { get; set; }
    public long? OperatorUserId { get; set; }
    public string? OperatorName { get; set; }
    public string Module { get; set; } = "";
    public string Action { get; set; } = "";
    public string? BusinessType { get; set; }
    public long? BusinessId { get; set; }
    public string? RequestMethod { get; set; }
    public string? RequestPath { get; set; }
    public string? ClientIp { get; set; }
    public byte Result { get; set; }
    public string? BeforeStatus { get; set; }
    public string? AfterStatus { get; set; }
    public string? ChangeSummary { get; set; }
    public string? ErrorCode { get; set; }
    public uint? DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
}
