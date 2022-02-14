using System;
using System.IO;
using System.Diagnostics;
using S2I_Extractor;
using System.Collections.Generic;
using System.Xml;
using System.Linq;

namespace S2I_Filter
{
    public class S2I_FilterActions
    {
        public ds_Parameters paramsObj;
        public Dictionary <string, ds_MS2Info> ms2InfoDic;

        public void MainActions(string[] args) //arg: dataDir + param file name or -help
        {
            //Read Params
            if (this.ReadParamFile(args) == false)
                return;

            //Start Program
            Stopwatch programWatch = new Stopwatch();
            programWatch.Start();

            //Extract S2I
            this.Extract_S2I();
            this.FilterIproByS2I();
 
            programWatch.Stop();
            Console.WriteLine("Finished\nElapsed time : {0} ms", programWatch.ElapsedMilliseconds.ToString());
            return;
        }

        public bool ReadParamFile(string[] arg) //arg[0]: dataDir; arg[1]: param file name
        {
            //Read parameter file
            if (arg.Length == 0)
                return false;

            if (arg[0].Equals("help", StringComparison.OrdinalIgnoreCase) || arg[0].Equals("-help", StringComparison.OrdinalIgnoreCase))
            {
                ds_Parameters.Help();
                return false;
            }

            if (!File.Exists(Path.Combine(arg[0],arg[1])))
            {
                Console.WriteLine("Parameter file not found...");
                ds_Parameters.Help();
                return false;
            }
            this.paramsObj = new ds_Parameters(arg[0], arg[1]);
            if (paramsObj.valid == false)
            {
                Console.WriteLine("Wrong paramemters...");
                ds_Parameters.Help();
                return false;
            }
            Console.WriteLine("Parameter file detected successfully\nReading and Processing...");
            return true;
        }

        public void Extract_S2I()
        {
            ProcessMassSpectra processS2I_Obj = new ProcessMassSpectra(this.paramsObj.dataType, this.paramsObj.cenWinSize);
            processS2I_Obj.ReadAllSpectrumFiles(this.paramsObj.rawDataLi);
            processS2I_Obj.ProcessAllMS2(this.paramsObj.isoWinSize, this.paramsObj.precursorTol, this.paramsObj.precurIsoTol);
            processS2I_Obj.ExportS2I(Path.Combine(this.paramsObj.dataDir, "table_S2I.csv"));
            this.ms2InfoDic = processS2I_Obj.ms2_InfoDi;
            return;
        }

        /// <summary>
        /// Reads DB + SL iprophet file, filter by features and write to new file
        /// </summary>
        public void FilterIproByS2I()
        {
            Console.WriteLine("Filtering database iProphet file and writing to new iProphet...");
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
                    double S2I = this.ms2InfoDic[scanID].s2i;
                    if (S2I >= this.paramsObj.S2IThresh)
                        modIproDbWriter.WriteNode(msmsRunReader, false);
                    else
                        msmsRunReader.Skip();
                    continue;
                }
                else if (msmsRunReader.EOF == true)
                    break;
                else //Other elements in msms_run_summary
                    modIproDbWriter.WriteNode(msmsRunReader, false);
            }
        }

    }
}

