namespace AzubiheftApi.Models;

public class WeekOverview
{
    public int Number { get; set; }
    public int CalendarWeek { get; set; }
    public int Year { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public ReportState State { get; set; }
}