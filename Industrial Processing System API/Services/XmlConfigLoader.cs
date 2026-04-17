using Industrial_Processing_System_API.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Industrial_Processing_System_API.Services
{
    public static class XmlConfigLoader
    {
        public static SystemConfigModel Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Configuration file not found: {filePath}");

            XDocument doc = XDocument.Load(filePath);
            XElement root = doc.Root ?? throw new Exception("XML is missing root element.");

            SystemConfigModel config = new()
            {
                WorkerCount = (int?)root.Element("WorkerCount")
                              ?? throw new Exception("Missing WorkerCount in XML."),
                MaxQueueSize = (int?)root.Element("MaxQueueSize")
                               ?? throw new Exception("Missing MaxQueueSize in XML.")
            };

            XElement? jobsElement = root.Element("Jobs");
            if (jobsElement != null)
            {
                foreach (XElement jobElement in jobsElement.Elements("Job"))
                {
                    string typeText = (string?)jobElement.Attribute("Type")
                                      ?? throw new Exception("Job is missing Type attribute.");

                    string payload = (string?)jobElement.Attribute("Payload")
                                     ?? throw new Exception("Job is missing Payload attribute.");

                    int priority = (int?)jobElement.Attribute("Priority")
                                   ?? throw new Exception("Job is missing Priority attribute.");

                    Job job = new()
                    {
                        Id = Guid.NewGuid(),
                        Type = Enum.Parse<JobType>(typeText, true),
                        Payload = payload,
                        Priority = priority
                    };

                    config.InitialJobs.Add(job);
                }
            }

            return config;
        }
    }
}
