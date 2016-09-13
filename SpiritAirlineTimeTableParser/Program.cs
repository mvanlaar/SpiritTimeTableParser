﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using PDFReader;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Data.SqlClient;
using System.Data;
using CsvHelper;
using Newtonsoft.Json.Linq;
using System.IO.Compression;

namespace SpiritAirlineTimeTableParser
{
    class Program
    {
        //public static readonly List<string> _SkyTeamAircraftCode = new List<string>() { "100", "313", "319", "321", "330", "333", "343", "346", "388", "733", "735", "310", "318", "320", "32S", "332", "340", "345", "380", "717", "734", "736", "737", "739", "73G", "73J", "73W", "747", "74M", "753", "75W", "763", "767", "772", "777", "77W", "788", "AB6", "AT5", "ATR", "CR2", "CR9", "CRK", "E70", "E90", "EM2", "EQV", "ER4", "F50", "M11", "M90", "SF3", "738", "73C", "73H", "73R", "744", "74E", "752", "757", "762", "764", "76W", "773", "77L", "787", "A81", "AR8", "AT7", "BUS", "CR7", "CRJ", "DH4", "E75", "E95", "EMJ", "ER3", "ERJ", "F70", "M88", "S20", "SU9" };
        public static readonly List<string> _ColombiaAirports = new List<string>() { "APO", "AUC", "AXM", "BSC", "EJA", "BAQ", "BOG", "BGA", "BUN", "CLO", "CTG", "CRC", "CZU", "CUC", "EYP", "FLA", "GIR", "GPI", "IBE", "LET", "MZL", "MQU", "EOH", "MDE", "MVP", "MTR", "NVA", "PSO", "PEI", "PPN", "PVA", "PUU", "PCR", "UIB", "RCH", "ADZ", "SJE", "SVI", "SMR", "RVE", "TME", "TLU", "TCO", "VUP", "VVC", "ACD", "AFI", "ACR", "ARQ", "NBB", "CPB", "CCO", "CUO", "CAQ", "CPL", "IGO", "CIM", "COG", "RAV", "BHF", "EBG", "ELB", "ECR", "LGT", "HTZ", "IPI", "JUO", "LMC", "LPD", "LPE", "MGN", "MCJ", "MFS", "MMP", "MTB", "NCI", "NQU", "OCV", "ORC", "RON", "PZA", "PTX", "PLT", "PBE", "PDA", "LQM", "NAR", "OTU", "SNT", "AYG", "SSL", "SOX", "TTM", "TCD", "TIB", "TBD", "TDA", "TRB", "URI", "URR", "VGZ", "LCR", "SQE", "SRS", "ULQ", "CVE", "PAL", "PYA", "TQS", "API" };

        public class IATAAirport
        {
            public string stop_id;
            public string stop_name;
            public string stop_desc;
            public string stop_lat;
            public string stop_lon;
            public string zone_id;
            public string stop_url;
        }

        static void Main(string[] args)
        {

            // https://www.spirit.com/content/documents/en-us/timetable06AUG2015.pdf

            // Downlaoding latest pdf from skyteam website
            string myDirpath = AppDomain.CurrentDomain.BaseDirectory + "\\data";
            Directory.CreateDirectory(myDirpath);
            string path = AppDomain.CurrentDomain.BaseDirectory + "data\\Spirit_Timetable.pdf";
            Uri url = new Uri("https://www.spirit.com/content/documents/en-us/timetable08SEP2016.pdf");
            const string ua = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)";
            const string referer = "https://www.spirit.com/RouteMaps.aspx";
            if (File.Exists(path))
            {
                WebRequest.DefaultWebProxy = null;
                using (System.Net.WebClient wc = new WebClient())
                {
                    wc.Headers.Add("user-agent", ua);
                    wc.Headers.Add("Referer", referer);
                    wc.Proxy = null;
                    Console.WriteLine("Downloading latest Spirit Airlines timetable pdf file...");
                    wc.DownloadFile(url, path);
                    Console.WriteLine("Download ready...");
                }
            }
            var text = new StringBuilder();
            CultureInfo ci = new CultureInfo("en-US");

            Regex rgxtime = new Regex(@" *(1[0-2]|[1-9]):([0-5][0-9])(a|p|A|P)");
            Regex rgxFlightNumber = new Regex(@"(\d{3,4})");
            Regex rgxIATAAirport = new Regex(@"\[[A-Z]{3}\]");
            Regex rgxdate1 = new Regex(@"(([0-9])|([0-2][0-9])|([3][0-1])) (Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)");
            Regex rgxdate2 = new Regex(@"Effective (?:(((Jan(uary)?|Ma(r(ch)?|y)|Jul(y)?|Aug(ust)?|Oct(ober)?|Dec(ember)?)\ 31)|((Jan(uary)?|Ma(r(ch)?|y)|Apr(il)?|Ju((ly?)|(ne?))|Aug(ust)?|Oct(ober)?|(Sept|Nov|Dec)(ember)?)\ (0?[1-9]|([12]\d)|30))|(Feb(ruary)?\ (0?[1-9]|1\d|2[0-8]|(29(?=,\ ((1[6-9]|[2-9]\d)(0[48]|[2468][048]|[13579][26])|((16|[2468][048]|[3579][26])00)))))))\,\ ((1[6-9]|[2-9]\d)\d{2}))");
            Regex rgxFlightDay = new Regex(@"[1234567]");
            Regex rgxFlightTime = new Regex(@"^([0-9]|0[0-9]|1[0-9]|2[0-3])H([0-9]|0[0-9]|1[0-9]|2[0-9]|3[0-9]|4[0-9]|5[0-9])M$");
            List<CIFLight> CIFLights = new List<CIFLight> { };
            List<Rectangle> rectangles = new List<Rectangle>();

            //rectangles.Add(new Rectangle(x+(j*offset), (y+i*offset), offset, offset));
            float distanceInPixelsFromLeft = 0;
            float distanceInPixelsFromBottom = 92;
            float width = 275;//pdfReader.GetPageSize(page).Width / 2;
            float height = 700; // pdfReader.GetPageSize(page).Height;
            // Format Paper
            // Letter		 612x792
            // A4		     595x842

            var firstpage = new Rectangle(
                        distanceInPixelsFromLeft,
                        distanceInPixelsFromBottom,
                        width,
                        height);

            var left = new Rectangle(
                        distanceInPixelsFromLeft,
                        distanceInPixelsFromBottom,
                        width,
                        height);

            var right = new Rectangle(
                       307,
                       distanceInPixelsFromBottom,
                       612,
                       height);


            rectangles.Add(left);
            rectangles.Add(right);

            // The PdfReader object implements IDisposable.Dispose, so you can
            // wrap it in the using keyword to automatically dispose of it
            Console.WriteLine("Opening PDF File...");

            //PdfReader reader = new PdfReader(path);


            using (var pdfReader = new PdfReader(path))
            {

                // Parsing first page for valid from and to dates.                

                ITextExtractionStrategy fpstrategy = new SimpleTextExtractionStrategy();

                var fpcurrentText = PdfTextExtractor.GetTextFromPage(
                    pdfReader,
                    1,
                    fpstrategy);

                fpcurrentText =
                    Encoding.UTF8.GetString(Encoding.Convert(
                        Encoding.Default,
                        Encoding.UTF8,
                        Encoding.Default.GetBytes(fpcurrentText)));

                MatchCollection matches = rgxdate2.Matches(fpcurrentText);

                string validfrom = matches[0].Value;
                validfrom = validfrom.Replace("Effective ", "");
                //string validto = matches[1].Value;
                DateTime ValidFrom = DateTime.ParseExact(validfrom, "MMMM d, yyyy", ci);
                // Spirit airlines to date is around a month valid
                DateTime ValidTo = ValidFrom.AddDays(90);

                // To And From Can continue on the other page; So do not reset when parsing new page. 
                string TEMP_FromIATA = null;
                string TEMP_ToIATA = null;


                // Loop through each page of the document
                for (var page = 2; page <= pdfReader.NumberOfPages; page++)
                {

                    Console.WriteLine("Parsing page {0}...", page);

                    foreach (Rectangle rect in rectangles)
                    {
                        ITextExtractionStrategy its = new CSVTextExtractionStrategy();
                        var filters = new RenderFilter[1];
                        filters[0] = new RegionTextRenderFilter(rect);

                        ITextExtractionStrategy strategy =
                            new FilteredTextRenderListener(
                                new CSVTextExtractionStrategy(), // new LocationTextExtractionStrategy()
                                filters);

                        var currentText = PdfTextExtractor.GetTextFromPage(
                            pdfReader,
                            page,
                            strategy);

                        currentText =
                            Encoding.UTF8.GetString(Encoding.Convert(
                                Encoding.Default,
                                Encoding.UTF8,
                                Encoding.Default.GetBytes(currentText)));

                        string[] lines = Regex.Split(currentText, "\r\n");                        
                        DateTime TEMP_ValidFrom = ValidFrom;
                        DateTime TEMP_ValidTo = ValidTo;
                        int TEMP_Conversie = 0;
                        Boolean TEMP_FlightMonday = false;
                        Boolean TEMP_FlightTuesday = false;
                        Boolean TEMP_FlightWednesday = false;
                        Boolean TEMP_FlightThursday = false;
                        Boolean TEMP_FlightFriday = false;
                        Boolean TEMP_FlightSaterday = false;
                        Boolean TEMP_FlightSunday = false;
                        DateTime TEMP_DepartTime = new DateTime();
                        DateTime TEMP_ArrivalTime = new DateTime();
                        Boolean TEMP_FlightCodeShare = false;
                        string TEMP_FlightNumber = null;
                        string TEMP_Aircraftcode = null;
                        TimeSpan TEMP_DurationTime = TimeSpan.MinValue;
                        Boolean TEMP_FlightNextDayArrival = false;
                        int TEMP_FlightNextDays = 0;
                        Boolean TEMP_FlightDirect = true;

                        foreach (string line in lines)
                        {
                            string[] values = line.SplitWithQualifier(',', '\"', true);

                            foreach (string value in values)
                            {
                                if (!String.IsNullOrEmpty(value.Trim()))
                                {
                                    // Trim the string
                                    string temp_string = value.Trim();
                                    // From and To
                                    if (rgxIATAAirport.Matches(temp_string).Count > 0)
                                    {
                                        if (temp_string.Contains("FROM"))
                                        {
                                            string airport = rgxIATAAirport.Match(temp_string).Groups[0].Value;
                                            airport = airport.Replace("[", "");
                                            airport = airport.Replace("]", "");
                                            TEMP_FromIATA = airport;
                                        }
                                        else
                                        {
                                            if (temp_string.Contains("TO"))
                                            {
                                                string airport = rgxIATAAirport.Match(temp_string).Groups[0].Value;
                                                airport = airport.Replace("[", "");
                                                airport = airport.Replace("]", "");
                                                TEMP_ToIATA = airport;
                                            }
                                        }
                                    }                                    
                                    // Depart and arrival times
                                    if (rgxtime.Matches(temp_string).Count > 0)
                                    {
                                        //if (rgxtime.Matches(temp_string).Count == 2)
                                        //{
                                        // Contains to and from date.
                                        foreach (Match ItemMatch in rgxtime.Matches(temp_string))
                                        {
                                            if (TEMP_DepartTime == DateTime.MinValue)
                                            {
                                                // Time Parsing 
                                                string y = ItemMatch.Value;
                                                y = y.ToUpper();
                                                y = y.Trim();
                                                TEMP_DepartTime = DateTime.ParseExact(y, "h:mmt", ci);
                                            }
                                            else
                                            {
                                                // There is a from value so this is to.
                                                string x = ItemMatch.Value;
                                                x = x.ToUpper();
                                                x = x.Trim();
                                                if (x.Contains("+1"))
                                                {
                                                    // Next day arrival
                                                    x = x.Replace("+1", "");
                                                    TEMP_FlightNextDays = 1;
                                                    TEMP_FlightNextDayArrival = true;
                                                }
                                                if (x.Contains("+2"))
                                                {
                                                    // Next day arrival
                                                    x = x.Replace("+2", "");
                                                    TEMP_FlightNextDays = 2;
                                                    TEMP_FlightNextDayArrival = true;
                                                }
                                                if (x.Contains("+-1"))
                                                {
                                                    // Next day arrival
                                                    x = x.Replace("+-1", "");
                                                    TEMP_FlightNextDays = -1;
                                                    TEMP_FlightNextDayArrival = true;
                                                }
                                                //DateTime.TryParse(x.Trim(), out TEMP_ArrivalTime);
                                                TEMP_ArrivalTime = DateTime.ParseExact(x, "h:mmt", ci);
                                            }
                                        }                                        
                                    }
                                    // FlightNumber Parsing
                                    if (rgxFlightNumber.IsMatch(temp_string) && TEMP_ArrivalTime != DateTime.MinValue && TEMP_FlightMonday == false && TEMP_FlightTuesday == false && TEMP_FlightWednesday == false && TEMP_FlightThursday == false && TEMP_FlightFriday == false && TEMP_FlightSaterday == false && TEMP_FlightSunday == false)
                                    {
                                        // Extra check for SU9 flight number and Aircraft Type
                                        if (TEMP_FlightNumber == null)
                                        {
                                            TEMP_FlightNumber = temp_string;
                                            if (temp_string.Contains("*"))
                                            {
                                                TEMP_FlightCodeShare = true;
                                                TEMP_FlightNumber = TEMP_FlightNumber.Replace("*", "");
                                            }
                                        }
                                    }
                                    // Parsing flightdays
                                    if ((rgxFlightDay.Matches(temp_string).Count > 0 || temp_string.Contains("Daily")) && TEMP_FlightNumber != null && TEMP_FlightNumber != temp_string)
                                    {
                                        // Flight days found!
                                        if (temp_string.Contains("Daily"))
                                        {
                                            // all days
                                            TEMP_FlightSunday = true;
                                            TEMP_FlightMonday = true;
                                            TEMP_FlightTuesday = true;
                                            TEMP_FlightWednesday = true;
                                            TEMP_FlightThursday = true;
                                            TEMP_FlightFriday = true;
                                            TEMP_FlightSaterday = true;
                                        }
                                        else
                                        {
                                            if (temp_string.StartsWith("X"))
                                            {
                                                // Alle days exept.
                                                TEMP_FlightSunday = true;
                                                TEMP_FlightMonday = true;
                                                TEMP_FlightTuesday = true;
                                                TEMP_FlightWednesday = true;
                                                TEMP_FlightThursday = true;
                                                TEMP_FlightFriday = true;
                                                TEMP_FlightSaterday = true;
                                                foreach (Match ItemMatch in rgxFlightDay.Matches(temp_string))
                                                {
                                                    int.TryParse(ItemMatch.Value, out TEMP_Conversie);
                                                    if (TEMP_Conversie == 1) { TEMP_FlightMonday = false; }
                                                    if (TEMP_Conversie == 2) { TEMP_FlightTuesday = false; }
                                                    if (TEMP_Conversie == 3) { TEMP_FlightWednesday = false; }
                                                    if (TEMP_Conversie == 4) { TEMP_FlightThursday = false; }
                                                    if (TEMP_Conversie == 5) { TEMP_FlightFriday = false; }
                                                    if (TEMP_Conversie == 6) { TEMP_FlightSaterday = false; }
                                                    if (TEMP_Conversie == 7) { TEMP_FlightSunday = false; }
                                                }
                                            }
                                            else
                                            {
                                                foreach (Match ItemMatch in rgxFlightDay.Matches(temp_string))
                                                {
                                                    int.TryParse(ItemMatch.Value, out TEMP_Conversie);
                                                    if (TEMP_Conversie == 1) { TEMP_FlightMonday = true; }
                                                    if (TEMP_Conversie == 2) { TEMP_FlightTuesday = true; }
                                                    if (TEMP_Conversie == 3) { TEMP_FlightWednesday = true; }
                                                    if (TEMP_Conversie == 4) { TEMP_FlightThursday = true; }
                                                    if (TEMP_Conversie == 5) { TEMP_FlightFriday = true; }
                                                    if (TEMP_Conversie == 6) { TEMP_FlightSaterday = true; }
                                                    if (TEMP_Conversie == 7) { TEMP_FlightSunday = true; }
                                                }
                                            }
                                        }
                                    }
                                    //// Aircraft parsing

                                    //if (temp_string.Length == 3)
                                    //{
                                    //    if (_SkyTeamAircraftCode.Contains(temp_string, StringComparer.OrdinalIgnoreCase))
                                    //    {
                                    //        if (TEMP_Aircraftcode == null)
                                    //        {
                                    //            TEMP_Aircraftcode = temp_string;
                                    //        }
                                    //    }
                                    //}
                                    // Stops?
                                    if (temp_string == "0" || temp_string == "1")
                                    {
                                        if (temp_string == "0")
                                        {
                                            TEMP_FlightDirect = true;
                                        }
                                        else
                                        {
                                            // check if all flightdays are filled in.
                                            if ((TEMP_FlightMonday == true || TEMP_FlightMonday == false) && (TEMP_FlightTuesday == true || TEMP_FlightTuesday == false) && (TEMP_FlightWednesday == true || TEMP_FlightWednesday == false) && (TEMP_FlightThursday == true || TEMP_FlightThursday == false) && (TEMP_FlightFriday == true || TEMP_FlightFriday == false) && (TEMP_FlightSaterday == true || TEMP_FlightSaterday == false) && (TEMP_FlightSunday == true || TEMP_FlightSunday == false))
                                            {
                                                // Days are filled so this is a stop
                                                TEMP_FlightDirect = false;
                                            }
                                        }
                                        // Last Column
                                        if (_ColombiaAirports.Contains(TEMP_FromIATA, StringComparer.OrdinalIgnoreCase) & !_ColombiaAirports.Contains(TEMP_ToIATA, StringComparer.OrdinalIgnoreCase) || (_ColombiaAirports.Contains(TEMP_ToIATA, StringComparer.OrdinalIgnoreCase) & !_ColombiaAirports.Contains(TEMP_FromIATA, StringComparer.OrdinalIgnoreCase)) || (_ColombiaAirports.Contains(TEMP_ToIATA, StringComparer.OrdinalIgnoreCase) & _ColombiaAirports.Contains(TEMP_FromIATA, StringComparer.OrdinalIgnoreCase)))
                                        {
                                            CIFLights.Add(new CIFLight
                                            {
                                                FromIATA = TEMP_FromIATA,
                                                ToIATA = TEMP_ToIATA,
                                                FromDate = TEMP_ValidFrom,
                                                ToDate = TEMP_ValidTo,
                                                ArrivalTime = TEMP_ArrivalTime,
                                                DepartTime = TEMP_DepartTime,
                                                FlightAircraft = @"Airbus A319/A320",
                                                FlightAirline = @"NK",
                                                FlightMonday = TEMP_FlightMonday,
                                                FlightTuesday = TEMP_FlightTuesday,
                                                FlightWednesday = TEMP_FlightWednesday,
                                                FlightThursday = TEMP_FlightThursday,
                                                FlightFriday = TEMP_FlightFriday,
                                                FlightSaterday = TEMP_FlightSaterday,
                                                FlightSunday = TEMP_FlightSunday,
                                                FlightNumber = TEMP_FlightNumber,
                                                FlightOperator = "NK",
                                                FlightDuration = null,
                                                FlightCodeShare = TEMP_FlightCodeShare,
                                                FlightNextDayArrival = TEMP_FlightNextDayArrival,
                                                FlightNextDays = TEMP_FlightNextDays,
                                                FlightDirect = TEMP_FlightDirect
                                            });
                                            // Cleaning All but From and To 
                                            TEMP_ValidFrom = ValidFrom;
                                            TEMP_ValidTo = ValidTo;
                                            TEMP_Conversie = 0;
                                            TEMP_FlightMonday = false;
                                            TEMP_FlightTuesday = false;
                                            TEMP_FlightWednesday = false;
                                            TEMP_FlightThursday = false;
                                            TEMP_FlightFriday = false;
                                            TEMP_FlightSaterday = false;
                                            TEMP_FlightSunday = false;
                                            TEMP_DepartTime = new DateTime();
                                            TEMP_ArrivalTime = new DateTime();
                                            TEMP_FlightNumber = null;
                                            TEMP_Aircraftcode = null;
                                            TEMP_DurationTime = TimeSpan.MinValue;
                                            TEMP_FlightCodeShare = false;
                                            TEMP_FlightNextDayArrival = false;
                                            TEMP_FlightNextDays = 0;
                                            TEMP_FlightDirect = true;
                                        }
                                    }

                                    //if (TEMP_Aircraftcode != null && rgxFlightTime.Matches(temp_string).Count > 0)
                                    //{
                                    //    // Aircraft code has been found so this has to be the flighttimes. and so the last value of the string...
                                    //    int intFlightTimeH = 0;
                                    //    int intFlightTimeM = 0;
                                    //    var match = rgxFlightTime.Match(temp_string);
                                    //    intFlightTimeH = int.Parse(match.Groups[1].Value);
                                    //    intFlightTimeM = int.Parse(match.Groups[2].Value);
                                    //    TEMP_DurationTime = new TimeSpan(0, intFlightTimeH, intFlightTimeM, 0);
                                    //    //int rgxFlightTimeH = rgxFlightTime.Match(temp_string).Groups[0].Value
                                    //    //TEMP_DurationTime = DateTime.ParseExact(temp_string, "HH\H mm \M", null);
                                    //    string TEMP_Airline = null;
                                    //    if (TEMP_Aircraftcode == "BUS") { TEMP_Airline = null; }
                                    //    else { TEMP_Airline = TEMP_FlightNumber.Substring(0, 2); }


                                    //}
                                    //if (temp_string.Contains("EFF ") || temp_string.Contains("DIS "))
                                    //{
                                    //    // custom from and to dates
                                    //    if (temp_string.Contains("EFF "))
                                    //    {
                                    //        temp_string = temp_string.Replace("EFF ", "");
                                    //        //TEMP_ValidFrom = DateTime.ParseExact(temp_string.Trim(), "MM/dd/YY", ci);
                                    //        CIFLights[CIFLights.Count - 1].FromDate = DateTime.ParseExact(temp_string.Trim(), "MM/dd/yy", ci);
                                    //        CIFLights[CIFLights.Count - 1].ToDate = ValidTo;
                                    //    }

                                    //    if (temp_string.Contains("DIS "))
                                    //    {
                                    //        temp_string = temp_string.Replace("DIS ", "");
                                    //        CIFLights[CIFLights.Count - 1].ToDate = DateTime.ParseExact(temp_string.Trim(), "MM/dd/yy", ci);
                                    //        CIFLights[CIFLights.Count - 1].FromDate = ValidFrom;
                                    //    }
                                    //}
                                    //if (temp_string.Contains("Operated by: "))
                                    //{
                                    //    // Ok, this has to be added to the last record.
                                    //    CIFLights[CIFLights.Count - 1].FlightOperator = temp_string.Replace("Operated by: ", "").Trim();
                                    //    CIFLights[CIFLights.Count - 1].FlightCodeShare = true;
                                    //}
                                    //if (temp_string.Equals("Consult your travel agent for details"))
                                    //{
                                    //    TEMP_ToIATA = null;
                                    //    TEMP_FromIATA = null;
                                    //    TEMP_ValidFrom = new DateTime();
                                    //    TEMP_ValidTo = new DateTime();
                                    //    TEMP_Conversie = 0;
                                    //    TEMP_FlightMonday = false;
                                    //    TEMP_FlightTuesday = false;
                                    //    TEMP_FlightWednesday = false;
                                    //    TEMP_FlightThursday = false;
                                    //    TEMP_FlightFriday = false;
                                    //    TEMP_FlightSaterday = false;
                                    //    TEMP_FlightSunday = false;
                                    //    TEMP_DepartTime = new DateTime();
                                    //    TEMP_ArrivalTime = new DateTime();
                                    //    TEMP_FlightNumber = null;
                                    //    TEMP_Aircraftcode = null;
                                    //    TEMP_DurationTime = TimeSpan.MinValue;
                                    //    TEMP_FlightCodeShare = false;
                                    //    TEMP_FlightNextDayArrival = false;
                                    //    TEMP_FlightNextDays = 0;
                                    //    TEMP_FlightDirect = true;

                                    //}
                                    Console.WriteLine(value);
                                }
                            }
                        }

                    }
                }
            }

            // You'll do something else with it, here I write it to a console window
            // Console.WriteLine(text.ToString());

            // Write the list of objects to a file.
            System.Xml.Serialization.XmlSerializer writer =
            new System.Xml.Serialization.XmlSerializer(CIFLights.GetType());
            string myDir = AppDomain.CurrentDomain.BaseDirectory + "\\output";
            Directory.CreateDirectory(myDir);
            StreamWriter file =
               new System.IO.StreamWriter("output\\output.xml");

            writer.Serialize(file, CIFLights);
            file.Close();

            string gtfsDir = AppDomain.CurrentDomain.BaseDirectory + "\\gtfs";
            System.IO.Directory.CreateDirectory(gtfsDir);

            Console.WriteLine("Reading IATA Airports....");
            string IATAAirportsFile = AppDomain.CurrentDomain.BaseDirectory + "IATAAirports.json";
            JArray o1 = JArray.Parse(File.ReadAllText(IATAAirportsFile));
            IList<IATAAirport> TempIATAAirports = o1.ToObject<IList<IATAAirport>>();
            var IATAAirports = TempIATAAirports as List<IATAAirport>;

            Console.WriteLine("Creating GTFS Files...");

            Console.WriteLine("Creating GTFS File agency.txt...");
            using (var gtfsagency = new StreamWriter(@"gtfs\\agency.txt"))
            {
                var csv = new CsvWriter(gtfsagency);
                csv.Configuration.Delimiter = ",";
                csv.Configuration.Encoding = Encoding.UTF8;
                csv.Configuration.TrimFields = true;
                // header 
                csv.WriteField("agency_id");
                csv.WriteField("agency_name");
                csv.WriteField("agency_url");
                csv.WriteField("agency_timezone");
                csv.WriteField("agency_lang");
                csv.WriteField("agency_phone");
                csv.WriteField("agency_fare_url");
                csv.WriteField("agency_email");
                csv.NextRecord();              
                    
                csv.WriteField("NK");
                csv.WriteField("Spirit Airlines");
                csv.WriteField("http://www.spirit.com/");
                csv.WriteField("America/Bogota");
                csv.WriteField("ES");
                csv.WriteField("");
                csv.WriteField("");
                csv.WriteField("");
                csv.NextRecord();                

                //csv.WriteField("AV");
                //csv.WriteField("Avianca");
                //csv.WriteField("http://www.avianca.com");
                //csv.WriteField("America/Bogota");
                //csv.WriteField("ES");
                //csv.WriteField("");
                //csv.WriteField("");
                //csv.WriteField("");
                //csv.NextRecord();
            }

            Console.WriteLine("Creating GTFS File routes.txt ...");


            using (var gtfsroutes = new StreamWriter(@"gtfs\\routes.txt"))
            {
                // Route record


                var csvroutes = new CsvWriter(gtfsroutes);
                csvroutes.Configuration.Delimiter = ",";
                csvroutes.Configuration.Encoding = Encoding.UTF8;
                csvroutes.Configuration.TrimFields = true;
                // header 
                csvroutes.WriteField("route_id");
                csvroutes.WriteField("agency_id");
                csvroutes.WriteField("route_short_name");
                csvroutes.WriteField("route_long_name");
                csvroutes.WriteField("route_desc");
                csvroutes.WriteField("route_type");
                csvroutes.WriteField("route_url");
                csvroutes.WriteField("route_color");
                csvroutes.WriteField("route_text_color");
                csvroutes.NextRecord();

                var routes = CIFLights.Select(m => new { m.FromIATA, m.ToIATA, m.FlightAirline }).Distinct().ToList();

                for (int i = 0; i < routes.Count; i++) // Loop through List with for)
                {
                    var FromAirportInfo = IATAAirports.Find(q => q.stop_id == routes[i].FromIATA);
                    var ToAirportInfo = IATAAirports.Find(q => q.stop_id == routes[i].ToIATA);

                    csvroutes.WriteField(routes[i].FromIATA + routes[i].ToIATA + "NK");
                    csvroutes.WriteField("NK");
                    csvroutes.WriteField(routes[i].FromIATA + routes[i].ToIATA);
                    csvroutes.WriteField(FromAirportInfo.stop_name + " - " + ToAirportInfo.stop_name);
                    csvroutes.WriteField(""); // routes[i].FlightAircraft + ";" + CIFLights[i].FlightAirline + ";" + CIFLights[i].FlightOperator + ";" + CIFLights[i].FlightCodeShare
                    csvroutes.WriteField(1101);
                    csvroutes.WriteField("");
                    csvroutes.WriteField("");
                    csvroutes.WriteField("");
                    csvroutes.NextRecord();
                }
            }

            List<string> agencyairportsiata =
             CIFLights.SelectMany(m => new string[] { m.FromIATA, m.ToIATA })
                     .Distinct()
                     .ToList();

            using (var gtfsstops = new StreamWriter(@"gtfs\\stops.txt"))
            {
                // Route record
                var csvstops = new CsvWriter(gtfsstops);
                csvstops.Configuration.Delimiter = ",";
                csvstops.Configuration.Encoding = Encoding.UTF8;
                csvstops.Configuration.TrimFields = true;
                // header                                 
                csvstops.WriteField("stop_id");
                csvstops.WriteField("stop_name");
                csvstops.WriteField("stop_desc");
                csvstops.WriteField("stop_lat");
                csvstops.WriteField("stop_lon");
                csvstops.WriteField("zone_id");
                csvstops.WriteField("stop_url");
                csvstops.NextRecord();

                for (int i = 0; i < agencyairportsiata.Count; i++) // Loop through List with for)
                {
                    //int result1 = IATAAirports.FindIndex(T => T.stop_id == 9458)
                    var airportinfo = IATAAirports.Find(q => q.stop_id == agencyairportsiata[i]);
                    csvstops.WriteField(airportinfo.stop_id);
                    csvstops.WriteField(airportinfo.stop_name);
                    csvstops.WriteField(airportinfo.stop_desc);
                    csvstops.WriteField(airportinfo.stop_lat);
                    csvstops.WriteField(airportinfo.stop_lon);
                    csvstops.WriteField(airportinfo.zone_id);
                    csvstops.WriteField(airportinfo.stop_url);
                    csvstops.NextRecord();
                }
            }

            Console.WriteLine("Creating GTFS File trips.txt, stop_times.txt, calendar.txt ...");

            using (var gtfscalendar = new StreamWriter(@"gtfs\\calendar.txt"))
            {
                using (var gtfstrips = new StreamWriter(@"gtfs\\trips.txt"))
                {
                    using (var gtfsstoptimes = new StreamWriter(@"gtfs\\stop_times.txt"))
                    {
                        // Headers 
                        var csvstoptimes = new CsvWriter(gtfsstoptimes);
                        csvstoptimes.Configuration.Delimiter = ",";
                        csvstoptimes.Configuration.Encoding = Encoding.UTF8;
                        csvstoptimes.Configuration.TrimFields = true;
                        // header 
                        csvstoptimes.WriteField("trip_id");
                        csvstoptimes.WriteField("arrival_time");
                        csvstoptimes.WriteField("departure_time");
                        csvstoptimes.WriteField("stop_id");
                        csvstoptimes.WriteField("stop_sequence");
                        csvstoptimes.WriteField("stop_headsign");
                        csvstoptimes.WriteField("pickup_type");
                        csvstoptimes.WriteField("drop_off_type");
                        csvstoptimes.WriteField("shape_dist_traveled");
                        csvstoptimes.WriteField("timepoint");
                        csvstoptimes.NextRecord();

                        var csvtrips = new CsvWriter(gtfstrips);
                        csvtrips.Configuration.Delimiter = ",";
                        csvtrips.Configuration.Encoding = Encoding.UTF8;
                        csvtrips.Configuration.TrimFields = true;
                        // header 
                        csvtrips.WriteField("route_id");
                        csvtrips.WriteField("service_id");
                        csvtrips.WriteField("trip_id");
                        csvtrips.WriteField("trip_headsign");
                        csvtrips.WriteField("trip_short_name");
                        csvtrips.WriteField("direction_id");
                        csvtrips.WriteField("block_id");
                        csvtrips.WriteField("shape_id");
                        csvtrips.WriteField("wheelchair_accessible");
                        csvtrips.WriteField("bikes_allowed ");
                        csvtrips.NextRecord();

                        var csvcalendar = new CsvWriter(gtfscalendar);
                        csvcalendar.Configuration.Delimiter = ",";
                        csvcalendar.Configuration.Encoding = Encoding.UTF8;
                        csvcalendar.Configuration.TrimFields = true;
                        // header 
                        csvcalendar.WriteField("service_id");
                        csvcalendar.WriteField("monday");
                        csvcalendar.WriteField("tuesday");
                        csvcalendar.WriteField("wednesday");
                        csvcalendar.WriteField("thursday");
                        csvcalendar.WriteField("friday");
                        csvcalendar.WriteField("saturday");
                        csvcalendar.WriteField("sunday");
                        csvcalendar.WriteField("start_date");
                        csvcalendar.WriteField("end_date");
                        csvcalendar.NextRecord();

                        //1101 International Air Service
                        //1102 Domestic Air Service
                        //1103 Intercontinental Air Service
                        //1104 Domestic Scheduled Air Service


                        for (int i = 0; i < CIFLights.Count; i++) // Loop through List with for)
                        {

                            // Calender

                            csvcalendar.WriteField(CIFLights[i].FromIATA + CIFLights[i].ToIATA + CIFLights[i].FlightNumber.Replace(" ", "") + String.Format("{0:yyyyMMdd}", CIFLights[i].FromDate) + String.Format("{0:yyyyMMdd}", CIFLights[i].ToDate));
                            csvcalendar.WriteField(Convert.ToInt32(CIFLights[i].FlightMonday));
                            csvcalendar.WriteField(Convert.ToInt32(CIFLights[i].FlightTuesday));
                            csvcalendar.WriteField(Convert.ToInt32(CIFLights[i].FlightWednesday));
                            csvcalendar.WriteField(Convert.ToInt32(CIFLights[i].FlightThursday));
                            csvcalendar.WriteField(Convert.ToInt32(CIFLights[i].FlightFriday));
                            csvcalendar.WriteField(Convert.ToInt32(CIFLights[i].FlightSaterday));
                            csvcalendar.WriteField(Convert.ToInt32(CIFLights[i].FlightSunday));
                            csvcalendar.WriteField(String.Format("{0:yyyyMMdd}", CIFLights[i].FromDate));
                            csvcalendar.WriteField(String.Format("{0:yyyyMMdd}", CIFLights[i].ToDate));
                            csvcalendar.NextRecord();

                            // Trips

                            //var item4 = _Airlines.Find(q => q.Name == CIFLights[i].FlightAirline);
                            string TEMP_IATA = "NK";
                            var FromAirportInfo = IATAAirports.Find(q => q.stop_id == CIFLights[i].FromIATA);
                            var ToAirportInfo = IATAAirports.Find(q => q.stop_id == CIFLights[i].ToIATA);


                            csvtrips.WriteField(CIFLights[i].FromIATA + CIFLights[i].ToIATA + TEMP_IATA);
                            csvtrips.WriteField(CIFLights[i].FromIATA + CIFLights[i].ToIATA + CIFLights[i].FlightNumber.Replace(" ", "") + String.Format("{0:yyyyMMdd}", CIFLights[i].FromDate) + String.Format("{0:yyyyMMdd}", CIFLights[i].ToDate));
                            csvtrips.WriteField(CIFLights[i].FromIATA + CIFLights[i].ToIATA + CIFLights[i].FlightNumber.Replace(" ", ""));
                            csvtrips.WriteField(ToAirportInfo.stop_name);
                            csvtrips.WriteField(CIFLights[i].FlightNumber);
                            csvtrips.WriteField("");
                            csvtrips.WriteField("");
                            csvtrips.WriteField("");
                            csvtrips.WriteField("1");
                            csvtrips.WriteField("");
                            csvtrips.NextRecord();

                            // Depart Record
                            csvstoptimes.WriteField(CIFLights[i].FromIATA + CIFLights[i].ToIATA + CIFLights[i].FlightNumber.Replace(" ", ""));
                            csvstoptimes.WriteField(String.Format("{0:HH:mm:ss}", CIFLights[i].DepartTime));
                            csvstoptimes.WriteField(String.Format("{0:HH:mm:ss}", CIFLights[i].DepartTime));
                            csvstoptimes.WriteField(CIFLights[i].FromIATA);
                            csvstoptimes.WriteField("0");
                            csvstoptimes.WriteField("");
                            csvstoptimes.WriteField("0");
                            csvstoptimes.WriteField("0");
                            csvstoptimes.WriteField("");
                            csvstoptimes.WriteField("");
                            csvstoptimes.NextRecord();
                            // Arrival Record
                            if (!CIFLights[i].FlightNextDayArrival)
                            {
                                csvstoptimes.WriteField(CIFLights[i].FromIATA + CIFLights[i].ToIATA + CIFLights[i].FlightNumber.Replace(" ", ""));
                                csvstoptimes.WriteField(String.Format("{0:HH:mm:ss}", CIFLights[i].ArrivalTime));
                                csvstoptimes.WriteField(String.Format("{0:HH:mm:ss}", CIFLights[i].ArrivalTime));
                                csvstoptimes.WriteField(CIFLights[i].ToIATA);
                                csvstoptimes.WriteField("2");
                                csvstoptimes.WriteField("");
                                csvstoptimes.WriteField("0");
                                csvstoptimes.WriteField("0");
                                csvstoptimes.WriteField("");
                                csvstoptimes.WriteField("");
                                csvstoptimes.NextRecord();
                            }
                            else
                            {
                                //add 24 hour for the gtfs time
                                int hour = CIFLights[i].ArrivalTime.Hour;
                                hour = hour + 24;
                                int minute = CIFLights[i].ArrivalTime.Minute;
                                string strminute = minute.ToString();
                                if (strminute.Length == 1) { strminute = "0" + strminute; }



                                csvstoptimes.WriteField(CIFLights[i].FromIATA + CIFLights[i].ToIATA + CIFLights[i].FlightNumber.Replace(" ", ""));
                                csvstoptimes.WriteField(hour + ":" + strminute + ":00");
                                csvstoptimes.WriteField(hour + ":" + strminute + ":00");
                                csvstoptimes.WriteField(CIFLights[i].ToIATA);
                                csvstoptimes.WriteField("2");
                                csvstoptimes.WriteField("");
                                csvstoptimes.WriteField("0");
                                csvstoptimes.WriteField("0");
                                csvstoptimes.WriteField("");
                                csvstoptimes.WriteField("");
                                csvstoptimes.NextRecord();
                            }
                        }
                    }
                }
            }


            // Create Zip File
            string startPath = gtfsDir;
            string zipPath = myDir + "\\SpiritAirlines.zip";
            if (File.Exists(zipPath)) { File.Delete(zipPath); }
            ZipFile.CreateFromDirectory(startPath, zipPath, CompressionLevel.Fastest, false);

            //Console.ReadKey();
            //Console.WriteLine("Insert into Database...");
            //for (int i = 0; i < CIFLights.Count; i++) // Loop through List with for)
            //{
            //    using (SqlConnection connection = new SqlConnection("Server=(local);Database=CI-Import;Trusted_Connection=True;"))
            //    {
            //        using (SqlCommand command = new SqlCommand())
            //        {
            //            command.Connection = connection;            // <== lacking
            //            command.CommandType = CommandType.StoredProcedure;
            //            command.CommandText = "InsertFlight";
            //            command.Parameters.Add(new SqlParameter("@FlightSource", 5));
            //            command.Parameters.Add(new SqlParameter("@FromIATA", CIFLights[i].FromIATA));
            //            command.Parameters.Add(new SqlParameter("@ToIATA", CIFLights[i].ToIATA));
            //            command.Parameters.Add(new SqlParameter("@FromDate", CIFLights[i].FromDate));
            //            command.Parameters.Add(new SqlParameter("@ToDate", CIFLights[i].ToDate));
            //            command.Parameters.Add(new SqlParameter("@FlightMonday", CIFLights[i].FlightMonday));
            //            command.Parameters.Add(new SqlParameter("@FlightTuesday", CIFLights[i].FlightTuesday));
            //            command.Parameters.Add(new SqlParameter("@FlightWednesday", CIFLights[i].FlightWednesday));
            //            command.Parameters.Add(new SqlParameter("@FlightThursday", CIFLights[i].FlightThursday));
            //            command.Parameters.Add(new SqlParameter("@FlightFriday", CIFLights[i].FlightFriday));
            //            command.Parameters.Add(new SqlParameter("@FlightSaterday", CIFLights[i].FlightSaterday));
            //            command.Parameters.Add(new SqlParameter("@FlightSunday", CIFLights[i].FlightSunday));
            //            command.Parameters.Add(new SqlParameter("@DepartTime", CIFLights[i].DepartTime));
            //            command.Parameters.Add(new SqlParameter("@ArrivalTime", CIFLights[i].ArrivalTime));
            //            command.Parameters.Add(new SqlParameter("@FlightNumber", CIFLights[i].FlightNumber));
            //            command.Parameters.Add(new SqlParameter("@FlightAirline", CIFLights[i].FlightAirline));
            //            command.Parameters.Add(new SqlParameter("@FlightOperator", CIFLights[i].FlightOperator));
            //            command.Parameters.Add(new SqlParameter("@FlightAircraft", CIFLights[i].FlightAircraft));
            //            command.Parameters.Add(new SqlParameter("@FlightCodeShare", CIFLights[i].FlightCodeShare));
            //            command.Parameters.Add(new SqlParameter("@FlightNextDayArrival", CIFLights[i].FlightNextDayArrival));
            //            command.Parameters.Add(new SqlParameter("@FlightDuration", CIFLights[i].FlightDuration));
            //            command.Parameters.Add(new SqlParameter("@FlightNextDays", CIFLights[i].FlightNextDays));
            //            command.Parameters.Add(new SqlParameter("@FlightNonStop", "True"));
            //            command.Parameters.Add(new SqlParameter("@FlightVia", DBNull.Value));


            //            foreach (SqlParameter parameter in command.Parameters)
            //            {
            //                if (parameter.Value == null)
            //                {
            //                    parameter.Value = DBNull.Value;
            //                }
            //            }


            //            try
            //            {
            //                connection.Open();
            //                int recordsAffected = command.ExecuteNonQuery();
            //            }

            //            finally
            //            {
            //                connection.Close();
            //            }
            //        }
            //    }
            //}
        }

    }

    public class CIFLight
    {
        // Auto-implemented properties. 
        public string FromIATA;
        public string ToIATA;
        public DateTime FromDate;
        public DateTime ToDate;
        public Boolean FlightMonday;
        public Boolean FlightTuesday;
        public Boolean FlightWednesday;
        public Boolean FlightThursday;
        public Boolean FlightFriday;
        public Boolean FlightSaterday;
        public Boolean FlightSunday;
        public DateTime DepartTime;
        public DateTime ArrivalTime;
        public String FlightNumber;
        public String FlightAirline;
        public String FlightOperator;
        public String FlightAircraft;
        public String FlightDuration;
        public Boolean FlightCodeShare;
        public Boolean FlightNextDayArrival;
        public int FlightNextDays;
        public Boolean FlightDirect;
        public string FlightVia;
    }

    
}
