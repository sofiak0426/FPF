using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using NPOI;
using NPOI.HSSF;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using System.Diagnostics;

namespace ResultReader
{
    public class TestClass
    {
        [Conditional("After_Parse_Print")]
        public void TestForSeqCombine(string XlsFile)
        {
            HSSFWorkbook wk;
            HSSFSheet hst;
            HSSFRow hr;
            FileStream fs = new FileStream(XlsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            wk = new HSSFWorkbook(fs);   //file ptr to HSS_wk  //check excel open
            hst = (HSSFSheet)wk.GetSheetAt(0);
            hr = (HSSFRow)hst.GetRow(0); //get row info #0

            Dictionary<string, int> modSeqDic = new Dictionary<string, int>();
            for (int rowNum = 1; rowNum <= hst.LastRowNum; rowNum++) //read row by row
            {
                hr = (HSSFRow)hst.GetRow(rowNum);
                //Parse protein/peptide info
                string pepStr = hr.GetCell(0) != null ? hr.GetCell(0).ToString() : "";
                string modStrs = hr.GetCell(1) != null ? hr.GetCell(1).ToString() : "";
                string modPep = "";

                if (modStrs != "")
                    modPep = this.Transfer_modPepSeq(pepStr, modStrs);
                else
                    modPep = pepStr;

                if (!modSeqDic.ContainsKey(modPep))
                    modSeqDic.Add(modPep, rowNum);
            }
            this.PrintModSeqDic(modSeqDic);
        }

        private string Transfer_modPepSeq(string pepName, string modInfos)
        {
            string[] ModInfos = modInfos.Split(':');   //(modInfos) 13=160.030649:2=160.030649
            string orgPepSeq = pepName;
            string returnseq = "";
            Dictionary<int, int> ModInfoDic = new Dictionary<int, int>();

            if (ModInfos.Length > 0) // reorder ModInfos by mod position(do mod from left to right)
            {
                for (int i = 0; i < ModInfos.Length; i++)
                {
                    int ModPos = int.Parse(ModInfos[i].Split('=')[0]);
                    double tmp_Mass = double.Parse(ModInfos[i].Split('=')[1]);
                    int ModMass = (int)tmp_Mass;
                    ModInfoDic.Add(ModPos - 1, ModMass);
                }

                for (int i = 0; i < orgPepSeq.Length; i++)
                {
                    returnseq += orgPepSeq[i];

                    if (ModInfoDic.ContainsKey(i))
                        returnseq += "[" + ModInfoDic[i].ToString() + "]";
                }
            }
            //ModInfos[i].Split('=')[0]
            return returnseq;
        }

        [Conditional("After_Parse_Print")]
        public void PrintModSeqDic(Dictionary<string, int> modSeqDic)
        {
            StreamWriter pro_sw = new StreamWriter(@"C:\Users\weijhe.GOING\Desktop\ModSeq.txt");
            foreach (string modSeqDic_key in modSeqDic.Keys)
            {
                String line = "";
                line += modSeqDic_key;
                pro_sw.WriteLine(line);
            }
            pro_sw.Flush();  // clear buffer in memory
            pro_sw.Close();
        }

        //test for PSM total number without repeat ones.
        public int CalcPSMs(ds_SearchResult S)
        {
            Dictionary<string, ds_Protein> protId_Dic = S.Protein_Dic;
            Dictionary<string, int> psmid_Dic = new Dictionary<string, int>();
            int count = 0;
            
            foreach (string protId_DicKey in protId_Dic.Keys)
            {
                foreach (string pepId_DicKey in protId_Dic[protId_DicKey].Peptide_Dic.Keys)
                {
                    Dictionary<string, ds_Peptide> pepId_Dic = protId_Dic[protId_DicKey].Peptide_Dic;
                    for (int i = 0; i < pepId_Dic[pepId_DicKey].PsmList.Count; i++) 
                    {
                        string tt = pepId_Dic[pepId_DicKey].PsmList[i].QueryNumber;
                        if (!psmid_Dic.ContainsKey(tt))
                        {
                            psmid_Dic.Add(tt, count);
                            count++;  //only count
                        }
                    }
                }
            }

            /*StreamWriter sw = new StreamWriter(@"C:\Users\weijhe.GOING\Desktop\PSMID.txt");
            foreach (string PSMDic_key in psmid_Dic.Keys)
            {
                String line = "";
                line += PSMDic_key;
                sw.WriteLine(line);
            }
            sw.Flush();  // clear buffer in memory
            sw.Close();*/

            return count;
        }
    }
}
