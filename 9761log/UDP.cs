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
            {   // get the message and fill out the source:
                byte[] message = socket.EndReceive(result, ref source);
                // convert to string then split to get the current values
                var csvData = System.Text.Encoding.Default.GetString(message);
                string[] newValues = csvData.Split(',');

                string[] oldValues = readLastCsvLineValues();  // used to check for repeats, new days/months and for updating the this month file

                // check for new day and new month, set gapCount and update oldValues
                if (validNewData(newValues, oldValues)) 
                {   // set the new day and new month flags
                    newDayNewMonthCheck(newValues, oldValues);
                    averagemAH = calculateAverage(Int16.Parse(newValues[CSV.DAY]));

                    log(csvData);       // valid data so write raw data to the CSV log file                    

                    if (newDay)
                        updateThisMonthFile(oldValues);     // write the last line of yesterday to the this month file

                    // add latest row to web page
                    String dataLineString = makePrintString(newValues);
                    makeSoilStringAndAddToFile(newValues); // create the soil moisture strings
                    updateWebPage(dataLineString);
                    // write a formatted version to console
                    Console.WriteLine(dataLineString);
                }
                // send a response to show received
                if (!supressUDPresponse)
                    SendUdp(responsePort, broadcastAddress, responsePort, Encoding.ASCII.GetBytes("Cstring"));
            }
            catch (Exception e)
            {
                Console.WriteLine("UDP Rx error: " + e.Message);  
            }

            // schedule the next receive operation once reading is done:
            socket.BeginReceive(new AsyncCallback(OnUdpData40030), socket);
        }
        static void SendUdp(int srcPort, string dstIp, int dstPort, byte[] data)
        {   // sends a udp packet
            using (UdpClient c = new UdpClient(srcPort))
                c.Send(data, data.Length, dstIp, dstPort);
        }

        static string makePrintString(string[] values)
        {   // returns a formatted string from the latest csv data       

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

        static void newDayNewMonthCheck(string[] values, string[] oldValues)
        {   // sets the flags newDay, New month, the value of gapCount            

            // set the new day and month flags by comparing the new values
            // with the last line of the csv file
            newDay = (Int16.Parse(values[CSV.DAY]) != Int16.Parse(oldValues[CSV.DAY]));
            newMonth = (Int16.Parse(values[CSV.MONTH]) != Int16.Parse(oldValues[CSV.MONTH]));
            if (newMonth)
                newDay = true;          // new month must be new day, even if same day number!

            //check for gap due to car data not being received
            checkForGap(values, oldValues); // sets gapFlag and gapCount                                         
        }

        private static bool validNewData(string[] values, string[] oldValues)
        {   // returns true if data is new and valid
            if (Int16.Parse(values[CSV.DAY]) * Int16.Parse(values[CSV.MONTH]) * Int16.Parse(values[CSV.YEAR]) == 0)
                return false;     // valid day month or year can't be zero

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

            return true;
        }

        private static string[] readLastCsvLineValues()
        {   // get the last line of the csv file
            string[] values = null;
            try
            {
                string lastLine = File.ReadLines(@"logCarBattery.csv").Last();
                values = lastLine.Split(',');       // also used to update thisMonth file
                if (values[0] == "")
                {
                    Console.WriteLine("Last line of csv is blank");
                    newDay = newMonth = false; 
                }                
            }
            catch (Exception e)
            {
                Console.WriteLine("CSV file read error: " + e.Message);
            }
            
            return values; 
        }

        private static void checkForGap(string[] values, string[] oldValues)
        {
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
