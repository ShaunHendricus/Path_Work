using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ETherRealTime
{
    class nudWithXML : NumericUpDown, IGetXML, IModified
    {
        public decimal multiplier = 1.0M;
        public bool modified = false;
        //If the XML value to be sent is NOT just the value of the NUD, we have a look up where the first dimension of the array is the value of the NUD and the 2nd dimension is the value to be sent.
        //ie for Drive the NUD has values 0, 1, 12 but we sent to the insturment 0,1,2
        private Int32[,] value_lookup;
        int x = 0;

        public void SetValueLookup(Int32[,] value_map)
        {
            value_lookup = value_map;
        }
        public string GetXML()
        {
            string value_string = "0";  //A Safe default!?

            if (value_lookup == null)
                value_string = Math.Round(Value * multiplier).ToString();
            else
            {
                for (x = 0; x < value_lookup.Length; x++)
                {
                    if (value_lookup[x, 0] == (Int32)Value)
                    {
                        value_string = value_lookup[x, 1].ToString();
                        break;
                    }
                }
            }


            string rtn_string = "";
            string[] strs = Tag.ToString().Split('/');
            //int x = 0;

            for (x = 0; x < strs.Length; x++ )
            {
                if (rtn_string.Length != 0)
                    rtn_string += "\r,";
                rtn_string += "<" + strs[x] + ">";
            }
            rtn_string += value_string;
            for (x = strs.Length - 1;x >= 0; x--)
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
            string value_string = Math.Round(Value * multiplier).ToString();
            string[] strs = Tag.ToString().Split('/');

            return "<" + strs[strs.Length - 1] + ">" + value_string + "</" + strs[strs.Length - 1] + ">\r";
        }
        public bool Modified()
        {
            return modified;
        }
    }
}
