using System;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace ResultReader
{
    [Serializable]
    public class ds_PSM
    {
        private string _queryNumber = "";
        private double _precursor_mz = 0; //Experimental m/z
        private double _pep_exp_mass = 0; //Experimental Mr
        private string _pep_scan_title = "";
        private float _elutionTime = 0;
        private int _scanNumber = 0;
        private string _rawDataFileName = "";
        private string _SPCE = "";
        private string _algorithmScore = "";  //peptideShaker: save diff scores in diff Search engines
        private int _charge = 0;
        private double _masserror = 0; //Mass error (calculated Mr - experimental Mr)

        private int _rank = 0;
        private bool _isBold = false;    //Mascot Only
        private string _validation = ""; //peptideShaker: true: confident, false: doubtful
        private int _missedCleavage = 0;
        private object _score;    //peptide
        private double _expectValue = 0; //Expectation value corresponding to ions score

        private Dictionary<int, double> _libraChanIntenDi = new Dictionary<int, double>();

        public ds_PSM(SearchResult_Source s)
        {
            switch (s)
            {
                case SearchResult_Source.Mascot_PepXml:
                case SearchResult_Source.Comet_PepXml:
                case SearchResult_Source.MSGF_PepXml:
                case SearchResult_Source.Myrimatch_PepXml:
                case SearchResult_Source.PD_PepXml:
                case SearchResult_Source.XTandem_PepXml:
                case SearchResult_Source.TPP_PepXml:
                    Dictionary<string, double> scoreDic = new Dictionary<string, double>(); //key: Score type, value: Score value
                    this.Score = scoreDic;
                    break;

                default:
                    this.Score = -1.0;
                    break;
            }

        }

        public string SPCE
        {
            get { return _SPCE; }
            set { _SPCE = value; }
        }

        public int scanNumber
        {
            get { return _scanNumber; }
            set { _scanNumber = value; }
        }

        public string rawDataFileName
        {
            get { return _rawDataFileName; }
            set { _rawDataFileName = value; }
        }

        public string QueryNumber
        {
            get { return _queryNumber; }
            set { _queryNumber = value; }
        }

        public string AlgorithmScore
        {
            get { return _algorithmScore; }
            set { _algorithmScore = value; }
        }

        public double Precursor_mz
        {
            get { return _precursor_mz; }
            set { _precursor_mz = value; }
        }

        public double Pep_exp_mass
        {
            get { return _pep_exp_mass; }
            set { _pep_exp_mass = value; }
        }

        public int Charge
        {
            get { return _charge; }
            set { _charge = value; }
        }

        public string Peptide_Scan_Title
        {
            get { return _pep_scan_title; }
            set { _pep_scan_title = value; }
        }

        public float ElutionTime
        {
            get { return _elutionTime; }
            set { _elutionTime = value; }
        }

        public double MassError
        {
            get { return _masserror; }
            set { _masserror = value; }
        }

        public int Rank
        {
            get { return _rank; }
            set { _rank = value; }
        }

        public bool IsBold
        {
            get { return _isBold; }
            set { _isBold = value; }
        }

        /// <summary>
        /// true: Confident false:doubtful
        /// </summary>
        public string Validation
        {
            get { return _validation; }
            set { _validation = value; }
        }

        public int MissedCleavage
        {
            get { return _missedCleavage; }
            set { _missedCleavage = value; }
        }

        public object Score
        {
            get { return _score; }
            set { _score = value; }
        }

        public double ExpectValue
        {
            get { return _expectValue; }
            set { _expectValue = value; }
        }

        public Dictionary<int, double> Libra_ChanIntenDi
        {
            get { return _libraChanIntenDi; }
            set { _libraChanIntenDi = value; }
        }

        public static ds_PSM DeepClone<ds_PSM>(ds_PSM obj)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;
                return (ds_PSM)formatter.Deserialize(ms);
            }
        }

    }
}
