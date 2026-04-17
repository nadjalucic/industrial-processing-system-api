using Industrial_Processing_System_API.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Industrial_Processing_System_API.Services
{
    public class FileLogger
    {
        private readonly string _logFilePath;
        private readonly SemaphoreSlim _fileSemaphore = new(1, 1);

        public FileLogger(string logFilePath)
        {
            _logFilePath = logFilePath;
        }

        public async Task LogAsync(JobEventArgs args)
        {
            string line = $"[{args.Timestamp:yyyy-MM-dd HH:mm:ss}] [{args.Status}] {args.Job.Id}, {(args.Result.HasValue ? args.Result.Value.ToString() : "N/A")}";

            if (!string.IsNullOrWhiteSpace(args.Message))
                line += $", {args.Message}";

            await _fileSemaphore.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(_logFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        public async Task LogAbortAsync(Job job)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ABORT] {job.Id}, N/A";

            await _fileSemaphore.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(_logFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }
    }
}