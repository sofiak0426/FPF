using System.Collections.Generic;
using ResultReader;

namespace FPF
{
    public class ds_DataContainer
    {
        public List<string> idsPsmIdLi = new List<string>(); //The list for ID of all valid PSMs from IDS
        public Dictionary<string, ds_Psm_ForFilter> icsPsmFFDic = new Dictionary <string, ds_Psm_ForFilter>();//Key: PSM ID, Value: ds_Psm_ForFilter: information for each valid PSM from ICS
        public ds_SearchResult idsIproResult; //iprophet file from IDS read by ResultReader
        public ds_SearchResult icsIproResult; //iprophet file from ICS read by ResultReader
    }
}
