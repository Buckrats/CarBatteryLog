using System;
using System.Linq;
using System.IO;

namespace CarBatteryLog
{
    partial class Program
    {
        static string makeSoilStringAndAddToFile(string csvData)
        {   // returns the formatted string         
            string[] values = csvData.Split(',');

            // create the soil header string
            soilHeader = String.Format("at {0,2}/{1}/{2} ", values[CSV.DAY], values[CSV.MONTH].Trim(),
                                            values[CSV.YEAR].Trim());     // day/ month/ year
            if (Int16.Parse(values[CSV.MONTH]) < 10)    // check months, add space if necessary
                soilHeader += " ";
            soilHeader += String.Format("{0,2}:", values[CSV.HOUR].Trim()); // hours
            soilHeader += String.Format("{0:00}", Int16.Parse(values[CSV.MINUTE])); // minutes            

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
            if (Int16.Parse(values[CSV.MINUTE]) >= 45)
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
    }
}
