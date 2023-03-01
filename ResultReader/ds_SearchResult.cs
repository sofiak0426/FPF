using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Diagnostics;

namespace ResultReader
{
    [Serializable]
    public class ds_SearchResult
    {
        private List<ds_BasicInfo> _basicInfoList = new List<ds_BasicInfo>(); //element: Inforamtion about fileNAme/SearchEngine/MISC...
        private Dictionary<string, ModificationPack> _varMod_Dic = new Dictionary<string, ModificationPack>(); //key: Varied ModChar_Mass, value: ModificationPack: includes varied modification inforamtion
        private Dictionary<string, ModificationPack> _fixedMod_Dic = new Dictionary<string, ModificationPack>();//key: Fixed ModChar_Mass, value: ModificationPack: includes fixed modification inforamtion
        private Dictionary<string, ds_Protein> _protein_Dic = new Dictionary<string, ds_Protein>(); //key: ds_Protein.AccNo , value: ds_Protein
        private Dictionary<string, ds_ProbabilityTable> _pepProphetProb_Dic = new Dictionary<string, ds_ProbabilityTable>(); //key: #charge or All(general) , value: ds_ProbTable(FDR/ProbMin)
        private Dictionary<string, ds_ProbabilityTable> _protProphetProb_Dic = new Dictionary<string, ds_ProbabilityTable>(); //key: All(general) , value: ds_ProbTable(FDR/ProbMin)
        //pepProphetProb in pepXML, ProtphetProb in protXML
        private Dictionary<int, List<string>> _protGroupName_Dic = new Dictionary<int, List<string>>(); //key: #proteinGroup , value: List_proteinName 
        private Dictionary<int, double> _protGroupProb_Dic = new Dictionary<int, double>(); //key: #proteinGroup , value: group prob (TPP) or confidence (PeptideShaker)
        private SearchResult_Source _source = SearchResult_Source.DontCare;  //where is SearchResult file from
        private Dictionary<string, List<string>> _pepProt_Dic = new Dictionary<string, List<string>>(); //key: peptide name , value: protein name list
        private Dictionary<string, List<string>> _pepProtGroup_Dic = new Dictionary<string, List<string>>(); //key: peptide name , value: protein name(only first protein) list
        private Dictionary<int, double> _libraChannelMz_Dic = new Dictionary<int, double>();  //key: channel number , value: mz

        /// <summary>
        /// element: Inforamtion about fileNAme/SearchEngine/MISC...
        /// </summary>
        public List<ds_BasicInfo> BasicInfoList
        {
            get { return _basicInfoList; }
            set { _basicInfoList = value; }
        }

        /// <summary>
        /// element: Inforamtion about where is SearchResult file from...
        /// </summary>
        public SearchResult_Source Source
        {
            get { return _source; }
            set { _source = value; }
        }

        /// <summary>
        /// key: Varied ModChar_Mass, value: ModificationPack: includes varied modification inforamtion
        /// </summary>
        public Dictionary<string, ModificationPack> VarMod_Dic
        {
            set { _varMod_Dic = value; }
            get { return _varMod_Dic; }
        }

        /// <summary>
        /// key: Fixed ModChar_Mass, value: ModificationPack: includes fixed modification inforamtion
        /// </summary>
        public Dictionary<string, ModificationPack> FixedMod_Dic
        {
            set { _fixedMod_Dic = value; }
            get { return _fixedMod_Dic; }
        }

        /// <summary>
        /// key: ds_Protein.AccNo , value: ds_Protein
        /// </summary>
        public Dictionary<string, ds_Protein> Protein_Dic
        {
            set { _protein_Dic = value; }
            get { return _protein_Dic; }
        }

        /// <summary>
        /// //key: #charge or All(general) , value: ProbTable(FDR/ProbMin)
        /// </summary>
        public Dictionary<string, ds_ProbabilityTable> PepProphetProb_Dic
        {
            set { _pepProphetProb_Dic = value; }
            get { return _pepProphetProb_Dic; }
        }

        /// <summary>
        /// //key: All(general) , value: ProbTable(FDR/ProbMin)
        /// </summary>
        public Dictionary<string, ds_ProbabilityTable> ProtProphetProb_Dic
        {
            set { _protProphetProb_Dic = value; }
            get { return _protProphetProb_Dic; }
        }

        /// <summary>
        /// key: #proteinGroup , value: List_proteinName
        /// </summary>
        public Dictionary<int, List<string>> ProtGroupName_Dic
        {
            set { _protGroupName_Dic = value; }
            get { return _protGroupName_Dic; }
        }

        /// <summary>
        /// key: #proteinGroup , value: group prob
        /// </summary>
        public Dictionary<int, double> ProtGroupProb_Dic
        {
            set { _protGroupProb_Dic = value; }
            get { return _protGroupProb_Dic; }
        }

        /// <summary>
        /// key: peptide name , value: protein name.
        /// </summary>
        public Dictionary<string, List<string>> PepProt_Dic
        {
            set { _pepProt_Dic = value; }
            get { return _pepProt_Dic; }
        }

        /// <summary>
        /// key: peptide name , value: protein group name
        /// </summary>
        public Dictionary<string, List<string>> PepProtGroup_Dic
        {
            set { _pepProtGroup_Dic = value; }
            get { return _pepProtGroup_Dic; }
        }

        /// <summary>
        /// key: channel number , value: mz
        /// </summary>
        public Dictionary<int, double> LibraChannelMz_Dic
        {
            set { _libraChannelMz_Dic = value; }
            get { return _libraChannelMz_Dic; }
        }

        public static ds_SearchResult DeepClone<ds_SearchResult>(ds_SearchResult obj)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;
                return (ds_SearchResult)formatter.Deserialize(ms);
            }
        }

        /// <summary>
        /// remove ProteinGroup with lower score (smaller than threshold score)
        /// </summary>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public void Filter_ProteinGroup(double threshold)
        {
            //Filter Protein_Group GroupName_Dic/GroupProb_Dic and Protein Dic
            List<int> ProtGroupKeys = new List<int>();
            foreach (int ProtGroupKey in this.ProtGroupProb_Dic.Keys)
            {
                ProtGroupKeys.Add(ProtGroupKey);
            }

            foreach (int ProtGroupKey in ProtGroupKeys) //each Protein_Group
            {
                if (this.ProtGroupProb_Dic[ProtGroupKey] < threshold) //need filter
                {
                    //First, Remove the Protein in Protein_Dic
                    int proteinNuminList = ProtGroupName_Dic[ProtGroupKey].Count;
                    
                    for (int i = 0; i < proteinNuminList; i++) //search all protein in one group
                    {
                        string need_delete_protein = (ProtGroupName_Dic[ProtGroupKey])[i];
                        if (this.Protein_Dic.ContainsKey(need_delete_protein))
                        {
                            this.Protein_Dic.Remove(need_delete_protein);
                        }

                    }
                    // Then filter Protein Group one index
                    this.ProtGroupProb_Dic.Remove(ProtGroupKey);
                    this.ProtGroupName_Dic.Remove(ProtGroupKey);
                }
            }

            //Finally, Remove Prot Prob < 0(-1 case becasue not appear in protein prophet)
            List<string> ProtKeys = new List<string>();
            bool meaningfulProtScore = false;
            foreach (string ProtKey in this.Protein_Dic.Keys)
                ProtKeys.Add(ProtKey);

            foreach (string ProtKey in ProtKeys)
            {
                if (this.Protein_Dic[ProtKey].Score >= 0.0) //anyone protein Score  > 0
                    meaningfulProtScore = true;
            }

            if (meaningfulProtScore == true)  //if protein Score is meaningful, filter (Score = -1) case. 
            {
                foreach (string ProtKey in ProtKeys)
                {
                    if (this.Protein_Dic[ProtKey].Score < threshold)
                        this.Protein_Dic.Remove(ProtKey);
                }
            }

            this.RefreshPepProt_Dic();
            this.RefreshPepProtGroup_Dic();
        }


        /// <summary>
        /// remove PSM parts with lower score (smaller tahn threshold),
        /// Then, remove peptide with #0 PSM and remove protein with #0 peptide
        /// </summary>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public void Filter_Peptide(double threshold)
        {
            List<string> ProtKeys = new List<string>();
            foreach (string ProtKey in this.Protein_Dic.Keys)
                ProtKeys.Add(ProtKey); //List for backup all ProtKey

            string KeyScoreType = this.GetKeyScoreStr();
            double KeyScore = -1.0;

            foreach (string ProtKey in ProtKeys)
            {
                List<string> PepKeys = new List<string>();
                foreach (string PepKey in this.Protein_Dic[ProtKey].Peptide_Dic.Keys)
                    PepKeys.Add(PepKey);    //List for backup all PepKey

                foreach (string PepKey in PepKeys)
                {
                    ds_Peptide PepObj = this.Protein_Dic[ProtKey].Peptide_Dic[PepKey];
                    List<ds_PSM> PsmListBackup = new List<ds_PSM>();
                    PepObj.PsmList.ForEach(i => PsmListBackup.Add(i));
                    // This will copy all the items from PsmList to PsmListBackup

                    for (int i = 0; i < PsmListBackup.Count; i++)
                    {
                        if (KeyScoreType == "")
                        {
                            KeyScore = (double)PsmListBackup[i].Score;
                        }
                        else
                        {
                            Dictionary<string, double> scoreDic = (Dictionary<string, double>)PsmListBackup[i].Score;
                            if (scoreDic.ContainsKey(KeyScoreType))
                                KeyScore = scoreDic[KeyScoreType];
                            else
                                KeyScore = -1.0;
                        }

                        if (KeyScore < threshold)
                            PepObj.PsmList.Remove(PsmListBackup[i]);
                    }

                    if (PepObj.PsmList.Count == 0)  //Remove Peptide with no PSM.
                        this.Protein_Dic[ProtKey].Peptide_Dic.Remove(PepKey);
                }

                if (this.Protein_Dic[ProtKey].Peptide_Dic.Count == 0)  //Remove Protein with no Peptide.
                    this.Protein_Dic.Remove(ProtKey);
            }

            //remove non-existing member of Protein in Protein Group dictionary
            List<int> PGKeys = new List<int>();
            foreach (int PGKey in this.ProtGroupName_Dic.Keys)
                PGKeys.Add(PGKey); //List for backup all Protein Group dictionary key(int)

            foreach (int PGKey in PGKeys)
            {
                if (!this.Protein_Dic.ContainsKey((this.ProtGroupName_Dic[PGKey])[0])) //only consider the most important member in each protein group
                {
                    this.ProtGroupName_Dic.Remove(PGKey);
                    this.ProtGroupProb_Dic.Remove(PGKey);
                }
            }

            this.RefreshPepProt_Dic();
            this.RefreshPepProtGroup_Dic();
        }

        //--- modified from Filter_Peptides, without the checking KeyScore, just checking if a peptide object has no PSMs or a protein object has no peptides ---//
        public void CheckSupportFromBottom()
        {
            List<string> protKeys = this.Protein_Dic.Keys.ToList();   //List for backup all ProtKey
            foreach (string protKey in protKeys)
            {
                List<string> pepKeys = this.Protein_Dic[protKey].Peptide_Dic.Keys.ToList();
                foreach (string pepKey in pepKeys)
                {
                    ds_Peptide PepObj = this.Protein_Dic[protKey].Peptide_Dic[pepKey];
                    if (PepObj.PsmList.Count == 0)  //Remove Peptide with no PSM.                    
                        this.Protein_Dic[protKey].Peptide_Dic.Remove(pepKey);                   
                }
                if (this.Protein_Dic[protKey].Peptide_Dic.Count == 0)  //Remove Protein with no Peptide.                
                    this.Protein_Dic.Remove(protKey);               
            }

            List<int> PGKeys = this.ProtGroupName_Dic.Keys.ToList();  //List for backup all Protein Group dictionary key(int)
            foreach (int PGKey in PGKeys)
            {
                if (!this.Protein_Dic.ContainsKey((this.ProtGroupName_Dic[PGKey])[0])) //only consider the most important member in each protein group
                {
                    this.ProtGroupName_Dic.Remove(PGKey);
                    this.ProtGroupProb_Dic.Remove(PGKey);
                }
            }
            this.RefreshPepProt_Dic();
            this.RefreshPepProtGroup_Dic();
        }


        /// <summary>
        /// input targetFDR to get minProb (need input probTableIndex [#charge]) 
        /// </summary>
        /// <param name="targetFDR"></param>
        /// <param name="probTableIndex"></param>
        /// <returns></returns>
        public float GetPepMinProbForFDR(float targetFDR, string probTableIndex)
        {
            float minProb = -1.0F;
            
            if(probTableIndex == "")
                probTableIndex = "all";     //default case

            if (this.PepProphetProb_Dic.ContainsKey(probTableIndex))
            {
                List<ds_ProbToFDR> ListProbToFDR = this.PepProphetProb_Dic[probTableIndex].ProbToFDRlist;
                //order first
                float[] ProbInfos = new float[ListProbToFDR.Count];
                float[] FDRInfos = new float[ListProbToFDR.Count];
                for (int i = 0; i < ListProbToFDR.Count; i++)
                {
                    ProbInfos[i] = ListProbToFDR[i].Min_prob;
                    FDRInfos[i] = ListProbToFDR[i].FDR_rate;
                }
                Array.Sort(FDRInfos, ProbInfos);  //care for the continuous same FDR case.

                minProb = ProbInfos[0];
                float maxFDR = 0.0F;  //find maxFDR which is lower than targetFDR.
                for (int i = 0; i < ListProbToFDR.Count; i++)
                {
                    if ((FDRInfos[i] >= maxFDR) && (FDRInfos[i] <= targetFDR))
                    {
                        if (FDRInfos[i] == maxFDR && ProbInfos[i] > minProb)
                            continue; // no need update larger prob in the same FDR

                        maxFDR = FDRInfos[i];
                        minProb = ProbInfos[i];
                    }
                }
            }
            return minProb;
        }

        /// <summary>
        /// input targetFDR to get minProb (no need input probTableIndex) 
        /// </summary>
        /// <param name="targetFDR"></param>
        /// <returns></returns>
        public float GetProtMinProbForFDR(float targetFDR)
        {
            float minProb = -1.0F;
            string probTableIndex = "all";    //default case

            if (this.ProtProphetProb_Dic.ContainsKey(probTableIndex))
            {
                List <ds_ProbToFDR> ListProbToFDR = this.ProtProphetProb_Dic[probTableIndex].ProbToFDRlist;
                //order first
                float[] ProbInfos = new float[ListProbToFDR.Count];
                float[] FDRInfos = new float[ListProbToFDR.Count];
                
                for (int i = 0; i < ListProbToFDR.Count; i++)
                {
                    ProbInfos[i] = ListProbToFDR[i].Min_prob;
                    FDRInfos[i] = ListProbToFDR[i].FDR_rate;
                }
                Array.Sort(FDRInfos, ProbInfos);  //care for the continuous same FDR case.

                minProb = ProbInfos[0];
                float maxFDR = 0.0F;  //find maxFDR which is lower than targetFDR.
                for (int i = 0; i < ListProbToFDR.Count; i++)
                {
                    if ((FDRInfos[i] >= maxFDR) && (FDRInfos[i] <= targetFDR))
                    {
                        if (FDRInfos[i] == maxFDR && ProbInfos[i] > minProb)
                            continue; // no need update larger prob in the same FDR

                        maxFDR = FDRInfos[i];
                        minProb = ProbInfos[i];
                    }      
                }
            }
            return minProb;
        }


        /// <summary>
        /// input targetFDR to get minProb (no need input probTableIndex) 
        /// </summary>
        /// <param name="targetFDR"></param>
        /// <returns></returns>
        public string GetKeyScoreStr()
        {
            string s = "";
            switch (this.Source)
            {
                case SearchResult_Source.Mascot_PepXml:
                    s = "ionscore";
                    break;

                case SearchResult_Source.PD_PepXml:
                    s = "XCorr";
                    break;

                case SearchResult_Source.Myrimatch_PepXml:
                    s = "mvh";
                    break;

                case SearchResult_Source.MSGF_PepXml:
                    s = "SpecEValue";
                    break;

                case SearchResult_Source.Comet_PepXml:
                    s = "expect";
                    break;

                case SearchResult_Source.XTandem_PepXml:
                    s = "hyperscore";
                    break;

                case SearchResult_Source.TPP_PepXml:
                    s = "peptideprophet_result";
                    break;

                case SearchResult_Source.PD_Percolator_PepXml:
                    s = "percolator_result";
                    break;

                default:
                    s = "";
                    break;
            }
            return s;
        }

        public void RefreshPepProt_Dic()
        {
            this.PepProt_Dic.Clear(); //clear dic

            List<string> protLi = this.Protein_Dic.Keys.ToList();
            foreach (string protKey in protLi)
            {
                List<string> pepLi = this.Protein_Dic[protKey].Peptide_Dic.Keys.ToList();
                foreach (string pepKey in pepLi)
                {
                    if (!this.PepProt_Dic.ContainsKey(pepKey))                    
                        this.PepProt_Dic.Add(pepKey, new List<string>());
                   
                    if (this.PepProt_Dic[pepKey].IndexOf(protKey) == -1)
                        this.PepProt_Dic[pepKey].Add(protKey);
                }
            }


            /*foreach (string ProtKey in this.Protein_Dic.Keys)
            {
                foreach (string PepKey in this.Protein_Dic[ProtKey].Peptide_Dic.Keys)
                {
                    if (!this.PepProt_Dic.ContainsKey(PepKey)) //this pepKey first appears 
                    {
                        List<string> protNameList = new List<string>();
                        protNameList.Add(ProtKey);
                        this.PepProt_Dic.Add(PepKey, protNameList);
                    }
                    else
                    {
                        List<string> protNameList = this.PepProt_Dic[PepKey];
                        if (!protNameList.Contains(ProtKey))  //check wheather protein has been existed in this List?
                            this.PepProt_Dic[PepKey].Add(ProtKey);
                    }

                }
            }*/
        }

        public void RefreshPepProtGroup_Dic()
        {
            this.PepProtGroup_Dic.Clear(); //clear dic
            List<int> removedNumbersinProteinGroupLi = new List<int>();

            foreach (int ProtGroupName_Dic_Key in this.ProtGroupName_Dic.Keys)
            {
                string ProtKey = (this.ProtGroupName_Dic[ProtGroupName_Dic_Key])[0]; //only consider first member in each protein group
                
                if (!this.Protein_Dic.ContainsKey(ProtKey))
                {
                    removedNumbersinProteinGroupLi.Add(ProtGroupName_Dic_Key);
                    continue;
                }
                
                foreach (string PepKey in this.Protein_Dic[ProtKey].Peptide_Dic.Keys)
                {
                    if (!this.PepProtGroup_Dic.ContainsKey(PepKey)) //this pepKey first appears 
                    {
                        List<string> protNameList = new List<string>();
                        protNameList.Add(ProtKey);
                        this.PepProtGroup_Dic.Add(PepKey, protNameList);
                    }
                    else
                    {
                        List<string> protNameList = this.PepProtGroup_Dic[PepKey];
                        if (!protNameList.Contains(ProtKey))  //check whether protein has been existed in this List?
                            this.PepProtGroup_Dic[PepKey].Add(ProtKey);
                    }

                }
            }

            //移除 proteinGroup裡面有、但是protein裡面沒有的entries
            foreach (int i in removedNumbersinProteinGroupLi)
            {
                this.ProtGroupName_Dic.Remove(i);
                this.ProtGroupProb_Dic.Remove(i);
            }

        }

        public void DeterminePeptideUniqueness()
        {
            List<ds_Protein> srProtObjLi = new List<ds_Protein>();
            if (this.PepProtGroup_Dic.Count == 0)
                srProtObjLi = this.Protein_Dic.Values.ToList<ds_Protein>();
            else            
                this.ProtGroupName_Dic.Values.ToList().ForEach(group => srProtObjLi.Add(this.Protein_Dic[group[0]])); // protein group的第一個當作代表   

            foreach (ds_Protein srProtObj in srProtObjLi)
            {
                foreach (ds_Peptide srPepObj in srProtObj.Peptide_Dic.Values)
                {
                    //--- 判斷此peptide是否為unique for protein ---//
                    int protNum = this.PepProtGroup_Dic.Count == 0 ? this.PepProt_Dic[srPepObj.ModifiedSequence].Count 
                                                                   : this.PepProtGroup_Dic[srPepObj.ModifiedSequence].Count;
                    srPepObj.b_IsUnique = (protNum == 1) ? true : false;
                }
            }
        }

        public List<string> MergePeptideFromDiffMod()
        {
            HashSet<string> pepSet = new HashSet<string>();
            foreach (string pep in this.PepProt_Dic.Keys)
            {
                string pepSeq = pep;
                int leftIndex;
                int rightIndex;
                while ((leftIndex = pepSeq.IndexOf("[")) != -1)
                {
                    rightIndex = pepSeq.IndexOf("]");
                    pepSeq = pepSeq.Substring(0, leftIndex) + pepSeq.Substring(rightIndex + 1);
                }
                if (pepSeq.StartsWith("n", StringComparison.Ordinal))
                    pepSeq = pepSeq.Substring(1);

                pepSet.Add(pepSeq);
            }
            return pepSet.ToList();
        }

        public List<string> MergeRedundantPeptide()
        {
            HashSet<string> pepSet = new HashSet<string>();
            foreach (string pep in this.PepProt_Dic.Keys)
            {
                string pepSeq = pep;               
                pepSet.Add(pepSeq);
            }
            return pepSet.ToList();
        }

        /// <summary>
        /// 移除proteinGroup representative以外的所有protein (因為這些將來不會拿來作id or quantitation)
        /// </summary>
        /// <param name="srObj"></param>
        public void FilterProteinDicUponProteinGroup()
        {
            if (this.ProtGroupName_Dic.Count == 0)
                return;

            List<string> proteinNameLi = new List<string>();
            List<int> removedNumInProteinGroupLi = new List<int>();
            foreach (int key in this.ProtGroupName_Dic.Keys)
            {
                if (proteinNameLi.Contains(this.ProtGroupName_Dic[key][0]))
                    continue;
                proteinNameLi.Add(this.ProtGroupName_Dic[key][0]);
            }
            Dictionary<string, ds_Protein> newProtDic = new Dictionary<string, ds_Protein>();

            //寫出新的ProtDic
            foreach (string proteinName in proteinNameLi)
            {
                if (this.Protein_Dic.ContainsKey(proteinName))
                    newProtDic.Add(proteinName, this.Protein_Dic[proteinName]);
            }

            this.Protein_Dic = newProtDic;
        }

        /// <summary>
        /// Merge List "Dic(string, List(string))-Dic-Dic-...-Dic(string, List(string))" into one Dic(string, List(string))
        /// </summary>
        /// <param name="dic_List"></param>
        /// <returns></returns>
        public Dictionary<string, List<string>> MergePepProtGroup_Dic(List<Dictionary<string, List<string>>> dic_List)
        {
            Dictionary<string, List<string>> merged_Dic = new Dictionary<string, List<string>>();

            for (int i = 0; i < dic_List.Count; i++)
            {
                Dictionary<string, List<string>> tmp_Dic = dic_List[i];

                foreach (string pepKey in tmp_Dic.Keys)
                {
                    List<string> tmp_List = tmp_Dic[pepKey];

                    if(!merged_Dic.ContainsKey(pepKey))
                        merged_Dic.Add(pepKey, tmp_List);
                    else
                    {
                        if (merged_Dic[pepKey].Equals(tmp_List))  //dic already has the same list case
                            continue;

                        for (int j = 0; j < tmp_List.Count; j++)
                        {
                            string ProteinStr = tmp_List[j];
                            if(merged_Dic[pepKey].Contains(ProteinStr))
                                continue;
                            else
                                merged_Dic[pepKey].Add(ProteinStr);
                        }  //merge different Protein name in Protein Name List

                    }
                } //end merging each dic
            } //end merging all dics
            return merged_Dic;
        }

        public Dictionary<string, List<string>> MergePepProt_Dic(List<Dictionary<string, List<string>>> dic_List)
        {
            Dictionary<string, List<string>> merged_Dic = this.MergePepProtGroup_Dic(dic_List);
            return merged_Dic;
        }

        //only for test
        [Conditional("After_Filter_Print")]
        public void PrintProtGroupToFile()
        {
            Dictionary<int, List<string>> protGroupName_Dic = this.ProtGroupName_Dic;
            Dictionary<int, double> protProb_Dic = this.ProtGroupProb_Dic;
            StreamWriter pro_sw = new StreamWriter(@"C:\Users\weijhe.GOING\Desktop\ProtIdGroup.txt");
            int prot_test_counter = 1;

            foreach (int protGroup_key in protGroupName_Dic.Keys)
            {
                String line = "";
                line += protGroup_key + ",";
                pro_sw.WriteLine("[{0}]:  " + line + "  Prob:{1},  ProtNum:{2}", prot_test_counter, protProb_Dic[protGroup_key], protGroupName_Dic[protGroup_key].Count);
                protGroupName_Dic[protGroup_key].ForEach(i => pro_sw.Write(i + ", "));
                pro_sw.WriteLine();
                pro_sw.WriteLine();
                prot_test_counter++;
            }
            pro_sw.Flush();  // clear buffer in memory
            pro_sw.Close();
        }

        //only for test
        [Conditional("After_Parse_Print")]
        public void PrintProtPepIdToFile()
        {
            Dictionary<string, ds_Protein> protId_Dic = this.Protein_Dic;
            StreamWriter pro_sw = new StreamWriter(@"C:\Users\weijhe.GOING\Desktop\ProtId.txt");
            StreamWriter pep_sw = new StreamWriter(@"C:\Users\weijhe.GOING\Desktop\PepId.txt");
            int prot_test_counter = 1;
            string KeyScoreType = this.GetKeyScoreStr();

            foreach (string prot_key in protId_Dic.Keys)
            {
                String line = "";
                line += prot_key + ",";
                //line += prot_key;
                pro_sw.WriteLine("[{0}]:  " + line + "  Prob:{1}", prot_test_counter, protId_Dic[prot_key].Score);
                //pro_sw.WriteLine(line);

                int pep_test_counter = 1;
                foreach (string pep_key in protId_Dic[prot_key].Peptide_Dic.Keys)
                {
                    line = "";
                    line += pep_key + ",";
                    pep_sw.WriteLine("[{0}][{1}]:  " + line + "_#PSM:[{2}]", prot_test_counter, pep_test_counter, protId_Dic[prot_key].Peptide_Dic[pep_key].PsmList.Count);

                    if (KeyScoreType == "")
                    {
                        protId_Dic[prot_key].Peptide_Dic[pep_key].PsmList.ForEach(i => pep_sw.Write("P:{0},  ", i.Score));
                    }
                    else
                    {

                        try
                        {
                            protId_Dic[prot_key].Peptide_Dic[pep_key].PsmList.ForEach(i => pep_sw.Write("P:{0},  ", ((Dictionary<string, double>)i.Score)[KeyScoreType]));
                        }
                        catch (KeyNotFoundException)
                        {
                            protId_Dic[prot_key].Peptide_Dic[pep_key].PsmList.ForEach(i => pep_sw.Write("P: no Key Score,  "));
                        }
                    }
                    pep_sw.WriteLine();
                    pep_sw.WriteLine();
                    pep_test_counter++;
                }
                prot_test_counter++;
            }
            pro_sw.Flush();  // clear buffer in memory
            pro_sw.Close();
            pep_sw.Flush();  // clear buffer in memory
            pep_sw.Close();
        }

        //only for test
        [Conditional("After_Filter_Print")]
        public void PrintPepNameToFile()
        {
            Dictionary<string, List<string>> pepProtDic = this.PepProt_Dic;
            StreamWriter pro_sw = new StreamWriter(@"C:\Users\weijhe.GOING\Desktop\pepProtDic_pepName.txt");

            foreach (string key in pepProtDic.Keys)
            {
                String line = "";
                line += key;
                pro_sw.WriteLine(line);
            }
            pro_sw.Flush();  // clear buffer in memory
            pro_sw.Close();
        }

        public void ComparePepProtDiKeys()
        {
            StreamWriter missedKeySW = new StreamWriter("D:/ProjectEdit/SRmissedPepKeys.txt");
            List<string> pepProtKeyLi = new List<string>(this.PepProt_Dic.Keys);
            foreach (string pepProtKey in pepProtKeyLi)
            {
                if (!this.PepProtGroup_Dic.ContainsKey(pepProtKey))
                {
                    missedKeySW.WriteLine(pepProtKey);
                }
            }
            missedKeySW.WriteLine("====================");
            List<string> pepProtGroupKeyLi = new List<string>(this.PepProtGroup_Dic.Keys);
            foreach (string pepProtGroupKey in pepProtGroupKeyLi)
            {
                if (!this.PepProt_Dic.ContainsKey(pepProtGroupKey))
                {
                    missedKeySW.WriteLine(pepProtGroupKey);
                }
            }


            missedKeySW.Close();
        }

    }



    [Serializable]
    public class NeutralLossPack
    {
        public int NeuIdentifier { get; set; }
        public double Neutral_loss { get; set; }

        public NeutralLossPack()
        {
            NeuIdentifier = 0;
            Neutral_loss = 0;
        }
    }

    [Serializable]
    public class ModificationPack
    {
        public int Identifier { get; set; }
        public string ModCode { get; set; }
        public double Mass_diff { get; set; }
        public List<NeutralLossPack> NeupackList { get; set; }

        public ModificationPack()
        {
            Identifier = 0;
            ModCode = "";
            Mass_diff = 0;
            NeupackList = new List<NeutralLossPack>();
        }
    }

    [Serializable]
    public class ds_BasicInfo
    {
        private string _filePath = "";
        private string _version = "";      //search engine version
        private string _datName = "";      //for Mascot only
        private string _db = "";
        private string _FastaVer = "";
        private string _searchPTM = "";    //Variable modifications
        private string _msTol = "";        //Peptide mass tolerance
        private string _msmsTol = "";      //Fragment mass tolerance
        private string _Instrument = "";   //Type of instrument
        private string _pvalueThread = ""; //Significance threshold
        private string _taxonomy = "";     //Taxonomy filter

        public string filePath
        {
            set { _filePath = value; }
            get { return _filePath; }
        }

        public string Version
        {
            set { _version = value; }
            get { return _version; }
        }

        public string datName
        {
            set { _datName = value; }
            get { return _datName; }
        }

        public string DB
        {
            set { _db = value; }
            get { return _db; }
        }

        public string FastaVer
        {
            set { _FastaVer = value; }
            get { return _FastaVer; }
        }

        public string searchPTM
        {
            set { _searchPTM = value; }
            get { return _searchPTM; }
        }

        public string msTol
        {
            set { _msTol = value; }
            get { return _msTol; }
        }

        public string msmsTol
        {
            set { _msmsTol = value; }
            get { return _msmsTol; }
        }

        public string Instrument
        {
            set { _Instrument = value; }
            get { return _Instrument; }
        }

        public string pvalueThread
        {
            set { _pvalueThread = value; }
            get { return _pvalueThread; }
        }

        public string taxonomy
        {
            set { _taxonomy = value; }
            get { return _taxonomy; }
        }
    }

    [Serializable]
    public class ds_ProbabilityTable
    {
        private string _charge;    //classfication type
        private List<ds_ProbToFDR> _probToFDRlist = new List<ds_ProbToFDR>();

        public string Charge
        {
            set { _charge = value; }
            get { return _charge; }
        }

        public List<ds_ProbToFDR> ProbToFDRlist
        {
            get { return _probToFDRlist; }
            set { _probToFDRlist = value; }
        }
    }

    [Serializable]
    public class ds_ProbToFDR
    {
        private float _min_prob;  //ex: 0.998   0.98  ...
        private float _FDR_rate;  //ex: 1%      2%    ...

        public float Min_prob
        {
            set { _min_prob = value; }
            get { return _min_prob; }
        }

        public float FDR_rate
        {
            set { _FDR_rate = value; }
            get { return _FDR_rate; }
        }
    }

}
