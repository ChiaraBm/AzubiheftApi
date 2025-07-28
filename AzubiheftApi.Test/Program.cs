using AzubiheftApi;
using AzubiheftApi.Models;

var azubiheftClient = new AzubiheftClient();

var username = Environment.GetEnvironmentVariable("AH_USERNAME")!;
var password = Environment.GetEnvironmentVariable("AH_PASSWORD")!;

await azubiheftClient.Login(username, password);

var weeks = await azubiheftClient.GetAllWeeks();

foreach (var week in weeks.Where(x => x.Number >= 62 && x.Number <= 99))
{
    await azubiheftClient.SendToTrainer(week.Number);
}