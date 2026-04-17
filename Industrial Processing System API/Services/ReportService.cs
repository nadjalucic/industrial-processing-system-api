using Industrial_Processing_System_API.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Industrial_Processing_System_API.Services
{
    public class ReportService
    {
        private readonly ProcessingSystem _processingSystem;
        private readonly string _reportsDirectory;
        private readonly object _reportLock = new();

        public ReportService(ProcessingSystem processingSystem, string reportsDirectory)
        {
            _processingSystem = processingSystem;
            _reportsDirectory = reportsDirectory;
            Directory.CreateDirectory(_reportsDirectory);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                    GenerateReport();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public void GenerateReport()
        {
            List<JobStatisticsRecord> stats = _processingSystem.GetStatisticsSnapshot();

            var executedByType = stats
                .Where(x => x.IsSuccess)
                .GroupBy(x => x.Type)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Type.ToString())
                .ToList();

            var averageDurationByType = stats
                .Where(x => x.IsSuccess)
                .GroupBy(x => x.Type)
                .Select(g => new
                {
                    Type = g.Key,
                    AverageMilliseconds = g.Average(x => x.Duration.TotalMilliseconds)
                })
                .OrderBy(x => x.Type.ToString())
                .ToList();

            var failedByType = stats
                .Where(x => x.IsFailed)
                .GroupBy(x => x.Type)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Type.ToString())
                .ToList();

            XDocument doc = new(
                new XElement("Report",
                    new XAttribute("GeneratedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                    new XElement("ExecutedJobsByType",
                        executedByType.Select(x =>
                            new XElement("TypeStatistics",
                                new XAttribute("Type", x.Type),
                                new XAttribute("Count", x.Count)
                            )
                        )
                    ),
                    new XElement("AverageExecutionTimeByType",
                        averageDurationByType.Select(x =>
                            new XElement("TypeStatistics",
                                new XAttribute("Type", x.Type),
                                new XAttribute("AverageMilliseconds", x.AverageMilliseconds)
                            )
                        )
                    ),
                    new XElement("FailedJobsByType",
                        failedByType.Select(x =>
                            new XElement("TypeStatistics",
                                new XAttribute("Type", x.Type),
                                new XAttribute("Count", x.Count)
                            )
                        )
                    )
                )
            );

            lock (_reportLock)
            {
                string filePath = GetNextReportPath();
                doc.Save(filePath);
            }
        }

        private string GetNextReportPath()
        {
            string[] files = Directory.GetFiles(_reportsDirectory, "report_*.xml");

            if (files.Length < 10)
            {
                var usedIndexes = files
                    .Select(file => Path.GetFileNameWithoutExtension(file))
                    .Select(name =>
                    {
                        string numberPart = name.Replace("report_", "");
                        return int.TryParse(numberPart, out int index) ? index : -1;
                    })
                    .Where(index => index >= 0)
                    .OrderBy(index => index)
                    .ToList();

                int nextIndex = 0;
                while (usedIndexes.Contains(nextIndex))
                {
                    nextIndex++;
                }

                return Path.Combine(_reportsDirectory, $"report_{nextIndex}.xml");
            }

            string oldestFile = files
                .Select(file => new
                {
                    File = file,
                    GeneratedAt = ReadGeneratedAt(file)
                })
                .OrderBy(x => x.GeneratedAt)
                .First()
                .File;

            return oldestFile;
        }

        private DateTime ReadGeneratedAt(string filePath)
        {
            try
            {
                XDocument doc = XDocument.Load(filePath);
                XElement? root = doc.Root;

                if (root == null)
                    return DateTime.MinValue;

                XAttribute? attr = root.Attribute("GeneratedAt");

                if (attr == null)
                    return DateTime.MinValue;

                if (DateTime.TryParse(attr.Value, out DateTime generatedAt))
                    return generatedAt;

                return DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }
}