using System;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace ResultReader
{
    [Serializable]
    public class ds_Peptide
    {
        private int _pep_start = 0;    //pep  in protein pos
        private int _pep_end = 0;
        private string _prevAA = "";    //The preceding residue, - if peptide is N-term of protein
        private string _sequence = "";
        private string _nextAA = "";    //The following residue, - if peptide is C-term of protein
        private string _modifiedSequence = ""; //<pep_var_mod_pos>0.000000301000.0</pep_var_mod_pos>
        // Pepxml name modified pep seq to avoid different names of the same peptide.
        private double _theoretical_mass = 0;//Calculated Mr; calculated relative molecular mass.
        private double _score = -1.0;  // Peptide Overall Score(total PSM)
        private string _validation = ""; //peptideShaker: true: confident, false: doubtful
        private bool _isunique = false;
        private List<ds_PSM> _psmList = new List<ds_PSM>();  //element: ds_PSM
        private List<ds_ModPosInfo> _modPosList = new List<ds_ModPosInfo>(); //element: ds_modPosInfo(Pos/mass)

        public int Pep_start
        {
            get { return _pep_start; }
            set { _pep_start = value; }
        }

        public int Pep_end
        {
            get { return _pep_end; }
            set { _pep_end = value; }
        }

        public double Theoretical_mass
        {
            get { return _theoretical_mass; }
            set { _theoretical_mass = value; }
        }

        public double Score
        {
            get { return _score; }
            set { _score = value; }
        }

        public string PrevAA
        {
            get { return _prevAA; }
            set { _prevAA = value; }
        }

        public string Sequence
        {
            get { return _sequence; }
            set { _sequence = value; }
        }

        public string NextAA
        {
            get { return _nextAA; }
            set { _nextAA = value; }
        }

        public string ModifiedSequence
        {
            get
            {
                if (_modifiedSequence != "")
                    return _modifiedSequence;
                else
                    return _sequence;
            }
            set { _modifiedSequence = value; }
        }

        public bool b_IsUnique
        {
            get { return _isunique; }
            set { _isunique = value; }
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
        /// element: ds_PSM
        /// </summary>
        public List<ds_PSM> PsmList
        {
            get { return _psmList; }
            set { _psmList = value; }
        }

        /// <summary>
        /// element: ds_modPosInfo(Pos/mass)
        /// </summary>
        public List<ds_ModPosInfo> ModPosList
        {
            get { return _modPosList; }
            set { _modPosList = value; }
        }

        public static ds_Peptide DeepClone<ds_Peptide>(ds_Peptide obj)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;
                return (ds_Peptide)formatter.Deserialize(ms);
            }
        }
    }

    [Serializable]
    public class ds_ModPosInfo
    {
        private int _modPos;
        private double _modMass;
        
        public int ModPos
        {
            get { return _modPos; }
            set { _modPos = value; }
        }
                
        public double ModMass
        {
            get { return _modMass; }
            set { _modMass = value; }
        }
    }
}
