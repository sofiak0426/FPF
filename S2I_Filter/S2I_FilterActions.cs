using System;
using System.IO;
using System.Diagnostics;
using S2I_Calculator;
using System.Collections.Generic;
using System.Xml;

namespace S2I_Filter
{
    public class S2I_FilterActions
    {
        public ds_Parameters paramsObj; //Object that stores all parameters
        public Dictionary <string, ds_MS2Info> ms2InfoDic; //Dictionary that stores all PSMs and their S2Is, extracted from the mzML files

        /// <summary>
        /// Main actions performed by S2I_Filter.
        /// 1. Reads the parameter file;
        /// 2. Calculates S2I from all mzML files available in the data directory with S2I_Calculator;
        /// 3. Parses through the input iProphet file and remove PSMs with S2I lower than threshold.
        /// </summary>
        /// <param name="args">Can be arg[0] = dataDir, arg[1] = param file name</param>
        public void MainActions(string[] args)
        {
            if (args.Length != 2)
                throw new ArgumentException("Please specify two arguments: the data directory and the parameter file name.");
            
            //Read Params
            this.ReadParamFile(args);

            Stopwatch programWatch = new Stopwatch();
            programWatch.Start();

            //Extract S2I
            this.Calculate_S2I();

            //Filter iProphet file
            this.FilterIproByS2I();
 
            programWatch.Stop();
            Console.WriteLine("Finished\nElapsed time : {0} ms", programWatch.ElapsedMilliseconds.ToString());
            return;
        }

        /// <summary>
        /// Parses the parameters or provide help to the user.
        /// </summary>
        /// <param name="args">Can be arg[0] = dataDir, arg[1] = param file name</param>
        /// <returns>True if the parameter file is successfully parsed; otherwise false</returns>
        public void ReadParamFile(string[] args) 
        {
            Console.WriteLine(Path.Combine(args[0], args[1]));
            if (!File.Exists(Path.Combine(args[0],args[1])))
                throw new ApplicationException("Parameter file not found...");

            this.paramsObj = new ds_Parameters(args[0], args[1]);          
            Console.WriteLine("Parameter file detected successfully\nReading and Processing...");
        }

        /// <summary>
        /// Calculate S2I of PSMs from all available mzML files in the data directory.
        /// Exports a csv file containing all PSMs with their individual S2I and precursor m/z values.
        /// Stores ms2 information of PSMs (including S2I information) into ms2InfoDic.
        /// </summary>
        public void Calculate_S2I()
        {
            ProcessMassSpectra processS2I_Obj = new ProcessMassSpectra(this.paramsObj.dataType, this.paramsObj.cenWinSize);
            processS2I_Obj.ReadAllSpectrumFiles(this.paramsObj.rawDataLi);
            processS2I_Obj.ProcessAllMS2(this.paramsObj.isoWinSize, this.paramsObj.precursorTol, this.paramsObj.precurIsoTol);
            processS2I_Obj.ExportS2I(Path.Combine(this.paramsObj.dataDir, "table_S2I.csv"));
            this.ms2InfoDic = processS2I_Obj.ms2_InfoDi;
            return;
        }

        /// <summary>
        /// Reads the input iProphet file, removes those with S2I lower than threshold, and writes a new iProphet file.
        /// </summary>
        public void FilterIproByS2I()
        {
            Console.WriteLine("Filtering iProphet file and writing to new iProphet...");
            //Xml reader setup
            XmlReaderSettings readerSettings = new XmlReaderSettings { IgnoreWhitespace = true };
            XmlReader iproDbReader = XmlReader.Create(Path.Combine(this.paramsObj.dataDir,
                this.paramsObj.dbIproFile), readerSettings);
            XmlReader msmsRunReader = iproDbReader;
            iproDbReader.Read(); //Jump to first node
            //Xml writer setup
            XmlWriterSettings writerSettings = new XmlWriterSettings { Indent = true, IndentChars = " " };
            XmlWriter modIproDbWriter = XmlWriter.Create(Path.Combine(this.paramsObj.dataDir, "adjS2I_" +
                this.paramsObj.dbIproFile), writerSettings);
            while (true)
            {
                if (iproDbReader.Name == "xml") //read xml header
                {
                    modIproDbWriter.WriteNode(iproDbReader, false);
                    continue;
                }
                else if (iproDbReader.Name == "msms_pipeline_analysis" && iproDbReader.NodeType == XmlNodeType.Element) //Start element of msms_pipeline_analysis
                {
                    modIproDbWriter.WriteStartElement(iproDbReader.Name, iproDbReader.GetAttribute("xmlns"));
                    modIproDbWriter.WriteAttributeString("date", iproDbReader.GetAttribute("date"));
                    modIproDbWriter.WriteAttributeString("xsi", "schemaLocation", iproDbReader.GetAttribute("xmlns:xsi"),
                        iproDbReader.GetAttribute("xsi:schemaLocation"));
                    modIproDbWriter.WriteAttributeString("summary_xml", iproDbReader.GetAttribute("summary_xml"));
                }
                else if (iproDbReader.Name == "msms_pipeline_analysis" && iproDbReader.NodeType == XmlNodeType.EndElement) //End element of msms_pipeline_analysis
                {
                    modIproDbWriter.WriteEndElement();
                }
                else if (iproDbReader.Name == "analysis_summary") //Other analysis summaries
                {
                    modIproDbWriter.WriteNode(iproDbReader, false);
                    continue;
                }
                else if (iproDbReader.Name == "msms_run_summary") //Contain PSM information
                {
                    modIproDbWriter.WriteStartElement(iproDbReader.Name);
                    modIproDbWriter.WriteAttributes(iproDbReader, false);
                    msmsRunReader = iproDbReader.ReadSubtree();
                    ReadMsmsRun(msmsRunReader, modIproDbWriter);
                    modIproDbWriter.WriteEndElement();
                    iproDbReader.Skip();
                    continue;
                }
                else
                    Console.WriteLine(String.Format("Warning: unexpected node {0}", iproDbReader.Name));
                iproDbReader.Read();
                if (iproDbReader.EOF == true)
                    break;
            }

            iproDbReader.Close();
            msmsRunReader.Close();
            modIproDbWriter.Close();
        }

        /// <summary>
        /// Read the msms_run_summary section of the iProphet file
        /// </summary>
        /// <param name="msmsRunReader"> xml reader for the section</param>
        /// <param name="modIproDbWriter"> output writer</param>
        private void ReadMsmsRun(XmlReader msmsRunReader, XmlWriter modIproDbWriter)
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
                            modIproDbWriter.WriteNode(msmsRunReader, false);
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
                    modIproDbWriter.WriteNode(msmsRunReader, false);
            }
        }

    }
}

