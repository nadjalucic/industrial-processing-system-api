using Industrial_Processing_System_API.Models;
using Industrial_Processing_System_API.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Industrial_Processing_System_API.Services
{
    public class ProcessingSystem
    {
        private class QueueItem
        {
            public Job Job { get; set; } = null!;
            public TaskCompletionSource<int> CompletionSource { get; set; } = null!;
        }

        private readonly List<QueueItem> _queue = new();
        private readonly object _queueLock = new();

        private readonly SemaphoreSlim _queueSignal = new(0);
        private readonly CancellationTokenSource _internalCts = new();

        private readonly ConcurrentDictionary<Guid, Job> _allJobs = new();
        private readonly ConcurrentDictionary<Guid, JobHandle> _handles = new();
        private readonly ConcurrentDictionary<Guid, byte> _submittedJobIds = new();

        private readonly List<JobStatisticsRecord> _statistics = new();
        private readonly object _statisticsLock = new();

        private readonly int _workerCount;
        private readonly int _maxQueueSize;

        private readonly Random _random = new();
        private readonly object _randomLock = new();

        public event EventHandler<JobEventArgs>? JobCompleted;
        public event EventHandler<JobEventArgs>? JobFailed;

        public ProcessingSystem(int workerCount, int maxQueueSize)
        {
            _workerCount = workerCount;
            _maxQueueSize = maxQueueSize;
        }

        public void Start()
        {
            for (int i = 0; i < _workerCount; i++)
            {
                Task.Run(() => WorkerLoopAsync(_internalCts.Token));
            }
        }

        public void Stop()
        {
            _internalCts.Cancel();
        }

        public JobHandle Submit(Job job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            if (!_submittedJobIds.TryAdd(job.Id, 0))
                throw new InvalidOperationException($"Job with ID {job.Id} already exists in the system.");

            TaskCompletionSource<int> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_queueLock)
            {
                if (_queue.Count >= _maxQueueSize)
                {
                    _submittedJobIds.TryRemove(job.Id, out _);
                    throw new InvalidOperationException("Queue is full. New jobs are being rejected.");
                }

                _queue.Add(new QueueItem
                {
                    Job = job,
                    CompletionSource = tcs
                });

                _queue.Sort((a, b) => a.Job.Priority.CompareTo(b.Job.Priority));
                _allJobs[job.Id] = job;
            }

            JobHandle handle = new()
            {
                Id = job.Id,
                Result = tcs.Task
            };

            _handles[job.Id] = handle;
            _queueSignal.Release();

            return handle;
        }

        public IEnumerable<Job> GetTopJobs(int n)
        {
            lock (_queueLock)
            {
                return _queue
                    .OrderBy(x => x.Job.Priority)
                    .Take(n)
                    .Select(x => x.Job)
                    .ToList();
            }
        }

        public Job GetJob(Guid id)
        {
            if (_allJobs.TryGetValue(id, out Job? job))
                return job;

            throw new KeyNotFoundException($"Job with ID {id} is not found.");
        }

        public List<JobStatisticsRecord> GetStatisticsSnapshot()
        {
            lock (_statisticsLock)
            {
                return _statistics.ToList();
            }
        }

        private async Task WorkerLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _queueSignal.WaitAsync(cancellationToken);

                    QueueItem? item = null;

                    lock (_queueLock)
                    {
                        if (_queue.Count > 0)
                        {
                            item = _queue[0];
                            _queue.RemoveAt(0);
                        }
                    }

                    if (item == null)
                        continue;

                    await ExecuteWithRetryAsync(item);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                }
            }
        }

        private async Task ExecuteWithRetryAsync(QueueItem item)
        {
            const int maxAttempts = 3;
            Job job = item.Job;
            TaskCompletionSource<int> tcs = item.CompletionSource;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                DateTime start = DateTime.Now;

                try
                {
                    Task<int> executionTask = ExecuteJobAsync(job);
                    Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(2));

                    Task finishedTask = await Task.WhenAny(executionTask, timeoutTask);

                    if (finishedTask != executionTask)
                        throw new TimeoutException("Job lasted more than 2 seconds.");

                    int result = await executionTask;
                    TimeSpan duration = DateTime.Now - start;

                    AddStatistics(job, true, false, duration);

                    tcs.TrySetResult(result);

                    JobCompleted?.Invoke(this, new JobEventArgs
                    {
                        Job = job,
                        Result = result,
                        Status = "COMPLETED",
                        Timestamp = DateTime.Now,
                        Attempt = attempt
                    });

                    return;
                }
                catch (Exception ex)
                {
                    TimeSpan duration = DateTime.Now - start;

                    JobFailed?.Invoke(this, new JobEventArgs
                    {
                        Job = job,
                        Result = null,
                        Status = "FAILED",
                        Timestamp = DateTime.Now,
                        Message = ex.Message,
                        Attempt = attempt
                    });

                    if (attempt == maxAttempts)
                    {
                        AddStatistics(job, false, true, duration);
                        tcs.TrySetException(ex);
                        return;
                    }
                }
            }
        }

        private void AddStatistics(Job job, bool isSuccess, bool isFailed, TimeSpan duration)
        {
            lock (_statisticsLock)
            {
                _statistics.Add(new JobStatisticsRecord
                {
                    JobId = job.Id,
                    Type = job.Type,
                    IsSuccess = isSuccess,
                    IsFailed = isFailed,
                    Duration = duration,
                    FinishedAt = DateTime.Now
                });
            }
        }

        private async Task<int> ExecuteJobAsync(Job job)
        {
            switch (job.Type)
            {
                case JobType.Prime:
                    var (numbers, threads) = PayloadParser.ParsePrimePayload(job.Payload);
                    return await PrimeCalculator.CountPrimesAsync(numbers, threads);

                case JobType.IO:
                    int delay = PayloadParser.ParseIoPayload(job.Payload);
                    await Task.Run(() => Thread.Sleep(delay));

                    lock (_randomLock)
                    {
                        return _random.Next(0, 101);
                    }

                default:
                    throw new NotSupportedException("Unknown job type.");
            }
        }
    }
}
