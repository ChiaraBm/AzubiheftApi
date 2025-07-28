namespace AzubiheftApi.Models;

public class ReportTask
{
    public ReportTaskType Type { get; set; }
    public string Content { get; set; }
    public int SequenceNumber { get; set; } = -1;
}