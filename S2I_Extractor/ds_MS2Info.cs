using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace S2I_Extractor
{
    public class ds_MS2Info
    {
        public string filename { get; set; }
        public int scanNum { get; set; }
        public int precursorCharge { get; set; }
        public string scanTitle { get; set; } //應該是以 filename.scanNum.ScanNum.precursorCharge 格式構成
        public double precursorMz { get; set; }
        public double s2i { get; set; }

        public double isolationWinTargetMz { get; set; }
        public double isolationWinLeftMz { get; set; }  // window左邊m/z
        public double isolationWinRightMz { get; set; } // window右邊m/z
    }
}
