using System;
using System.Collections.Generic;

//set enum in this namespace
namespace ResultReader
{
    public enum SearchResult_Source : int
    {
        Mascot_PepXml             = 0,    // pepXml from Mascot: Save "ionscore" value as peptide score in PepXmlProtXmlReader.cs
        XTandem_PepXml            = 1,    // pepXml from X!tandem : Save "hyperscore" value as peptide score in PepXmlProtXmlReader.cs 
        TPP_PepXml                = 2,    // pepXml from TPP Process : Save "peptideprophet_result" probability as peptide score in PepXmlProtXmlReader.cs
        PD_PepXml                 = 3,    // pepXml from PD: Save "XCorr" value as peptide score in PepXmlProtXmlReader.cs /// skip first basic info only including PD working log
        Comet_PepXml              = 4,    // pepXml from Comet: Save "expect" value as peptide score in PepXmlProtXmlReader.cs
        MSGF_PepXml               = 5,    // pepXml from MSGF: Save "SpecEValue" value as peptide score in PepXmlProtXmlReader.cs
        Myrimatch_PepXml          = 6,    // pepXml from Myrimatch: Save "mvh" value as peptide score in PepXmlProtXmlReader.cs
        Mayu                      = 7,    // Mayu Process: Don't save peptide score of Pepxml in PepXmlProtXmlReader.cs
        PeptideShaker             = 8,    // PeptideShaker Process
        Mascot                    = 9,    // Mascot : Pure Mascot Process
        XTandem_tXml              = 10,   // XTandem : Pure XTandem Process
        PD_Percolator_PepXml      = 11,   // XTandem : Pure XTandem Process
        DontCare                  = 99,   // default: Parser Actions like TPP
    };

    public enum XmlParser_Action : int
    {
        Read_PepXml            = 0, //For PepXmlProtXmlReader: Only Read PepXml 
        Read_ProtXml           = 1, //For PepXmlProtXmlReader: Only Read ProtXml
        Read_PepAndProtXml     = 2, //For PepXmlProtXmlReader: Both Read PepXml and ProtXml
    };

}
