using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Industrial_Processing_System_API.Utils
{
    public static class PrimeCalculator
    {
        public static async Task<int> CountPrimesAsync(int maxNumber, int threadCount)
        {
            if (maxNumber < 2)
                return 0;

            int chunkSize = (int)Math.Ceiling((double)(maxNumber - 1) / threadCount);
            List<Task<int>> tasks = new();

            for (int i = 0; i < threadCount; i++)
            {
                int start = 2 + i * chunkSize;
                int end = Math.Min(maxNumber, start + chunkSize - 1);

                if (start > end)
                    break;

                tasks.Add(Task.Run(() => CountPrimesInRange(start, end)));
            }

            int[] results = await Task.WhenAll(tasks);
            return results.Sum();
        }

        private static int CountPrimesInRange(int start, int end)
        {
            int count = 0;

            for (int i = start; i <= end; i++)
            {
                if (IsPrime(i))
                    count++;
            }

            return count;
        }

        private static bool IsPrime(int number)
        {
            if (number < 2) return false;
            if (number == 2) return true;
            if (number % 2 == 0) return false;

            int limit = (int)Math.Sqrt(number);

            for (int i = 3; i <= limit; i += 2)
            {
                if (number % i == 0)
                    return false;
            }

            return true;
        }
    }
}
