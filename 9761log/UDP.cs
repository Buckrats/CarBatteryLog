using System;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;

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
        // end of class
    }
    // end of namespace
}
