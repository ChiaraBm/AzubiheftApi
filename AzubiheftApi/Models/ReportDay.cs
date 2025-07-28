namespace AzubiheftApi.Models;

public class ReportDay
{
    public DateOnly Date { get; set; }
    public ReportTask[] Tasks { get; set; }
}