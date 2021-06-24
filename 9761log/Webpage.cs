using System;
using System.IO;
using System.Linq;

namespace CarBatteryLog
{
    partial class Program
    {
        public static void updateWebPage(string message)
        {  // writes message to todayFile if file exists - does nothing if it does not, unless new day 
            if (todayFileNotAvailable())
                return;

            try
            {   // check for new day, if it is update yesterday file, this month file and create empty today file
                if (newDay)
                {   // save existing data as a new 'yesterday' file
                    File.Copy(todayFile, yesterdayFile, true); // 'true' will overwrite previous version
                    Console.WriteLine("yesterday file updated");
                    // create a new today file
                    createNewTodayFile(message);

                    if (newMonth)
                        updateLastMonthFile();
                }
                else
                {   //not a new day, so update today file
                    updateTodayFile(message);
                }

                // produce the file for the today web page
                combineTodayandYesterday();

                updateHeaderFiles();    
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static void updateHeaderFiles()
        {    // overwrite the latest data into flashFile.txt for the web page header
            using (StreamWriter writer = File.CreateText(flashFile))
            {       
                writer.WriteLine(flashHeader);
            }

            // overwrite the latest data into soilFile.txt for the soil web page header
            using (StreamWriter writer = File.CreateText(soilHeaderFile))
            {       
                writer.WriteLine(soilHeader);
            }
        }

        private static void updateTodayFile(string message)
        {   // not a new day, so append the data
            using (StreamWriter writer = File.AppendText(todayFile))
            {
                while (gapCount > 1)
                {       // add * lines to fill gap                                
                    writer.WriteLine("*                                     ");
                    gapCount--;
                }

                writer.WriteLine(message);
            }
        }

        private static void updateLastMonthFile()
        {
            // put last month file into archive
            archiveFile();

            // create a new 'lastMonth' file
            File.Copy(directory + thisMonthFile, directory + lastMonthFile, true); // 'true' will overwrite previous version
            Console.WriteLine("last month file updated");

            createNewThisMonthFile();       // creates an empty file with just the header line
        }

        private static void createNewTodayFile(string message)
        {   // creates a new today file, empty if at midnight, or with * lines if not
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
        }

        private static bool todayFileNotAvailable()
        {
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
            {
                Console.WriteLine("File " + todayFile + " not available");
                return true; // give up
            }
            else
                return false;
        }

        static void archiveFile()
        {   // copies lastMonthFile into a file called YYMM.txt
            // create the filename               
            string archiveFileName = (DateTime.Now.AddMonths(-1).Year % 100).ToString("00") 
                                    + DateTime.Now.AddMonths(-1).Month.ToString("00") + ".txt";
            try 
            {      
                File.Copy(lastMonthFile, archiveFileName, true); // 'true' will overwrite previous version
                Console.WriteLine("Archive file " + archiveFileName + " created.");
            }
            catch (Exception exp)
            {
                Console.WriteLine("Archive file creation error : " + exp.Message);
                return;
            }
        }
        static void combineTodayandYesterday()
        {       // combines the today and yesterday text files into combinedFile for the webpage
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

        static void updateThisMonthFile(String[] values)
        {
            int tubCount = getUnitNames();         

            string dayLine = createDayLineString(values, tubCount);  // formats the values for the day into a string

            try
            {   // append the line to the 'this month' file
                using (StreamWriter writer = File.AppendText(directory + thisMonthFile))
                {
                    writer.WriteLine(dayLine);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("This month file error: " + e.Message);
            }
        }

        private static int getUnitNames()
        {   // updates unitNames array, and returns how many names found
            try
            {   // get the plant tub names   
                unitNames = File.ReadAllLines(@"unitNames.txt");
            }
            catch (Exception e)
            {
                Console.WriteLine("Tub name error: " + e.Message);
            }

            int tubCount = unitNames.Count(s => s != null);     // count the number of tub names in the file
            return tubCount;
        }

        private static string createHeaderLineString(int tubCount)
        {
            String headerLine = String.Format("<tr><th width =\"5 % \">Date</th ><th width =\"5 % \">Car Moved</th ><th width =\"5 % \">Battery Voltage </th>");
            headerLine += String.Format("<th width = \"5 % \">Charge State</th><th width = \"5 % \">Peak Solar Current</th>");
            headerLine += String.Format("<th width =\"5 % \" >Day's Charge</th><th width =\"5 % \" >Average Charge</th>");

            for (int i = 0; i < tubCount; i++)
                headerLine += String.Format("<th width =\"5 % \" >{0}</th >", unitNames[i]);
            return headerLine;
        }

        private static string createDayLineString(string[] values, int tubCount)
        {
            String dayLine;
            String movedString = "";
            if (gapDay)
                movedString = "Yes";

            dayLine = String.Format("<tr><td>{0,2}/{1}/{2}</td> ", values[CSV.DAY], values[CSV.MONTH].Trim(),
                                          values[CSV.YEAR].Trim());     // day/ month/ year
            dayLine += String.Format("<td>{0}</td>", movedString);
            dayLine += String.Format("<td>{0:##.00}V</td> ", Int16.Parse(values[CSV.V1]) / 100.0);       // voltage
            dayLine += String.Format("<td>{0, 3}%</td> ", calculatePercentage(Int16.Parse(values[CSV.V1])));
            dayLine += String.Format("<td>{0,6:0.0}mA</td><td>{1,4:####}mAH</td>",
                                        (Int16.Parse(values[CSV.C1PEAK]) / 10.0), values[CSV.mAH].ToString().Trim());
            dayLine += String.Format("<td>{0,4}mAH</td>", averagemAH);

            for(int i = 0; i < tubCount; i++)
                dayLine += String.Format("<td>{0,3}% {1:0.00}V</td>", Int16.Parse(values[CSV.SOIL1 + i]), Int16.Parse(values[CSV.SOIL_VOLTAGE1 + i])/ 1000.0 );

            return dayLine;
        }

        private static void createNewThisMonthFile()
        {
            int tubCount = getUnitNames();      // update the unit names array, and find how many there are

            string headerLine = createHeaderLineString(tubCount); // puts the tub names into a string for the table header

            using (StreamWriter writer = File.CreateText(directory + thisMonthFile))
            {
                writer.WriteLine(headerLine);
            }
        }

        private static void addLineToMonthFile()
        {
            // get the last line of the csv file
            string lastLine = File.ReadLines(@"logCarBattery.csv").Last();
            string[] oldValues = lastLine.Split(',');
            if (oldValues[0] == "")
            {
                Console.WriteLine("Last line of csv is blank");
                newDay = newMonth = false;
                return;
            }
            updateThisMonthFile(oldValues);
        }

        // end of class
    }
    // end of namespace
}
