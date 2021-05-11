using System.Collections.Generic;
using ResultReader;

namespace iproxml_filter
{
    public class ds_DataContainer
    {
        public List<string> dbPsmNameLi = new List<string>(); //list for all database-searched PSM names
        public Dictionary<string, ds_PsmInfo> dbSpstPsmInfoDic = new Dictionary <string, ds_PsmInfo>();
        public ds_SearchResult iproDbResult;
        public ds_SearchResult iproDbSpstResult;
    }
}
