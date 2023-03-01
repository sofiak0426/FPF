using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ResultReader
{
    public class ds_ScanTitleInfo
    {
        public int scanNum { get; set; }           
        public String rawDataName { get; set; }    
        public String SPCE { get; set; }           
        public String titleType { get; set; }

        public ds_ScanTitleInfo()
        {
            scanNum = -1;
            rawDataName = "";
            SPCE = "";
            titleType = "";
        }
    }
}
