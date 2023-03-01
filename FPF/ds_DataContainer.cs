using System.Collections.Generic;
using ResultReader;

namespace FPF
{
    public class ds_DataContainer
    {
        public List<string> dbPsmIdLi = new List<string>(); //The list for ID of all valid PSMs from DB searching
        public Dictionary<string, ds_Psm_ForFilter> dbslPsmFFDic = new Dictionary <string, ds_Psm_ForFilter>();//Key: PSM ID, Value: ds_Psm_ForFilter: information for each valid PSM from identification based on DB+SL searching
        public ds_SearchResult dbIproResult; //iprophet file fromidentification based on DB searching read by ResultReader
        public ds_SearchResult dbslIproResult; //iprophet file from identification based on DB+SL searching read by ResultReader
    }
}
