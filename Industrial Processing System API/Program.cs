using Industrial_Processing_System_API.Models;
using Industrial_Processing_System_API.Services;

namespace Industrial_Processing_System_API
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                string configPath = Path.Combine("SystemConfig", "SystemConfig.xml");
                SystemConfigModel config = XmlConfigLoader.Load(configPath);

                ProcessingSystem processingSystem = new(config.WorkerCount, config.MaxQueueSize);
                FileLogger logger = new("jobs.log");
                ReportService reportService = new(processingSystem, "Reports");

                processingSystem.JobCompleted += (sender, eventArgs) =>
                {
                    _ = logger.LogAsync(eventArgs);
                };

                processingSystem.JobFailed += (sender, eventArgs) =>
                {
                    _ = logger.LogAsync(eventArgs);

                    if (eventArgs.Attempt == 3)
                    {
                        _ = logger.LogAbortAsync(eventArgs.Job);
                    }
                };

                processingSystem.Start();

                Console.WriteLine("Loading initial jobs from XML...");
                foreach (Job job in config.InitialJobs)
                {
                    try
                    {
                        JobHandle handle = processingSystem.Submit(job);
                        Console.WriteLine($"Initial job added: {job.Id} | {job.Type} | priority {job.Priority}");

                        _ = handle.Result.ContinueWith(t =>
                        {
                            if (t.IsCompletedSuccessfully)
                                Console.WriteLine($"Job {job.Id} completed. Result = {t.Result}");
                            else
                                Console.WriteLine($"Job {job.Id} is not completed successfully.");
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error submitting initial job: {ex.Message}");
                    }
                }

                CancellationTokenSource cts = new();
                Task reportTask = reportService.StartAsync(cts.Token);

                int producerThreadCount = 3;
                List<Task> producerTasks = new();

                for (int i = 0; i < producerThreadCount; i++)
                {
                    int producerId = i + 1;

                    producerTasks.Add(Task.Run(async () =>
                    {
                        Random random = new(Guid.NewGuid().GetHashCode());

                        while (!cts.Token.IsCancellationRequested)
                        {
                            try
                            {
                                Job newJob;

                                if (random.Next(0, 2) == 0)
                                {
                                    newJob = new Job
                                    {
                                        Id = Guid.NewGuid(),
                                        Type = JobType.Prime,
                                        Payload = $"numbers:{random.Next(5000, 30000):N0}".Replace(",", "_") + $",threads:{random.Next(1, 10)}",
                                        Priority = random.Next(1, 6)
                                    };
                                }
                                else
                                {
                                    newJob = new Job
                                    {
                                        Id = Guid.NewGuid(),
                                        Type = JobType.IO,
                                        Payload = $"delay:{random.Next(500, 5000):N0}".Replace(",", "_"),
                                        Priority = random.Next(1, 6)
                                    };
                                }

                                JobHandle handle = processingSystem.Submit(newJob);

                                Console.WriteLine($"Producer {producerId} added job {newJob.Id} | {newJob.Type} | priority {newJob.Priority}");

                                _ = handle.Result.ContinueWith(t =>
                                {
                                    if (t.IsCompletedSuccessfully)
                                        Console.WriteLine($"Job {newJob.Id} completed. Result = {t.Result}");
                                    else
                                        Console.WriteLine($"Job {newJob.Id} failed.");
                                });
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Producer {producerId} error: {ex.Message}");
                            }

                            await Task.Delay(1000);
                        }
                    }));
                }

                Console.WriteLine();
                Console.WriteLine("System started.");
                Console.WriteLine("Press ENTER to stop...");
                Console.ReadLine();

                Console.WriteLine("\n--- Top jobs: ---");

                var topJobs = processingSystem.GetTopJobs(5).ToList();

                if (topJobs.Count == 0)
                {
                    Console.WriteLine("No jobs in queue.");
                }
                else
                {
                    foreach (var job in topJobs)
                    {
                        Console.WriteLine($"{job.Id} | {job.Type} | Priority={job.Priority}");
                    }
                }

                Console.WriteLine("\n--- Test GetJob ---");

                try
                {
                    var firstJobId = config.InitialJobs.First().Id;

                    var job = processingSystem.GetJob(firstJobId);

                    Console.WriteLine($"Found job: {job.Id} | {job.Type} | Priority={job.Priority}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }

                cts.Cancel();
                processingSystem.Stop();

                await Task.WhenAll(producerTasks);
                await reportTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}