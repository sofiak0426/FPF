using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ResultReader
{
    public class TitleParser
    {                

        public TitleParser()
        {
        }

        public ds_ScanTitleInfo parse(string title)
        {
            ds_ScanTitleInfo stiObj = new ds_ScanTitleInfo();

            //--- Bruker ---//
            // Cmpd 1630, +MSn(487.30), 23.6 min
            if (title.Contains("Cmpd ") && title.Contains("+MSn"))
            {
                string specNo = title.Substring((title.IndexOf("Cmpd ") + 5));
                stiObj.scanNum = Int32.Parse(specNo.Split(',')[0]);
                stiObj.titleType = "Bruker";
            }

            //--- DTASuperCharge ---//
            // Elution from: 34.373 to 34.373 period: 0 experiment: 1 cycles: 1 precIntensity: 413158.0 FinneganScanNumber: 4871 MStype: enumIsNormalMS rawFile: SILAC-A1.raw
            else if (title.Contains("Elution from:") && title.Contains("FinneganScanNumber:") && title.Contains("rawFile:"))
            {
                stiObj.scanNum = Int32.Parse(title.Substring(title.IndexOf("ScanNumber:") + 11, title.IndexOf("MStype") - title.IndexOf("ScanNumber:") - 11).Trim());
                stiObj.rawDataName = title.Substring(title.LastIndexOf(":") + 1, title.LastIndexOf(".RAW", StringComparison.CurrentCultureIgnoreCase) - title.LastIndexOf(":") - 1).Trim();
                stiObj.titleType = "DTASupercharge";
            }

            //--- extractmsn ---//
            // FvB-1.2291.2291.2.dta
            // "50cmTH265minHela4ug_DDAHCDOT_16042501.00765.00765.2"
            // "RTINSECONDS=4763.59429.59429.3" : no rawDataName but retain scan number
            else if (title.Split('.').Length >= 4 && !title.Contains("scans") && !title.Contains("FinneganScanNumber") && !title.Contains(":"))
            {
                for (int i = 1; i < title.Split('.').Length; i++)
                {
                    int r = 0;
                    bool b = Int32.TryParse(title.Split('.')[i], out r);
                    if (b)
                    {
                        stiObj.scanNum = Int32.Parse(title.Split('.')[i]);
                        break;
                    }
                }
                stiObj.rawDataName = title.Split('.')[0];
                stiObj.titleType = "extractmsn";

                if (stiObj.rawDataName.Contains("RTINSECONDS=")) //Remove wrong file name
                    stiObj.rawDataName = "";
            }


            // ---- Hela1ug_QC_Middle_DDA_150226_01_150227005127.t.xml ---- // s can't Parse
            // "File: \"G:\\FusionData\\DIARaw\\Hela1ug_QC_Middle_DDA_150226_01_150227005127.raw\"; SpectrumID: \"432\"; scans: \"1488\" RTINSECONDS=302 "

            //--- Fe_2013Q1_01 ---// s can Parse
            // Cmpd 1084, +MSn(497.31), 16.2 min
            else if (title.Contains("File") && title.Contains(" Spectrum") && title.Contains(" scans: "))
            {
                string s = title.Substring((title.IndexOf(" scans: ") + 8));
                int exp_i = 0;

                if(int.TryParse(s, out exp_i)) //s can Parse
                {
                    stiObj.scanNum = Int32.Parse(s);
                    stiObj.titleType = "Fe_2013Q1_01";
                }
                else //s can't Parse
                {
                    string[] lineArr = title.Replace("\"", "").Split(new char[] { ',', ' ' });
                    stiObj.rawDataName = Path.GetFileName(lineArr[1].Replace(";", ""));
                    stiObj.scanNum = int.Parse(lineArr[5]);
                    stiObj.titleType = "IDPickerWaikok";
                }
            }

            //--- mgfQStar ---//  ps. no scan number
            // File: 940409 114-116 Z 2.wiff, Sample: 940409 114-116 Z 2 (sample number 1), Elution: 21.26 min, Period: 1, Cycle(s): 925 (Experiment 4)
            else if (title.Contains("File:") && title.Contains("Sample:") && title.Contains("Elution:"))
            {
                stiObj.scanNum = -1;
                stiObj.rawDataName = System.IO.Path.GetFileNameWithoutExtension(title.Substring(title.IndexOf("File: ") + 6, title.IndexOf(", Sample:") - title.IndexOf("File: ") - 6));
                stiObj.SPCE = this.getSPCE_mgfQStar(title);
                stiObj.titleType = "mgfQStar";
            }

            //--- mgfWaters ---//
            // 1076: Sum of 2 scans in range 2272 (rt=3433.63, f=2, i=360) to 2280 (rt=3446.34, f=2, i=362) [Z:\awan\Awan_CE 15 neg_0711213.raw]
            // 527: Scan 2784 (rt=3304.43, f=2, i=233) [FilePath.FileExtension]
            else if ((title.Contains("Scan") || (title.Contains("Sum of") && title.Contains("scans"))) && title.Contains("rt="))    //&& title.Contains("f=") && title.Contains("i="))  有時候轉出來的title沒有f跟i
            {
                //--- scan number ---//
                if (title.Contains("scans"))   //Sum of 2 scans
                    stiObj.scanNum = Int32.Parse(title.Substring(title.IndexOf("range") + 5, title.IndexOf('(') - (title.IndexOf("range") + 5)));
                else                            //single Scan
                    stiObj.scanNum = Int32.Parse(title.Substring((title.IndexOf("Scan") + 4), (title.IndexOf("(") - (title.IndexOf("Scan") + 4))).Trim());
                //--- raw data name ---//
                stiObj.rawDataName = (title.Substring(title.IndexOf("[") + 1, (title.LastIndexOf("]") - (title.IndexOf("[") + 1))));
                if (stiObj.rawDataName.Contains("\\"))  //因為路徑類型有可能是//140.123.... or Z:\  in 20081023
                    stiObj.rawDataName = Path.GetFileNameWithoutExtension(stiObj.rawDataName);
                stiObj.titleType = "mgfWaters";
            }

            //--- mzData ---//
            //spectrumId=722 Polarity=positive ScanMode=MassScan Time In Seconds=4691.78 acqNumber=3971
            //spectrumId=3819 Filter=ITMS + c NSI d Full ms2 463.16@pqd32.50 [50.00-1400.00] PeakProcessing=continuous Polarity=positive ScanMode=MassScan TimeInMinutes=37.673165 acqNumber=3819 <---by 植物暨微生物研究所, 2009/2/26 Charles
            else if (title.Contains("spectrumId=") && title.Contains("Polarity=") && title.Split(' ')[title.Split(' ').Length - 1].Contains("acqNumber="))
            {
                string specNo = title.Substring((title.IndexOf("acqNumber") + 10));
                if (specNo.Contains(":"))
                    stiObj.scanNum = Int32.Parse(specNo.Substring(0, specNo.IndexOf(":")));
                else if (specNo.Contains(" "))
                    stiObj.scanNum = Int32.Parse(specNo.Substring(0, specNo.IndexOf(" ")));
                else
                    stiObj.scanNum = Int32.Parse(specNo);
                stiObj.titleType = "mzData";
            }

            //--- mzData 2 ---//
            // spectrumId=1308 acqNumber=3823 ScanMode=MassScan Polarity=positive
            // spectrumId=3128 acqNumber:=87428748 acqNumber:=87548756 acqNumber:=87688784 acqNumber=8790 ScanMode=MassScan Polarity=positive
            else if (title.Contains("spectrumId=") && title.Contains("Polarity=") && (title.Contains("acqNumber:=") || title.Contains("acqNumber=")) && !title.Split(' ')[title.Split(' ').Length - 1].Contains("acqNumber="))
            {
                string specNo = title.Substring((title.IndexOf("spectrumId=") + 11));
                stiObj.scanNum = Int32.Parse(specNo.Split(' ')[0]);
                stiObj.titleType = "mzData 2";
            }

            //---mzData 3---//
            // yani_iTRAQ8plex_F1-10-ALL-s30-F003676
            //  SpectrumID: &quot;8846&quot;; File: &quot;D:\YuJuChenLab Data\yani\ITRAQ8plex\yani_iTRAQ8plex_F4-2.raw&quot;
            else if (title.Contains("SpectrumID:") && title.Contains("File: "))
            {
                string specNo = title.Split(';')[0].Substring((title.Split(';')[0].IndexOf("SpectrumID:") + 11));
                string filePos = title.Split(';')[1].Substring((title.Split(';')[1].IndexOf("File: ") + 5));
                stiObj.scanNum = Int32.Parse(specNo.Substring(specNo.IndexOf("\"") + 1, (specNo.LastIndexOf("\"") - specNo.IndexOf("\"") - 1)));
                stiObj.rawDataName = Path.GetFileName(filePos.Substring(filePos.IndexOf("\"") + 1, (filePos.LastIndexOf("\"") - filePos.IndexOf("\"") - 1)));
                stiObj.titleType = "mzData 3";
            }

            //--- raw2msm ---//
            // Elution from: 19.41 to 23.44 period: iTRAQ_6-mix_1to5_PQD32_profile_1,54pmol_Q0,55.raw experiment: 1 cycles: 1 precIntensity: 6913811.0 FinneganScanNumber: 821        
            else if (title.Contains("Elution from") && title.Contains("FinneganScanNumber:") && !title.Contains("rawFile:"))
            {
                int parts = title.Split(':').Length;
                stiObj.scanNum = Int32.Parse(title.Split(':')[parts - 1].Trim());
                stiObj.rawDataName = title.Substring(title.IndexOf("period: ") + 8, title.IndexOf(" experiment: ") - title.IndexOf("period: ") - 12);    //.raw記得也要去掉
                stiObj.titleType = "raw2msm";
            }

            //--- raw2msm2 ---//
            // RawFile: 20101101_labelfree_18_3_pH8_1.raw FinneganScanNumber: 2245 _sil_
            else if (!title.Contains("Elution from") && title.Contains("FinneganScanNumber:") && title.Contains("RawFile:"))
            {
                string part = title.Substring(title.IndexOf("FinneganScanNumber: ") + 20);
                stiObj.scanNum = Int32.Parse(part.Split(' ')[0]);
                string str = title.Substring(title.IndexOf("RawFile: ") + 9);
                stiObj.rawDataName = str.Split(' ')[0].Replace(".raw", "");
                stiObj.titleType = "raw2msm2";
            }

            //--- TripleTOF ---//
            // Locus:1.1.1.2958.2
            else if (title.Contains("Locus:") && title.Split('.').Length >= 5)
            {
                if (title.Contains("File"))  //mmmtoast edit for new locus title 2012SEP15
                    stiObj.rawDataName = title.Split('\"')[1].Split('.')[0];
                stiObj.SPCE = this.getSPCE_TripleTOF(title);
                stiObj.titleType = "TripleTOF";
            }

            //--- TripleTOF 2 ---//
            //Precursor: 362.2180, Sample: Enolase_test_110328_1.wiff (sample 1), Time: 22.97 min, Cycle & Experiment List: (2014, 2)
            else if (title.Contains("Precursor:") && title.Contains("Cycle & Experiment List:") && title.Contains("Sample:"))
            {
                stiObj.rawDataName = System.IO.Path.GetFileNameWithoutExtension(title.Substring(title.IndexOf("Sample: ") + 8, title.IndexOf(" (sample") - title.IndexOf("Sample: ") - 8));
                stiObj.SPCE = this.getSPCE_TripleTOF2(title);
                stiObj.titleType = "TripleTOF 2";
            }

            //--- unknown ---//
            //File:20101027ID67.mzXML Scans:3047 RT:19.8338min Charge:2+ Fragmentation:cid
            else if (title.Contains("File:") && title.Contains("Scans") && title.Contains("Fragmentation"))
            {
                string part = title.Substring(title.IndexOf("Scans:") + 6);
                stiObj.scanNum = Int32.Parse(part.Split(' ')[0]);
                stiObj.titleType = "unknown";
            }

            //--- yani_iTRAQ ---//
            // case 1: yani_iTRAQ8plex_F4-1.91.91.3 File:”yani_iTRAQ8plex_F4-1.raw”, NativeID:”controllerType=0 controllerNumber=1 scan=91” RTINSECONDS=108.8298
            // case 2: Orbi - 100104_A549_8plex_IPG400_inj1_4_fr_35.2705.2705.4 File:"Orbi - 100104_A549_8plex_IPG400_inj1_4_fr_35.raw", NativeID:"controllerType=0 controllerNumber=1 scan=2705"
            else if (title.Contains("File:") && title.Contains("NativeID:") && title.Contains("scan"))
            {
                //--- 舊的parse方法，有問題 ---//
                //string[] lineArr = title.Replace("\"", "").Split(new char[] { ',', ' ' });
                //stiObj.rawDataName = lineArr[1].Replace("File:", "");
                //stiObj.scanNum = int.Parse(lineArr[5].Replace("scan=", ""));
                
                //--- 新的 parsing ---//
                //--- parse scan number ---//
                int startPos = title.LastIndexOf("scan=");
                int endPos = title.IndexOf("\"", startPos);
                string str = title.Substring(startPos, endPos - startPos);
                stiObj.scanNum = int.Parse(str.Replace("scan=", ""));
                //--- parse file name ---//
                startPos = title.IndexOf("File");
                endPos = title.IndexOf(",", startPos);
                str = title.Substring(startPos, endPos - startPos);
                stiObj.rawDataName = str.Replace("File:", "").Replace("\"", "").Replace(".raw", ""); 

            }
           
            else if (title.Contains("File_") && title.Contains("_SpectrumID_") && title.Contains("_scans_"))
            {
                // 20171127 新的case
                //File_FN_S:\2017-09-16_Batch014_TMT10_Prot_QE(IoC)\20170916_LC_TMTB014_prot_F01_01.raw_RN_SpectrumID_FN_2105_RN_scans_FN_4640
                //File_FN_C:\TPP-temp\2017-03-14_LC-Batch004_TMT10_QE\20170314_LC_TMTB4_prot_F02_01.raw_RN_SpectrumID_FN_1845_RN_scans_FN_4351
                if (title.Contains("File_FN_") && title.Contains("RN_scans_FN_"))
                {
                    int index = title.IndexOf("_RN_SpectrumID_FN_");
                    stiObj.rawDataName = Path.GetFileName(title.Substring(title.IndexOf("File_") + 8, index - title.IndexOf("File_") - 8));
                    stiObj.scanNum = Int32.Parse(title.Substring(title.LastIndexOf('_') + 1));
                    stiObj.titleType = "IDPickerWaikok2-2";
                }
                // IDPicker檔案內的標題:       File_D:\YuJuChenLab Data\TPP\LC\2016-11-15_PC_Px2roteome\20161115_PC9_200ug_prot_RP_F3.raw_SpectrumID_34_scans_3191
                else
                {
                    int index = title.IndexOf("_SpectrumID_");
                    stiObj.rawDataName = Path.GetFileName(title.Substring(title.IndexOf("File_") + 5, index - title.IndexOf("File_") - 5));
                    stiObj.scanNum = Int32.Parse(title.Substring(title.LastIndexOf('_') + 1));
                    stiObj.titleType = "IDPickerWaikok2";
                }
            }
            
            
            // cancer moonshot 偉國search result 檔案內的標題 (和上面差一個冒號): File:C:\TPP-temp\2017-03-14_LC-Batch004_TMT10_QE\20170314_LC_TMTB4_prot_F01_01.raw_SpectrumID_263_scans_2618
            else if (title.Contains("File:") && title.Contains("_SpectrumID_") && title.Contains("_scans_"))
            {
                int index = title.IndexOf("_SpectrumID_");
                stiObj.rawDataName = Path.GetFileName(title.Substring(title.IndexOf("File:") + 5, index - title.IndexOf("File:") - 5));
                stiObj.scanNum = Int32.Parse(title.Substring(title.LastIndexOf('_') + 1));
                stiObj.titleType = "MoonshotWaikok";
            }


            else
            {
                stiObj.rawDataName = "Untreated" + title;
                stiObj.titleType = "CAN'T BE DECIDED";
            }
            

            return stiObj;
        }


        private string getSPCE_TripleTOF2(string title)
        {
            //////Precursor: 362.2180, Sample: Enolase_test_110328_1.wiff (sample 1), Time: 22.97 min, Cycle & Experiment List: (2014, 2)////
            ////////sample=1 period=1 cycle=2014 experiment=2
            string s;
            string p;
            string c;
            string e;

            s = title.Substring(title.IndexOf("(sample ") + 8, title.IndexOf("), Time: ") - title.IndexOf("(sample ") - 8);
            p = "1";
            string cycleexp = title.Substring(title.IndexOf("Cycle & Experiment List: (") + 26, title.Length - title.IndexOf("Cycle & Experiment List: (") - 27);
            c = cycleexp.Split(',')[0].Trim();
            e = cycleexp.Split(',')[1].Trim();
            string SPCE = s + "." + p + "." + c + "." + e;
            return SPCE;
        }

        private string getSPCE_TripleTOF(string title)
        {
            //////Locus:1.1.1.2952.4////////////sample=1 period=1 cycle=2866 experiment=12
            string s;
            string p;
            string c;
            string e;

            s = title.Split('.')[1];
            p = title.Split('.')[2];
            c = title.Split('.')[3];
            e = title.Split('.')[4].Split(' ')[0]; //2012AUG17 mmmtoast Nemo Nai-Yuan FOR NEW LOCUS DATA..
            string SPCE = s + "." + p + "." + c + "." + e;
            return SPCE;
        }

        private string getSPCE_mgfQStar(string title)
        {
            string s;
            string p;
            string c;
            string e;

            s = title.Substring(title.IndexOf("sample number") + 14, 1);
            p = title.Substring(title.IndexOf("Period: ") + 8, 1);
            p = (Int32.Parse(p) - 1).ToString();

            int s1 = title.IndexOf("Cycle(s): ") + 10;
            int s2 = title.IndexOf(' ', s1);
            c = title.Substring(s1, s2 - s1);
            int s3 = 0;
            while (s3 < c.Length)
            {
                if (!(c[s3] >= '0' && c[s3] <= '9'))
                    break;
                s3++;
            }
            c = c.Substring(0, s3);
            if (c.EndsWith(","))
            {
                c = c.Substring(0, c.Length - 1);
            }
            e = title.Substring(title.IndexOf("Experiment ") + 11, 1);

            //_query2spce[query] = s + "." + p + "." + c + "." + e;
            string SPCE = s + "." + p + "." + c + "." + e;
            return SPCE;
        }
    }
}
