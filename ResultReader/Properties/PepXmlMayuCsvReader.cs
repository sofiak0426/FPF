using System;
using System.Collections.Generic;
using System.IO;

namespace ResultReader
{
    public class PepXmlMayuCsvReader
    {

        ds_SearchResult searchResultObj = new ds_SearchResult();
        Dictionary<string, int> itemName_Dic = new Dictionary<string, int>();  //string: item name, int: column number
        
        //List<int> debugLossPSM_Line_List = new List<int>();
        //int debugLineCounter = 0;

        /// <summary>
        /// Parse_Mayu: one fileName you want to Parse plz put in first Param, second file put into second slot if needed
        /// </summary>
        /// <param name="pepLv_pepXml">pepXML file's Name(pepXML file) after peptide prophet</param>
        /// <param name="protLv_protCvs">Result file's Name(Csv file) after running Mayu</param>
        /// <returns>ds_SearchResult</returns>
        public ds_SearchResult ReadFiles(string pepLv_pepXml, string protLv_protCsv)
        {
            this.searchResultObj.Source = SearchResult_Source.Mayu;
            if ((pepLv_pepXml != "") && (protLv_protCsv != ""))
            {
                //read pepXML from pepXML reader
                PepXmlProtXmlReader PepXmlReaderObj = new PepXmlProtXmlReader();
                this.searchResultObj = PepXmlReaderObj.ReadFiles(pepLv_pepXml, "", XmlParser_Action.Read_PepXml, SearchResult_Source.Mayu);
                //Then, read Csvs of Mayu
                this.ReadMayuCsv(protLv_protCsv);
            }

            //remove peptides which are not appear in Mayu csv
            this.searchResultObj.Filter_Peptide(0.0F);

            return this.searchResultObj;
        }


        /// <summary>
        /// Parse peptide probability to update the result of peptide prophet
        /// </summary>
        /// <param name="protCvs" CVS file name></param>
        private void ReadMayuCsv(string protCsv)
        {
            StreamReader CsvLine = new StreamReader(new FileStream(protCsv, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            bool initialFlag = false; //distinguish whether line is in first row or not.
            bool findFlag = false; //each PSM should be find in original case 
            string line = ""; 

            while ((line = CsvLine.ReadLine()) != null)
            {
                //this.debugLineCounter++;

                string[] elements = line.Split(',');

                if (!initialFlag) //save ColumneVerseItemName
                    initialFlag = this.DicSaveItemName(elements);
                else //parse additional info to searchResultObj 
                    findFlag = this.Parse_Csv_Info(elements);

                //if (!findFlag) debug
                    //this.debugLossPSM_Line_List.Add(debugLineCounter);
            }

        }

        /// <summary>
        /// DicSaveItem: key= ItemName, value: columnNumber
        /// </summary>
        /// <param name="Elements" each element in one row></param>
        /// <param name="itemName_Dic" Dic for "element Name" Verse "which column"></param>
        /// <returns></returns>
        private bool DicSaveItemName(string[] Elements)
        {
            for (int index = 0; index < Elements.GetLength(0); index++)
            {
                this.itemName_Dic.Add(Elements[index], index);
            }
            return true;
        }

        /// <summary>
        /// Parse specific info from CVS.
        /// </summary>
        /// <param name="Elements" each element in one row></param>
        /// <param name="itemName_Dic" Dic for "element Name" Verse "which column"></param>
        private bool Parse_Csv_Info(string[] Elements)
        {
            string  psmName        =  Elements[this.itemName_Dic["scan"]];
            string  pepName        =  Elements[this.itemName_Dic["pep"]];
            string  protName       =  Elements[this.itemName_Dic["prot"]];
            string  modInfo        =  Elements[this.itemName_Dic["mod"]];
            double  score          =  Convert.ToDouble(Elements[this.itemName_Dic["score"]]);
            bool    decoy          =  (Elements[this.itemName_Dic["decoy"]]=="1");
            float   mFDR           =  Convert.ToSingle(Elements[this.itemName_Dic["mFDR"]]);
            string  PepDic_Index   =  "";
            string  PepDic_Index2  =  "";  //for additional n-term mod not shown in Mayu
            ds_Peptide temp_pepObj =  new ds_Peptide();
            bool findFlag = false;  //whether find PSM in the original searchResult.

            //decide peptide_index
            if (modInfo!="")
                PepDic_Index = this.Transfer_modPepSeq(pepName, modInfo);
            else
                PepDic_Index = pepName;

            PepDic_Index2 = "n[43]" + PepDic_Index;

            if (this.searchResultObj.Protein_Dic.ContainsKey(protName) //without n-term mod
                && this.searchResultObj.Protein_Dic[protName].Peptide_Dic.ContainsKey(PepDic_Index))
            {   
                //check whether PSM includes in Peptide's PSMList. If yes: update it.
                temp_pepObj = this.searchResultObj.Protein_Dic[protName].Peptide_Dic[PepDic_Index];
                findFlag = this.update_pepScore(temp_pepObj, psmName, score);
            }

            if ((findFlag == false)  //with n-term mod
                && this.searchResultObj.Protein_Dic[protName].Peptide_Dic.ContainsKey(PepDic_Index2)
                && this.searchResultObj.Protein_Dic.ContainsKey(protName))
            {
                temp_pepObj = this.searchResultObj.Protein_Dic[protName].Peptide_Dic[PepDic_Index2];
                findFlag = this.update_pepScore(temp_pepObj, psmName, score);
            }

            return findFlag;
        }

        /// <summary>
        /// combine pepName and modInfos into modPepSeq for peptide_index   
        /// </summary>
        /// <param name="pepName"></param>
        /// <param name="modInfos"></param>
        /// <returns></returns>
        private string Transfer_modPepSeq(string pepName, string modInfos)
        {
            string[] ModInfos = modInfos.Split(':');   //(modInfos) 13=160.030649:2=160.030649
            string orgPepSeq = pepName;
            string returnseq = "";
            Dictionary<int, int> ModInfoDic = new Dictionary<int, int>();

            if (ModInfos.Length > 0) // reorder ModInfos by mod position(do mod from left to right)
            {
                for(int i = 0; i < ModInfos.Length; i++)
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

        //update peptide's score in PSM level
        private bool update_pepScore(ds_Peptide pepObj, string psmName, double score)
        {
            bool findFlag = false;

            foreach (ds_PSM temp_psmObj in pepObj.PsmList)
            {
                if (temp_psmObj.QueryNumber == psmName)
                {
                    temp_psmObj.Score = score;
                    findFlag = true;
                    break;
                }
            }

            return findFlag;
        }

    }
}
