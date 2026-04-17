using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Industrial_Processing_System_API.Models
{
    public class JobStatisticsRecord
    {
        public Guid JobId { get; set; }
        public JobType Type { get; set; }
        public bool IsSuccess { get; set; }
        public bool IsFailed { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime FinishedAt { get; set; }
    }
}
