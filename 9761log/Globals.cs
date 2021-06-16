

namespace CarBatteryLog
{
    partial class Program
    {
        private const int listenPort1 = 40030;
        private const int responsePort = 40031;
        private const int currentPort = 40032;

        const string directory = "N:/CarBatteryLog/temp/";
        const string logFile = "logCarBattery.csv";     // name of log file - must exist already to be used
        const string todayFile = "today.txt";           // name of web page include file
        const string yesterdayFile = "yesterday.txt";   // name of web page include file
        const string combinedFile = "combined.txt";      // name of web page include file
        const string flashFile = "flashFile.txt";       // file that contains just the latest data
        const string thisMonthFile = "thisMonth.txt";   // file that contains the charge data for this month
        const string lastMonthFile = "lastMonth.txt";   // file that contains the charge data for last month
        const string thisMonthHeaderFile = directory + "thisMonthHeader.txt"; // table header for this month page
                                                        //  const string soilMonthFile = "soilMonth.txt";   // file that contains the soil data for this month
        const string soilHeaderFile = "soilHeader.txt";   // file that contains the soil latest data
        const string soilHistoryHeader = "soilHistoryHeader.txt"; // header file for the web page body
        const string soilHistoryFile = "soilHistory.txt";   // file that contains the soil latest data
        const int SOIL_LINES_COUNT_MAX = 24;            // maximum number of lines in the soil history file, 24 = one day
        const string broadcastAddress = "255.255.255.255";

        static bool newDay = false;         // gets set in newDayNewMonthCheck 
        static bool newMonth = false;       // gets set in newDayNewMonthCheck 
        static string flashHeader = "";     // gets set in makePrintString 
        static string dayRecord = "";       // gets set in newDayNewMonthCheck 
        static string soilHeader = "";     // gets set in makeSoilString 
        const int noOfUnits = 5;            // the number of soil monitoring units
        static string[] unitNames = new string[noOfUnits + 1];

        static bool gapDay = false;         // set for a day if there is a gap in the record
        static int gapCount = 0;            // the number of gaps in the current record    

        const int DAYS = 5;
        static int averagemAH = 0;
    }

    public static class CSV
    {
        public const int DAY = 0;
        public const int MONTH = 1;
        public const int YEAR = 2;
        public const int HOUR = 3;
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
