using System.Collections.Generic;
using ResultReader;

namespace iproxml_filter
{
    public class ds_DataContainer
    {
        public List<string> dbPsmNameLi = new List<string>(); //list for all database-searched PSM names
        public Dictionary<string, ds_DbSpecPsm> dbSpecPsmDic = new Dictionary<string, ds_DbSpecPsm>();
<<<<<<< HEAD
        public ds_SearchResult dbSpstIproResult;
=======
        public ds_SearchResult dbSpecIproResult;
>>>>>>> f70e3c0a2d04ccdf2d34744ace106d0068f12d9a
    }
}
