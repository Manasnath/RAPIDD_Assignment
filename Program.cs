using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static void Main(string[] args)
    {
        RunAsync().GetAwaiter().GetResult();
    }

    static async Task RunAsync()
    {
        string url = "https://rc-vault-fap-live-1.azurewebsites.net/api/gettimeentries?code=vO17RnE8vuzXzPJo5eaLLjXjmRW07law99QTD90zat9FfOQJKKUcgQ==";

        HttpClient client = new HttpClient();
        var response = await client.GetStringAsync(url);

        var entries = JsonSerializer.Deserialize<List<EmployeeWorkTimeEntry>>(response, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var summary = entries
            .GroupBy(e => string.IsNullOrWhiteSpace(e.EmployeeName) ? "Unknown" : e.EmployeeName)
            .Select(g => new
            {
                Name = g.Key,
                TotalHours = g.Sum(x => x.HoursWorked)
            })
            .OrderByDescending(x => x.TotalHours)
            .ToList();

        string html = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Employee Work Summary</title>
    <style>
        body { font-family: Arial; margin: 40px; background-color: #f8f9fa; }
        table { width: 70%; border-collapse: collapse; margin: auto; }
        th, td { border: 1px solid #ddd; padding: 10px; text-align: left; }
        th { background-color: #007bff; color: white; }
        tr:nth-child(even) { background-color: #f2f2f2; }
    </style>
</head>
<body>
<h2 style='text-align:center;'>Employee Total Time Worked</h2>
<table>
<tr><th>Name</th><th>Total Hours Worked</th></tr>";

        foreach (var item in summary)
        {
            string color = item.TotalHours < 100 ? " style='background-color: #ffcccc;'" : "";
            html += $"<tr{color}><td>{item.Name}</td><td>{item.TotalHours:F2}</td></tr>";
        }

        html += "</table></body></html>";

        File.WriteAllText("EmployeeReport.html", html);
        Console.WriteLine("✅ HTML report generated: EmployeeReport.html");

        var pieData = summary
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .ToDictionary(x => x.Name, x => x.TotalHours);

        if (pieData.Count > 0)
        {
            GeneratePieChart(pieData);
        }
        else
        {
            Console.WriteLine("⚠️ No valid employee names found. Pie chart skipped.");
        }
    }

    public static void GeneratePieChart(Dictionary<string, double> data)
    {
        int width = 700, height = 600;
        Bitmap bmp = new Bitmap(width, height);
        Graphics g = Graphics.FromImage(bmp);
        g.Clear(Color.White);

        double total = data.Values.Sum();
        float startAngle = 0;
        Random rand = new Random();

        foreach (var kvp in data)
        {
            float sweepAngle = (float)(kvp.Value / total * 360.0);
            Color color = Color.FromArgb(rand.Next(256), rand.Next(256), rand.Next(256));
            Brush b = new SolidBrush(color);
            g.FillPie(b, 50, 50, width - 250, height - 100, startAngle, sweepAngle);
            startAngle += sweepAngle;
        }

        int legendX = 500;
        int legendY = 80;
        foreach (var kvp in data)
        {
            float percentage = (float)(kvp.Value / total * 100);
            g.DrawString($"{kvp.Key}: {percentage:F1}%", new Font("Arial", 10), Brushes.Black, legendX, legendY);
            legendY += 20;
        }

        bmp.Save("EmployeePieChart.png", ImageFormat.Png);
        Console.WriteLine("✅ Pie chart generated: EmployeePieChart.png");
    }
}

public class EmployeeWorkTimeEntry
{
    public string EmployeeName { get; set; }
    public string StarTimeUtc { get; set; }
    public string EndTimeUtc { get; set; }

    public double HoursWorked
    {
        get
        {
            if (DateTime.TryParse(StarTimeUtc, null, System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime start) &&
                DateTime.TryParse(EndTimeUtc, null, System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime end))
            {
                return Math.Abs((end - start).TotalHours);
            }
            return 0;
        }
    }
}
