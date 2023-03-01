using System;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace ResultReader
{
    [Serializable]
    public class ds_Protein
    {
        private string _protID = "";
        private string _desc = "";
        private double _score = -1;  // TPP is prob. (protein)
        private double _mass = 0; // TPP has it in protein level.
        private string _validation = ""; //peptideShaker: true: confident, false: doubtful
        private Dictionary<string, ds_Peptide> _Peptide_Dic = new Dictionary<string, ds_Peptide>();
        // key: ds_Peptide._modifiedSequence , value: ds_Peptide
        private List<string> _alterProtlist = new List<string>();
        //element: Name of Alter Protein

        public string ProtID
        {
            get { return _protID; }
            set { _protID = value; }
        }

        public string Description
        {
            get { return _desc; }
            set { _desc = value; }
        }

        public double Score
        {
            get { return _score; }
            set { _score = value; }
        }

        public double Mass
        {
            get { return _mass; }
            set { _mass = value; }
        }

        /// <summary>
        /// true: Confident false:doubtful
        /// </summary>
        public string Validation
        {
            get { return _validation; }
            set { _validation = value; }
        }

        /// <summary>
        /// key: ds_Peptide._modifiedSequence , value: ds_Peptide
        /// </summary>
        public Dictionary<string, ds_Peptide> Peptide_Dic
        {
            set { _Peptide_Dic = value; }
            get { return _Peptide_Dic; }
        }

        /// <summary>
        /// element: Name of Alter Protein
        /// </summary>
        public List<string> AlterProtlist
        {
            get { return _alterProtlist; }
            set { _alterProtlist = value; }
        }

        public static ds_Protein DeepClone<ds_Protein>(ds_Protein obj)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;
                return (ds_Protein)formatter.Deserialize(ms);
            }
        }
    }
}
