using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Industrial_Processing_System_API.Utils
{
    public static class PayloadParser
    {
        public static (int numbers, int threads) ParsePrimePayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                throw new ArgumentException("Prime payload is empty.");

            string[] parts = payload.Split(',', StringSplitOptions.RemoveEmptyEntries);

            int numbers = 0;
            int threads = 1;

            foreach (string part in parts)
            {
                string[] keyValue = part.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (keyValue.Length != 2)
                    continue;

                string key = keyValue[0].Trim().ToLower();
                string value = keyValue[1].Trim().Replace("_", "");

                if (key == "numbers")
                    numbers = int.Parse(value);
                else if (key == "threads")
                    threads = int.Parse(value);
            }

            threads = Math.Clamp(threads, 1, 8);

            return (numbers, threads);
        }

        public static int ParseIoPayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                throw new ArgumentException("IO payload is empty.");

            string[] keyValue = payload.Split(':', StringSplitOptions.RemoveEmptyEntries);

            if (keyValue.Length != 2)
                throw new FormatException("IO payload is not in the correct format.");

            string key = keyValue[0].Trim().ToLower();
            string value = keyValue[1].Trim().Replace("_", "");

            if (key != "delay")
                throw new FormatException("IO payload must be in the format delay:number");

            return int.Parse(value);
        }
    }
}
