﻿using System.Text.RegularExpressions;

void PrintHelp()
{
    var now = DateTime.Now;
    Console.WriteLine("Run as: \r\nCalendarAgeUpdater.exe  group_with_age target_pattern\r\n [year]");
    Console.WriteLine("regex_to_find\r\n\tregex matching events in calendar with group matching year and groups that you can address in target pattern. Sample: (.*) - birthday \\((.*)\\)\r\n");
    Console.WriteLine("group_with_year\r\n\tindex of group with birth year\r\n");
    Console.WriteLine("target_pattern\r\n\tpattern of target event name where '$number' is marking groups from regex_to_find and $Y stands for age. Character + is delimeter. Sample: $1+$Y+($2)");
    Console.WriteLine($"year\r\n\tyear that will be used for event listing. Current year is default ({now.Year})");
}

void PrintConfiguration(string regex, int group, string targetPattern, int eventsYear)
{
    Console.WriteLine($"Regex to find: {regex}, \r\nGroup with age: {group}, \r\nTarget pattern: {targetPattern}, Year to list: {eventsYear}");
    Console.WriteLine();
}

string GetNewName(Match regexMatch, string targetPattern, int yearGroup, int yearForAge)
{
    try
    {
        var year = int.Parse(regexMatch.Groups[yearGroup].Value);
        var age = yearForAge - year;
        string target = string.Empty;

        var tokens = targetPattern.Split("+", StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            var matchGroup = Regex.Match(token, "(.*)\\$([0-9]+)(.*)");
            var matchYear = Regex.Match(token, "(.*)\\$Y(.*)");
            if (matchGroup.Success)
            {
                var group = int.Parse(matchGroup.Groups[2].Value);
                target += $"{matchGroup.Groups[1].Value}{regexMatch.Groups[group].Value}{matchGroup.Groups[3].Value}";
            }
            else if (matchYear.Success)
            {// year
                target += $"{matchYear.Groups[1].Value}{age}{matchYear.Groups[2].Value}";
            }
            else
            {
                target += token;
            }
        }

        return target;
    }
    catch (Exception)
    {
        return String.Empty;
    }
}

try
{
    string regexFind = string.Empty;
    Regex rFind;
    int yearGroup = 0;
    int eventsYear = DateTime.Now.Year;

    string targetPattern = string.Empty;

    if (args.Length >= 3)
    {
        regexFind = args[0];
        rFind = new Regex(regexFind, RegexOptions.IgnoreCase);
        yearGroup = int.Parse(args[1]);
        targetPattern = args[2];

        if (args.Length > 3)
            eventsYear = int.Parse(args[3]);

        PrintConfiguration(regexFind, yearGroup, targetPattern, eventsYear);
    }
    else
    {
        PrintHelp();
        return;
    }

    var calendarApi = new GoogleApiHandler.CalendarApi();
    Console.WriteLine("Sign in to your Google account and consent permissions for the application");

    // authenticate
    await calendarApi.AutenticateAsync("auth.json");

    // list calendars
    int i = 0;
    var calendars = calendarApi.ListCalendars().ToDictionary(x => ++i);

    Console.WriteLine("Listed calendars (select target): \r\n");
    foreach (var (n, c) in calendars)
        Console.WriteLine($"{n}: {c.Name}");

    Console.WriteLine();
    var read = Console.ReadLine();
    if (string.IsNullOrEmpty(read))
    {
        Console.WriteLine("No calendar selected. Exiting.");
        return;
    }

    var index = int.Parse(read);
    var calendar = calendars[index];
    Console.WriteLine($"Selected calendar: {index} - {calendar.Name}");
    Console.WriteLine();

    var events = calendarApi.GetAllEvents(calendar.Id, eventsYear);
    var filtered = events
        .Select(x => (rFind.Match(x.Description), x))
        .Where(x => x.Item1.Success)
        .Select(x => (x.Item2, GetNewName(x.Item1, targetPattern, yearGroup, eventsYear)));

    while (true)
    {
        Console.WriteLine($"Press: \r\n\ta\tlist all events\r\n\tf\tlist filtered events\r\n\tr\tpairs of results\r\n\ts\tstart renaming\r\n\tother\texit");
        read = Console.ReadLine();

        if (read == "a")
        {
            foreach (var e in events)
                Console.WriteLine($"{e.Date}: {e.Description}");
        }
        else if (read == "f")
        {
            foreach (var pair in filtered)
                Console.WriteLine($"{pair.Item1.Date}: {pair.Item1.Description}");
        }
        else if (read == "r")
        {
            foreach (var pair in filtered)
                Console.WriteLine($"{pair.Item1.Date}: {pair.Item1.Description} -> {pair.Item2}");
        }
        else if (read == "s")
        {
            foreach (var pair in filtered)
            {
                if (!string.IsNullOrEmpty(pair.Item2))
                    calendarApi.UpdateEventSummary(calendar.Id, pair.Item1.Id, pair.Item2);
            }

            Console.WriteLine($"Events updated. New events in calendar: ");
            events = calendarApi.GetAllEvents(calendar.Id, eventsYear);
            foreach (var e in events)
                Console.WriteLine($"{e.Date}: {e.Description}");

            break;
        }
        else
            break;
    }
}
catch (Exception e)
{
    Console.WriteLine($"Error: \r\n{e}");
}