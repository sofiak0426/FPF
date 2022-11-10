using System.Collections.Generic;
using ResultReader;

namespace FPF
{
    public class ds_DataContainer
    {
        public List<string> dbPsmIdLi = new List<string>(); //The list for ID of all valid database-searched PSMs
        public Dictionary<string, ds_Psm_ForFilter> dbSpstPsmFFDic = new Dictionary <string, ds_Psm_ForFilter>();//Key: PSM ID, Value: ds_Psm_ForFilter: information for each valid PSM in DB + SL
        public ds_SearchResult iproDbResult; //DB iprophet search result from ResultReader
        public ds_SearchResult iproDbSpstResult; //DB + SL iprophet search result from ResultReader
    }
}
