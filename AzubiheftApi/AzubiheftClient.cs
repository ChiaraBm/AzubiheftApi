using System.Globalization;
using AngleSharp;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using AzubiheftApi.Models;
using MoonCore.Helpers;

namespace AzubiheftApi;

public class AzubiheftClient : IDisposable
{
    private readonly HttpClient HttpClient;

    public AzubiheftClient(HttpClient? httpClient = null)
    {
        if (httpClient == null)
        {
            HttpClient = new HttpClient(new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                UseCookies = true,
                CookieContainer = new()
            });

            HttpClient.BaseAddress = new Uri("https://www.azubiheft.de/");

            HttpClient.DefaultRequestHeaders.Add("priority", "u=0, i");
            HttpClient.DefaultRequestHeaders.Add("referer", "https://www.azubiheft.de/");
            HttpClient.DefaultRequestHeaders.Add("sec-ch-ua",
                "\"Not)A;Brand\";v=\"8\", \"Chromium\";v=\"138\", \"Brave\";v=\"138\"");
            HttpClient.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
            HttpClient.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Linux\"");
            HttpClient.DefaultRequestHeaders.Add("sec-fetch-dest", "document");
            HttpClient.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
            HttpClient.DefaultRequestHeaders.Add("sec-fetch-site", "same-origin");
            HttpClient.DefaultRequestHeaders.Add("sec-fetch-user", "?1");
            HttpClient.DefaultRequestHeaders.Add("sec-gpc", "1");
            HttpClient.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");
            HttpClient.DefaultRequestHeaders.Add("user-agent",
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36");
        }
        else
            HttpClient = httpClient;
    }

    public async Task Login(string username, string password)
    {
        // Fetch login page to obtain form parameters
        var loginPage = await FetchPage("Login.aspx");

        // View and validation parameters
        var viewStateGenerator = loginPage.GetElementById("__VIEWSTATEGENERATOR")?.GetAttribute("value") ?? "";
        var eventValidation = loginPage.GetElementById("__EVENTVALIDATION")?.GetAttribute("value") ?? "";
        var eventTarget = loginPage.GetElementById("__EVENT_TARGET")?.GetAttribute("value") ?? "";
        var eventArgument = loginPage.GetElementById("__EVENTARGUMENT")?.GetAttribute("value") ?? "";
        var viewState = loginPage.GetElementById("__VIEWSTATE")?.GetAttribute("value") ?? "";

        // Build the login form
        var formValues = new Dictionary<string, string>();

        // View and validation parameters
        formValues["__VIEWSTATEGENERATOR"] = viewStateGenerator;
        formValues["__EVENTTARGET"] = eventTarget;
        formValues["__EVENTARGUMENT"] = eventArgument;
        formValues["__VIEWSTATE"] = viewState;
        formValues["__EVENTVALIDATION"] = eventValidation;

        // Static values
        formValues["ctl00$ContentPlaceHolder1$chk_Persistent"] = "on";
        formValues["ctl00$ContentPlaceHolder1$cmd_Login"] = "Anmelden";
        formValues["ctl00$ContentPlaceHolder1$HiddenField_isMobile"] = "false";

        // Insert user data into form
        formValues["ctl00$ContentPlaceHolder1$txt_Benutzername"] = username;
        formValues["ctl00$ContentPlaceHolder1$txt_Passwort"] = password;

        var formContent = new FormUrlEncodedContent(formValues);

        // Send form to azubiheft
        await HttpClient.PostAsync("https://www.azubiheft.de/Login.aspx", formContent);
    }

    public async Task<Overview> GetOverview()
    {
        var document = await FetchPage("https://www.azubiheft.de/Azubi/Default.aspx");

        var response = new Overview();

        response.InProgress = int.Parse(document.GetElementById("s1Wert")?.TextContent ?? "0");
        response.Missing = int.Parse(document.GetElementById("s5Wert")?.TextContent ?? "0");
        response.AtTrainer = int.Parse(document.GetElementById("s3Wert")?.TextContent ?? "0");
        response.Denied = int.Parse(document.GetElementById("s4Wert")?.TextContent ?? "0");
        response.Accepted = int.Parse(document.GetElementById("s6Wert")?.TextContent ?? "0");
        response.Total = int.Parse(document.GetElementById("s7Wert")?.TextContent ?? "0");

        return response;
    }

    public async Task<WeekOverview[]> GetAllWeeks()
    {
        var responses = new List<WeekOverview>();

        var document = await FetchPage("https://www.azubiheft.de/Azubi/Ausbildungsnachweise.aspx");

        var tabElement = document.GetElementById("Tab1")!;

        foreach (var child in tabElement.Children)
        {
            if (!child.HasAttribute("onclick"))
                continue;

            var response = new WeekOverview();

            response.CalendarWeek = int.Parse(child.QuerySelector(".sKW")?.TextContent ?? "0");

            response.Number = int.Parse(child.QuerySelector(".hdBlue2")?.TextContent.Split("Nr.")[1].Trim() ?? "0");

            response.Year = int.Parse(child.QuerySelector(".KW")?.LastElementChild?.TextContent ?? "0");

            response.StartDate = DateOnly.ParseExact(
                child.QuerySelector(".dCtD")?.TextContent.Split('-')[0].Trim() ?? "01.01.1900",
                "dd.MM.yyyy",
                CultureInfo.InvariantCulture
            );

            response.EndDate = DateOnly.ParseExact(
                child.QuerySelector(".dCtD")?.LastElementChild?.TextContent.Split('-')[0].Trim() ?? "01.01.1900",
                "dd.MM.yyyy",
                CultureInfo.InvariantCulture
            );

            var state = child.QuerySelector(".StText")?.TextContent ?? "N/A";

            if (state.Contains("Fehlt"))
                response.State = ReportState.Missing;
            else if (state.Contains("Genehmigt"))
                response.State = ReportState.Accepted;
            else if (state.Contains("In Arbeit"))
                response.State = ReportState.InProgress;
            else
                response.State = ReportState.NotDefined;

            responses.Add(response);
        }

        return responses.ToArray();
    }

    public async Task<ReportDay[]> LoadWeek(int number)
    {
        var page = await FetchPage($"https://www.azubiheft.de/Azubi/Wochenansicht.aspx?NachweisNr={number}");

        var result = new List<ReportDay>();

        foreach (var child in page.GetElementById("divTB")!.Children)
        {
            if (!child.HasAttribute("onclick"))
                continue;

            var entry = new ReportDay();
            var tasks = new List<ReportTask>();

            entry.Date = DateOnly.ParseExact(
                child.QuerySelector(".dh103")?.FirstElementChild?.TextContent.Split(",")[1].Trim() ?? "01.01.1900",
                "dd.MM.yyyy", CultureInfo.InvariantCulture);

            foreach (var subChild in child.QuerySelector(".Details103")!.Children)
            {
                var task = new ReportTask();

                if (!subChild.ClassList.Contains("table103"))
                    continue;

                var typeNameRaw = subChild.QuerySelector(".d3")?.TextContent;
                var typeName = typeNameRaw?.Split("Art:")[1].Trim()!;

                task.Type = ParseReportTaskType(typeName);

                var contentElement = subChild.QuerySelector(".d50")!;
                task.Content = contentElement.TextContent;

                foreach (var elementChild in contentElement.Children)
                    task.Content += "\n" + elementChild.TextContent;

                tasks.Add(task);
            }

            entry.Tasks = tasks.ToArray();

            result.Add(entry);
        }

        return result.ToArray();
    }

    public async Task<ReportDay> LoadDay(DateOnly date)
    {
        var dayStr = GetDateString(date);
        var url = $"https://www.azubiheft.de/Azubi/Tagesbericht.aspx?Datum={dayStr}";

        var page = await FetchPage(url);

        var result = new ReportDay();
        result.Date = date;

        var tasks = new List<ReportTask>();

        foreach (var element in page.GetElementById("GridViewTB")!.QuerySelectorAll(".d0.mo")!)
        {
            if (!element.HasAttribute("data-seq"))
                continue;

            var sequenceNumber = int.Parse(element.GetAttribute("data-seq") ?? "-1");
            
            // Pseudo control element has the sequence number -1
            if(sequenceNumber == -1)
                continue;
            
            var task = new ReportTask();
            
            task.SequenceNumber = sequenceNumber;

            var typeName = element.QuerySelector(".row1.d3")?.TextContent ?? "";

            task.Type = ParseReportTaskType(typeName);

            var contentElement = element.QuerySelector(".row7.d5")!;
            task.Content = contentElement.TextContent;

            foreach (var elementChild in contentElement.Children)
                task.Content += "\n" + elementChild.TextContent;

            tasks.Add(task);
        }

        result.Tasks = tasks.ToArray();

        return result;
    }

    public async Task CreateTask(int number, DateOnly date, ReportTaskType type, string content)
    {
        await UpdateInternalTask(number, date, type, content, 0);
    }

    public async Task CreateTask(int number, DateOnly date, ReportTask task)
    {
        await CreateTask(number, date, task.Type, task.Content);
    }

    public async Task UpdateTask(int number, DateOnly date, int sequenceNumber, ReportTaskType type, string content)
    {
        await UpdateInternalTask(number, date, type, content, sequenceNumber);
    }

    public async Task UpdateTask(int number, DateOnly date, ReportTask task)
    {
        if(task.SequenceNumber == -1)
            throw new ArgumentException("The sequence number needs to be set");
        
        await UpdateTask(number, date, task.SequenceNumber, task.Type, task.Content);
    }

    public async Task DeleteTask(int number, DateOnly date, int sequenceNumber, ReportTaskType type, string content)
    {
        await UpdateInternalTask(number, date, type, content, -sequenceNumber);
    }

    public async Task DeleteTask(int number, DateOnly date, ReportTask task)
    {
        await DeleteTask(number, date, task.SequenceNumber, task.Type, task.Content);
    }

    private async Task UpdateInternalTask(
        int number,
        DateOnly date,
        ReportTaskType type,
        string content,
        int sequenceNumber
    )
    {
        var dayStr = GetDateString(date);
        var url = $"https://www.azubiheft.de/Azubi/XMLHttpRequest.ashx?Datum={dayStr}&BrNr={number}&BrSt=1&BrVorh=Yes";

        var multipartFormContent = new MultipartFormDataContent();

        multipartFormContent.Add(new StringContent("0"), "disablePaste");
        multipartFormContent.Add(new StringContent(sequenceNumber.ToString()), "Seq");
        multipartFormContent.Add(new StringContent(((int)type).ToString()), "Art_ID");
        multipartFormContent.Add(new StringContent("0"), "Abt_ID");
        multipartFormContent.Add(new StringContent("00:00"), "Dauer");
        multipartFormContent.Add(new StringContent(content), "Inhalt");
        multipartFormContent.Add(new StringContent("12"), "jsVer");

        await HttpClient.PostAsync(url, multipartFormContent);
    }

    private ReportTaskType ParseReportTaskType(string name)
    {
        if (name.Contains("Betrieb"))
            return ReportTaskType.Work;
        else if (name.Contains("Urlaub"))
            return ReportTaskType.Holiday;
        else if (name.Contains("Feiertag"))
            return ReportTaskType.PublicHoliday;
        else if (name.Contains("Arbeitsunfähig"))
            return ReportTaskType.Sick;
        else if (name.Contains("Schule"))
            return ReportTaskType.School;
        else
            return ReportTaskType.Work;
    }

    private string GetDateString(DateOnly date)
    {
        return
            $"{date.Year}{Formatter.IntToStringWithLeadingZeros(date.Month, 2)}{Formatter.IntToStringWithLeadingZeros(date.Day, 2)}";
    }

    private async Task<IHtmlDocument> FetchPage(string url)
    {
        var response = await HttpClient.GetStringAsync(url);

        // Build a searchable document object using AngleSharp html parser
        var context = BrowsingContext.New(Configuration.Default);
        var parser = context.GetService<IHtmlParser>()!;

        return await parser.ParseDocumentAsync(response);
    }

    public void Dispose()
    {
        HttpClient.Dispose();
    }
}