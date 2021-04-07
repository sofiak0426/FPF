using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using ResultReader;

namespace iproxml_filter
{
    //Define thread actions

    public class Actions
    {
        public readonly string[] paramNames = {
            "Database Iprophet Search File","Database + SpectraST Iprophet Search File",
            "Channel Number","Reference Channel","Charge", "Mass", "Peptide Length",
            "Intra-Peptide Euclidean Distance","Intra-Protein Euclidean Distance" };

        //Global Variables
        public string mainDir;
        public string iproDbFile;
        public string iproDbSpstFile;
        public int channelCnt;
        public int refChan;

        //Data storage
        public ds_DataContainer dataContainerObj;
        public ds_PsmFilterParam filterListObj;

        /// <summary>
        /// Defines thread action by id
        /// </summary>
        public string DoWorkerJobs(int id)
        {
            switch (id)
            {
                case 0:
                    ReadDbIproFile();
                    break;
                case 1:
                    //ReadDbSpstIntens(this.mainDir + iproDbSpstFile);
                    break;
                case 2:
                    ReadDbSpstPepProt(this.mainDir + iproDbSpstFile);
                    break;
            }
            return (String.Format("{0} Done", id));
        }

        /// <summary>
        /// Performs main actions.
        /// 1. Read Parameter File
        /// 2. Read PSM names from database search iprophet file
        /// 3. Read PSM information with search result reader from database and spectrast search iprophet file
        /// 4. Calculate intra-peptide and intra-protein euclidean distance
        /// 5. Parse the database and spectrast search iprophet file again, filter and write to new file
        /// </summary>
        /// <param name="mainDir">Directory that stores the parameter file and all iprophet files. It is also where the new iprophet file will be written to.</param>
        /// <param name="paramFile">Parameter file name</param>
        ///
        public void MainActions(string mainDir, string paramFile)
        {
            //Read parameters file
            this.mainDir = mainDir;
            dataContainerObj = new ds_DataContainer();
            filterListObj = new ds_PsmFilterParam();
<<<<<<< HEAD
            Console.WriteLine(this.mainDir + paramFile);
=======
>>>>>>> f70e3c0a2d04ccdf2d34744ace106d0068f12d9a
            this.ReadParamFile(this.mainDir + paramFile);

            List<int> workerIds = new List<int>{0,1,2};
            Parallel.ForEach(workerIds, workerId =>
            {
                string result = this.DoWorkerJobs(workerId);
                Console.WriteLine(result); //temporarily for testing
            });
            //Calculate intra-pep and intra-prot euclidean distance for each PSM
            this.CalIntraProtEuDist();

            return;

        }

        private void ReadParamFile(string paramFile)
        {
<<<<<<< HEAD
            Console.WriteLine("Reading parameter file...");
=======
>>>>>>> f70e3c0a2d04ccdf2d34744ace106d0068f12d9a
            string line;
            using StreamReader paramFileReader = new StreamReader(paramFile);
            int lineCnt = 0; //count current number of valid lines
            while ((line = paramFileReader.ReadLine()) != null)
            {
                //Skip anotations or empty lines
                if (line[0] == '#' || line == "\n")
                    continue;
                lineCnt++;

                String[] lineElementsArr = line.Split(':');
                lineElementsArr[1] = lineElementsArr[1].Trim(' ');

                string errorCode = String.Format("Parameter error:" +
                    "Have you modified the parameter {0} to \"{1}\"?",
                    paramNames[lineCnt - 1], lineElementsArr[0]);

                //parameter names does not match default
                if (lineElementsArr[0] != paramNames[lineCnt - 1])
                    throw new ApplicationException(errorCode);

                //if a feature filter is empty, skip the feature
                if (lineCnt >= 5 && String.IsNullOrEmpty(lineElementsArr[1]))
                    continue;

                switch (lineCnt)
                {
                    case 1: //Database Iprophet Search File:
                        iproDbFile = lineElementsArr[1];
                        break;
                    case 2: //"Database + SpectraST Iprophet Search File"
                        iproDbSpstFile = lineElementsArr[1];
                        break;
                    case 3: //Channel Number
                        int.TryParse(lineElementsArr[1], out channelCnt);
                        break;
                    case 4: //Reference Channel
                        int.TryParse(lineElementsArr[1], out refChan);
                        break;
                    default: //for features
                        if(this.AddFilters(lineCnt, lineElementsArr[1]) == false)
                            throw new ApplicationException(errorCode);
                        break;
                }
            }
            paramFileReader.Close();
            //filterListObj.PrintFilter(); //testing
            return;
        }

        /// <summary>
        /// 
        /// </summary>
<<<<<<< HEAD
        /// <param name="lineCnt"> Specify number of feature in the param name list</param>
        /// <param name="filterStr">The string that contains features, read from param file</param>
=======
        /// <param name="lineCnt"> Specify feature in the </param>
        /// <param name="filterStr"></param>
>>>>>>> f70e3c0a2d04ccdf2d34744ace106d0068f12d9a
        private bool AddFilters(int lineCnt, string filterStr)
        {
            String[] filterArr = filterStr.Split(',');
            foreach (string filter in filterArr)
            {
                if (filter.Trim(' ').IndexOf('-') == -1)
                    throw new ApplicationException(String.Format("Feature {0}: " +
                        "You entered in the wrong filter format", paramNames[lineCnt - 1]));

                String[] filtLimArr = filter.Trim(' ').Split('-');
                (double lowerLim, double upperLim) filtLim;
                if (filtLimArr[0] == String.Empty)
                    filtLim.lowerLim = Double.NegativeInfinity;
                else
                    double.TryParse(filtLimArr[0], out filtLim.lowerLim);
                if (filtLimArr[1] == String.Empty)
                    filtLim.upperLim = Double.PositiveInfinity;
                else
                    double.TryParse(filtLimArr[1], out filtLim.upperLim);

<<<<<<< HEAD
                if (!filterListObj.AddFilter(paramNames[lineCnt - 1], filtLim))
=======
                if (!filterListObj.Add(paramNames[lineCnt - 1], filtLim))
>>>>>>> f70e3c0a2d04ccdf2d34744ace106d0068f12d9a
                    return false;
            }
            return true;
        }

        private void ReadDbIproFile()
        {
            using XmlReader dbFileReader = XmlReader.Create(iproDbFile);
            while (dbFileReader.Read())
            {
                if (dbFileReader.NodeType != XmlNodeType.Element)
                    continue;
                if (dbFileReader.Name == "spectrum_query")
                    this.dataContainerObj.dbPsmNameLi.Add(dbFileReader.GetAttribute("spectrum"));
            }
            Console.WriteLine("Finished database search iprophet parsing: {0:G}", this.dataContainerObj.dbPsmNameLi.Count);
            return;
        }

        //spec to spst
        /*
        private void ReadDbSpstIntens(string iproSpecFile)
        {
            using XmlReader iproFileReader = XmlReader.Create(iproDbSpstFile);
            while (iproFileReader.Read())
            {
                if (iproFileReader.NodeType != XmlNodeType.Element)
                    continue;

                if (iproFileReader.Name == "spectrum_query") //Read only spectrum name and intensities
                {
                    string psmName = iproFileReader.GetAttribute("spectrum");
                    List<double> libraIntenLi = new List<double>();
                    XmlReader PsmReader = iproFileReader.ReadSubtree();
                    while (PsmReader.Read())
                    {
                        if (PsmReader.NodeType != XmlNodeType.Element ||
                            PsmReader.Name != "libra_result")
                            continue;

                        //read intensities
                        XmlReader intensReader = PsmReader.ReadSubtree();
                        while (intensReader.Read())
                        {
                            if (intensReader.Name != "intensity")
                                continue;
                            double.TryParse(intensReader.GetAttribute("normalized"),
                                out double intens);
                            libraIntenLi.Add(intens);
                        }
                        break;
                    }
                    List<double> errorLi = CalRatio(libraIntenLi);
                    dataContainerObj.dbSpecPsmDic.Add(psmName, new ds_DbSpecPsm(psmName, errorLi));
                }
            }
        }
        */
        private void ReadDbSpstPepProt(string iproSpstFile)
        {
            PepXmlProtXmlReader spstIproFileReader = new PepXmlProtXmlReader();
            this.dataContainerObj.dbSpstIproResult = spstIproFileReader.ReadFiles(iproSpstFile, "",
                XmlParser_Action.Read_PepXml, SearchResult_Source.TPP_PepXml);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="intensLi"></param>
        /// <returns></returns>
        private List<double> CalRatio(List<double> intensLi) //calculate channel error of psms
        {
            List<double> ratioLi = new List<double>();
            for (int i = 0; i < channelCnt; i++)
            {
                if (i + 1 == refChan) //skip reference channel
                    continue;
                double ratio = intensLi[i] / intensLi[refChan - 1];
                ratioLi.Add(Math.Round(ratio, 4));
            }
            return ratioLi;
        }

        private void CalIntraProtEuDist() //calculate intra-prot euclidean for each psm in db + spec file
        {
            foreach (KeyValuePair<string, ds_Protein> prot in dataContainerObj.dbSpstIproResult.Protein_Dic)
            {
                //get all psm names in this protein
                List<string> psmsInProtLi = new List<string>();
                foreach (KeyValuePair<string, ds_Peptide> pep in prot.Value.Peptide_Dic)
                {
                    foreach (ds_PSM psm in pep.Value.PsmList)
                    {
                        psmsInProtLi.Add(psm.QueryNumber);
                    }
                }

                //if there is only one PSM in this protein: set euclidean to 0
                if (psmsInProtLi.Count == 1)
                    continue;

                //get all ratios of psms in this protein
                Dictionary<string, List<double>> allPsmRatioDic = new Dictionary<string, List<double>>();
                foreach (string psmName in psmsInProtLi)
                {
                    List<double> error = new List<double>();
                    error = dataContainerObj.dbSpecPsmDic[psmName].ErrorLi;
                    allPsmRatioDic.Add(psmName, error);
                }

                //calculate euclidean
                foreach (KeyValuePair<string, List<double>> psm in allPsmRatioDic)
                {
                    for (int i = 0; i < channelCnt - 1; i++)
                    { }
                }

            }
        }
    }
}
