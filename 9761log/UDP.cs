using System;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Linq;

namespace CarBatteryLog
{
    partial class Program
    {
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
        static void SendUdp(int srcPort, string dstIp, int dstPort, byte[] data)
        {   // sends a udp packet
            using (UdpClient c = new UdpClient(srcPort))
                c.Send(data, data.Length, dstIp, dstPort);
        }

        static string makePrintString(string csvData)
        {   // returns the formatted string         
            string[] values = csvData.Split(',');

            // start the flash header string
            flashHeader = "at ";

            // create the result string    
            string result = String.Format("{0,2}/{1}/{2} ", values[CSV.DAY], values[CSV.MONTH].Trim(),
                                            values[CSV.YEAR].Trim());     // day/ month/ year
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
            flashHeader += String.Format(" Charge level = {0}%", calculatePercentage(Int16.Parse(values[CSV.V1])));
            flashHeader += String.Format("</br>Solar current = {0,6:0.0}mA", (Int16.Parse(values[CSV.C1]) / 10.0));
            flashHeader += String.Format(" Peak Solar current = {0,6:0.0}mA", (Int16.Parse(values[CSV.C1PEAK]) / 10.0));
            flashHeader += String.Format("</br>Today's charge = {0,4:####}mAH", values[CSV.mAH].ToString().Trim());
            flashHeader += String.Format(" Running average over " + DAYS.ToString() + " days = " + averagemAH.ToString() + "mAH");

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
                                        
            averagemAH = calculateAverage(Int16.Parse(values[CSV.DAY]));

            //check for gap due to car data not being received
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

                updateThisMonthFile(oldValues);

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
        static int calculatePercentage(int volts)
        {    // calculates the % charge level for volts, using a quadratic function
            if (volts > 1280)
                return 100; // fully charged

            double voltage = volts / 100.0;
            double coeff2 = -0.8236;
            double coeff1 = 21.471;
            double coeff0 = -138.85;

            double result = coeff2 * voltage * voltage + coeff1 * voltage + coeff0;

            if (result > 1)
                result = 1;
            if (result < 0)
                result = 0;

            return (int)(100 * result);
        }

        // end of class
    }
    // end of namespace
}
