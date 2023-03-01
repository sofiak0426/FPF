using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ResultReader
{
    public class SPCEToScanNum
    {        
        Dictionary<string, int> codeToScanNumDi = new Dictionary<string, int>();

        public SPCEToScanNum(String tableFilePath)
        {
            StreamReader sr = new StreamReader(tableFilePath);
            String line;
            while ((line = sr.ReadLine()) != null)
            {
                String[] lineArr = line.Split();
                this.codeToScanNumDi.Add(lineArr[0], Int32.Parse(lineArr[1]));
            }
        }

        public int getScanNum(String spceCode)
        {
            int scanNum;
            codeToScanNumDi.TryGetValue(spceCode, out scanNum);
            if(scanNum == 0)    // 找不到會被設成 0 
                scanNum = -1;
            return scanNum;
        }
            
    }
}
