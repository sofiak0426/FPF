using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ResultReader
{
    public class PepXmlMayuCsvReader
    {

        ds_SearchResult searchResultObj = new ds_SearchResult();
        Dictionary<string, int> itemName_Dic = new Dictionary<string, int>();  //string: item name, int: column number

        private HashSet<string> csvProtNameSet = new HashSet<string>();  // 2017-05/12 .csv中每讀一行記錄protein，重複的不記。最後轉換成為searchResultObj.proteinGroupName_Dic
        private List<string> ntermModMassStrLi = new List<string>();     // 2017-12/13 從searchResultObj取出fixModDic跟varModDic中存在的n-terminal modification mass整數
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
                this.MakeProtGroupNameDic();
            }

            
            //remove peptides which are not appear in Mayu csv
            this.searchResultObj.Filter_Peptide(0.0F);
            this.searchResultObj.RefreshPepProt_Dic();
            this.searchResultObj.RefreshPepProtGroup_Dic();

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
            string line = "";

            // 2017-12/12 因為Mayu不會在peptide seq前面列出nterminal modification, 從searchResult的fixModDic跟varModDic找出可能的nterm modification種類存起來
            this.ProcessNtermMod(this.searchResultObj.FixedMod_Dic.Keys.ToList());
            this.ProcessNtermMod(this.searchResultObj.VarMod_Dic.Keys.ToList());

            while ((line = CsvLine.ReadLine()) != null)
            {
                //this.debugLineCounter++;

                string[] elements = line.Split(',');

                if (!initialFlag) //save ColumneVerseItemName
                    initialFlag = this.DicSaveItemName(elements);
                else //parse additional info to searchResultObj 
                    this.Parse_Csv_Info(elements);

                //if (!findFlag)//debug                
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
        private void Parse_Csv_Info(string[] Elements)
        {
            string  psmName        =  Elements[this.itemName_Dic["scan"]];
            string  pepName        =  Elements[this.itemName_Dic["pep"]];
            string  protName       =  Elements[this.itemName_Dic["prot"]];
            string  modInfo        =  Elements[this.itemName_Dic["mod"]];
            double  score          =  Convert.ToDouble(Elements[this.itemName_Dic["score"]]);
            bool    decoy          =  (Elements[this.itemName_Dic["decoy"]]=="1");
            float   mFDR           =  Convert.ToSingle(Elements[this.itemName_Dic["mFDR"]]);           
            

            //decide peptide name
            string peptideId = pepName;
            if (modInfo!="")
                peptideId = this.Transfer_modPepSeq(pepName, modInfo);

            //--- 每行protein就當作proteinGroup，使用HashSet使得不會有重複string。Mayu的.csv並不會有protGroup，但為了後續使用方便需要protGroup_Dic ---//
            this.csvProtNameSet.Add(protName);


            if (!this.searchResultObj.Protein_Dic.ContainsKey(protName))
                return;

            ds_Peptide temp_pepObj = new ds_Peptide();
            // check目前的peptideId是否可以在已建立的Hierarchy中的protein(protName)下找到
            if (this.searchResultObj.Protein_Dic[protName].Peptide_Dic.ContainsKey(peptideId))
            {
                //check whether PSM includes in Peptide's PSMList. If yes: update it.
                temp_pepObj = this.searchResultObj.Protein_Dic[protName].Peptide_Dic[peptideId];
                this.update_pepScore(temp_pepObj, psmName, score);
            }
            // 若前面的peptideId找不到，則依照可能的nterminal mod依次修改peptideId，是否就可以找到
            else if (this.ntermModMassStrLi.Count > 0)
            {
                for (int i = 0; i < this.ntermModMassStrLi.Count; i++)
                {
                    string pep_ntermMod = this.ntermModMassStrLi[i] + peptideId;

                    // 目前假設的nterminal mod找不到的話，continue
                    if (!this.searchResultObj.Protein_Dic[protName].Peptide_Dic.ContainsKey(pep_ntermMod))  
                        continue;

                    temp_pepObj = this.searchResultObj.Protein_Dic[protName].Peptide_Dic[pep_ntermMod];
                    if(this.update_pepScore(temp_pepObj, psmName, score))
                        break;
                }
            }
            else { }

            return;
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
                if (temp_psmObj.QueryNumber.Equals(psmName))
                {
                    temp_psmObj.Score = score;
                    findFlag = true;
                    break;
                }
            }
            return findFlag;
        }

        //--- 20170510 用protName_Li的每個protein作為proteinGroup並依序給予protGroupNum，放進searchResultObj的ProtGroupName_Dic ---//
        private void MakeProtGroupNameDic()
        {
            int i = 0;
            foreach (string protName in this.csvProtNameSet)
            {
                List<string> protGroupLi = new List<string>();
                protGroupLi.Add(protName);
                this.searchResultObj.ProtGroupName_Dic.Add(i, protGroupLi);
                i++;
            }
        }

        private void ProcessNtermMod(List<string> modDiKeysLi)
        {
            foreach (string mod in modDiKeysLi)
            {
                string aaMod = mod.Split('_')[0];
                if (aaMod.Equals("n"))
                {
                    double mass = Math.Round(Double.Parse(mod.Split('_')[1]));
                    this.ntermModMassStrLi.Add("n[" + mass.ToString() + "]");
                }
            }
        }
    }
}
