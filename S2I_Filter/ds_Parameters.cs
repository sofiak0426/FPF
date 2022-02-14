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

        /// <summary>
        /// Read parameters from the parameter file and construct a parameter object.
        /// </summary>
        /// <param name="dataDir">The data directory which contains the parameter file and all the mzML files</param>
        /// <param name="paramFile">The parameter file name </param>
        public ds_Parameters(string dataDir, string paramFile)
        {
            this.dataDir = dataDir;
            this.rawDataLi = new List<string>();

            StreamReader paramReader = new StreamReader(Path.Combine(this.dataDir, paramFile));
            string line;
            while ((line = paramReader.ReadLine()) != null)
            {
                //Skip annotations or empty lines
                if (line == "")
                    continue;
                if (line == "\n")
                    continue;
                else if (line[0] == '#')
                    continue;

                String[] lineElementsArr = line.Split(':');
                lineElementsArr[0] = lineElementsArr[0].Trim();
                lineElementsArr[1] = lineElementsArr[1].Trim();

                //Check if the parameter name is valid
                string errorCode = String.Format("Error:" + "have you modified the parameter to \"{0}\"?", lineElementsArr[0]);
                if (this.ValidateParamName(lineElementsArr[0])) //If the line specifies a parameter
                {
                    if (this.GetParamIsSet(lineElementsArr[0]) == true) //Check whether the param is specified by the user already
                    {
                        errorCode = String.Format("Error: you have repeatedly specify the parameter \"{0}\"", lineElementsArr[0]);
                        throw new ApplicationException(errorCode);
                    }
                    this.SetParamAsTrue(lineElementsArr[0]);
                }
                else
                    throw new ApplicationException(errorCode);

                //Set parameter values
                switch (lineElementsArr[0])
                {
                    case "Database iProphet Search File":
                        {
                            this.dbIproFile = lineElementsArr[1];
                            if (!File.Exists(Path.Combine(this.dataDir, this.dbIproFile)))
                                throw new FileLoadException("The iProphet file does not exist!");
                            break;
                        }
                    case "DataType":
                        {
                            this.dataType = lineElementsArr[1];
                            if (!this.dataType.Equals("Centroid", StringComparison.OrdinalIgnoreCase) && !this.dataType.Equals("Profile", StringComparison.OrdinalIgnoreCase))
                                throw new ApplicationException("Please specify dataType as either Centroid or Profile...");
                            break;
                        }
                    case "Centroid Window Size":
                        {
                            double winSize;
                            bool canParse = double.TryParse(lineElementsArr[1], out winSize);
                            this.cenWinSize = canParse ? winSize : 0;
                            if (this.cenWinSize <= 0)
                                throw new ApplicationException("Please specify centroid window size as a float number larger than 0");
                            break;
                        }
                    case "Isolation Window Size":
                        {
                            double winSize;
                            bool canParse = double.TryParse(lineElementsArr[1], out winSize);
                            this.isoWinSize = canParse ? winSize : 0;
                            if (this.isoWinSize <= 0)
                                throw new ApplicationException("Please specify isolation window size as a float number larger than 0");
                            break;
                        }
                    case "Precursor m/z Tolerance":
                        {
                            double tol;
                            bool canParse = double.TryParse(lineElementsArr[1], out tol);
                            this.precursorTol = canParse ? tol : 0;
                            if (this.precursorTol <= 0)
                                throw new ApplicationException("Please specify precursor m/z tolerance as a float number larger than 0");
                            break;
                        }
                    case "Precursor Isotopic Peak m/z Tolerance":
                        {
                            double tol;
                            bool canParse = double.TryParse(lineElementsArr[1], out tol);
                            this.precurIsoTol = canParse ? tol : 0;
                            if (this.precurIsoTol <= 0)
                                throw new ApplicationException("Please specifyprecursor isotopic peak m/z tolearance as a float number larger than 0");
                            break;
                        }
                    case "S2I Threshold":
                        {
                            double s2i;
                            bool canParse = double.TryParse(lineElementsArr[1], out s2i);
                            this.S2IThresh = canParse ? s2i : 0;
                            if (this.S2IThresh < 0 || this.S2IThresh > 1)
                                throw new ApplicationException("Please specify S2I threshold as a float number between 0 and 1");
                            break;
                        }
                    default:
                        break;
                }
            }

            //Check if all parameters are set
            List<string> missingParams = this.CheckAllParamsSet();
            if (missingParams.Count > 0) //Some of the parameters are missing
            {
                string errorcode = "Error: you didn't specify the values of the following parameters:\n";
                foreach (string missingParam in missingParams)
                    errorcode += String.Format("\"{0}\"\n", missingParam);
                throw new ApplicationException(errorcode);
            }

            //Store all mzML file names
            List<string> ext = new List<string> { "*.mzML", "*.mzXML" };
            foreach (String fileExtension in ext)
            {
                foreach (String file in Directory.EnumerateFiles(this.dataDir, fileExtension, SearchOption.TopDirectoryOnly))
                    this.rawDataLi.Add(file);
            }
            return;
        }

        //A dictionary that stores whether the global param values is correctly specified by user or not
        //Key: Parameter name in param file; Value: if the param is correctly specified by the user
        private Dictionary<string, bool> _paramIsSetDic = new Dictionary<string, bool>{
            {"Database iProphet Search File", false},
            {"DataType", false},
            {"Centroid Window Size", false},
            {"Isolation Window Size", false},
            {"Precursor m/z Tolerance", false},
            {"Precursor Isotopic Peak m/z Tolerance", false},
            {"S2I Threshold", false}
        };

        /// <summary>
        /// Check whether the current param name in the param file corresponds to one of the the correct param names in the dictionary.
        /// True: param name is correct; False: there is no corresponding param name
        /// </summary>
        public bool ValidateParamName(string paramNameInParamFile)
        {
            return _paramIsSetDic.ContainsKey(paramNameInParamFile);
        }

        /// <summary>
        /// If a param is correctly specified by user, change the item value (whose key is the parameter name) in _paramIsSetDic to true
        /// </summary>
        public void SetParamAsTrue(string paramName)
        {
            _paramIsSetDic[paramName] = true;
        }

        /// <summary>
        /// Check if a param is correctly specified by the user or not
        /// </summary>
        /// <returns></returns>
        public bool GetParamIsSet(string paramName)
        {
            return _paramIsSetDic[paramName];
        }

        /// <summary>
        /// Check whether all params in _paramIsSetDic are correctly specified by the user.
        /// Then return a list containing all parameters names that are not specified. 
        /// If all params are correctly specified, an empty list will by returned.
        /// </summary>
        public List<string> CheckAllParamsSet()
        {
            List<string> missingParams = new List<string>();
            foreach (KeyValuePair<string, bool> feature_hasValue in _paramIsSetDic)
            {
                if (feature_hasValue.Value == false)
                    missingParams.Add(feature_hasValue.Key);
            }
            return missingParams;
        }
    }
}
