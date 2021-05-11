using System.Collections.Generic;
using ResultReader;

namespace iproxml_filter
{
    public class ds_DataContainer
    {
        public List<string> dbPsmIdLi = new List<string>(); //list for all database-searched PSM names
        public Dictionary<string, ds_Psm_ForFilter> dbSpstPsmFFDic = new Dictionary <string, ds_Psm_ForFilter>();//Key: PSM id
        public ds_SearchResult iproDbResult;
        public ds_SearchResult iproDbSpstResult;
    }
}
