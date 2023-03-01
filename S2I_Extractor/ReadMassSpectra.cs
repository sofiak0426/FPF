using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MSReader;
using MSDataStructure;
using MSProcessor;
using System.Xml;

namespace S2I_Calculator
{
    public class ReadMassSpectra
    {
        public Dictionary<string, ds_isolationWin> ms2_isoWinInfoDi { get; set; }


        public ReadMassSpectra()
        {
            this.ms2_isoWinInfoDi = new Dictionary<string, ds_isolationWin>();
        }



        public MSRawData ReadRawMzML(string filePath, string msDataType, double centroidWinSize)
        {
            MSRawData msrdObj = new MSRawData();
            if (Path.GetExtension(filePath).Equals(".mzml", StringComparison.OrdinalIgnoreCase))
            {
                mzMLReader mzmlRdObj = new mzMLReader();
                msrdObj = mzmlRdObj.ReadmzML(filePath, true);

                if (msDataType.Equals("Profile", StringComparison.OrdinalIgnoreCase))   //若為profile mode, 則把所有ms1作centroiding, ms2保留不動
                {
                    Console.WriteLine("Converting to centroid mode: {0}", Path.GetFileNameWithoutExtension(filePath));
                    msrdObj = this.CentroidMS1(msrdObj, centroidWinSize); 
                }
            }
            else if (Path.GetExtension(filePath).Equals(".mzxml", StringComparison.OrdinalIgnoreCase))
            {
                mzXMLReader mzxmlRdObj = new mzXMLReader();
                msrdObj = mzxmlRdObj.ReadmzXML(filePath, true);

                if (msDataType.Equals("Profile", StringComparison.OrdinalIgnoreCase))   //若為profile mode, 則把所有ms1作centroiding, ms2保留不動
                {
                    Console.WriteLine("Converting to centroid mode: {0}", Path.GetFileNameWithoutExtension(filePath));
                    msrdObj = this.CentroidMS1(msrdObj, centroidWinSize);
                }
            }
            return msrdObj;
        }

        public void ReadMzMLAndGetIsolationWindowInfo(string filePath)
        {
            string filename = Path.GetFileNameWithoutExtension(filePath);
            

            XmlReader mzMLReader = XmlReader.Create(filePath);
            while (mzMLReader.Read())
            {
                if (mzMLReader.NodeType != XmlNodeType.Element)
                    continue;

                if (mzMLReader.Name == "spectrum")
                {
                    int scan_num = -1;
                    string id = mzMLReader.GetAttribute("id");
                    string[] id_splits = id.Split(' ');
                    foreach (string id_split in id_splits)
                    {
                        if (id_split.StartsWith("scan="))                       
                            scan_num = int.Parse(id_split.Split('=')[1]);                        
                    }

                    XmlReader spectrumReader = mzMLReader.ReadSubtree();
                    spectrumReader.MoveToContent();
                    this.ReadNode_Spectrum(spectrumReader, filename, scan_num);
                }

            }

        }

        private void ReadNode_Spectrum(XmlReader spectrumReader, string filename, int scanNum)
        {
            ds_isolationWin isoWinObj = new ds_isolationWin();
            string chargeState = "";

            while (spectrumReader.Read())
            {
                if (spectrumReader.NodeType != XmlNodeType.Element)
                    continue;

                if (spectrumReader.GetAttribute("name") == "ms level")
                {
                    if (spectrumReader.GetAttribute("value") != "2")
                        break;
                }

                if (spectrumReader.Name == "isolationWindow")
                {
                    XmlReader isoWinReader = spectrumReader.ReadSubtree();
                    isoWinReader.MoveToContent();
                    isoWinObj = this.ReadNode_IsolationWindow(isoWinReader);
                }
                else if (spectrumReader.Name == "selectedIon")
                {
                    XmlReader chargeStateFinder = spectrumReader.ReadSubtree();
                    chargeStateFinder.MoveToContent();
                    while (chargeStateFinder.Read())
                    {
                        if (chargeStateFinder.NodeType != XmlNodeType.Element)
                            continue;

                        if (chargeStateFinder.GetAttribute("name") == "charge state")
                            chargeState = chargeStateFinder.GetAttribute("value");
                    }
                }
            }
            if (chargeState != "" && isoWinObj.valid == true)
            {
                string scanTitle = filename + "." + scanNum.ToString().PadLeft(5, '0') + "." + scanNum.ToString().PadLeft(5, '0')
                    + "." + chargeState;

                if(!this.ms2_isoWinInfoDi.ContainsKey(scanTitle))
                    this.ms2_isoWinInfoDi.Add(scanTitle, isoWinObj);
            }

        }

        private ds_isolationWin ReadNode_IsolationWindow(XmlReader isoWinReader)
        {           

            ds_isolationWin isoWinInfo = new ds_isolationWin();
            while (isoWinReader.Read())
            {
                if (isoWinReader.NodeType != XmlNodeType.Element)
                    continue;

                if (isoWinReader.GetAttribute("name") == "isolation window target m/z")
                {
                    isoWinInfo.isolationWinTargetMz = double.Parse(isoWinReader.GetAttribute("value"));
                }
                else if (isoWinReader.GetAttribute("name") == "isolation window lower offset")
                {
                    isoWinInfo.isolationWinLowerOffset = double.Parse(isoWinReader.GetAttribute("value"));
                }
                else if (isoWinReader.GetAttribute("name") == "isolation window upper offset")
                {
                    isoWinInfo.isolationWinUpperOffset = double.Parse(isoWinReader.GetAttribute("value"));
                }
            }

            if (isoWinInfo.isolationWinTargetMz != null && isoWinInfo.isolationWinLowerOffset != null && isoWinInfo.isolationWinUpperOffset != null)
                isoWinInfo.valid = true;

            return isoWinInfo;
           
        }




        private MSRawData CentroidMS1(MSRawData msrdObj, double centroidWinSize)
        {
            PointsCentroid pcObj = new PointsCentroid();
            foreach (MSScan scan in msrdObj.ScanDic.Values)
            {
                if (scan.MsLevel != 1)
                    continue;
                MSPoints newPoints = pcObj.Centroid(scan.Points, centroidWinSize);
                scan.Points = newPoints;
            }
            return msrdObj;
        }
    }
}
