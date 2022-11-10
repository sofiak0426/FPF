using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace FPF
{
    class ds_FilteredOutPsms
    {
        //Recording PSM that meet the filtering criteria
        //Key: PSM ID; Value: a dictionary containing features that meet filtering criteria and their respective feature values
        //Dictionary inside -> Key: feature name; Value: value of feature
        private Dictionary<string, Dictionary<string, string>> psmMeetingCritDic = new Dictionary<string, Dictionary<string, string>>();

        //Counting of PSMs meeting each number of criteria. 
        //e.g. [10,5,6...] -> 10 PSMs meeting none of the filter criteria, 5 PSMs meeting only one of the filter criteria, 6 PSMs meeting two of the filter criteria, etc.
        private List<int> meetingCritNumPsmCnt = new List<int>();
        string _filteredOutFile = "";

        //Initialization
        public ds_FilteredOutPsms(int featNum)
        {
            meetingCritNumPsmCnt = Enumerable.Repeat(0, featNum).ToList();
        }

        public string FilteredOutFile
        {
            get { return this._filteredOutFile; } 
            set { this._filteredOutFile = value; }
        }

        /// <summary>
        /// Adds a filtered out PSM to psmMeetinCritDic and records how many feature criteria it has met by updating MeetingCritNumPsmCnt.
        /// </summary>
        /// <param name="psmName"></param>
        /// <param name="featMeetingCritDic"></param>
        public void AddFilteredOutPsm(string psmName, Dictionary<string, string> featMeetingCritDic)
        {
            this.psmMeetingCritDic.Add(psmName, featMeetingCritDic);
            this.meetingCritNumPsmCnt[featMeetingCritDic.Count - 1]++;
        }

        public void FilteredOutPsmsToFile()
        {
            List<string> filteredOutFileLines = new List<string>();
            //Add header
            filteredOutFileLines.Add("PSM ID,Number of features meeting criteria,Feature and its value meeting criteria");
            foreach (KeyValuePair<string, Dictionary<string, string>> psm in this.psmMeetingCritDic)
            {
                string line = String.Format("{0},{1},", psm.Key, psm.Value.Count);
                foreach (KeyValuePair<string, string> feat in psm.Value)
                    line += String.Format("{0}: {1},", feat.Key, feat.Value);
                line.Trim(',');
                filteredOutFileLines.Add(line);
            }
            File.WriteAllLines(this._filteredOutFile, filteredOutFileLines);
        }

        public void MeetingCritNumPsmCntToConosle()
        {
            //Console.WriteLine(this.meetingCritNumPsmCnt.Sum());
            //this.meetingCritNumPsmCnt[0] = filteredOutCnt - this.meetingCritNumPsmCnt.Sum();
            Console.WriteLine("Number of PSMs that meet different numbers of criteria:");
            Console.WriteLine(String.Format("1 criterion: {0}",this.meetingCritNumPsmCnt[0]));
            for (int i = 1; i < this.meetingCritNumPsmCnt.Count; i++)
            {
                Console.WriteLine(String.Format("{0} criteria: {1}", i + 1, this.meetingCritNumPsmCnt[i]));
            }
        }
    }
}
