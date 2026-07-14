using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ETherRealTime
{
    class radbutWithXML : RadioButton, IGetXML, IModified
    {
        public bool modified = false;
        public string full_XML = "";

        //In some case ONE XML tag is represented by several Rad Buttons.
        // In this case, we set the Rad Button "Checked" when the value string is the same as value_string_to_set_checked.
        public string value_string_to_set_checked = "";
        public bool Modified()
        {
            return modified;
        }
        public string value_string = "0";
        /// <summary>
        /// We create the full XML line from the Tag of the control which contains the XML Tag and Path. The value is a string stored in value_string
        /// </summary>
        /// <returns></returns>
        public string GetXML()
        {
            string rtn_string = "";
            string[] strs = Tag.ToString().Split('/');

            if (value_string == "")
                return "";

            int x = 0;

            for (x = 0; x < strs.Length; x++)
            {
                if (rtn_string.Length != 0)
                    rtn_string += "\r,";
                rtn_string += "<" + strs[x] + ">";
            }
            rtn_string += value_string;
            for (x = strs.Length - 1; x >= 0; x--)
            {
                rtn_string += "</" + strs[x] + ">\r";
                if (x > 0)
                    rtn_string += ",";
            }

            return rtn_string;
        }
        /// <summary>
        /// We create the XML line from the Tag of the control which contains the XML Tag but IGNORING the Path. The value is a string stored in value_string
        /// </summary>
        /// <returns></returns>
        public string GetXMLnoPath()
        {
            string[] strs = Tag.ToString().Split('/');

            if (value_string == "")
                return "";

            return "<" + strs[strs.Length - 1] + ">" + value_string + "</" + strs[strs.Length - 1] + ">\r";
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            modified = true;
        }
    }
    
}
