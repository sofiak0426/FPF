using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace S2I_Extractor
{
    public class ds_isolationWin
    {
        public double isolationWinTargetMz { get; set; }
        public double isolationWinLowerOffset { get; set; }  // window左邊m/z範圍
        public double isolationWinUpperOffset { get; set; } // window右邊m/z範圍
        public bool valid = false;
    }
}
