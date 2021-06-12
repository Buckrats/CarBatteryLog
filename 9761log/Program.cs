using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Timers;
using System.Net.Mail;
using System.IO;
using Newtonsoft.Json;

namespace _9761log
{
    class Program
    {
        const bool supressUDPresponse = true;      // set to false to send a response

        const string logFile = "logCarBattery.csv";     // name of log file - must exist already to be used
        const string todayFile = "today.txt";           // name of web page include file
        const string yesterdayFile = "yesterday.txt";   // name of web page include file
        const string combinedFile = "combined.txt";      // name of web page include file
        const string flashFile = "flashFile.txt";       // file that contains just the latest data
        const string thisMonthFile = "thisMonth.txt";   // file that contains the charge data for this month
        const string lastMonthFile = "lastMonth.txt";   // file that contains the charge data for last month
      //  const string soilMonthFile = "soilMonth.txt";   // file that contains the soil data for this month
        const string soilHeaderFile = "soilHeader.txt";   // file that contains the soil latest data
        const string soilHistoryHeader = "soilHistoryHeader.txt"; // header file for the web page body
        const string soilHistoryFile = "soilHistory.txt";   // file that contains the soil latest data
        const int SOIL_LINES_COUNT_MAX = 24;            // maximum number of lines in the soil history file, 24 = one day
        const string broadcastAddress = "255.255.255.255";

        private const int listenPort1 = 40030;
        private const int responsePort = 40031;
        private const int currentPort = 40032;

        static bool newDay = false;         // gets set in newDayNewMonthCheck 
        static bool newMonth = false;       // gets set in newDayNewMonthCheck 
        static string flashHeader = "";     // gets set in makePrintString 
        static string dayRecord = "";       // gets set in newDayNewMonthCheck 
        static string soilHeader = "";     // gets set in makeSoilString 

        static bool gapDay = false;         // set for a day if there is a gap in the record
        static int gapCount = 0;            // the number of gaps in the current record 
        const int MAX_GAP_COUNT = 3;        // how many gaps count as car being moved

        const int DAYS = 5;              
        static int averagemAH = 0;  

        static void Main(string[] args)
        {
            UdpClient socket40030 = new UdpClient(listenPort1); // `new UdpClient()` to auto-pick port
            // schedule the first receive operation:
            socket40030.BeginReceive(new AsyncCallback(OnUdpData40030), socket40030);
            UdpClient socket40032 = new UdpClient(currentPort); 
            // schedule the first receive operation: STOPPED!
     //       socket40032.BeginReceive(new AsyncCallback(OnUdpData40032), socket40032);

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
        static void OnUdpData40030(IAsyncResult result)
        {   // callback for udp data
            UdpClient socket = result.AsyncState as UdpClient;
            // points towards whoever had sent the message:
            IPEndPoint source = new IPEndPoint(0, 0);

            try
            {   // get the actual message and fill out the source:
                byte[] message = socket.EndReceive(result, ref source);    
            
                // convert to string
                var str = System.Text.Encoding.Default.GetString(message);

                // check for new day and new month, the value of gapCount and set the dayRecord string in case it is needed
                if (newDayNewMonthCheck(str)) 
                {   // not a repeat, so write raw data to the CSV log file
                    log(str);

                    // add latest row to web page
                    String temp = makePrintString(str);
                    String temp1 = makeSoilString(str); // create the soil moisture strings
                    updateWebPage(temp);
                    // write a formatted version to console
                    Console.WriteLine(temp + temp1);                    
                }
                // send a response to show received
                if (!supressUDPresponse)
                    SendUdp(responsePort, broadcastAddress, responsePort, Encoding.ASCII.GetBytes("Cstring"));

                // schedule the next receive operation once reading is done:
                socket.BeginReceive(new AsyncCallback(OnUdpData40030), socket);
            }
            catch (Exception e)
            {

                Console.WriteLine("UDP Rx error: " + e.Message);
                // schedule the next receive operation:
                socket.BeginReceive(new AsyncCallback(OnUdpData40030), socket);
            }
}
        static void OnUdpData40032(IAsyncResult result)
        {   // callback for udp csv data from current monitor
         
            // this is what had been passed into BeginReceive as the second parameter:
            UdpClient socket = result.AsyncState as UdpClient;
            // points towards whoever had sent the message:
            IPEndPoint source = new IPEndPoint(0, 0);
            // get the actual message and fill out the source:
            byte[] message = socket.EndReceive(result, ref source);
                    
            // convert to string
            var str = System.Text.Encoding.Default.GetString(message);
        //    Console.WriteLine("CSV message=" + str);
            
            // convert to string array for display
            string[] values = str.Split(',');

            // create the result string
            string resultString = String.Format("{0,2}/{1}/{2} ", values[current.DAY], values[current.MONTH].Trim(),
                                            values[current.YEAR].Trim());     // day/ month/ year
            if (Int16.Parse(values[current.MONTH]) < 10)    // check months, add space if necessary
                resultString += " ";
            resultString += String.Format("{0,2}:", values[current.HOUR].Trim()); // hours
            resultString += String.Format("{0:00}", Int16.Parse(values[current.MINUTE])); // minutes            
            resultString += String.Format(" {0:##.00}V ", (Int16.Parse(values[current.V1]) / 100.0));
            resultString += String.Format(" {0:##.0}mA", 
                                 (Int16.Parse(values[current.Current + Int16.Parse(values[current.COUNT]) - 1]) / 10.0));

            Console.WriteLine(resultString);
            
            saveCurrentCSV(values, str);

            // schedule the next receive operation once reading is done:
            socket.BeginReceive(new AsyncCallback(OnUdpData40032), socket);
        }
        static void saveCurrentCSV(string[] values, string csvString)
        {        
            string filename = String.Format("{0:D2}{1}{2:D2}.csv", values[current.YEAR].Trim(), 
                                        Int16.Parse(values[current.MONTH]).ToString("00").Trim(),
                                        Int16.Parse(values[current.DAY]).ToString("00").Trim());            

            if (File.Exists(filename))
            { // add the new data
                using (StreamWriter writer = File.AppendText(filename)) 
                    writer.WriteLine(csvString);
            }
            else
            {// create a new day file
                using (StreamWriter writer = File.CreateText(filename))                  
                    writer.WriteLine(csvString);
            }
        }

        static string makePrintString(string csvData)
        {   // returns the formatted string         
            string[] values = csvData.Split(',');

            // start the flash header string
            flashHeader = "at ";

            // create the result string    
            string result = String.Format("{0,2}/{1}/{2} ", values[CSV.DAY], values[CSV.MONTH].Trim(), 
                                            values[CSV.YEAR].Trim() );     // day/ month/ year
            if (Int16.Parse(values[CSV.MONTH]) < 10)    // check months, add space if necessary
                result += " ";
            result += String.Format("{0,2}:", values[CSV.HOUR].Trim()); // hours
            result += String.Format("{0:00}", Int16.Parse(values[CSV.MINUTE])); // minutes
            // add the date and time to flashheader string
            flashHeader += result;  
            // complete the result string
            result += String.Format(" {0:##.00}V ", (Int16.Parse(values[CSV.V1]) / 100.0)); //  voltage

            result += String.Format("{0,6:0.0}mA", (Int16.Parse(values[CSV.C1]) / 10.0));  // current
            result += String.Format(" {0,4:####}mAH", values[CSV.mAH].ToString().Trim());

            // calculate the running average of solar charge
            averagemAH = calculateAverage(Int16.Parse(values[CSV.DAY]));

            // complete the flash header string
            flashHeader += String.Format("</br>Battery voltage = {0:##.00}V", (Int16.Parse(values[CSV.V1]) / 100.0));
            flashHeader += String.Format(" Charge level = {0}%", calculatePercentage( Int16.Parse(values[CSV.V1])));
            flashHeader += String.Format("</br>Solar current = {0,6:0.0}mA", (Int16.Parse(values[CSV.C1]) / 10.0));            
            flashHeader += String.Format(" Peak Solar current = {0,6:0.0}mA", (Int16.Parse(values[CSV.C1PEAK]) / 10.0));
            flashHeader += String.Format("</br>Today's charge = {0,4:####}mAH", values[CSV.mAH].ToString().Trim());
            flashHeader += String.Format(" Running average over " + DAYS.ToString() + " days = " + averagemAH.ToString() + "mAH");

            return result; 
        }

        static string makeSoilString(string csvData)
        {   // returns the formatted string         
            string[] values = csvData.Split(',');

            // create the soil header string
            soilHeader = String.Format("at {0,2}/{1}/{2} ", values[CSV.DAY], values[CSV.MONTH].Trim(),
                                            values[CSV.YEAR].Trim());     // day/ month/ year
            if (Int16.Parse(values[CSV.MONTH]) < 10)    // check months, add space if necessary
                soilHeader += " ";
            soilHeader += String.Format("{0,2}:", values[CSV.HOUR].Trim()); // hours
            soilHeader += String.Format("{0:00}", Int16.Parse(values[CSV.MINUTE])); // minutes 
                  
            string[] unitNames = new string[5];

            try
            {   // get the plant tub names   
                unitNames = File.ReadAllLines(@"unitNames.txt");
            }
            catch (Exception e)
            {
                Console.WriteLine("Tub name error: " + e.Message);
            }

            soilHeader += String.Format(" <table>");

            soilHeader += String.Format("<tr><td> {0}:</td> ", unitNames[0]);
            soilHeader += String.Format("<td> Moisture = {0}%</td></tr>", Int16.Parse(values[CSV.SOIL1]));
                      
            soilHeader += String.Format("<tr><td> {0}:</td> ", unitNames[1]);
            soilHeader += String.Format(" <td>Moisture = {0}%</td></tr>", Int16.Parse(values[CSV.SOIL2]));
          
            soilHeader += String.Format("<tr><td> {0}:</td> ", unitNames[2]);
            soilHeader += String.Format("<td> Moisture = {0}%</td></tr>", Int16.Parse(values[CSV.SOIL3]));
         
            soilHeader += String.Format("<tr><td> {0}:</td> ", unitNames[3]);
            soilHeader += String.Format("<td> Moisture = {0}%</td></tr>", Int16.Parse(values[CSV.SOIL4]));
            soilHeader += String.Format("</table>");

            // add data to soil history file if quarter to hour
            if (Int16.Parse(values[CSV.MINUTE]) == 45)
            {
                try
                {   // check number of lines in file
                    var lines = File.ReadAllLines(@soilHistoryFile);
                    if (lines.Count() >= SOIL_LINES_COUNT_MAX)
                    {  // need to remove first line to maintain size of file
                        File.WriteAllLines(@soilHistoryFile, lines.Skip(1).ToArray());
                    }

                    string header = String.Format("<tr><th width =\"10%\">Date</th ><th width =\"10%\">{0}</th >" +
                        "<th width = \"10%\">{1}</th ><th width = \"10%\">{2}</th ><th width =\"10%\" >{3}</th ></tr >",
                        unitNames[0], unitNames[1], unitNames[2], unitNames[3]);
                    File.WriteAllText(soilHistoryHeader, header);

                    string temp = String.Format("<tr><td>{0:00}/{1:00}/{2}  {3:00}:{4:00}<td>{5}%</td><td>{6}%</td><td>{7}%</td><td>{8}%</td></tr>",
                        values[CSV.DAY], values[CSV.MONTH].Trim(), values[CSV.YEAR].Trim(),
                        values[CSV.HOUR].Trim(), Int16.Parse(values[CSV.MINUTE]),
                        Int16.Parse(values[CSV.SOIL1]), Int16.Parse(values[CSV.SOIL2]),
                        Int16.Parse(values[CSV.SOIL3]), Int16.Parse(values[CSV.SOIL4])
                        );
                    File.AppendAllText(@soilHistoryFile, temp + Environment.NewLine);
                }
                catch (Exception e)
                {
                    Console.WriteLine("soil history error: " + e.Message);
                }
            }

            // complete the result string
            string result = String.Format(" {0:##}% ", Int16.Parse(values[CSV.SOIL1])); //  moisture
            return result;
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

    public static class CSV
    {
        public const int DAY = 0;
        public const int MONTH = 1;
        public const int YEAR = 2;
        public const int HOUR= 3;
        public const int MINUTE = 4;
        public const int V1 = 5;
        public const int V2 = 6;
        public const int V3 = 7;
        public const int C1 = 8;
        public const int mAH = 9;
        public const int C1PEAK = 10;
        public const int GAP_DAY = 11;
        public const int SOIL1 = 12;
        public const int SOIL2 = 13;
        public const int SOIL3 = 14;
        public const int SOIL4 = 15;
        public const int SOIL5 = 16;
        public const int SOIL6 = 17;
        public const int SOIL7 = 18;
    }
    public static class current
    {
        public const int DAY = 0;
        public const int MONTH = 1;
        public const int YEAR = 2;
        public const int HOUR = 3;
        public const int MINUTE = 4;
        public const int COUNT = 5;
        public const int V1 = 6;
        public const int V2 = 7;
        public const int V3 = 8;
        public const int mAH = 9;
        public const int Current = 10;
    }
}
