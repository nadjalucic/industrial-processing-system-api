using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Industrial_Processing_System_API.Models
{
    public class JobEventArgs : EventArgs
    {
        public Job Job { get; set; } = null!;
        public int? Result { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public int Attempt { get; set; }
    }
}
