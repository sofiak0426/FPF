using System;
using System.Collections.Generic;
using System.Text;

namespace iproxml_filter
{
    public static class Parameters
    {
        public enum ParameterType : int
        {
            DbIproFile = 1,
            DbSpstIproFile = 2,
            OutputFile = 3,
            ChannelNum = 4,
            RefChan = 5,
            DecoyPrefix = 6,
            Charge = 7,
            Mass = 8,
            PepLen = 9,
            AvgInten = 10,
            IntraPepEuDist = 11,
            IntraProEuDist = 12
        };

        public static readonly Dictionary<ParameterType, string> parameterDic = new Dictionary<ParameterType, string> {
            { ParameterType.DbIproFile,"Database Iprophet Search File" },
            { ParameterType.DbSpstIproFile , "Database + SpectraST Iprophet Search File" },
            { ParameterType.OutputFile, "Output File" },
            { ParameterType.ChannelNum, "Channel Number" },
            { ParameterType.RefChan, "Reference Channel" },
            { ParameterType.DecoyPrefix, "Decoy Prefix" },
            { ParameterType.Charge, "Charge" },
            { ParameterType.Mass, "Mass" },
            { ParameterType.PepLen, "Peptide Length" },
            { ParameterType.AvgInten, "Average Intensity" },
            { ParameterType.IntraPepEuDist, "Intra-Peptide Euclidean Distance" },
            { ParameterType.IntraProEuDist, "Intra-Protein Euclidean Distance"}
        };
    }
}
