using System.Collections.Generic;
using ResultReader;

namespace iproxml_filter
{
    public class ds_DataContainer
    {
        public List<string> dbPsmNameLi = new List<string>(); //list for all database-searched PSM names
        public Dictionary<string, PsmInfo> psmInfoDic = new Dictionary <string, PsmInfo>();
        public ds_SearchResult iproDbSpstResult;
    }
}
