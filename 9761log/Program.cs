using System;
using System.Net.Sockets;
using System.IO;

namespace CarBatteryLog
{
    partial class Program
    {
        const bool supressUDPresponse = true;      // set to false to send a response
        const string version = "2";

        static void Main()
        {
            UdpClient socket40030 = new UdpClient(listenPort1); // `new UdpClient()` to auto-pick port
            // schedule the first receive operation:
            socket40030.BeginReceive(new AsyncCallback(OnUdpData40030), socket40030);
            UdpClient socket40032 = new UdpClient(currentPort); 
  
            Console.WriteLine("Setup V" + version + " complete");

            if (supressUDPresponse)
                Console.WriteLine("UDP response supressed!");

            // main loop
            while (true)
            {
                System.Threading.Thread.Sleep(100); //don't use all the cpu time
                int input = Console.Read();
                if ((input > 0) && (input != 10))
                {   // normally used for testing only 
                   // string str = "1, 5, 21, 6, 15, 1296, 326, 516, 316, 794, 1855, 0, 34, 35, 36, 37";
                    Console.WriteLine("Received char " + input.ToString() + " version " + version);

                    archiveFile();
                }
            }
        }     
        static bool IsFileLocked(FileInfo file)
        {   // checks if file can be opened for reading
            try
            {     
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to; or being processed by another thread;
                //or does not exist (has already been processed)
                return true;
            }

            //file is not locked
            return false;
        }
         
        // end of class: Program
    }
}
