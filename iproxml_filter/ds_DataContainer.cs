using System.Collections.Generic;
using ResultReader;

namespace iproxml_filter
{
    public class ds_DataContainer
    {
        public List<string> dbPsmNameLi = new List<string>(); //list for all database-searched PSM names
        public Dictionary<string, double> intraPepEuDistDic = new Dictionary <string, double>();
        public Dictionary<string, double> intraProtEuDistDic = new Dictionary<string, double>();
        public ds_SearchResult iproDbSpstResult;
    }
}
