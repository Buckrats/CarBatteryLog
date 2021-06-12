using System;
using System.Linq;
using System.Net.Sockets;
using System.IO;

namespace CarBatteryLog
{
    partial class Program
    {
        const bool supressUDPresponse = true;      // set to false to send a response

        static void Main(string[] args)
        {
            UdpClient socket40030 = new UdpClient(listenPort1); // `new UdpClient()` to auto-pick port
            // schedule the first receive operation:
            socket40030.BeginReceive(new AsyncCallback(OnUdpData40030), socket40030);
            UdpClient socket40032 = new UdpClient(currentPort); 
  
            Console.WriteLine("Setup V1 complete");

            if (supressUDPresponse)
                Console.WriteLine("UDP response supressed!");

            // main loop
            while (true)
            {
                System.Threading.Thread.Sleep(100); //don't use all the cpu time
                int input = Console.Read();
                if ((input > 0) && (input != 10))
                {
                   // string str = "1, 5, 21, 6, 15, 1296, 326, 516, 316, 794, 1855, 0, 34, 35, 36, 37";
                    Console.WriteLine("Received char " + input.ToString() + " Calling test");
                    updateWebPage("test");
                 //   makeMonthFile();
                    //    String temp1 = makeSoilString(str);
                }
            }
        }
             


        static bool newDayNewMonthCheck(string csvData)
        {   // returns false if a repeat or not valid, otherwise returns true and
            // sets the flags newDay, New month, the value of gapCount and 
            // sets up dayRecord string in case it is needed
            if (!File.Exists(logFile))
                return false;     // can't check last line if file does not exist!
            // split the current values
            string[] values = csvData.Split(',');

            // get the last line of the csv file
            string lastLine = File.ReadLines(@"logCarBattery.csv").Last();            
            string[] oldValues = lastLine.Split(',');
            if (oldValues[0] == "")
            {
                Console.WriteLine("Last line of csv is blank");
                newDay = newMonth = false;
                return false;
            }

            if (Int16.Parse(values[CSV.DAY]) == 0)
                return false;     // valid day can't be zero
            if (Int16.Parse(values[CSV.MONTH]) == 0)
                return false;     // valid month can't be zero
            if (Int16.Parse(values[CSV.YEAR]) == 0)
                return false;     // valid year can't be zero

            // check for repeat
            if ((Int16.Parse(values[CSV.DAY]) == Int16.Parse(oldValues[CSV.DAY])) &&
                (Int16.Parse(values[CSV.MONTH]) == Int16.Parse(oldValues[CSV.MONTH])) &&
                (Int16.Parse(values[CSV.YEAR]) == Int16.Parse(oldValues[CSV.YEAR])) &&
                (Int16.Parse(values[CSV.HOUR]) == Int16.Parse(oldValues[CSV.HOUR])) &&
                (Int16.Parse(values[CSV.MINUTE]) == Int16.Parse(oldValues[CSV.MINUTE]))
                )
            {   // repeat transmission, so return false
                Console.WriteLine("Repeat transmission");
                return false;
            }

            // set the new day and month flags by comparing the new values
            // with the last line of the csv file
            newDay = (Int16.Parse(values[CSV.DAY]) != Int16.Parse(oldValues[CSV.DAY]));
            newMonth = (Int16.Parse(values[CSV.MONTH]) != Int16.Parse(oldValues[CSV.MONTH]));

            if (newMonth)
                newDay = true;          // new month must be new day, even if same day number!

            //check for gap
            if (newDay)
            {   // just check minutes since midnight
                gapCount = (Int16.Parse(values[CSV.HOUR]) * 60 + Int16.Parse(values[CSV.MINUTE])) / 15;
            }
            else
            {   // not new day so compare current time to previous in csv file
                gapCount = (Int16.Parse(values[CSV.HOUR]) * 60 + Int16.Parse(values[CSV.MINUTE]) -
                            (Int16.Parse(oldValues[CSV.HOUR]) * 60 + Int16.Parse(oldValues[CSV.MINUTE]))) / 15;
            }

            if (gapCount > 1)
                Console.WriteLine("Gap count = " + gapCount.ToString());

            gapDay = Int16.Parse(values[CSV.GAP_DAY]) != 0;

            if (gapDay)
                Console.WriteLine("Gap day!");            

            // calculate the running average of solar charge
            averagemAH = calculateAverage(Int16.Parse(values[CSV.DAY]));

            if (newDay)
            {   // set up the string for the day record
                if (gapDay) // check if car has moved
                    dayRecord = String.Format("* ");     // add marker to day record
                else
                    dayRecord = String.Format("");

                dayRecord += String.Format("{0,2}/{1}/{2} ", oldValues[CSV.DAY], oldValues[CSV.MONTH].Trim(),
                                          oldValues[CSV.YEAR].Trim());     // day/ month/ year
                dayRecord += String.Format("Battery = {0:##.00}V ", Int16.Parse(oldValues[CSV.V1]) / 100.0);       // voltage
                dayRecord += String.Format("Charge level = {0, 3}% ", calculatePercentage(Int16.Parse(oldValues[CSV.V1])));
                dayRecord += String.Format("Peak Solar current = {0,6:0.0}mA, Solar charge = {1,4:####}mAH",
                                            (Int16.Parse(oldValues[CSV.C1PEAK]) / 10.0), oldValues[CSV.mAH].ToString().Trim());
                
                dayRecord += String.Format(" Running average = {0,4}mAH", averagemAH);
     
             //   gapDay = false; // reset flag for new day        
            }
            return true; // show a new data point
        }

        static int calculateAverage(int today)
        { // returns the average of the mAH for the last DAYS 
            int average = 0;
            string[] values = new string[15];
            using (FileStream fs = new FileStream(@logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                // start at next to last byte ( since last is typically a linefeed).
                fs.Seek(0, SeekOrigin.End);
                fs.Seek(-1, SeekOrigin.Current);

                for (int i = 0; i < DAYS; i++)
                {
                    values = findPreviousDay(fs, today);
                    if (values[0] == "")
                    { // error reading csv file
                        return 9999;        // flag error
                    }

                    today = Int16.Parse(values[CSV.DAY]);
                    average += Int16.Parse(values[CSV.mAH]);
                }
            }
            average = average / DAYS;            
            return average;
        }

        static string[] findPreviousDay(FileStream fs, int day)
        {   // returns a string array of the values from the end of the previous day
            string tempString = "";
            string[] values = new string[15];
            bool notFound = true;

            while (notFound)
            {
                tempString = readLineBackwards(fs);
                if (tempString == "")
                {   // error reading csv file
                    values[0] = "";  // flag the error
                    return values;
                }
                
                values = tempString.Split(',');
                if (Int16.Parse(values[CSV.DAY]) != day)
                    notFound = false;
            }          
            return values;
        }

        static string readLineBackwards(FileStream fs)
        {   // returns a string containing the line
            bool notFound = true;
            const int DATA_SIZE = 100;
            int count = DATA_SIZE;      // start at end of array
            byte[] buffer = new byte[1];  // used to read each byte from file
            byte[] data = new byte[DATA_SIZE + 1];
            string tempString = "";

            try
            {
                while (notFound)
                {   // read backwards to a line feed
                    fs.Seek(-1, SeekOrigin.Current);
                    fs.Read(buffer, 0, 1);
                    if (buffer[0] == '\n')
                        notFound = false;
                    else
                        data[count--] = buffer[0]; // write into data array backwards

                    fs.Seek(-1, SeekOrigin.Current); // fs.Read(...) advances the position, so we need to go back again
                }
               // create string from array, starting at last element written (count + 1)  
                tempString = System.Text.Encoding.UTF8.GetString(data, count + 1, data.Length - count - 1);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine("Line too long in csv file...");
            }

            return tempString;   // string is null if there was an error in reading the file
        }   
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
                                gapCount-- ;
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
                Console.WriteLine("Archive filename = " +  archiveFile);

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
                while (lineNum < todayData.Length )
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

        static bool IsFileLocked(FileInfo file)
        {
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

        static int calculatePercentage(int volts)
        {    // calculates the % charge level for volts, using a quadratic function
            if (volts > 1280)
                return 100; // fully charged
            
            double voltage = volts / 100.0;
            double result = 0;
            double coeff2 = -0.8236;
            double coeff1 = 21.471;
            double coeff0 = -138.85;

            result = coeff2 * voltage * voltage + coeff1 * voltage + coeff0;

            if (result > 1)
                result = 1;
            if (result < 0)
                result = 0;

            return (int) (100 * result);
        }
        static void SendUdp(int srcPort, string dstIp, int dstPort, byte[] data)
        {   // sends a udp packet
            using (UdpClient c = new UdpClient(srcPort))
                c.Send(data, data.Length, dstIp, dstPort);
        }

        static void makeMonthFile()
        {
            Console.WriteLine("Making temp.txt file");
            string[] values = new string[15];
            int today = 24;
            int month = 5;

            using (FileStream fs = new FileStream(@logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                // start at next to last byte ( since last is typically a linefeed).
                fs.Seek(0, SeekOrigin.End);
                fs.Seek(-1, SeekOrigin.Current);

                for (int i = 0; i < 45; i++)
                {
                    values = findPreviousDay(fs, today);
                    if (values[0] == "")
                    { // error reading csv file
                        Console.WriteLine("No values found");
                        return ;        // flag error
                    }
                    today = Int16.Parse(values[CSV.DAY]);
                    
                    if (Int16.Parse(values[CSV.MONTH]) != month)
                    {
                        Console.WriteLine("last month found");
                        break;
                    }
                    int average = 0;

                    string record = String.Format("");
                    record += String.Format("{0,2}/{1}/{2} ", values[CSV.DAY], values[CSV.MONTH].Trim(),
                                              values[CSV.YEAR].Trim());     // day/ month/ year
                    record += String.Format("Battery = {0:##.00}V ", Int16.Parse(values[CSV.V1]) / 100.0);       // voltage
                    record += String.Format("Charge level = {0, 3}% ", calculatePercentage(Int16.Parse(values[CSV.V1])));
                    record += String.Format("Peak Solar current = {0,6:0.0}mA, Solar charge = {1,4:####}mAH",
                                                (Int16.Parse(values[CSV.C1PEAK]) / 10.0), values[CSV.mAH].ToString().Trim());

                    record += String.Format(" Running average = {0,4}mAH", average);

                    Console.WriteLine(record);

                    string content = File.ReadAllText("temp.txt");
                    
                    File.WriteAllText("temp.txt", record + Environment.NewLine + content);
                 
                }
            }

        }

        // end of class: Program
    }
}
