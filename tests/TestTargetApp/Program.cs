using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            foreach (var arg in args)
            {
                Console.WriteLine(arg);
            }

            var logFile = "";
            if (args.Contains("-log-events-to"))
            {
                logFile = args.SkipWhile(x => x.StartsWith("-")).FirstOrDefault(x => x != "-log-events-to");
                File.WriteAllText(logFile, DateTime.Now.ToString("s") + ": target started\r\n");
            }

            if (args.Contains("-wait-for-5000"))
                Thread.Sleep(5000);

            if (logFile != "")
                File.AppendAllText(logFile, DateTime.Now.ToString("s") + ": target exited");
        }
    }
}