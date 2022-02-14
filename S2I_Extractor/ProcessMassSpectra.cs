using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MSReader;
using MSDataStructure;
using MSProcessor;

namespace S2I_Extractor
{
    public class ProcessMassSpectra
    {
        public string msDataType { get; set; }
        public double centroidWinSize { get; set; }

        public double isolationWinSize { get; set; }
        public double prcrTol { get; set; }
        public double prcrIsotopeTol { get; set; }
        public Dictionary<string, MSRawData> msrdDi { get; set; }
        public Dictionary<string, ds_MS2Info> ms2_InfoDi { get; set; }
        public Dictionary<string, ds_isolationWin> ms2_IsolationWinInfoDi { get; set; }  // 從mzML另外讀取的關於isolation window size的資訊


        public ProcessMassSpectra(string dataType, double centroidSize)
        {
            this.msDataType = dataType;
            this.centroidWinSize = centroidSize;
            this.msrdDi = new Dictionary<string, MSRawData>();
            this.ms2_InfoDi = new Dictionary<string, ds_MS2Info>();
        }

        public void ReadAllSpectrumFiles(List<string> filePathLi)
        {
            ReadMassSpectra rmsObj = new ReadMassSpectra();
            foreach (string filePath in filePathLi)
            {
                string fileName_NoExt = Path.GetFileNameWithoutExtension(filePath);
                MSRawData msrdObj =  rmsObj.ReadRawMzML(filePath, this.msDataType, this.centroidWinSize);
                this.msrdDi.Add(fileName_NoExt, msrdObj);

                rmsObj.ReadMzMLAndGetIsolationWindowInfo(filePath);
            }
            this.ms2_IsolationWinInfoDi = rmsObj.ms2_isoWinInfoDi;
        }

        public void ProcessAllMS2(double isoWinSize, double precursorTol, double precursorIsotopeTol)
        {
            this.isolationWinSize = isoWinSize;
            this.prcrTol = precursorTol;
            this.prcrIsotopeTol = precursorIsotopeTol;

            this.ms2_InfoDi = new Dictionary<string, ds_MS2Info>();
            List<string> fileNameLi = this.msrdDi.Keys.ToList();
            foreach (string fileName in fileNameLi)
            {
                MSRawData msrdObj = this.msrdDi[fileName];
                foreach (int scan_num in msrdObj.ScanDic.Keys)
                {
                    if (msrdObj.ScanDic[scan_num].MsLevel != 2)
                        continue;


                    MSScan ms2Scan = msrdObj.ScanDic[scan_num];
                    MSScan precursorScan = msrdObj.ScanDic[ms2Scan.PrecursorScanNum];

                    ds_MS2Info ms2iObj = new ds_MS2Info();
                    ms2iObj.filename = fileName;
                    ms2iObj.scanNum = scan_num;
                    ms2iObj.precursorCharge = (int)ms2Scan.PrecursorCharge;
                    ms2iObj.precursorMz = ms2Scan.PrecursorMZ;
                    ms2iObj.scanTitle = ms2iObj.filename + "." + scan_num.ToString().PadLeft(5, '0') + "." + scan_num.ToString().PadLeft(5, '0')
                        + "." + ms2iObj.precursorCharge.ToString();
                    double S2I = 0;

                    S2I = this.CalcS2I(ms2Scan, precursorScan, ms2iObj.scanTitle);
                   
                    
                    ms2iObj.s2i = S2I;
                    this.ms2_InfoDi.Add(ms2iObj.scanTitle, ms2iObj);
                }
            }
        }

        public void ExportS2I(string outputFilePath)
        {
            StreamWriter sw = new StreamWriter(outputFilePath);
            sw.WriteLine("scan_title,S2I,precursorMz");

            List<string> scanTitles = this.ms2_InfoDi.Keys.ToList();           
            foreach (string scanTitle in scanTitles)           
                sw.WriteLine(scanTitle + "," + this.ms2_InfoDi[scanTitle].s2i + "," + this.ms2_InfoDi[scanTitle].precursorMz);
            sw.Flush();
            sw.Close();
        }

        private double CalcS2I(MSScan ms2Scan, MSScan precursorScan, string scanTitle)
        {
            double s2i = 1;
            //--- obtain the peak list in specific window in MS1(precursorScan) ---//
            Dictionary<double, double> mzIntenDi = this.ms2_IsolationWinInfoDi.ContainsKey(scanTitle) ? 
                this.GetPartialPeaks_According2mzmlIsoWinInfo(precursorScan, ms2Scan, scanTitle) 
                :this.GetPartialPeaks(precursorScan, ms2Scan);                            //取出MS1 isolation window中所有 m/z 的 intensity，存成dictionary
           
            if (mzIntenDi.Count() < 1)
                return s2i;

            double targetMz = this.ms2_IsolationWinInfoDi.ContainsKey(scanTitle) ? this.ms2_IsolationWinInfoDi[scanTitle].isolationWinTargetMz : ms2Scan.PrecursorMZ;

            Dictionary<double, string> mzPINDi = this.DetermineIsotopeMzLi(mzIntenDi, targetMz, ms2Scan.PrecursorCharge); //標記每根peak是precursor或isotope或noise的Dictionary；一開始都設為noise("N")，後續用函式AnnotatePeaks更新標記 (P or I)
            double signalInten = 0;
            double totalInten = 0;
            List<double> mzLi = mzIntenDi.Keys.ToList();
            foreach (double mz in mzLi)
            {
                totalInten += mzIntenDi[mz];

                if (!mzPINDi[mz].Equals("N"))        // 把 P & I 的intensity加總, 當成signal (分子)        
                    signalInten += mzIntenDi[mz];
            }
            s2i = signalInten / totalInten;

            return s2i;
        }

        private Dictionary<double, double> GetPartialPeaks(MSScan precursorScan, MSScan ms2Scan)
        {
            double precursorMz = ms2Scan.PrecursorMZ;
            int lowerIdx, upperIdx;

            lowerIdx = precursorScan.Points.mzList.FindIndex(var => var > precursorMz - this.isolationWinSize / 2);
            upperIdx = precursorScan.Points.mzList.FindLastIndex(var => var < precursorMz + this.isolationWinSize / 2);


            Dictionary<double, double> resultDi = new Dictionary<double, double>();
            if (lowerIdx < 0 || upperIdx < 0)
                return resultDi;

            for (int i = lowerIdx; i <= upperIdx; i++)
                resultDi.Add(precursorScan.Points.mzList[i], precursorScan.Points.heightList[i]);

            return resultDi;
        }

        private Dictionary<double, double> GetPartialPeaks_According2mzmlIsoWinInfo(MSScan precursorScan, MSScan ms2Scan, string scanTitle)
        {
            double targetMz = this.ms2_IsolationWinInfoDi[scanTitle].isolationWinTargetMz;
            double lowerOffset = this.ms2_IsolationWinInfoDi[scanTitle].isolationWinLowerOffset;
            double upperOffset = this.ms2_IsolationWinInfoDi[scanTitle].isolationWinUpperOffset;

            int lowerIdx = precursorScan.Points.mzList.FindIndex(var => var > targetMz - lowerOffset);
            int upperIdx = precursorScan.Points.mzList.FindLastIndex(var => var < targetMz + upperOffset);

            Dictionary<double, double> resultDi = new Dictionary<double, double>();
            if (lowerIdx < 0 || upperIdx < 0)
                return resultDi;

            for (int i = lowerIdx; i <= upperIdx; i++)
            {
                if (!resultDi.ContainsKey(precursorScan.Points.mzList[i]))
                    resultDi.Add(precursorScan.Points.mzList[i], precursorScan.Points.heightList[i]);
            }

            return resultDi;
        }


        private Dictionary<double, string> DetermineIsotopeMzLi(Dictionary<double, double> mzIntenDi, double precursorMz, double charge)
        {
            Dictionary<double, string> mzPINDi = new Dictionary<double, string>();  //標記每根peak是precursor或isotope或noise的Dictionary；一開始都設為N(代表noise)，後續用函式AnnotatePeaks更新標記
            List<double> mzLi = mzIntenDi.Keys.ToList();
            mzLi.ForEach(mz => mzPINDi.Add(mz, "N"));

            // 往右找尋isotope peaks並更新mzPINDi，不需要decreasing intensities check
            mzPINDi = this.AnnotatePeaks(mzIntenDi, mzPINDi, precursorMz, charge, 1.0, false);

            // 如果precursor mass小於1500，把目前的mzPINDi回傳 (不往左找)
            // 若大於1500，很有可能在m/z更小的地方還有isotope，則進行反方向的AnnotatePeaks，且要check是否高度會呈decreasing(最後一個參數設為true)
            if (precursorMz * charge - charge * 1.00794 < 1500)
                return mzPINDi;

            // 往左找isotope peaks並更新mzPINDi，需要decreasing intensities check
            mzPINDi = this.AnnotatePeaks(mzIntenDi, mzPINDi, precursorMz, charge, -1.0, true);

            return mzPINDi;
        }

        private Dictionary<double, string> AnnotatePeaks(Dictionary<double, double> mzIntenDi, Dictionary<double, string> mzPINDi, double precursorMz, double charge, double isotopeUnit, bool checkDecreasingOrder)
        {
            double currentIsoMz = precursorMz;
            bool findPrcr = true;
            double height_PreviousPeak = double.MaxValue;

            while (Math.Abs(currentIsoMz - precursorMz) <= this.isolationWinSize / 2 /*+ this.prcrIsotopeTol*/)
            {
                double tol = findPrcr ? this.prcrTol : this.prcrIsotopeTol;
                double currentIsoCddMz = this.LocateOnePeak(mzIntenDi, currentIsoMz, tol);  // 給isotope理論mz & tolerance, 回傳真正在scan中的mz

                if (currentIsoCddMz != -1.0 && checkDecreasingOrder)
                {
                    double currentIsoCddHei = mzIntenDi[currentIsoCddMz];
                    mzPINDi[currentIsoCddMz] = currentIsoCddHei > height_PreviousPeak ? "N" : findPrcr ? "P" : "I";
                    height_PreviousPeak = currentIsoCddHei > height_PreviousPeak ? height_PreviousPeak : currentIsoCddHei;

                }
                else if (currentIsoCddMz != -1.0)
                {
                    mzPINDi[currentIsoCddMz] = findPrcr ? "P" : "I";
                }
                else
                {
                    // 在預期的m/z處沒有peak被找到，則1. 現在是往正方向的話可以繼續找，用預期的m/z往右； 2. 現在往負方向的話就不繼續找了，把currentIsoCddMz設成一個之後會距離precursorMz超過winSize的值
                    currentIsoCddMz = isotopeUnit > 0 ? currentIsoMz : -100/*precursorMz + this.isolationWinSize - mzStep / charge*/;
                }

                findPrcr = false;
                currentIsoMz = currentIsoCddMz + isotopeUnit / charge;
            }

            return mzPINDi;
        }
        private double LocateOnePeak(Dictionary<double, double> mzIntenDi, double mz, double isoMzTol)
        {
            List<double> mzLi = mzIntenDi.Keys.ToList();
            double isoCddMz = -1.0;     //若此區間內沒有peak，回傳值為-1.0
            double currentMaxHeight = 0;

            for (int i = 0; i < mzLi.Count; i++)
            {
                if (Math.Abs(mzLi[i] - mz) < isoMzTol && mzIntenDi[mzLi[i]] > currentMaxHeight)
                {
                    currentMaxHeight = mzIntenDi[mzLi[i]];
                    isoCddMz = mzLi[i];
                }
            }

            return isoCddMz;
        }
      
    }
}
