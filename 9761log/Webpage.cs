using System;
using System.IO;

namespace CarBatteryLog
{
    partial class Program
    {
        public static void updateWebPage(string message)
        {  // writes message to todayFile if file exists - does nothing if it does not, unless new day 

            FileInfo carDatafile = new FileInfo(@todayFile);
            if (IsFileLocked(carDatafile))
            {
                int count = 3;
                while (IsFileLocked(carDatafile) & count > 0)
                {
                    count--;
                    System.Threading.Thread.Sleep(500); // wait for other process to end
                    Console.WriteLine("retry file " + todayFile);
                }
            }
            if (IsFileLocked(carDatafile))
                return; // give up

            try
            {   // check for new day, if it is update yesterday file, this month file and empty webFile
                if (newDay)
                {   // save existing data as a new 'yesterday' file
                    File.Copy(todayFile, yesterdayFile, true); // 'true' will overwrite previous version
                    Console.WriteLine("yesterday file updated");
                    // create a new today file
                    using (StreamWriter writer = File.CreateText(todayFile))
                    {
                        writer.WriteLine();     // blank line to start

                        while (gapCount > 1)
                        {       // add * lines to fill gap                                
                            writer.WriteLine("*                                     ");
                            gapCount--;
                        }

                        writer.WriteLine(message);
                    }
                    // produce the file for the today web page
                    combineTodayandYesterday();

                    // update the this month file
                    if (File.Exists(thisMonthFile))
                    {
                        using (StreamWriter writer = File.AppendText(thisMonthFile))
                        {
                            writer.WriteLine(dayRecord);
                        }
                    }

                    if (newMonth)
                    {   // put last month file into archive
                        archiveFile();

                        // create a new 'lastMonth' file
                        File.Copy(thisMonthFile, lastMonthFile, true); // 'true' will overwrite previous version
                        Console.WriteLine("last month file updated");
                        // empty thisMonth file for new month
                        using (StreamWriter writer = File.CreateText(thisMonthFile))
                        {
                            writer.WriteLine("");
                        }
                    }
                }
                else
                {
                    if (File.Exists(todayFile))
                    {
                        // not a new day, so append the data
                        using (StreamWriter writer = File.AppendText(todayFile))
                        {
                            while (gapCount > 1)
                            {       // add * lines to fill gap                                
                                writer.WriteLine("*                                     ");
                                gapCount--;
                            }

                            writer.WriteLine(message);
                        }
                        // produce the file for the today web page
                        combineTodayandYesterday();
                    }
                    else
                        Console.WriteLine("No " + todayFile);
                }

                using (StreamWriter writer = File.CreateText(flashFile))
                {       // overwrite the latest data into flashFile.txt for the web page header
                    writer.WriteLine(flashHeader);
                }

                using (StreamWriter writer = File.CreateText(soilHeaderFile))
                {       // overwrite the latest data into soilFile.txt for the soil web page header
                    writer.WriteLine(soilHeader);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        static void archiveFile()
        {
            // create the filename
            string[] archiveData = File.ReadAllLines(lastMonthFile);
            int i = -1;
            try
            {   // find the first line with data
                while (archiveData[++i] == "") ;

                // get the date 
                string[] date = archiveData[i].Split(' ');
                // split the date
                string[] values = date[1].Split('/');

                string archiveFile = values[2];
                if (Int16.Parse(values[1]) < 10)
                    archiveFile = archiveFile + "0";

                archiveFile = archiveFile + values[1] + ".txt";
                Console.WriteLine("Archive filename = " + archiveFile);

                File.Copy(lastMonthFile, archiveFile, true); // 'true' will overwrite previous version
            }
            catch (Exception exp)
            {
                Console.WriteLine("Archive file creation error : " + exp.Message);
                return;
            }
        }
        static void combineTodayandYesterday()
        {       // combines the today and yesterday files for the webpage
            string[] todayData = File.ReadAllLines(todayFile);
            string[] yesterdayData = File.ReadAllLines(yesterdayFile);

            using (StreamWriter writer = File.CreateText(combinedFile))
            {
                int lineNum = 0;
                while (lineNum < todayData.Length)
                {
                    writer.Write(todayData[lineNum]);
                    string temp = "";

                    if (lineNum < yesterdayData.Length)
                    {
                        temp = yesterdayData[lineNum];
                        if (temp.Length > 20)
                        {
                            temp = temp.Substring(14);
                            temp = "        Yesterday:" + temp;
                        }
                    }

                    writer.WriteLine(temp);
                    lineNum++;
                }
            }
        }
        // end of class
    }
    // end of namespace
}
