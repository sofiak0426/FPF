using System;
using System.IO;
using System.Diagnostics;
using S2I_Calculator;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;

namespace S2I_Filter
{
    public class S2I_FilterActions
    {
        public ds_Parameters paramsObj = new ds_Parameters(); //Object that stores all parameters
        public Dictionary <string, ds_MS2Info> ms2InfoDic; //Dictionary that stores all PSMs and their S2Is, extracted from the mzML files

        /// <summary>
        /// Main actions performed by S2I_Filter.
        /// 1. Reads the parameter file;
        /// 2. Calculates S2I from all mzML files available in the data directory with S2I_Calculator;
        /// 3. Parses through the iProphet file from IDS and remove PSMs with S2I lower than threshold.
        /// </summary>
        /// <param name="paramFile">arg[1] = param file name</param>
        public void MainActions(string paramFile)
        {        
            //Read Params
            this.ReadParamFile(paramFile);

            Stopwatch programWatch = new Stopwatch();
            programWatch.Start();

            //Extract S2I
            this.Calculate_S2I();

            //Filter iProphet file
            this.FilterIproByS2I();
 
            programWatch.Stop();
            return;
        }

        /// <summary>
        /// Parses the parameters or provide help to the user.
        /// </summary>
        /// <param name="paramFile">param file name</param>
        /// <returns>True if the parameter file is successfully parsed; otherwise false</returns>
        public void ReadParamFile(string paramFile)
        {
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), paramFile)))
                throw new ApplicationException("Parameter file not found!");
            Console.WriteLine("Reading parameter file...");
            StreamReader paramReader = new StreamReader(Path.Combine(Directory.GetCurrentDirectory(), paramFile));
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

                String[] lineElementsArr = line.Split(new[] { ':' }, 2);
                lineElementsArr[0] = lineElementsArr[0].Trim().ToLower();
                lineElementsArr[0] = Regex.Replace(lineElementsArr[0], @"\s+", " ");
                lineElementsArr[1] = lineElementsArr[1].Trim();


                //Check if the parameter name is valid
                string errorCode = String.Format("Error: some parameter has been modified to \"{0}\"?", lineElementsArr[0]);
                if (this.paramsObj.ValidateParamName(lineElementsArr[0])) //If the line specifies a parameter
                {
                    if (this.paramsObj.GetParamIsSet(lineElementsArr[0]) == true) //Check whether the param is specified by the user already
                    {
                        errorCode = String.Format("Error: the parameter \"{0}\" has been repeatedly specified", lineElementsArr[0]);
                        throw new ApplicationException(errorCode);
                    }
                    this.paramsObj.SetParamAsTrue(lineElementsArr[0]);
                }
                else
                    throw new ApplicationException(errorCode);

                //Set parameter values
                switch (lineElementsArr[0])
                {
                    case "main directory":
                        {
                            this.paramsObj.MainDir = lineElementsArr[1];
                            break;
                        }
                    case "iprophet file from identification based on database searching (ids)":
                        {
                            this.paramsObj.idsIproFile = lineElementsArr[1];
                            break;
                        }
                    case "datatype":
                        {
                            this.paramsObj.DataType = lineElementsArr[1];
                            if (!this.paramsObj.DataType.Equals("centroid", StringComparison.OrdinalIgnoreCase) &&
                                !this.paramsObj.DataType.Equals("profile", StringComparison.OrdinalIgnoreCase))
                                throw new ApplicationException("Please specify dataType as either Centroid or Profile!");
                            break;
                        }
                    case "centroid window size":
                        {
                            if (this.paramsObj.DataType.Equals("centroid", StringComparison.OrdinalIgnoreCase))
                                break;
                            double winSize;
                            bool canParse = double.TryParse(lineElementsArr[1], out winSize);
                            this.paramsObj.CenWinSize = canParse ? winSize : 0;
                            if (this.paramsObj.CenWinSize <= 0)
                                throw new ApplicationException("Please specify centroid window size as a float number larger than 0");
                            break;
                        }
                    case "isolation window size":
                        {
                            double winSize;
                            bool canParse = double.TryParse(lineElementsArr[1], out winSize);
                            this.paramsObj.IsoWinSize = canParse ? winSize : 0;
                            if (this.paramsObj.IsoWinSize <= 0)
                                throw new ApplicationException("Please specify isolation window size as a float number larger than 0");
                            break;
                        }
                    case "precursor m/z tolerance":
                        {
                            double tol;
                            bool canParse = double.TryParse(lineElementsArr[1], out tol);
                            this.paramsObj.PrecursorTol = canParse ? tol : 0;
                            if (this.paramsObj.PrecursorTol <= 0)
                                throw new ApplicationException("Please specify precursor m/z tolerance as a float number larger than 0");
                            break;
                        }
                    case "precursor isotopic peak m/z tolerance":
                        {
                            double tol;
                            bool canParse = double.TryParse(lineElementsArr[1], out tol);
                            this.paramsObj.PrecurIsoTol = canParse ? tol : 0;
                            if (this.paramsObj.PrecurIsoTol <= 0)
                                throw new ApplicationException("Please specify precursor isotopic peak m/z tolerance as a float number larger than 0");
                            break;
                        }
                    case "s2i threshold":
                        {
                            double s2i;
                            bool canParse = double.TryParse(lineElementsArr[1], out s2i);
                            this.paramsObj.S2IThresh = canParse ? s2i : 0;
                            if (this.paramsObj.S2IThresh < 0 || this.paramsObj.S2IThresh > 1)
                                throw new ApplicationException("Please specify S2I threshold as a float number between 0 and 1");
                            break;
                        }
                    default:
                        break;
                }
            }

            //Check if all parameters are set
            List<string> missingParams = this.paramsObj.CheckAllParamsSet();
            if (missingParams.Count > 0) //Some of the parameters are missing
            {
                string errorcode = "Error: the values of the following parameters are not specified:\n";
                foreach (string missingParam in missingParams)
                    errorcode += String.Format("\"{0}\"\n", missingParam);
                throw new ApplicationException(errorcode);
            }

            //Store all mzML file names
            List<string> ext = new List<string> { "*.mzML", "*.mzXML" };
            foreach (String fileExtension in ext)
            {
                foreach (String file in Directory.EnumerateFiles(Path.Combine(this.paramsObj.MainDir), fileExtension, SearchOption.TopDirectoryOnly))
                    this.paramsObj.RawDataLi.Add(file);
            }
            return;
        }

        /// <summary>
        /// Calculate S2I of PSMs from all available mzML files in the data directory.
        /// Exports a csv file containing all PSMs with their individual S2I and precursor m/z values.
        /// Stores ms2 information of PSMs (including S2I information) into ms2InfoDic.
        /// </summary>
        public void Calculate_S2I()
        {
            ProcessMassSpectra processS2I_Obj = new ProcessMassSpectra(this.paramsObj.DataType, this.paramsObj.CenWinSize);
            processS2I_Obj.ReadAllSpectrumFiles(this.paramsObj.RawDataLi);
            processS2I_Obj.ProcessAllMS2(this.paramsObj.IsoWinSize, this.paramsObj.PrecursorTol, this.paramsObj.PrecurIsoTol);
            processS2I_Obj.ExportS2I(Path.Combine(this.paramsObj.MainDir, "table_S2I.csv"));
            this.ms2InfoDic = processS2I_Obj.ms2_InfoDi;
            return;
        }

        /// <summary>
        /// Reads the input iProphet file from IDS, removes those with S2I lower than threshold, and writes a new iProphet file.
        /// </summary>
        public void FilterIproByS2I()
        {
            if (!File.Exists(Path.Combine(this.paramsObj.MainDir, this.paramsObj.idsIproFile)))
                throw new FileLoadException("iProphet file from IDS not found!");


            Console.WriteLine("Filtering iProphet file and writing to new iProphet...");
            //Xml reader setup
            XmlReaderSettings readerSettings = new XmlReaderSettings { IgnoreWhitespace = true };
            XmlReader idsIproReader = XmlReader.Create(Path.Combine(this.paramsObj.MainDir,
                this.paramsObj.idsIproFile), readerSettings);
            XmlReader msmsRunReader = idsIproReader;
            idsIproReader.Read(); //Jump to first node
            //Xml writer setup
            XmlWriterSettings writerSettings = new XmlWriterSettings { Indent = true, IndentChars = " " };
            XmlWriter modIdsIproWriter = XmlWriter.Create(Path.Combine(this.paramsObj.MainDir, "adjS2I_" +
                this.paramsObj.idsIproFile), writerSettings);
            while (true)
            {
                if (idsIproReader.Name == "xml") //read xml header
                {
                    modIdsIproWriter.WriteNode(idsIproReader, false);
                    continue;
                }
                else if (idsIproReader.Name == "msms_pipeline_analysis" && idsIproReader.NodeType == XmlNodeType.Element) //Start element of msms_pipeline_analysis
                {
                    modIdsIproWriter.WriteStartElement(idsIproReader.Name, idsIproReader.GetAttribute("xmlns"));
                    modIdsIproWriter.WriteAttributeString("date", idsIproReader.GetAttribute("date"));
                    modIdsIproWriter.WriteAttributeString("xsi", "schemaLocation", idsIproReader.GetAttribute("xmlns:xsi"),
                        idsIproReader.GetAttribute("xsi:schemaLocation"));
                    modIdsIproWriter.WriteAttributeString("summary_xml", idsIproReader.GetAttribute("summary_xml"));
                }
                else if (idsIproReader.Name == "msms_pipeline_analysis" && idsIproReader.NodeType == XmlNodeType.EndElement) //End element of msms_pipeline_analysis
                {
                    modIdsIproWriter.WriteEndElement();
                }
                else if (idsIproReader.Name == "analysis_summary") //Other analysis summaries
                {
                    modIdsIproWriter.WriteNode(idsIproReader, false);
                    continue;
                }
                else if (idsIproReader.Name == "msms_run_summary") //Contain PSM information
                {
                    modIdsIproWriter.WriteStartElement(idsIproReader.Name);
                    modIdsIproWriter.WriteAttributes(idsIproReader, false);
                    msmsRunReader = idsIproReader.ReadSubtree();
                    ReadMsmsRun(msmsRunReader, modIdsIproWriter);
                    modIdsIproWriter.WriteEndElement();
                    idsIproReader.Skip();
                    continue;
                }
                else
                    Console.WriteLine(String.Format("Warning: unexpected node {0}", idsIproReader.Name));
                idsIproReader.Read();
                if (idsIproReader.EOF == true)
                    break;
            }

            idsIproReader.Close();
            msmsRunReader.Close();
            modIdsIproWriter.Close();
        }

        /// <summary>
        /// Read the msms_run_summary section of the iProphet file
        /// </summary>
        /// <param name="msmsRunReader"> xml reader for the section</param>
        /// <param name="modIdsIproWriter"> output writer</param>
        private void ReadMsmsRun(XmlReader msmsRunReader, XmlWriter modIdsIproWriter)
        {
            //Reader setup
            XmlReaderSettings readerSettings = new XmlReaderSettings { IgnoreWhitespace = true };
            msmsRunReader.Read(); //Jump to first node

            while (true)
            {
                if (msmsRunReader.Name == "msms_run_summary") //itself
                    msmsRunReader.Read();
                else if (msmsRunReader.Name == "spectrum_query") //filter PSMs
                {
                    string scanID = msmsRunReader.GetAttribute("spectrum");
                    double S2I;
                    try
                    {
                        S2I = this.ms2InfoDic[scanID].s2i;
                        if (S2I >= this.paramsObj.S2IThresh)
                            modIdsIproWriter.WriteNode(msmsRunReader, false);
                        else
                            msmsRunReader.Skip();
                    }
                    catch //If the user forgets a mzML file, for instance
                    {
                        Console.WriteLine(String.Format("PSM {0} is not found in the current mzML files.", scanID));
                    }
                }
                else if (msmsRunReader.EOF == true)
                    break;
                else //Other elements in msms_run_summary
                    modIdsIproWriter.WriteNode(msmsRunReader, false);
            }
        }

    }
}

