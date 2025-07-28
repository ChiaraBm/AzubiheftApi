using AzubiheftApi;

var azubiheftClient = new AzubiheftClient();

var username = Environment.GetEnvironmentVariable("AH_USERNAME")!;
var password = Environment.GetEnvironmentVariable("AH_PASSWORD")!;

await azubiheftClient.Login(username, password);

var data = await azubiheftClient.LoadDay(new DateOnly(2025, 07, 28));

var taskToDelete = data.Tasks.First(x => x.Content.Contains("delete me"));

await azubiheftClient.DeleteTask(101, new DateOnly(2025, 07, 28), taskToDelete);