using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace S2I_Filter
{
    public class ds_Parameters
    {
        public string dataDir { get; set; }
        public string dbIproFile { get; set; }
        public string dataType { get; set; }
        public double cenWinSize { get; set; }
        public double isoWinSize { get; set; }
        public double precursorTol { get; set; }
        public double precurIsoTol { get; set; }
        public double S2IThresh { get; set; }
        public List<string> rawDataLi { get; set; }
        public bool valid { get; set; }

        public ds_Parameters()
        {
        }

        public ds_Parameters(string dataDir, string paramFilePath)
        {
            this.dataDir = dataDir;
            this.dataType = "";
            this.cenWinSize = 0;
            this.isoWinSize = 0;
            this.precursorTol = 0;
            this.precurIsoTol = 0;
            this.S2IThresh = 0;
            this.valid = false;
            this.rawDataLi = new List<string>();

            StreamReader sr = new StreamReader(Path.Combine(this.dataDir,paramFilePath));
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (line.StartsWith("Database iProphet Search File:"))
                {
                    this.dbIproFile = line.Split(':')[1].Trim();
                }
                else if (line.StartsWith("DataType:"))
                {
                    this.dataType = line.Split(':')[1].Trim();
                }
                else if (line.StartsWith("Centroid Window Size:"))
                {
                    double winSize;
                    bool canParse = double.TryParse(line.Split(':')[1].Trim(), out winSize);
                    this.cenWinSize = canParse ? winSize : 0;
                }
                else if (line.StartsWith("Isolation Window Size:"))
                {
                    double winSize;
                    bool canParse = double.TryParse(line.Split(':')[1].Trim(), out winSize);
                    this.isoWinSize = canParse ? winSize : 0;
                }
                else if (line.StartsWith("Precursor m/z Tolerence:"))
                {
                    double tol;
                    bool canParse = double.TryParse(line.Split(':')[1].Trim(), out tol);
                    this.precursorTol = canParse ? tol : 0;
                }
                else if (line.StartsWith("Precursor Isotopic Peak m/z Tolerence:"))
                {
                    double tol;
                    bool canParse = double.TryParse(line.Split(':')[1].Trim(), out tol);
                    this.precurIsoTol = canParse ? tol : 0;
                }
                else if (line.StartsWith("S2I Threshold:"))
                {
                    double s2i;
                    bool canParse = double.TryParse(line.Split(":")[1].Trim(), out s2i);
                    this.S2IThresh = canParse ? s2i : 0;
                }
                else { }
            }

            if (this.dbIproFile == "" || this.dataType == "" || this.cenWinSize <= 0 || this.isoWinSize <= 0 || this.precursorTol <= 0 || this.precurIsoTol <= 0 || this.S2IThresh < 0 ||
                this.S2IThresh > 1 || (!this.dataType.Equals("Centroid", StringComparison.OrdinalIgnoreCase) && !this.dataType.Equals("Profile", StringComparison.OrdinalIgnoreCase)))
            {
                this.valid = false;
            }
            else
            {
                this.valid = true;
                List<string> ext = new List<string> { "*.mzML", "*.mzXML" };
                foreach (String fileExtension in ext)
                {
                    foreach (String file in Directory.EnumerateFiles(this.dataDir, fileExtension, SearchOption.TopDirectoryOnly))
                        this.rawDataLi.Add(file);
                }
            }

            if(!this.dataType.Equals("Centroid", StringComparison.OrdinalIgnoreCase) && !this.dataType.Equals("Profile", StringComparison.OrdinalIgnoreCase))           
                Console.WriteLine("Please specify dataType as either Centroid or Profile...");
            
        }

        static public void Help()
        {
            Console.WriteLine("Please provide a TXT file specifying the following parameters:");
            Console.WriteLine("Database iProphet Search File: [iProphet file name placed in the folder above]");
            Console.WriteLine("Data type: [Centroid or Profile]");
            Console.WriteLine("Centroid Window Size: [a float number, used when the data is in profile mode]");
            Console.WriteLine("Isolation Window Size: [a float number]");
            Console.WriteLine("Precursor m/z Tolerence: [a float number]");
            Console.WriteLine("Precursor Isotopic Peak m/z Tolerence: [a float number, usually identical to precursorTol]");
            Console.WriteLine("S2I Threshold: [a float number, PSMs with S2I below the threshold will be removed]");
        }
    }
}
