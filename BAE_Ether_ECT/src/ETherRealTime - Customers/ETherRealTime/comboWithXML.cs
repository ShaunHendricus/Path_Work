using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ETherRealTime
{
    class comboWithXML : ComboBox, IGetXML, IModified
    {
        public bool modified = false;
        public string value_string;
        public string full_XML;

        /// <summary>
        /// The ComboBoxes can be quite involved and do not always offer an obvious link between their contents and the XML that should be produced.
        /// Therefore the code creating the object should set the Tag with the complete XML string. This can include XML Heirachy by using
        /// '\r' carriage returns. ie to set the 2nd Probe type the tag could be somethign like:
        /// "<DUAL_FREQUENCY>\r<PROBE_TYPE>1</PROBE_TYPE>\r</DUAL_FREQUENCY>\r"
        /// </summary>
        /// <returns></returns>
        public string GetXML()
        {
            return full_XML;
        }
        /// <summary>
        /// We create the XML line from the Tag of the control which contains the XML Tag but IGNORING the Path. The value is a string stored in value_string
        /// </summary>
        /// <returns></returns>
        public string GetXMLnoPath()
        {
            string[] strs = Tag.ToString().Split('/');
            return "<" + strs[strs.Length - 1] + ">" + value_string + "</" + strs[strs.Length - 1] + ">\r";
        }
        public bool Modified()
        {
            if (full_XML == null)
                modified = false;
            return modified;
        }
    }
}
