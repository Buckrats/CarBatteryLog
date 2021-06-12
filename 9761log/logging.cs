using System;
using System.IO;

namespace CarBatteryLog
{
    partial class Program
    {
        public static void log(string logMessage)
        {  // writes logMessage to logFile if file exists - does nothing if it does not

            FileInfo fi = new FileInfo(@logFile);
            if (IsFileLocked(fi))
            {
                int count = 3;
                while (IsFileLocked(fi) & count > 0)
                {
                    count--;
                    System.Threading.Thread.Sleep(500); // wait for other process to end
                    Console.WriteLine("retry file " + logFile);
                }
            }

            try
            {
                if (File.Exists(logFile))
                {   // write the new data
                    using (StreamWriter writer = File.AppendText(logFile))
                    {
                        writer.WriteLine(logMessage);
                    }
                }
                else
                    Console.WriteLine("No log.csv file");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
