namespace AzubiheftApi.Models;

public class Overview
{
    public int InProgress { get; set; }
    public int Missing { get; set; }
    public int AtTrainer { get; set; }
    public int Denied { get; set; }
    public int Accepted { get; set; }
    public int Total { get; set; }
}