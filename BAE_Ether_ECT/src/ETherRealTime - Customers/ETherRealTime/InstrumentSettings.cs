using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Windows.Forms;

namespace ETherRealTime
{
    class InstrumentSettings
    {
        ArrayList XML_Values = new ArrayList(); //This is a list of PARENT XML tags, so there may well be only one, ie INSTRUMENT with all others being children of this.
        ControlValue current_control_value;

        Int32 tag_counter = 0;

        public InstrumentSettings() //An empty constructor. Used before anything is connected to prevent NULL
        {
        }
        public InstrumentSettings(StringBuilder xml_settings, out bool xml_ok)
        {
            //Handle the fact that some files use \r and some use \n:
            string[] xml_strings = xml_settings.ToString().Split('\r');
            if (xml_strings.Length == 1)    //ie Nothing to split using \r
                xml_strings = xml_settings.ToString().Split('\n');

            if (xml_strings.Length < 2)
            {
                xml_ok = false;
                return;
            }
            //We do sometimes fail to get a complete file back, so do a basic check. SOMETIMES the final TAG is not in the last element, especially if the data has come from a file.
            if (!(xml_strings[xml_strings.Length - 1] == "</INSTRUMENT>" || xml_strings[xml_strings.Length - 2] == "</INSTRUMENT>"))
            {
                xml_ok = false;
                return;
            }
            //Go through each string now and see if the XML tag is used by any of the controls.
            foreach (string str in xml_strings)
            {
                ProcessXMLString(str);
            }
            xml_ok = true;
        }
        /// <summary>
        /// Pass in one LONG string (probably from a file) that contains all the XML.
        /// </summary>
        /// <param name="xml_settings"></param>
        /// <param name="xml_ok"></param>
        public InstrumentSettings(String xml_settings, out bool xml_ok)
        {
            //Handle the fact that some files use \r and some use \n:
            string[] xml_strings = xml_settings.Split('\r');
            if (xml_strings.Length == 1)    //ie Nothing to split using \r
                xml_strings = xml_settings.ToString().Split('\n');

            if (xml_strings.Length < 2)
            {
                xml_ok = false;
                return;
            }

            //Go through each string now and see if the XML tag is used by any of the controls.
            foreach (string str in xml_strings)
            {
                if (ProcessXMLString(str) == false)
                {
                    xml_ok = false;
                    return;
                }
            }
            xml_ok = true;
        }

        /// <summary>
        /// How many Children are in thie ControlValue?
        /// Calling this also resets the child counter that is used by GetNextChild()
        /// </summary>
        /// <returns></returns>
        public Int32 TopTagCount()
        {
            tag_counter = 0;

            if (XML_Values == null)
                return 0;
            return XML_Values.Count;
        }
        /// <summary>
        /// A function that is useful in a loop, it just gets thte next child until they run out, when it returns NULL.
        /// Reset the Children by calling ChildrenCount.
        /// </summary>
        /// <returns></returns>
        public ControlValue GetNextTag()
        {
            if (XML_Values == null || tag_counter >= XML_Values.Count)
                return null;

            return (ControlValue)(XML_Values[tag_counter++]);
        }

        public bool ProcessXMLString(string str)
        {
            string temp = str.Trim();
            int first_close_bracket = temp.IndexOf('>');
            int last_open_bracket = temp.LastIndexOf('<');
            int first_end_sign = temp.IndexOf('/');
            int first_open_bracket;
            int first_space;
            //int position_in_str = -1;
            string tag = "";
            string value = "";

            if (str.Contains("DUAL_FREQUENCY"))
                tag = "ian";    //Just to break on!

            while (str.Length > 1)  //At a minimum must have an Open and Close tri-bracket <>
            {
                first_open_bracket = str.IndexOf('<');
                first_close_bracket = str.IndexOf('>');
                first_space = str.IndexOf(' ');
                first_end_sign = str.IndexOf('/');
                //Get the first TAG.
                if (first_close_bracket <= first_open_bracket)
                    return true;
                tag = str.Substring(first_open_bracket+1, first_close_bracket - first_open_bracket -1);
                //Some lines have a tag at the start and then the data then a closing '/' without the tag again, check for this! (ie <DATA IACS="0" IACS_NPL="0" SHIM="0"/>)
                if ((first_space > 0 && first_space < first_close_bracket) && first_end_sign == first_close_bracket-1)
                {
                    tag = tag.Replace("/", "");
                    //In this situation the start tag ends at the first SPACE and internal data can be also be split by a SPACE
                    string[] values = tag.Split(' ');
                    //BUT If the value had a space in it, the split will be wrong. TO fix this, we go through the array backwards
                    // and each val that doesn't end in a '"' has the string after it appended to it.
                    for (int x = values.Length - 2; x > 0; x--)
                    {
                        if (values[x].EndsWith("\""))
                            continue;
                        values[x] += " " + values[x + 1];
                        values[x + 1] = ""; //Set top blank so we can remove afterwards.
                    }
                    ControlValue tmp = new ControlValue(values[0]);
                    current_control_value.AddChild(tmp);
                    current_control_value = tmp;
                    for (int x = 1; x < values.Length; x++ )
                    {
                        //Problem we have is that some VALES may have a space in them, ie "LOAD & SAVE", these have been split up now, so we must 
                        string[] split_up = values[x].Split('=');    //Split up the various values and tags that are split up by an '='
                        if (split_up.Length == 1)
                            continue;
                        //For these embedded tags, we add the parent tag then a SPACE then the child.
                        ControlValue tmp2 = new ControlValue(values[0] + " " + split_up[0].Replace("\"", ""), split_up[1].Replace("\"", ""));
                        current_control_value.AddChild(tmp2);
                        CheckControlValueDefaults(tmp2);
                    }
                    current_control_value = tmp.parent;
                    return true;
                }
                //Remove the tag we've just read.
                str = str.Substring(first_close_bracket+1);
                //Look to see where the first opening bracket may be
                first_open_bracket = str.IndexOf('<');
                //We read in a VALUE until we reach an open bracket OR the end of the line
                if (first_open_bracket == -1)
                    first_open_bracket = str.Length;
                value = str.Substring(0, first_open_bracket);
                //Remove the value we've just read.
                str = str.Substring(first_open_bracket);

                //We now have a Tag, or Value or End Tag.
                if (tag.Length > 0 && tag[0] == '/')    //END tag.
                {
                    if (current_control_value == null)  //There Has been a problem, but we may have read in the first set of XML.
                        return false;
                    //If the end tag matches the start tag of the current ControlValue, we go back to the parent.
                    if (current_control_value.xml_path == tag.Substring(1))
                    {
                        //If there's a parent, go to it. If not, we must have finished!
                        if (current_control_value.parent != null)
                        {
                            current_control_value = current_control_value.parent;
                        }
                        else
                        {
                            current_control_value = null;
                        }
                    }
                }
                else if (tag.Length > 0)                //START tag and perhaps a VALUE.
                {
                    //If it's a start tag, we add this as a child to the existing tag, or if no existing tag, create one as this must be the first!
                    if (current_control_value == null)
                    {
                        current_control_value = new ControlValue(tag, value);
                        //Check the Control Value. This also sets up things like the multipliers.
                        CheckControlValueDefaults(current_control_value);
                        XML_Values.Add(current_control_value);
                    }
                    else    //Make it a child, so create a new ControlValue, add it to the parent and set it to be the current one!
                    {
                        ControlValue ctv = new ControlValue(tag, value);    //Create a new ControlValue.
                        current_control_value.AddChild(ctv);                //Add the new child to the parents Chlidren.
                        //Check the Control Value. This also sets up things like the multipliers.
                        CheckControlValueDefaults(ctv);
                        current_control_value = ctv;                        //Set this child to be the current.
                    }
                }
                else if (value.Length > 0)              //VALUE only.
                {
                    current_control_value.display_value = value;
                }
                else                                    //ERROR!
                {
                    return false;
                }
            }
            //Do we have a block start tag?
/*            if (first_close_bracket > last_open_bracket)
            {
                value = temp.Substring(1, first_close_bracket - 1);
                if (first_end_sign == -1)   //Starting group tag
                {
                    path += value + "/";
                }
                else if (value.StartsWith("/")) //Are we a closing group Tag?
                {
                    value = value.Substring(1); //Remove the preceeding '/'
                    if (path.EndsWith(value + "/"))
                        path = path.Replace(value + "/", "");
                }
            }
            else if (first_end_sign > first_close_bracket)
            {
                //Get the value from the end of the string.
                tag = temp.Substring(first_end_sign + 1, temp.Length - first_end_sign - 2);
                //Check that the string starts with the same
                if (!temp.StartsWith("<" + tag))
                    return false;
                value = temp.Substring(first_close_bracket + 1, last_open_bracket - first_close_bracket - 1);

                //Lets extract the useful info and store the data!
                if (path == "")
                    UpdateOrAddControlValue(tag, tag, value);
                else
                    UpdateOrAddControlValue(path + tag, tag, value);
            }
 * */
            return true;
        }
        private void CheckControlValueDefaults(ControlValue ctrl_val)
        {
            switch (ctrl_val.xml_path)
            {
                case "FREQUENCY":
                    //ctrl_val.integer_value = Convert.ToInt32(value);

                    if (ctrl_val.integer_value > 1000000)
                        ctrl_val.multiplier = 1000000;
                    else if (ctrl_val.integer_value > 1000)
                        ctrl_val.multiplier = 1000;
                    else
                        ctrl_val.multiplier = 1;
                    ctrl_val.decimal_display_value = (decimal)ctrl_val.integer_value / ctrl_val.multiplier;
                    break;
                case "PHASE":
                    //ctrl_val.integer_value = Convert.ToInt32(value);
                    ctrl_val.multiplier = 1000;
                    ctrl_val.decimal_display_value = (decimal)ctrl_val.integer_value / ctrl_val.multiplier;
                    break;
                case "STRETCH":
                    //ctrl_val.integer_value = Convert.ToInt32(value);
                    ctrl_val.multiplier = 1000;
                    ctrl_val.decimal_display_value = (decimal)ctrl_val.integer_value / ctrl_val.multiplier;
                    break;
                case "GAIN_X":
                    //ctrl_val.integer_value = Convert.ToInt32(value);
                    ctrl_val.multiplier = 10;
                    ctrl_val.decimal_display_value = (decimal)ctrl_val.integer_value / ctrl_val.multiplier;
                    break;
                case "GAIN_Y":
                    //ctrl_val.integer_value = Convert.ToInt32(value);
                    ctrl_val.multiplier = 10;
                    ctrl_val.decimal_display_value = (decimal)ctrl_val.integer_value / ctrl_val.multiplier;
                    break;
                case "DRIVE":
                    //ctrl_val.integer_value = Convert.ToInt32(value);
                    if (ctrl_val.integer_value == 1)
                        ctrl_val.decimal_display_value = 6;
                    else if (ctrl_val.integer_value == 2)
                        ctrl_val.decimal_display_value = 10;
                    break;
                case "INPUT_GAIN":
                    //ctrl_val.integer_value = Convert.ToInt32(value);
                    if (ctrl_val.integer_value == 1)
                        ctrl_val.decimal_display_value = 12;
                    break;
                case "MANUF_DATE":
                    //ctrl_val.display_value = value;
                    break;
                case "FILTER_HP":
                    //ctrl_val.integer_value = Convert.ToInt32(value);
                    ctrl_val.multiplier = 100;
                    ctrl_val.decimal_display_value = (decimal)ctrl_val.integer_value / ctrl_val.multiplier;
                    break;
                case "FILTER_LP":
                    //ctrl_val.integer_value = Convert.ToInt32(value);
                    ctrl_val.multiplier = 100;
                    ctrl_val.decimal_display_value = (decimal)ctrl_val.integer_value / ctrl_val.multiplier;
                    break;
                case "PROBE_TYPE":
                    //ctrl_val.integer_value = Convert.ToInt32(value);

                    if (ctrl_val.parent != null && ctrl_val.parent.xml_path == "SETTINGS")    //No parent, must be the main Probe_type
                    {
                        switch (ctrl_val.display_value)
                        {
                            case "0":
                                ctrl_val.display_value = "Abs-12";
                                break;
                            case "2":
                                ctrl_val.display_value = "Bridge";
                                break;
                            case "3":
                                ctrl_val.display_value = "Reflection";
                                break;
                            case "4":
                                ctrl_val.display_value = "Rotary";
                                break;
                            case "5":
                            case "6":
                            case "7":
                            case "8":
                            case "1":
                                ctrl_val.display_value = "Abs-00";
                                break;
                        }
                        //if (string_to_highlight != "")
                        //   cbProbeTypeCh1.SelectedIndex = cbProbeTypeCh1.FindString(string_to_highlight);
                    }
                    //Probe 2 type
                    else if (ctrl_val.parent != null && ctrl_val.parent.xml_path == "DUAL_FREQUENCY")    //Parent should be Dual Freq
                    {
                        switch (ctrl_val.display_value)
                        {
                            case "0":
                                ctrl_val.display_value = "Abs-12";
                                break;
                            case "1":
                                ctrl_val.display_value = "Abs-00";
                                break;
                            case "2":
                                ctrl_val.display_value = "Bridge";
                                break;
                            case "3":
                                ctrl_val.display_value = "Reflection";
                                break;
                            case "4":
                                ctrl_val.display_value = "Rotary";
                                break;
                            case "5":
                                ctrl_val.display_value = "Differential";
                                break;
                            case "6":
                                ctrl_val.display_value = "Reflection";
                                break;
                            case "7":
                                ctrl_val.display_value = "Diff, int Load";
                                break;
                            case "8":
                                ctrl_val.display_value = "Diff, ext Load";
                                break;
                        }
                        //if (string_to_highlight != "")
                        //    cbProbeTypeCh2.SelectedIndex = cbProbeTypeCh2.FindString(string_to_highlight);
                    }
                    break;
                case "VERSION":
                    //ctrl_val.display_value = value;
                    break;
                case "INSTRUMENT":
                    //ctrl_val.display_value = value;
                    break;
                case "INSTRUMENT_ID":
                    //ctrl_val.display_value = value;
                    break;
                case "ROTARY_PROBE_TYPE":    //Taking the number, set the control to have the correct string selected.
                    ctrl_val.display_value = "";
                    //ctrl_val.integer_value = Convert.ToInt32(value);
                    //Only 3 options, 0=ETher, 1=Rohmann, 2=Zetec
                    switch (ctrl_val.display_value)
                    {
                        case "0":
                            ctrl_val.display_value = "ETher";
                            break;
                        case "1":
                            ctrl_val.display_value = "Rohmann";
                            break;
                        case "2":
                            ctrl_val.display_value = "Zetec";
                            break;
                    }
                    
                    //if (string_to_highlight != "")
                    //    cbRotaryType.SelectedIndex = cbRotaryType.FindString(string_to_highlight);
                    break;
                case "ACTION":
                    //if (path == "ALARMS/ALARM/ACTION")    //Taking the number, set the control to have the correct string selected.
                    {   //0=Freeze, 1=Tone, 2=Freeze & Tone, 3=None
                        ctrl_val.display_value = "";
                        //ctrl_val.integer_value = Convert.ToInt32(value);
                        switch (ctrl_val.display_value)
                        {
                            case "0":
                                ctrl_val.display_value = "Freeze";
                                break;
                            case "1":
                                ctrl_val.display_value = "Tone";
                                break;
                            case "2":
                                ctrl_val.display_value = "Freeze & Tone";
                                break;
                            case "3":
                                ctrl_val.display_value = "None";
                                break;
                        }
                        //if (string_to_highlight != "")
                        //    cbAlarmAction.SelectedIndex = cbAlarmAction.FindString(string_to_highlight);
                    }
                    break;
                case "SOURCE":
                    //if (path == "ALARMS/ALARM/SOURCE")    //Taking the number, set the control to have the correct string selected.
                    {
                        ctrl_val.display_value = "";
                        //ctrl_val.integer_value = Convert.ToInt32(value);
                        //The Alarm can trigger off of Ch1, Ch2 or Either (Both). THis relates to the FiFos in the machine so Ch1 could be the Mix channel if that is configured to be Pane1 Ch1.
                        switch (ctrl_val.display_value)
                        {
                            case "0":
                                ctrl_val.display_value = "Ch 1";
                                break;
                            case "1":
                                ctrl_val.display_value = "Ch 2";
                                break;
                            case "2":
                                ctrl_val.display_value = "Both";
                                break;
                        }
                        //if (string_to_highlight != "")
                        //    cbAlarmSource.SelectedIndex = cbAlarmSource.FindString(string_to_highlight);
                    }
                    break;
                case "ENABLED":
                    //ctrl_val.integer_value = Convert.ToInt32(value);
                    //The button is called alarm OFF, so set the decimal_display_value to be the opposite of the integer value.
                    ctrl_val.decimal_display_value = ctrl_val.integer_value==1?0:1;
                        //rbAlarmOff.full_XML = "<ALARMS>\r<ALARM>\r<ENABLED>" + rbAlarmOff.value_string + "</ENABLED>\r</ALARMS>\r</ALARM>\r";
                    break;
                case "TYPE":
                    //ctrl_val.integer_value = Convert.ToInt32(value);
                    ctrl_val.decimal_display_value = ctrl_val.integer_value;
                    if (ctrl_val.display_value == "1")   //1=Box
                        ctrl_val.display_value = "Box";
                    else                //0=Sector
                        ctrl_val.display_value = "Sector";
                    //rbAlarmSector.full_XML = "<ALARMS>\r<ALARM>\r<TYPE>" + rbAlarmSector.value_string + "</TYPE>\r</ALARMS>\r</ALARM>";
                    break;
                case "RPM":
                    //ctrl_val.integer_value = Convert.ToInt32(value);
                    ctrl_val.decimal_display_value = ctrl_val.integer_value;
                    ctrl_val.multiplier = 1;
                    break;
                case "FREQUENCIES":
                    //ctrl_val.integer_value = Convert.ToInt32(value);
                    ctrl_val.decimal_display_value = ctrl_val.integer_value;
                    ctrl_val.multiplier = 1;
                    break;
				case "IO_PORT_CONFIG":

					break;
                case "ETHER_PROTOCOL":
                case "USB_OUTPUT":
                case "TEXT_COLOUR":
                case "BACKGROUND_COLOUR":
                case "BLANK_KEY_1":
                case "BLANK_KEY_2":
                case "LANGUAGE":
                case "POWER_OFF_TIME":
                case "SCREEN_DIM_TIME":
                case "FONT":
                case "LOAD":
                case "RS232":
                case "COLOUR_SCHEME":
                case "LOG_ENABLED":
                case "BACKLIGHT":
                case "AUTO_LOAD":
                case "PERSISTENCE":
                case "SWEEP":
                case "WF_STEPS":
                case "TB_SWEEPS":
                case "DATE_FORMAT":
                case "GAIN_LOCK":
                case "GAIN_INCREMENT":
                case "FILTER_INCREMENT":
                case "FILTER_LOCK":
                case "SCREEN_FLIP":
                case "AUTO_PHASE_ANGLE":
                case "AUTO_PHASE_RADIUS":
                case "TIME_DIFF":
                case "SPOT_SIZE":
                case "SPOT_COLOUR":
                case "DUAL_FREQUENCY/SPOT_COLOUR":
                case "DUAL_FREQUENCY/SPOT_SIZE":
                case "DUAL_FREQUENCY/GAIN_LOCK":
                case "DUAL_FREQUENCY/GAIN_INCREMENT":
                case "DUAL_FREQUENCY/BAL_X":
                case "DUAL_FREQUENCY/BAL_Y":
                case "MIX/SPOT_COLOUR":
                case "MIX/SPOT_SIZE":
                case "MIX/GAIN_LOCK":
                case "MIX/GAIN_INCREMENT":
                case "INSTRUMENT_MODE":
                case "FAVOURITE_GUIDE":
                case "IO_PIN1_CONFIG":
                case "IO_PIN1_DETAILS":
                case "IO_PIN2_CONFIG":
                case "IO_PIN2_DETAILS":
                case "ALARMS/ALARM/LEFT":
                case "ALARMS/ALARM/RIGHT":
                case "ALARMS/ALARM/TOP":
                case "ALARMS/ALARM/BOTTOM":
                case "ALARMS/ALARM/START":
                case "ALARMS/ALARM/END":
                case "ALARMS/ALARM/INNER":
                case "ALARMS/ALARM/OUTER":
                case "ALARMS/ALARM/COLOUR":
                    /*	And again for the next alarm!
                    "ALARMS/ALARM/LEFT"
                    "ALARMS/ALARM/RIGHT"
                    "ALARMS/ALARM/TOP"
                    "ALARMS/ALARM/BOTTOM"
                    "ALARMS/ALARM/START"
                    "ALARMS/ALARM/END"
                    "ALARMS/ALARM/INNER"
                    "ALARMS/ALARM/OUTER"
                    "ALARMS/ALARM/COLOUR"*/
                case "PANES/PANE/PANE_TYPE":
                case "PANES/PANE/GRATICULE":
                case "PANES/PANE/GRAT_SIZE":
                case "PANES/PANE/SPOT_INFO":
                case "PANES/PANE/OFFSET_X":
                case "PANES/PANE/OFFSET_Y":
                case "PANES/PANE/PANE_SOURCE":
	                /*And again as there's 2 sources.
                    "PANES/PANE/PANE_SOURCE"
                    "PANES/PANE/PANE_SIZE"
	                    And now Pane 2!
                    "PANES/PANE/PANE_TYPE"
                    "PANES/PANE/PANE_SIZE"
                    "PANES/PANE/GRATICULE"
                    "PANES/PANE/GRAT_SIZE"
                    "PANES/PANE/OFFSET_X"
                    "PANES/PANE/OFFSET_Y"
                    case "PANES/PANE/LOCATION"
                    "PANES/PANE/PANE_SOURCE"*/
                    try
                    {
                        //ctrl_val.integer_value = Convert.ToInt32(value);
                        //The button is called alarm OFF, so set the decimal_display_value to be the opposite of the integer value.
                        ctrl_val.decimal_display_value = Convert.ToDecimal(ctrl_val.integer_value);
                    }
                    catch
                    {
                        ctrl_val.integer_value = 0;
                        ctrl_val.decimal_display_value = 0;
                    }
                    break;
                default:
                    //Not been processed!?
                    break;
            } //end of switch
        }

        internal void SetControlValues(System.Windows.Forms.Control ctrl)
        {
            
        }
		/// <summary>
		/// From the full path of an XML tag, get the details about it. 
		/// </summary>
		/// <param name="tag">Must be the full path right form the top element, seperated by a '/' ie INSTRUMENT/INSTRUMENT_ID</param>
		/// <returns></returns>
        public ControlValue GetControlValueFromTagAndPath(string tag)
        {
            String[] path = tag.Split('/');
            foreach (ControlValue ct_val in XML_Values)
            {
                if (ct_val.xml_path == path[0]) //We#ve found the first element
                {
                    if (path.Length == 1)   //If this was the only element, jobs done!
                        return ct_val;
                    return IterateThroughPath(ct_val, 1, tag);
                }
            }
            return null;
        }
        /// <summary>
        /// Allows the funstion above to iterate through a path seperated by '/'.
        /// </summary>
        /// <param name="ct_val"></param>
        /// <param name="p"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        private ControlValue IterateThroughPath(ControlValue ct_val, int p, string tag)
        {
            String[] path = tag.Split('/');
            int children = ct_val.ChildrenCount();

            while (children-- > 0)
            {
                ControlValue val = ct_val.GetNextChild();
                if (val == null)
                    return null;
                if (val.xml_path == path[p]) //We've found the Pth element
                {
                    if (p == path.Length-1)
                        return val;
                    return IterateThroughPath(val, p + 1, tag);
                }
            }
            return null;
        }
        /// <summary>
        /// From a provided Tag and a number that represents the nth time that tag is present, return the Decimal associated with the tag.
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="nth_occurance"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        internal bool GetValue(string tag, int nth_occurance, out decimal val)
        {
            tag_counter = 0;
            ControlValue ctv;   //Where we store the found result!

            val = 0;
            for(int x=0; x<XML_Values.Count;x++)
            {
                ctv = IterateToNthTag((ControlValue)XML_Values[x], tag, nth_occurance);
                if (ctv != null)
                {
                    val = ctv.decimal_display_value;
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// From a provided Tag and a number that represents the nth time that tag is present, return the String associated with the tag.
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="nth_occurance"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        internal bool GetValue(string tag, int nth_occurance, out string val)
        {
            tag_counter = 0;
            ControlValue ctv;   //Where we store the found result!

            val = "";
            for (int x = 0; x < XML_Values.Count; x++)
            {
                ctv = IterateToNthTag((ControlValue)XML_Values[x], tag, nth_occurance);
                if (ctv != null)
                {
                    val = ctv.display_value;
                    return true;
                }
            }
            return false;
        }
        private ControlValue IterateToNthTag(ControlValue cont_val, string tag, int nth_one)
        {
            if (cont_val.xml_path == tag)   //Found the correct tag
            {
                if (nth_one == tag_counter)
                    return cont_val;
                tag_counter++;
            }
            int child_count = cont_val.ChildrenCount();
            for (int x = 0; x < child_count; x++)
            {
                ControlValue tmp = IterateToNthTag(cont_val.GetNextChild(), tag, nth_one);
                if (tmp != null)
                    return tmp;
            }
            return null;
        }
    }
    /// <summary>
    /// Every value read from the XML is stored in an object of this class.
    /// The one thing it must have is an XML Path or name. This is used to match up against controls on the form.
    /// ie FREQUENCY, DUAL_FREQUENCY/FREQUENCY
    /// </summary>
    class ControlValue
    {
        public String xml_path;
        public Int32 integer_value;
        public String display_value;
        public String display_units;
        public Int32 multiplier;
        public Decimal decimal_display_value;
        public ControlValue parent;

        //Access Children through functions
        private ArrayList Children; //Each TAG can have numerous children ControlValues.
        private Int32 child_counter = 0;

        public ControlValue(String path, String value)
        {
            xml_path = path;
            display_value = value;
            try
            {
                if (value.StartsWith("0x")) //A hex value that we should try to convert.
                {
                    value = value.Substring(2);
                    integer_value = int.Parse(value, System.Globalization.NumberStyles.HexNumber);
                    decimal_display_value = Convert.ToDecimal(integer_value);
                }
                else
                {
                    Decimal.TryParse(value, out decimal_display_value);
                      //  decimal_display_value = Convert.ToDecimal(value);
                    int.TryParse(value, out integer_value);
                    //    integer_value = Convert.ToInt32(value);
                }
            }
            catch
            {
                decimal_display_value = 0;
                integer_value = 0;
            }
            display_units = "";
            multiplier = 0;
        }
        public ControlValue(String path, Int32 value)
        {
            xml_path = path;
            integer_value = value;
            decimal_display_value = Convert.ToDecimal(value);
            display_value = "";
            display_units = "";
            multiplier = 0;
        }
        public ControlValue(String path)
        {
            xml_path = path;
            decimal_display_value = 0;
            integer_value = 0;
            display_value = "";
            display_units = "";
            multiplier = 0;
        }
        /// <summary>
        /// Add a child ControlValue to this ControlValue.
        /// </summary>
        /// <param name="new_child"></param>
        public void AddChild(ControlValue new_child)
        {
            //If there are currently no children, create the List.
            if (Children == null)
                Children = new ArrayList();

            Children.Add(new_child);
            //Add ourself as the new childs parent
            new_child.parent = this;
        }
        /// <summary>
        /// How many Children are in thie ControlValue?
        /// Calling this also resets the child counter that is used by GetNextChild()
        /// </summary>
        /// <returns></returns>
        public Int32 ChildrenCount()
        {
            child_counter = 0;

            if (Children == null)
                return 0;
            return Children.Count;
        }
        /// <summary>
        /// A function that is useful in a loop, it just gets thte next child until they run out, when it returns NULL.
        /// Reset the Children by calling ChildrenCount.
        /// </summary>
        /// <returns></returns>
        public ControlValue GetNextChild()
        {
            if (Children == null || child_counter >= Children.Count)
                return null;

            return (ControlValue)(Children[child_counter++]);
        }
    }
}
