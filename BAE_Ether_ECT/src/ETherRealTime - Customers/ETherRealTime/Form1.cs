using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.IO.Ports;
using System.Management;
using System.Threading;
using System.Collections; // need to add System.Management to your project references.
using System.Windows.Forms.DataVisualization.Charting;
using ETherCheckDataAcq;
using System.Runtime.InteropServices;
using Win32bits;
using System.Security.Permissions;
using ETherSoftwareComponentStore;

namespace ETherRealTime
{
    public partial class ETherRealtime : Form
    {
        [StructLayout(LayoutKind.Explicit)]
        struct ByteArray
        {
            [FieldOffset(0)]
            public Int32 Int1;
            [FieldOffset(0)]
            public byte Bt1;
            [FieldOffset(1)]
            public byte Bt2;
            [FieldOffset(2)]
            public byte Bt3;
            [FieldOffset(3)]
            public byte Bt4;
        }
        //If we're using the DLL to conenct, we need this.
        ETherCheckDataAcq.ETherRealtimeClass EtherObj;
        string dll_expected_file = "";      //If we request a file while we're connected via the DLL, we must remember the filename we requested!

        /// <summary>Windows message sent when a device is inserted or removed</summary>
        public const int WM_DEVICECHANGE = 0x0219;
		public enum eInstrumentType
		{
			NOTHING,
			WELDCHECK,
			AEROCHECK,
			WELDCHECK_P,
			AEROCHECK_P,
			PHASECHECK,
			RAILCHECK,
			WELDVUE,
			STEELCHECK,
			ETI300,
			EMBEDEC,
			WELDCHECK3,
			VICTOR22D
		}
		public eInstrumentType connected_instrument = eInstrumentType.NOTHING;

        /// <summary>WParam for above : A device was inserted</summary>
        //public const int DEVICE_ARRIVAL = 0x8000;
        /// <summary>WParam for above : A device was removed</summary>
        //public const int DEVICE_REMOVECOMPLETE = 0x8004;
        XML_Handler instrument_settings;
        XML_Handler loaded_file_header_info;
        Int32 data_point_count = 0, last_seconds_points_count = 1;
        Int32[] values = new Int32[8];    //Data read from each packet, we then copy this to the realtime_values where they are transferred to the screen on a timer.
        Int32[] realtime_values = new Int32[8];    //Data read from each packet
        Int32[] prev = new Int32[6];    //A PREVIOUS record of what was read from each packet. Used to check for missing data in debug mode.
        int bytes = 0;
        FileStream fs; //fs = filestream for realtime logging.
        Byte[] serial_data_buffer = new Byte[25600];    //25600 is 25bytes * 1024. 25 bytes is 1 header then 8 bytes for Ch1, Ch2 & Mix
		Int32 CScanner_buf_pos = 0;
        //byte[] engineering_mode_key_sequence = { 4, 4, 5, 6, 3, 4, 1 }; //Pressing DOWN, DOWN, LEFT, RIGHT, UP, DOWN put the PC package in Engineering Mode. The last char is irrelevant
        char[] engineering_mode_key_sequence = { 'D', 'D', 'L', 'R', 'U', 'D', ' ' }; //Pressing DOWN, DOWN, LEFT, RIGHT, UP, DOWN put the PC package in Engineering Mode. The last char is irrelevant
        UInt16 engineering_mode_sequence_position = 0;
        Int16 current_instrument = -1;      //Which f the connected instruments is classified as the "current".
        //bool debugging = false;
        bool[] status_alarm_was_off = {true, true};   //Start with the ALARM off.
        string previous_USB_port = "";      //We remember the last used USB port, so that in the event of auto connect, we can search for it.
		ArrayList text_box_strings = new ArrayList();   //We use this to add the contents of all text boxes before saving in XML
        //////// The following is what the status byte is made up from: ////////
        public const int NOTHING = 0;
        //Least Significant Nibble
        public const int ROTARY_SYNC_PULSE_BIT = 0x01;
        public const int ALARM_BIT1 = 0x02; // Alarm 1.
        public const int ALARM_BIT2 = 0x04; //The 2nd alarm for the Component Check
        //Most Significant Nibble
        public const int FILE_SIZE = 0x10;
        public const int FILE_DATA = 0x20;
        public const int FILE_NAME = 0x30;
        public const int REALTIME_DATA_RAW = 0x40;
        public const int REALTIME_DATA_POSTPROCESS = 0x50;
        public const int XML_HEADER = 0x60;
        public const int SINGLE_CHAN_POST = 0x70;
        public const int NON_REALTIME = 0x80;
        public const int CONDUCTIVITY = 0x90;
        public const int VEE_SCAN_RESPONSE = 0xA0;
        public const int CSCAN_ENC_TRIGGERED = 0xB0;
        //The following are only used AFTER the data has been processed by ETherRealtime of the DLL, so we can use the lower NIBBLE too.
        public const int SINGLE_CHANNEL_WELD_AERO_FROM_ETD_FILE = 0x99;   //This value is ONLY used here, not in the instruments!!!!!!!
        public const int DUAL_CHANNEL_AERO_FROM_ETD_FILE = 0x88;   //This is ONLY used here, not in the instruments!!!!!!!
        public const int STATUS = 0x77; //This is ONLY used here, not in the instruments!!!!!!!
        public const int ENCODER = 0x78; //This is ONLY used here, not in the instruments!!!!!!!
        public const int COUNTER = 0x79; //This is ONLY used here, not in the instruments!!!!!!!

        public const int MAX_TX_PACKET_SIZE = 64;      //The packet size expected by the instrument

        public const int SAMPLE_BUF_SIZE = 8000;
        int data_transfer_state = NOTHING;  //When we receive a status byte, we set here what sort of data we are now expecting.
        bool viewing_file = false;          //Set flag to TRUE if the user clicks on VIEW rather than GET FILE.
		TreeNode next_download_file_node;		//If we need to download another file after one has finished, store the number of it here.
        //Real time PhasePlane stuff!
        public const int CHAN1_BUFFER = 0;
        public const int CHAN2_BUFFER = 2;
        public const int MIX_BUFFER = 4;
        public const int STATUS_BUFFER = 6;
        public const int COUNTER_BUFFER = 8;
        public const int ENCODER_BUFFER = 10;
        public const int CONDUCTIVITY_BUFFER = 12;
        public const int ANGLE_VECTOR_BUFFER = 14;    //If adding any more, update the line below:
        Point[,] graph_points = new Point[ANGLE_VECTOR_BUFFER * 2, SAMPLE_BUF_SIZE];   //4000 points is half a second, 4000 that is visible and 4000 that is being erased. 2 Channels, and 2 buffers so we can be reading one and writing the other! ALSO, we now have a Status byte channel, Encoder and Counter!

        int buf_num = 0;
        int[] write_position = new int[2] { 0, 0}; //Only 0 and 2 are used though!

        Series S = new Series("Timebase Chan1");
        Series S2 = new Series("Timebase Chan2");
        Series Spp = new Series("Phaseplane Chan1");    //PhasePlane series
        Series Spp2 = new Series("Phaseplane Chan2");    //PhasePlane series
		string previous_source1 = "";	//Keep a copy of what was in the ComboBox Sources on the PhasePlane tab so we only reprocess if the data changes.
		string previous_source2 = "";
        Int16 skip_counter = 0;
        Int16 points_to_skip = 3;
        char[] data_ch = new char[0];       //Array of chars used to send a file to the instrument.
        ArrayList expanded_nodes = new ArrayList();
        StringBuilder str_bldr = new StringBuilder();           //A string builder used to convert the incoming bytes to a string then display as the settings XML.
        //      Image grat_img = (Image)Properties.Resources.GRID_2;
        ArrayList commands_to_send = new ArrayList();       //Keep an array of any commands that want to be sent to the instrument.
        bool logging_realtime_data = false;     //Set this to TRUE once we have received the latest XML from the instrument when the user wants to start logging.
        GraphData[] FileData;   //We have a GraphData object for each type of data in the file, ie X1, X2, Y1, Y2. POPULATED when a Logging File is Scanned.
        decimal gain_ratio_value = 1;    //When user selects Fixed gain Lock, the ration of X to Y must be stored and then used when either the X or Y is varied!
        bool do_not_send_value_to_instrument = false;   //When a controls value is changed by the user, new value is sometimes is sent to the instrument, BUT, if the value is updated programaticaly, DON'T send it!
        bool phaseplane_paused = false;     //It is possible to PAUSE the realtime data display graphs, this holds the state.
        Control big_NumericUpDown_parent = null;    //When we are using the BIG NUD control (for the touch-screen tablet), store which control we're mimicing.
		Control big_NumericUpDown_brother = null;	//As above, link the BUG NUD to the normal conrtrols used to change values, jsut to keep them in sync.
        bool block_mouse_down = false;      //So the app works with the touchscreen tablet, we block the mouse down event if we see the CLICK first.
        double previous_timebase_zoom_value = 0.1; //We remember the zoom size of the Timebase X axis, which is the only scrollable axis. If the Zoom has changed we reprocess Min & Max valules, otherwise it's only a scroll event.
		bool reverse_refresh = false;	//If we choose to remove the reverse encoder values, then we store that in a flag so we can refresh all timebase data.
        //Drag & Drop stuff:
        ArrayList drag_items = new ArrayList(); //A list of the items currently being dragged.
        Int16 validData = 0;    //If set to > 0 indicates that drag and drop file is OK.
		String last_seen_EmbedEC = "";
		String current_EmbedEC = "";
		decimal previous_Dig_IO_rate = 0;	//We remember the previous rate so we only show the warning boxes if the value changes in to a different range.
		DigitalIOSequence D_IO_Seq;
		byte[] _RxData = new byte[256];
		//Scanner control
		//SerialPort serial_port_scanner = null;
		StreamReader str_scanner = null;
		Int16 current_init = 0;	//Which init command are we sending next to the Scanner!

        public ETherRealtime()
        {
            InitializeComponent();
			panelKeypad.Visible = false;
			tabControl1.Width += 258;
            PrepareTimebaseGraph();

			this.Text = "ETher Realtime - v1.6.6";		//For the type SINGLE_CHANNEL_WELD_AERO_FROM_ETD_FILE I have a hardcoded routine that automatically balances the data on the first set of values, when loading in a file.
			//this.Text = "ETher Realtime - v1.6.5";	//Will open files from ETherMap with a <CHANNELS> tag
			//this.Text = "ETher Realtime - v1.6.4";	//Some controls were anchored to the wrong sides, ie VIEW, OPEN etc on FileSystem could get lost off the screen.
			//this.Text = "ETher Realtime - v1.6.3";	//The CScanner now has a configurable BAUD rate, the slower it is the better it behaves! 9600 is reasonable.
			//this.Text = "ETher Realtime - v1.6.2";	//When changing from the PhasePlane tab to Data Fromat and back to PhasePlane, the timer was getting left OFF for drawing the live data, fixed.
			//this.Text = "ETher Realtime - v1.6.1";	//Changed my XML_Handler class to put back in the code that handles multipliers, converting 200,000 to 200 and a 1000 multiplier (kHz).
			//this.Text = "ETher Realtime - v1.6.0";	//Improvements to the speed of populating the graphs. Also added buttons to select which data sources are used to set the Y axis of the timebase.
			//this.Text = "ETher Realtime - v1.5.8";	//A new button on the Datafiles tab to save existing data to CSV format. Also fixed the fact that the last set of data points was always = 0!
			//this.Text = "ETher Realtime - v1.5.7";	//Improved resizing of tabs when the main form is resized. Before some controls were hidden when connecting to EmbedEC.
			//this.Text = "ETher Realtime - v1.5.6";	//A new checkbox, cdAutoLock (Tie Phase Axis on Graph page). When checked user can zoom in on the timebase and the timebase Y axis range is used for the Phaseplanes axis.
			//this.Text = "ETher Realtime - v1.5.5";	//RAILCHECK changes. 1) Can now add an Operator and a Landmark from the Settings page, this is hidden until some settings have arrived indicating we are connected to a RailCheck.
			//this.Text = "ETher Realtime - v1.5.5";	//RAILCHECK changes. 1) Viewing a .CSV file will attempt to also download the associated XML file.
			//this.Text = "ETher Realtime - v1.5.4";	//Finally releasing with the RS232 to the Victor about there and the document: Communicating with a compatible ETherNDE Instrument over Virtual COM Port v1.4, written.
			//this.Text = "ETher Realtime - v1.5.3";	//Getting the RS232 working for the the Victor.
			//this.Text = "ETher Realtime - v1.5.2";	//Couple of bugs where I had assumed FileData[5] held Encoder data, there may not BE a FileData[5]!
			//this.Text = "ETher Realtime - v1.5.1";	//Some how 1.5.0 didn't have the checkbox to remove backward encoder readings on the PhasePlane tab!?
			//this.Text = "ETher Realtime - v1.5.0";	//Release for Geismar during visit to Holland.
			//this.Text = "ETher Realtime - v1.4.1";	//Can now export a C-Scan to Excel. Also will now handle a CScan where the raw data has been saved for 2 channels.
			//this.Text = "ETher Realtime - v1.4.0";	//Lots of improvements for the RailCheck version. Better graphs, ability to load in the RialCheck CSV files, keypad hidden when not conencted
			//this.Text = "ETher Realtime - v1.3.5";	//Added functionality so that the EmbedEC from version 
            //this.Text = "ETher Realtime - v1.3.4";	//The EtherCheckDataAcq.dll now writes files to a diferent folder, therefore we must look in a different folder! New IO tabs
			//this.Text = "ETher Realtime - v1.3.3";    //When connected to the BondCheck in resonnance mode, data is only return back at approx 20/s, so assuming 8000/s for persistence doesn't work too well.
														//Added new Tab for the IO of the EmbedEC.
			//this.Text = "ETher Realtime - v1.3.2";    //When loading in ETher files, program automatically jumps to the Phaseplane graph and selects the first X and Y channels!
            //this.Text = "ETher Realtime - v1.3.1";    //We can now drag and drop files in to the FileSystem window, as long as the desired location is highlighted.
            //this.Text = "ETher Realtime - v1.3.0";    //Everything updated. Now a drop-down for the 2 possible displays on the Phase Plane and the Timebase. Can show X, Y, Status, Encoders etc etc.
            //this.Text = "ETher Realtime - v1.2.5";    //Improved layout and button sizes for touch-screen tablet. Added a BIG NumericUpDown that is used by the last clicked variable, ie Freq or Gain.
            //this.Text = "ETher Realtime - v1.2.4";    //Now we have a PAUSE/PLAY button and the InstrumentSettings class is instantiated on boot, just that it's empty but stops NULL issues.
            //this.Text = "ETher Realtime - v1.2.3";    //Improved handling of fast changing NUDs to the EmbedEC. Added Gain Locks
            //this.Text = "ETher Realtime - v1.2.2";    //All settings for the EmbedEC are sent in realtime and the controls are shown instead of the keypad. Allows realtime phase change etc. while viewing the phase plane.
            //this.Text = "ETher Realtime - v1.2.1";    //The Interpolate check box should work on BOTH channels. Removed the button to Scan Files, the Read ETher files button will do all files now.
            //this.Text = "ETher Realtime - v1.2.0";    //If the data format is SingleChanPost, we must remove the 320,-240 offset that is applied to the Processed data.
            //this.Text = "ETher Realtime - v1.1.9";    //The ability to load in an ETD file now works, but not for our own LOG file. The only drawback is that we do not have the ability to create a LOG data while connected via the DLL.
            //this.Text = "ETher Realtime - v1.1.8";    //OK, we now use the DLL. This has sorted the stability issues on Win10. Also added callbacks for when a file has been received and the progress bar during this reception.
            //this.Text = "ETher Realtime - v1.1.7";    //Double buffer the data now, so that writing to one while we're reading from the other, might improve things??
            //this.Text = "ETher Realtime - v1.1.6";    //Can now display 2 channels. Also, realtime data is stored in variables then added to the GUI on a timer, not by using Invoke. This is try to speed things up.
            //this.Text = "ETher Realtime - v1.1.5";    //PhasePlane will display 1,1 instead of 0,0. This stops the axis going funny. ALso now only draws 1 in every 4 points to help Windows 10.
            //this.Text = "ETher Realtime - v1.1.4";    //Fixed the file logging which had got broken somewhere along the line!
            //this.Text = "ETher Realtime - v1.1.3";    //Tidied it up a little. Dssplays a warning if NOT in Single Channel Post mode and viewing graphs. Controls resize a bit better.
            //this.Text = "ETher Realtime - v1.1.2";    //Improved to work with EmbedEC (Veritor replacement).
            //this.Text = "ETher Realtime - v1.1.1";    //Coms improved so can sit in a loop in handleReceivedBytes. Can now also VIEW files!
            //this.Text = "ETher Realtime - v1.1.0";    //When NOT in Engineering Mode hides many buttons, ie RS232, errors etc etc. All in prep for the MOD Tender visit.
            //this.Text = "ETher Realtime - v1.0.3";    //Changed the refreshing of the ports to be on a timer like the ATE. Seems to work smoother.
            //this.Text = "ETher Realtime - v1.0.2";    //Added buttons for instructing the instrument to Reload, Reprocess & Save the master settings.
            //this.Text = "ETher Realtime - v1.0.1";    //Improved GUI buttons, comms a bit more reliable. Time for a new release!
            //this.Text = "ETher Realtime - v1.0.0";    //First release

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && args[1] == "E")
				this.Text += " Engineering mode";

            if (args.Length > 1 && args[1] == "F")  //Full Screen!
                this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            //cbUseDLL.Visible = false;     We leave the option to connect via the DLL open as must NOT be DLL for Data Logging!
            btRefreshPorts.Visible = false;
            
            cbInstrumentRealtime.Visible = false;
            label4.Visible = false;
            tbInvalidPacket.Visible = false;
            cbRealTimeDisplay.Visible = false;
            label8.Visible = false;
            //btScanFile.Visible = false;
            lbTx.Visible = false;
            tbTx.Visible = false;
            lbMasterSettings.Visible = false;
            // btReprocessmasterSettings.Visible = false;
            btSaveMasterSettings.Visible = false;
            btReloadMasterSettings.Visible = false;
            rbRS232Transmit.Visible = false;
            rbUSBTransmit.Visible = false;
            btLoadVeeScanSettings.Visible = false;
            btBeadSeat.Visible = false;
            tbBytes.Top = gbUSB.Bottom + 5;
            lbBytes.Top = gbUSB.Bottom + 13;

            //These are only shown when we connect to an EmbedEC.
            panelEmbedEC.Visible = false;
            panelEmbedEC.Location = panelKeypad.Location;
            lbBIGNudFreq.Text = "";
			nudPersistenceEncoder.Location = nudPersistence.Location;

            nudPhaseCh1.multiplier = 1000;
            nudPhaseCh2.multiplier = 1000;
            nudPhaseChMix.multiplier = 1000;
            nudPhaseEmbedECOnly.multiplier = 1000;
            nudFrequencyCh1.multiplier = 1;
            nudFrequencyCh2.multiplier = 1;
            nudFrequencyEmbedECOnly.multiplier = 1;
            nudGainXch1.multiplier = 10;
            nudGainXch2.multiplier = 10;
            nudGainXchMix.multiplier = 10;
            nudGainYch1.multiplier = 10;
            nudGainYch2.multiplier = 10;
            nudGainYchMix.multiplier = 10;
            nudGainXEmbedECOnly.multiplier = 10;
            nudGainYEmbedECOnly.multiplier = 10;
            nudHighPassFilterCh1.multiplier = 100;
            nudHighPassFilterCh2.multiplier = 100;
            nudLowPassFilterCh1.multiplier = 100;
            nudLowPassFilterCh2.multiplier = 100;
            nudFiltHPEmbedECOnly.multiplier = 100;
            nudFiltLPEmbedECOnly.multiplier = 100;
            nudAlarmStretch.multiplier = 1000;
            tbTxString.Tag = 0;
            rbAlarmSector.value_string_to_set_checked = "0";
            rbAlarmBox.value_string_to_set_checked = "1";
            nudDriveGain.SetValueLookup(new Int32[,]{ {0, 0}, {6, 1}, {10, 2} });
            nudInputGainCh1.SetValueLookup(new Int32[,] { { 0, 0 }, { 12, 1 } });
            nudInputGainCh2.SetValueLookup(new Int32[,] { { 0, 0 }, { 12, 1 } });
            ctPhase.BringToFront();
            ctTimebase.BringToFront();
            btPause.BringToFront();     //We want the PAUSE button on top of everything!
            
			string read_line;
            StreamReader str = null;
            try
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\ETherRealTime\\config.txt";
                FileInfo fi = new FileInfo(path);
                if (!fi.Exists)
                    return;

                str = new StreamReader(fi.FullName);

                read_line = str.ReadLine();
                while (read_line != null)
                {
                    if (read_line.StartsWith("<TEXTBOX Name="))
                    {
                        SetControlText(this, read_line);
                    }
                    read_line = str.ReadLine();
                }
            }
            catch
            {
            }
            finally
            {
                if (str != null)
                    str.Close();
            }
			//Load in the dataGeneral data set, used to store data on application shut down.
			try
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\ETherRealTime";
                dataGeneral.ReadXml(path + "\\General.xml");
				last_seen_EmbedEC = GetDataSetValue("LAST_EMBEDEC");
            }
            catch (SystemException se)
            {
                MessageBox.Show("General Settings error:\n" + se.Message);
            }

			timerRefreshPorts.Start();
            instrument_settings = new XML_Handler();
        }

        private void PrepareTimebaseGraph()
        {
            ctTimebase.Series.Clear();
            ctTimebase.Titles.Clear();
            ctTimebase.Titles.Add("Timebase");

            ctTimebase.ChartAreas[0].AxisX.MinorGrid.Enabled = true;
            ctTimebase.ChartAreas[0].AxisX.MinorGrid.LineDashStyle = ChartDashStyle.Dot;
            ctTimebase.ChartAreas[0].AxisX.MinorGrid.LineColor = Color.LightGray;
            ctTimebase.ChartAreas[0].AxisX.MajorGrid.Enabled = true;
            ctTimebase.ChartAreas[0].AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            ctTimebase.ChartAreas[0].AxisX.MajorGrid.LineColor = Color.LightGray;
            ctTimebase.ChartAreas[0].AxisX.Minimum = 0;
            //We throw away half of the data before displaying, hence the 4000. (For the EmbedEC we throw away 3 quarters, but as the sample rate is 16000, still get 4000 points per second.
            ctTimebase.ChartAreas[0].AxisX.Maximum = 4000;
            ctTimebase.ChartAreas[0].AxisX.ScaleView.Zoomable = true;

            ctTimebase.ChartAreas[0].AxisY.MinorGrid.Enabled = false;
            ctTimebase.ChartAreas[0].AxisY.MajorGrid.Enabled = true;
            ctTimebase.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            ctTimebase.ChartAreas[0].AxisY.MajorGrid.LineColor = Color.LightGray;
            ctTimebase.ChartAreas[0].AxisY.Minimum = -240;
            ctTimebase.ChartAreas[0].AxisY.Maximum = 240;
            ctTimebase.ChartAreas[0].AxisY.ScaleView.Zoomable = true;
            ctTimebase.ChartAreas[0].CursorX.IsUserSelectionEnabled = true;
            ctTimebase.ChartAreas[0].CursorY.IsUserSelectionEnabled = true;
            ctTimebase.ChartAreas[0].AxisY.Tag = -1;
            ctTimebase.ChartAreas[0].AxisX.Tag = -1;

            S.IsVisibleInLegend = false;
            S.ChartType = SeriesChartType.FastLine;
            S.BorderWidth = 1;
            S.MarkerSize = 1;
            S.Color = Color.Red;
            S.IsValueShownAsLabel = false;
            S.BorderDashStyle = ChartDashStyle.Solid;
            S.Tag = -1;
            //Channel 2, only add this when RAW data is selected when the user goes to the Phaseplane Tab.
            S2.IsVisibleInLegend = false;
            S2.ChartType = SeriesChartType.FastLine;
            S2.BorderWidth = 1;
            S2.MarkerSize = 1;
            S2.Color = Color.Blue;
            S2.IsValueShownAsLabel = false;
            S2.BorderDashStyle = ChartDashStyle.Solid;
            S2.Tag = -1;
            ctTimebase.Series.Add(S);

            //Phaseplane Graph
            ctPhase.Series.Clear();
            ctPhase.Titles.Clear();
            ctPhase.Titles.Add("Phaseplane");

            ctPhase.ChartAreas[0].AxisX.MinorGrid.Enabled = true;
            ctPhase.ChartAreas[0].AxisX.MinorGrid.LineDashStyle = ChartDashStyle.Dot;
            ctPhase.ChartAreas[0].AxisX.MinorGrid.LineColor = Color.LightGray;
            ctPhase.ChartAreas[0].AxisX.MajorGrid.Enabled = true;
            ctPhase.ChartAreas[0].AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            ctPhase.ChartAreas[0].AxisX.MajorGrid.LineColor = Color.LightGray;
            ctPhase.ChartAreas[0].AxisX.Minimum = -320;
            ctPhase.ChartAreas[0].AxisX.Maximum = 320;

            ctPhase.ChartAreas[0].AxisY.MinorGrid.Enabled = false;
            ctPhase.ChartAreas[0].AxisY.MajorGrid.Enabled = true;
            ctPhase.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            ctPhase.ChartAreas[0].AxisY.MajorGrid.LineColor = Color.LightGray;
            ctPhase.ChartAreas[0].AxisY.Minimum = -240;
            ctPhase.ChartAreas[0].AxisY.Maximum = 240;
            ctPhase.ChartAreas[0].AxisX.IntervalAutoMode = IntervalAutoMode.FixedCount;
            ctPhase.ChartAreas[0].AxisY.IntervalAutoMode = IntervalAutoMode.FixedCount;
            ctPhase.ChartAreas[0].AxisY.Tag = -1;
            ctPhase.ChartAreas[0].AxisX.Tag = -1;

            Spp.IsVisibleInLegend = false;
            Spp.ChartType = SeriesChartType.FastPoint;
            Spp.BorderWidth = 1;
            Spp.MarkerSize = 1;
            Spp.Color = Color.Red;
            Spp.IsValueShownAsLabel = false;
            Spp.BorderDashStyle = ChartDashStyle.Solid;
            Spp.Tag = -1;
            //Channel 2, only add this when RAW data is selected when the user goes to the Phaseplane Tab.
            Spp2.IsVisibleInLegend = false;
            Spp2.ChartType = SeriesChartType.FastPoint;
            Spp2.BorderWidth = 1;
            Spp2.MarkerSize = 1;
            Spp2.Color = Color.Blue;
            Spp2.IsValueShownAsLabel = false;
            Spp2.BorderDashStyle = ChartDashStyle.Solid;
            Spp2.Tag = -99;	//Default to OFF
            ctPhase.Series.Add(Spp);

        }
        /// <summary>
        /// The USB Connect button has been pressed.
        /// The button either displays "Connect" if the USB is currently disconnected, or "Disconnect" if currently connected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btConnectUSB_Click(object sender, EventArgs e)
        {
            string com_port = "";

            if (btConnectUSB.Text == "Connect")
            {
                if (cbComPortsUSB.SelectedItem == null)
                    return;

                btConnectUSB.Text = "Disconnect";  //Assume the connection will succeed and set the button text to "Disconnect". If connection fails, the catch sets the text back to "Connect"               

                com_port = cbComPortsUSB.SelectedItem.ToString().Replace(" VCP", "");
                try
                {
                    data_point_count = 0;
                    rbUSBTransmit.Enabled = true;
                    rbUSBTransmit.Checked = true;
                    timerPhasePlane.Enabled = true;
                    previous_USB_port = cbComPortsUSB.SelectedItem.ToString();

                    if (cbUseDLL.Checked)
                    {
                        //Create the DLL
                        if (EtherObj == null)
                            ConnectToDLL();

                        bool port_result = EtherObj.OpenSerialConnection(Convert.ToInt32(com_port.Substring(3)));
                        if (!port_result)
                            throw (new SystemException());

                    }

					panelKeypad.Visible = true;
					tabControl1.Width -= 258;
                }
                catch (SystemException ex)
                {
                    btConnectUSB.Text = "Connect";
                    timerPhasePlane.Enabled = false;
                    serialPortUSB.Close();
                    rbUSBTransmit.Enabled = false;
					int pos = this.Text.IndexOf(" Connected");
					if (pos >= 0)
						this.Text = this.Text.Substring(0, pos);
                    if (cbAutoConnect.Checked)
                        tbConsole.AppendText(ex.Message + Environment.NewLine);
                    else
                        MessageBox.Show(ex.Message);
                }
            }
            else
            {
                try
                {
                    timerPhasePlane.Enabled = false;
					timerDigitalIOSequence.Enabled = false;
					panelKeypad.Visible = false;
					panelEmbedEC.Visible = false;
					tabControl1.Width += 258;
                    StopLogging();
					//Check to see if we were connected to an EmbedEC..
					if (instrument_settings.GetControlValueFromTagAndPath("INSTRUMENT") != null)
					{
						String inst = instrument_settings.GetControlValueFromTagAndPath("INSTRUMENT").display_value;
						if (inst != null && inst == "EmbedEC" && current_EmbedEC != "")
						{
							plVictorChannel.Visible = false;	//Just for RS232 to a Victor
							lbClickToRequest.Visible = false;	//Just for RS232 to a Victor
							//Update the XML in the rtb with the correct value from each control.
							TakeXMLAndUpdateValue();
							//Save the XML file
							string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\ETherRealTime" + "\\EmbedEC.xml";
							rtbXMLSettings.SaveFile(path, RichTextBoxStreamType.PlainText);
						}
					}
					current_EmbedEC = "";	//An EmbedEC is no longer connected

                    if (cbUseDLL.Checked)
                    {
                        EtherObj.CloseSerialConnection();
                        EtherObj = null;
                    }
                    else
						serialPortUSB.Close();
                    btConnectUSB.Text = "Connect";
                    rbUSBTransmit.Enabled = false;
					int pos = this.Text.IndexOf(" Connected");
					if (pos >= 0)
						this.Text = this.Text.Substring(0, pos);
                }
                catch (SystemException ex)
                {
                    tbConsole.AppendText(ex.Message + Environment.NewLine);
                    //MessageBox.Show(ex.Message);
                }
            }
        }

        private void StopLogging()
        {
            logging_realtime_data = false;
            if (fs != null)
            {
                fs.Close();
                fs = null;
            }
            btLogging.Invoke(new Action(() => btLogging.BackColor = Color.Red));
            //Now enable the Radio Buttons for the logging type.
            rbLoggingClickToSave.Enabled = true;
            rbLoggingRealtime.Enabled = true;
            btLOG.Visible = false;
        }

        private void serialPortUSB_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            bytes = serialPortUSB.BytesToRead;

            this.serialPortUSB.DataReceived -= serialPortUSB_DataReceived;

        }

        /// <summary>
        /// Iterate through a TreeNode and save it's name if it is expanded, simples!
        /// </summary>
        /// <param name="node"></param>
        void PopulateExpandedNodes(TreeNode node)
        {
            if (node.IsExpanded)
            {
                expanded_nodes.Add(node.Text);
                foreach (TreeNode nd in node.Nodes)
                    PopulateExpandedNodes(nd);
            }
        }
        void ReExpandNodes(TreeNode node)
        {
            //See if our name is in the list
            string name = node.Text;
            object expanded_name_obj = null;

            foreach (object obj in expanded_nodes)
            {
                if (obj.ToString() == name)
                {
                    node.Expand();
                    expanded_name_obj = obj;
                    break;
                }
            }
            //If we were expanded then go through our children, if we weren't then our children can't be possibly be expanded
            if (expanded_name_obj != null)
            {
                expanded_nodes.Remove(expanded_name_obj);   //We were expanded, so remove our name from the array list.
                foreach (TreeNode nd in node.Nodes)
                {
                    ReExpandNodes(nd);
                }
            }
        }
        /// <summary>
        /// A file has been sent up from the instrument (except filesystem file FS.txt).
        /// Save the file!
        /// </summary>
        /// <param name="file_name_received"></param>
        private void HandleFileReceived(string file_name_received)
        {
            SaveFileDialog sfd = new SaveFileDialog();

            if (file_name_received.EndsWith("latest_settings.xml")) //Indicates this is the file system description.
            {
                bool xml_ok = false;
                try
                {
                    
                    StreamReader str = new StreamReader(file_name_received);
                    String data = str.ReadToEnd();
                    //About to start logging, so first save the Instrument Settings!

                    if (btLogging.BackColor == Color.Green && fs != null)
                    {
                        for (int x=0; x<data.Length; x++)
                            fs.WriteByte(Convert.ToByte(data[x]));

                        if (rbLoggingRealtime.Checked)
                            logging_realtime_data = true;
                        else
                            btLOG.Visible = true;
                    }
                    instrument_settings = new XML_Handler(data, out xml_ok, file_name_received);
                    lbXMLFileName.Text = "Live Data";   //Change the label above the RTB so it no longer indicates a File Name.
                    rtbXMLSettings.Text = data;
                }
                catch (SystemException)
                {
                    tbConsole.AppendText("Parsing of XML failed." + Environment.NewLine);
                }
                //If the XML was OK, trigger the refreshing of all controls. ELSE, request the data again!
                if (xml_ok)
                    RefreshSettingsControlsValues(); //Now refresh the dispay of the controls.
                else
                    timerRequestXML.Start();    //Waits 0.5s then requests the XML again.
                return;
            }
            else if (viewing_file)   //Use a SaveFileDialog and get a new file name to save as.
            {
                viewing_file = false;
                dll_expected_file = ""; //Reset this as the main code keeps a store of any file it is expecting.
                return;
            }

            sfd.Title = "Select Save File";
            sfd.FileName = Path.GetFileName(file_name_received);    //Strip the path from the Save file.
            sfd.Filter = "All files (*.*)|*.*";
            //Loop round until the file was succesfully saved to one identified by the user OR they cancel.
            while (true)
            {
                if (sfd.ShowDialog() != DialogResult.OK)
                    return;
                try
                {
                    File.Copy(file_name_received, sfd.FileName, true);
                    File.Delete(file_name_received);
                    break;
                }
                catch
                {
                    DialogResult dr = MessageBox.Show("Could not save file, OK to select another or Cancel.");
                    if (dr == System.Windows.Forms.DialogResult.Cancel)
                        break;
                }
            }
        }

        /// <summary>
        /// Simply add a portion of a byte array to the global string builder str_bldr.
        /// </summary>
        /// <param name="serial_data_buffer"></param>
        /// <param name="pos"></param>
        /// <param name="bytes_to_write"></param>
        private void AddToString(byte[] serial_data_buffer, int pos, int bytes_to_write)
        {
            for (int x = pos; x < pos + bytes_to_write; x++)
            {
                str_bldr.Append((char)serial_data_buffer[x]);
            }
        }

        /// <summary>
        /// Reset the data point count every 1s, therefore the variable will hold the number of PACKETS received each second.
        /// ALSO, Check for auto-connect!
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer1s_reconnect_pointcount_Tick(object sender, EventArgs e)
        {
            tbPointsCount.Text = data_point_count.ToString();
			last_seconds_points_count = data_point_count;
            data_point_count = 0;   //Just trying to maximise baud rate

            if (cbAutoConnect.Checked && btConnectUSB.Text == "Connect" && cbComPortsUSB.Text != "")
                this.Invoke(new Action(() => btConnectUSB_Click(this, new EventArgs())));
        }
        /// <summary>
        /// Allow the user to select a path to a log file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btLogToFile_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() != DialogResult.OK)
                return;
            try
            {
                //This functionality moved to when the instrument has received the FILE_SIZE of the XML_HEADER and then the XML_HEADER
                /*btLogToFile.Text = "Logging";
                fs = new FileStream(fbd.SelectedPath + "/log_data.etd", System.IO.FileMode.Create);*/

                //The text box must hold the complete path ready for when we receive the header.
                tbLoggingToFile.Text = fbd.SelectedPath + "/log_data.etd";
            }
            catch (SystemException ex)
            {
                btLogToFile.Text = "Log to File";
                MessageBox.Show(ex.Message);
            }

        }

        private void RefreshVCPPorts()
        {
            timerRefreshPorts.Stop();
            cbComPortsUSB.Items.Clear();
            string previous_port = cbComPortsUSB.Text; //Remember what the text was in the combo box so we can see if it changes!
            cbComPortsUSB.Text = "";

            // Get a list of serial port names. 
            string[] ports = SerialPort.GetPortNames();

            Console.WriteLine("The following serial ports were found:");

            // Display each port name to the console. 
            foreach (string port in ports)
            {
                cbComPortsUSB.Items.Add(port);
                Console.WriteLine(port);
            }

            //Now, if there is a port name stored in: previous_USB_port AND (Auto Connect is selected OR we're actually connected), we use that port
            if (previous_USB_port != "" && (cbAutoConnect.Checked || btConnectUSB.Text == "Disconnect"))
            {
                int index = cbComPortsUSB.Items.IndexOf(previous_USB_port);
                if (index != -1)
                {
                    cbComPortsUSB.SelectedIndex = index;
                }
            }
            //////////////USB IDs//////////////////////
            ManagementObjectCollection collection;
            try
            {
                using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_PnPEntity"))
                    collection = searcher.Get();
                foreach (var device in collection)
                {
                    Console.WriteLine((string)(device.GetPropertyValue("Description")));
                    if ((string)(device.GetPropertyValue("Description")) == "STMicroelectronics Virtual COM Port")
                    {
                        //Extract the com port number from the caption:
                        string caption = (string)device.GetPropertyValue("Caption");
                        //Lets check that there is a COM port associated with this device.
                        if (!caption.Contains("(COM"))
                            continue;

                        //The Com port is in brackets at the end of the string:
                        caption = caption.Substring(caption.LastIndexOf('(') + 1);  //Extract the text after the last '('
                        caption = caption.Substring(0, caption.Length - 1);     //Remove the final ')'

                        //Any available com port should already be found by the code above that only looks for Com ports (rather than USB specific ones). We find it then append the AutoDetect text.
                        int index = cbComPortsUSB.Items.IndexOf(caption);
                        if (index == -1)
                            continue;

                        cbComPortsUSB.Items[index] = caption + " VCP";
                        if (cbComPortsUSB.SelectedIndex == -1)  //We look for a VCP to set by default, but only if at this point there is still nothing selected.
                        {
                            cbComPortsUSB.SelectedIndex = index;

                            Console.WriteLine("DeviceID:" + device.GetPropertyValue("DeviceID") +
                                                "\nCaption:" + device.GetPropertyValue("Caption") +
                                                "\nClassGuid:" + device.GetPropertyValue("ClassGuid") +
                                                "\nCreationClassName:" + device.GetPropertyValue("CreationClassName") +
                                                "\nDescription:" + device.GetPropertyValue("Description") +
                                                "\nErrorDescription:" + device.GetPropertyValue("ErrorDescription") +
                                                "\nName:" + device.GetPropertyValue("Name") +
                                                "\nPNPDeviceID:" + device.GetPropertyValue("PNPDeviceID") +
                                                "\nService:" + device.GetPropertyValue("Service") +
                                                "\nStatus:" + device.GetPropertyValue("Status") +
                                                "\nSystemCreationClassName:" + device.GetPropertyValue("SystemCreationClassName") +
                                                "\nSystemName:" + device.GetPropertyValue("SystemName"));
                        }
                    }
                }
            }
            catch (SystemException)
            {
            }
            if (previous_port != cbComPortsUSB.Text && btConnectUSB.Text == "Disconnect")   //A com port has changed so we must disconnect!
            {   //Trigger a push of the CONNECT/DISCONNECT button which will disconnect the port.
                this.Invoke(new Action(() => btConnectUSB_Click(this, new EventArgs())));
            }
        }
        /// <summary>
        /// A Key has been been clicked that has a Long Press function.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void KeyWithLongPressFunctionality_MouseDown(object sender, MouseEventArgs e)
        {
            //If neither of the buttons of USB or RS232 are checked, we try to enable one of them if they are enabled, ie they are connected.
            if (!(rbUSBTransmit.Checked && rbUSBTransmit.Enabled) && !(rbRS232Transmit.Checked && rbRS232Transmit.Enabled))
            {
                if (rbUSBTransmit.Enabled)
                    rbUSBTransmit.Checked = true;
                else if (rbRS232Transmit.Enabled)
                    rbRS232Transmit.Checked = true;
                else
                {
                    CheckEngineeringModeSequence(Convert.ToByte(((Button)sender).Tag));
                    return;
                }
            }
            StartKeyPressTimer(Convert.ToByte(((Button)sender).Tag));
        }

        private void StartKeyPressTimer(byte button_id)
        {
            timerLongKeyPress.Tag = button_id;
            timerLongKeyPress.Start();

            engineering_mode_sequence_position = 0;
        }

        private void KeyWithLongPressFunctionality_MouseUp(object sender, MouseEventArgs e)
        {
            if (timerLongKeyPress.Enabled)  //the 3 seconds aren't up so send a short normal click
            {
                byte key_code = Convert.ToByte(((Button)sender).Tag);
                timerLongKeyPress.Tag = 0;
                timerLongKeyPress.Stop();   //Stop the timer so no long press is sent
				byte[] bt;
				if (serialPortRS232.IsOpen)
					bt = new byte[]{ 3, 0, key_code};	//3=length
				else
					bt = new byte[]{ 1, 0, key_code};
                WriteToInstrument(bt, current_instrument);

                CheckEngineeringModeSequence(key_code);
            }
        }

        private void KeyWithOUTLongPressFunctionality_Click(object sender, EventArgs e)
        {
            byte key_code = Convert.ToByte(((Button)sender).Tag);
            CheckEngineeringModeSequence(key_code);

            //If neither of the buttons of USB or RS232 are checked, we try to enable one of them if they are enabled, ie they are connected.
            if (!(rbUSBTransmit.Checked && rbUSBTransmit.Enabled) && !(rbRS232Transmit.Checked && rbRS232Transmit.Enabled))
            {
                if (rbUSBTransmit.Enabled)
                    rbUSBTransmit.Checked = true;
                else if (rbRS232Transmit.Enabled)
                    rbRS232Transmit.Checked = true;
                else
                    return;
            }
			byte[] bt;

			if (serialPortRS232.IsOpen)
				bt = new byte[]{ 3, 0, key_code};	//3=length,
			else
				bt = new byte[]{ 1, 0, key_code};

            WriteToInstrument(bt, current_instrument);

        /*    if (engineering_mode_key_sequence[engineering_mode_sequence_position] == key_code)
                engineering_mode_sequence_position++;
            else
                engineering_mode_sequence_position = 0;*/

            //The button to Toggle Beadseat Comp. needs the text and Tag changing on each press
            if (key_code == 25)
            {
                btBeadSeat.Tag = 26;
                btBeadSeat.Text = "Send Beadseat OFF";
            }
            else if (key_code == 26)
            {
                btBeadSeat.Tag = 25;
                btBeadSeat.Text = "Send Beadseat ON";
            }

        }

        private void CheckEngineeringModeSequence(byte key_code)
        {
            //Handle switching in to Engineering mode!
            if (key_code == 3 && engineering_mode_key_sequence[engineering_mode_sequence_position] == 'U')  //3 = Up
                engineering_mode_sequence_position++;
            else if (key_code == 5 && engineering_mode_key_sequence[engineering_mode_sequence_position] == 'L') //5 = Left
                engineering_mode_sequence_position++;
            else if (key_code == 6 && engineering_mode_key_sequence[engineering_mode_sequence_position] == 'R') //6 = Right
                engineering_mode_sequence_position++;
            else if (key_code == 4 && engineering_mode_key_sequence[engineering_mode_sequence_position] == 'D') //4 = Down
            {
                engineering_mode_sequence_position++;
                if (engineering_mode_sequence_position == 6)
                {
                    //in_engineering_mode = true;
                    btBeadSeat.Visible = true;
                    btLoadVeeScanSettings.Visible = true;

                    btRefreshPorts.Visible = true;
                    cbInstrumentRealtime.Visible = true;
                    label4.Visible = true;
                    tbInvalidPacket.Visible = true;
                    cbRealTimeDisplay.Visible = true;
                    label8.Visible = true;
                    //btScanFile.Visible = true;
                    lbTx.Visible = true;
                    tbTx.Visible = true;
                    lbMasterSettings.Visible = true;
                    btReprocessmasterSettings.Visible = true;
                    btSaveMasterSettings.Visible = true;
                    btReloadMasterSettings.Visible = true;
                    rbRS232Transmit.Visible = true;
                    rbUSBTransmit.Visible = true;
                    /*gbUSB.Location = new Point(10, 10);
                    tbBytes.Top = gbUSB.Bottom + 5;
                    lbBytes.Top = gbUSB.Bottom + 13;*/
                }
            }
            else// (key_code == 1 || key_code == 2)  //OK or CANCEL
                engineering_mode_sequence_position = 0;
        }
        /// <summary>
		/// We start the process of leaving the bootloader of the Eti and requesting it to run its main application.
		/// The unit must disconnect and reconnect the USB, to handle this we have a timer to disconnect from our end
		/// and then another timer to reconnect.
		/// </summary>
		/// <param name="bootloader_version"></param>
        private void JumpToSoftware(int bootloader_version)
        {
            string bootloader_version_string = (bootloader_version >> 16 & 0xFF).ToString() + "." + (bootloader_version >> 8 & 0xFF).ToString() + "." + (bootloader_version & 0xFF).ToString();
            tbConsole.AppendText("Connected to Bootloader version: " + bootloader_version_string + ", Jump to main code." + Environment.NewLine);
            tbConsole.ScrollToCaret();
            WriteToInstrument(1, 0, "<USB_OUTPUT>99</USB_OUTPUT>", 0);
            timerReconnect.Interval = 3000;
            timerReconnect.Start();
            //Application.DoEvents();	//Make sure the above command has been sent before disconnecting!
            //this.Invoke(new Action(() => tsBTConnect_Click(this, new EventArgs())));
        }
        /// <summary>
        /// CALLBACK from DLL when an instrument parameter has been changed, this is the acknowledgment.
		/// Now we know a value has been updated, we can set the controls to show that value.
        /// </summary>
        /// <param name="full_filename">Full path & filename of where the new file can be found.</param>
        public void NewHardwareResponse(Byte command_ID, Byte channel, Int32 command_value)
        {
            string error_str = "";
            decimal command_value_decimal = (decimal)command_value;

            if (command_ID == 0x42) //Battery level. The channel value is the battery percentage charged (0-100%) 200=Mains connected.
            {
                /*if (command_value == prev_battery_level)
                    return;
                if (_eti300config.Visible)
                {
                    prev_battery_level = command_value;
                    _eti300config.Invoke(new Action(() => _eti300config.SetBatteryLevel(command_value)));
                }
                return;*/
            }
            else if (command_ID == 0x49)    //This is a command from the Bootloader!
            {
                switch (channel)
                {
                    case 3: //Version of the Bootloader, we get here when we FIRST connect to the bootloader.
                        /*if ((command_value & 0xFF0000) == 0xFF0000) //Is this the BootLoaderBoot!?
                        {
                            this.Invoke(new Action(() => LoadFirmware(command_value))); //Pass in the bootloader version to the LoadFormware, in case it must be checked.
                        }
                        else if (_eti300config != null && _eti300config.ETiFirmwareUpgradeSet())    //We are conencted to a Bootloader AND user has requested new Firmware update
                        {
                            this.Invoke(new Action(() => LoadFirmware(command_value))); //Pass in the bootloader version to the LoadFormware, in case it must be checked.
                            _eti300config.ClearFirmwareCheckBox();
                        }
                        else*/
                            this.Invoke(new Action(() => JumpToSoftware(command_value)));
                        break;
                    /*case 7: //This was supposed to be sent from the Eti bootloader before erasing ROM, but it didn't get sent in time. So, if we now receive this, it means the ROM wasn't erased!
                        this.Invoke(new Action(() => rtbConsole.AppendText("Bootloader invalid HEX file length.\n")));
                        MessageBox.Show("Error with HEX file length beign transmitted.\nPower cycle the ETi-300.");
                        break;
                    case 8:
                        this.Invoke(new Action(() => rtbConsole.AppendText("Firmware Upgrade complete.\n")));
                        //Send a POWER OFF key press to the instrument.
                        TriggerEvent(new GraphEventArgs(-1, GraphEventArgs.MESSAGE_TYPE.TX_KEY_PRESS, 1, 0, 23));
                        Application.DoEvents();
                        MessageBox.Show("The ETi-300 has been powered OFF.", "Upgrade Complete");
                        break;
                    case 9: //Sent from the MAIN application indicating that it has set the bytes for the Bootloader to read, then we send the power OFF command.
                        this.Invoke(new Action(() => rtbConsole.AppendText("Bootloader ready, power unit ON.\n")));
                        //Send a POWER OFF key press to the instrument.
                        TriggerEvent(new GraphEventArgs(-1, GraphEventArgs.MESSAGE_TYPE.TX_KEY_PRESS, 1, 0, 23));
                        break;
                    case 10:    //Sent from the MAIN application indicating that it has FAILED to set the bytes for the Bootloader to read.
                        this.Invoke(new Action(() => rtbConsole.AppendText("Bootloader bytes FAILED to be written to ROM.\n")));
                        //Send a POWER OFF key press to the instrument.
                        TriggerEvent(new GraphEventArgs(-1, GraphEventArgs.MESSAGE_TYPE.TX_KEY_PRESS, 1, 0, 23));
                        break;
                    case 11:	//BootLoaderBoot has sent this and has uploaded a new Boot Loader.
                        this.Invoke(new Action(() => rtbConsole.AppendText("Boot Loader Upgrade complete.\n")));
                        //Send a POWER OFF key press to the instrument.
                        TriggerEvent(new GraphEventArgs(-1, GraphEventArgs.MESSAGE_TYPE.TX_KEY_PRESS, 1, 0, 23));
                        Application.DoEvents();
                        _eti300config.SetFirmwareCheckBox();
                        MessageBox.Show("The main firmware must now be re-loaded.\nThe ETi-300 has been powered OFF.", "Bootloader Upgrade Complete");
                        break;
                    */
                }
                return;
            }

            //Set the value to the relevant Phaseplane AND the channel_values and channel_values_etherMap.
            /*foreach (PhasePlane pp in _phase_planes)
            {
                if (pp == null || pp._displaying_channel != channel)
                    continue;

                string console_message = "Inst. Response: Command=" + command_ID.ToString() + " Chan=" + channel.ToString() + " Value=" + command_value.ToString() + "\n";
                switch (command_ID)
                {
                    case 0x46:  //Frequency
                        EC_Data._ChannelCollection[channel].channel_values.frequency = EC_Data._ChannelCollection[channel].channel_values_etherMap.frequency = command_value;
                        if (command_value == 0) //this means the channel is OFF!
                            EC_Data._ChannelCollection[channel].channel_values.Connector = EC_Data._ChannelCollection[channel].channel_values_etherMap.Connector = "Off";
                        pp.SetFrequency((int)command_value, ref error_str);
                        if (_current_active_pane != -1 && _phase_planes[_current_active_pane]._displaying_channel == channel)
                            this.Invoke(new Action(() => tsTBFreq.Text = command_value_decimal.ToString()));
                        //We need to refresh all of the Channel names in the drop-down as they relate to the Frequency
                        EC_Data.UpdateChannelNames(true, channel);
                        //this.Invoke(new Action(() => tsCBSource.Items[channel] = EC_Data._ChannelCollection[channel].channel_values_etherMap.Name));
                        this.Invoke(new Action(() => PopulateSourcesComboBox()));
                        break;
                    case 0x50:  //Phase
                                //command_value_decimal /= 10;
                        EC_Data._ChannelCollection[channel].channel_values.phase = EC_Data._ChannelCollection[channel].channel_values_etherMap.phase = command_value_decimal / 10.0M;
                        pp.SetPhase(command_value_decimal / 10.0M, ref error_str);
                        this.Invoke(new Action(() => tsTBPhase.Text = (command_value_decimal / 10.0M).ToString("f2")));
                        break;
                    case 0x58:  //Gain X
                                //command_value_decimal /= 10;
                        EC_Data._ChannelCollection[channel].channel_values.gain_x = EC_Data._ChannelCollection[channel].channel_values_etherMap.gain_x = command_value_decimal / 10.0M;
                        pp.SetGainX(command_value_decimal / 10.0M, ref error_str);
                        this.Invoke(new Action(() => tsTBGainX.Text = (command_value_decimal / 10.0M).ToString("f2")));
                        break;
                    case 0x59:  //Gain Y
                                //command_value_decimal /= 10;
                        EC_Data._ChannelCollection[channel].channel_values.gain_y = EC_Data._ChannelCollection[channel].channel_values_etherMap.gain_y = command_value_decimal / 10.0M;
                        pp.SetGainY(command_value_decimal / 10.0M, ref error_str);
                        this.Invoke(new Action(() => tsTBGainY.Text = (command_value_decimal / 10.0M).ToString("f2")));
                        break;
                    case 0x4C:  //LP Filter
                                //command_value_decimal /= 100;
                        EC_Data._ChannelCollection[channel].channel_values.filter_LP = EC_Data._ChannelCollection[channel].channel_values_etherMap.filter_LP = command_value_decimal / 100.0M;
                        pp.SetFilterLP(command_value_decimal / 100.0M, ref error_str);
                        this.Invoke(new Action(() => tsTBLPFilter.Text = (command_value_decimal / 100.0M).ToString("f2")));
                        break;
                    case 0x48:  //HP Filter
                                //command_value_decimal /= 100;
                        EC_Data._ChannelCollection[channel].channel_values.filter_HP = EC_Data._ChannelCollection[channel].channel_values_etherMap.filter_HP = command_value_decimal / 100.0M;
                        pp.SetFilterHP(command_value_decimal / 100.0M, ref error_str);
                        this.Invoke(new Action(() => tsTBHPFilter.Text = (command_value_decimal / 100.0M).ToString("f2")));
                        break;
                }
             
                this.Invoke(new Action(() => tbConsole.AppendText(console_message)));
            }*/
        }
        /// <summary>
        /// Send an Array of bytes to the instrument.
        /// If we are to send over RS232, we must convert the binary to ASCII and make them comma seperated
        /// </summary>
        /// <param name="bytes">Array of bytes to send</param>
        /// <param name="destination_instrument">Which instrument to send the data to (if more than 1 is connected)</param>
        private void WriteToInstrument(Byte[] bytes, int destination_instrument)
        {
            if (rbUSBTransmit.Checked && serialPortUSB.IsOpen)
            {
                if (serialPortUSB.IsOpen)
                {
                    if (lbTx.Text != "Tx USB")
                        lbTx.Text = "Tx USB";

                    serialPortUSB.Write(bytes, 0, bytes.Length);
                }
            }
            else if (rbRS232Transmit.Checked && serialPortRS232.IsOpen)
            {
                StringBuilder stb = new StringBuilder();

                if (lbTx.Text != "Tx RS232")
                    lbTx.Text = "Tx RS232";

				serialPortRS232.Write(bytes, 0, bytes.Length);

				string response = "", hex_response = "";

				if (bytes[0] == 0)
					return;

				response += bytes[0].ToString();
				hex_response += "0x"+Convert.ToString(bytes[0], 16);
				for (int x = 1; x < bytes[0]; x++)
				{
					response += ",";
					response += bytes[x].ToString();
					hex_response += ",0x";
					hex_response += Convert.ToString(bytes[x], 16);

				}
            }
			else if (cbUseDLL.Checked && EtherObj != null)
			{
				EtherObj.WriteToInstrument(bytes);
			}
			else
			{
				tbConsole.AppendText("Selected Communication medium is Closed"+Environment.NewLine);
				return;
			}
			tbTx.Text = "";
            foreach (byte bt in bytes)
                tbTx.Text += Convert.ToString(bt) + ",";
        }
        /// <summary>
        /// Send a string to the instrument.
        /// The command is added to a list and every tick of the timer another command from the list is sent.
        /// This is to give the instrument time to process each command. The timer is not used when sending binary data
        /// to the instrument.
        /// </summary>
        /// <param name="command">string to send</param>
        /// <param name="destination_instrument">Which instrument to send the data to (if more than 1 is connected)</param>
        private void WriteToInstrument(byte first_byte, byte second_byte, string command, int destination_instrument)
        {
            Int32 length = command.Length;
            tbConsole.Invoke(new Action(() => tbConsole.AppendText(command + Environment.NewLine)));

			tbTx.Text = "";
            byte[] bt = new byte[length + 2];
            bt[0] = first_byte;
			tbTx.Text += Convert.ToString(bt[0]) + ",";
            bt[1] = second_byte;
			tbTx.Text += Convert.ToString(bt[1]) + ",";
            for (Int32 x = 0; x < length; x++)
            {
                bt[x + 2] = (byte)command[x];
				tbTx.Text += Convert.ToString(bt[x + 2]) + ",";
            }

            commands_to_send.Add(bt);
            if (!timerWriteToInstrument.Enabled)
                timerWriteToInstrument.Start();
        }
        private void timerLongKeyPress_Tick(object sender, EventArgs e)
        {
            byte[] bt = { 1, 0, 99 };   //We replace the 99 with the key press

            timerLongKeyPress.Stop();   //Stop the timer so no long press is sent

			if (serialPortRS232.IsOpen)
				bt[0] = 3;

            bt[2] = (byte)(timerLongKeyPress.Tag);   //All LONG presses are 12 greater than the usual single presses.
            bt[2] += 12;

            timerLongKeyPress.Tag = "";
            if (bt[2] != 99)
                WriteToInstrument(bt, current_instrument);
        }

        private void btFileSystem_Click(object sender, EventArgs e)
        {
            byte[] bt = { 1, 5, 1 };    //Request the instrument to export its filesystem!
            WriteToInstrument(bt, current_instrument);
        }
        /// <summary>
        /// This method will filter the messages that are passed for usb device change messages only. 
        /// And parse them and take the appropriate action 
        /// </summary>
        /// <param name="m">a ref to Messages, The messages that are thrown by windows to the application.</param>
        /// <example> This sample shows how to implement this method in your form.
        /// <code>
        /// 
        [SecurityPermissionAttribute(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        protected override void WndProc(ref Message m)
        {
            
            if (m.Msg == WM_DEVICECHANGE)	// we got a device change message! A USB device was inserted or removed
            {
                //this.Invoke(new Action(() => btRefreshPorts_Click(this, new EventArgs())));
                timerRefreshPorts.Start();
                tbConsole.Invoke(new Action(() => tbConsole.AppendText("Device Change message"+Environment.NewLine)));
            }
            /*else
            {
                //Trying the NEW touchscreen stuff:
                switch (m.Msg)
                {
                    case Win32.WM_POINTERDOWN:
                        tbConsoleFileScan.AppendText("WM_POINTERDOWN\n");
                        break;
                    case Win32.WM_POINTERUP:
                        tbConsoleFileScan.AppendText("WM_POINTERUP\n");
                        break;
                    case Win32.WM_POINTERUPDATE:
                        tbConsoleFileScan.AppendText("WM_POINTERUPDATE\n");
                        break;
                    case Win32.WM_POINTERCAPTURECHANGED:
                        tbConsoleFileScan.AppendText("WM_POINTERCAPTURECHANGED\n");
                        break;

                    default:
                        base.WndProc(ref m);
                        return;
                }
                int pointerID = Win32.GET_POINTER_ID(m.WParam);
                Win32.POINTER_INFO pi = new Win32.POINTER_INFO();
                if (!Win32.GetPointerInfo(pointerID, ref pi))
                {
                    Win32.CheckLastError();
                }
                bool processed = false;
                switch (m.Msg)
                {
                    case Win32.WM_POINTERDOWN:
                        tbConsoleFileScan.AppendText("WM_POINTERDOWN\n");
                        break;
                    case Win32.WM_POINTERUP:
                        tbConsoleFileScan.AppendText("WM_POINTERUP\n");
                        break;
                    case Win32.WM_POINTERUPDATE:
                        tbConsoleFileScan.AppendText("WM_POINTERUPDATE\n");
                        break;
                    case Win32.WM_POINTERCAPTURECHANGED:
                        tbConsoleFileScan.AppendText("WM_POINTERCAPTURECHANGED\n");
                        break;
                }
            }
             * */
            base.WndProc(ref m);	    // pass message on to base form
        }

        private void btClear_Click(object sender, EventArgs e)
        {
            //tbInvalidPacket.Invoke(new Action(() => tbInvalidPacket.Text = invalid_packet.ToString()));
            tbInvalidPacket.Text = "";
            tbTx.Text = "";
            tbConsole.Text = "";
            tbC1X.Text = "";
            tbC1Y.Text = "";
            tbC2X.Text = "";
            tbC2Y.Text = "";
            tbCmixX.Text = "";
            tbCmixY.Text = "";
            tbTxString.Tag = 0;
            tbTxString.Clear();
			progressBar1.Value = 0;

        }
        /// <summary>
        /// The Check Box for Auto-Connect has changed.
        /// If we have the functionality to save the state of this, it needs to be added here.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbAutoConnect_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void tabControl1_Selected(object sender, TabControlEventArgs e)
        {
            if (e.TabPage.Text == "PhasePlane")
            {
                if (phaseplane_paused)
                    return;

  //IJD              btClearPhasePlane_Click(sender, new EventArgs());   //Call the Clear Screen button when we change tabs to the Phase Plane. Otherwise, the graticule isn't redrawn.

                
                if (cbSource.SelectedIndex == -1)    //Make sure SOMETHING is selected!
                {
					if (cbSource.Items.Count != 0)
					{
						cbSource.SelectedIndexChanged -= cbSource_SelectedIndexChanged;
						cbSource.SelectedIndex = 0;
						cbSource.SelectedIndexChanged += cbSource_SelectedIndexChanged;
					}
                }
                if (cbSource2.SelectedIndex == -1)    //Make sure SOMETHING is selected!
                {
					if (cbSource.Items.Count != 0)
					{
						cbSource2.SelectedIndexChanged -= cbSource2_SelectedIndexChanged;
						cbSource2.SelectedIndex = 0;
						cbSource2.SelectedIndexChanged += cbSource2_SelectedIndexChanged;
					}
                }
				if (cbSource.Items.Count > 0 && cbSource2.Items.Count > 0)
				{
					ProcessGraphs();
					SetGraphAxisValues();
				}
            }
            else if (e.TabPage.Text == "Settings")
            {
                ChangeUSBInstrumentOutput();
            }
        }

  
        /// <summary>
        /// Send a command to the instrument to change it's USB output to the currently selected Radio Button.
        /// </summary>
        private void ChangeUSBInstrumentOutput()
        {
            string command = "";

			points_to_skip = 1;	//We skip

			//The Timebase graph is USUALLY time based, unless we are using CSCAN encoder triggered in which case it's distance!
			lbSweep.Text = "Sweep Time";
			if (nudSweepTime.Value < 1.0M)
				nudSweepTime.Value = 1.0M;
            //Populate the source ComboBox with any data that may have been loaded in
			PopulateSourceComboBoxes();
            

			//C-Scan triggered data uses a points total persistence rather than a time based one.
			nudPersistence.Visible = true;
			nudPersistenceEncoder.Visible = false;

            // Value of the USB Output is from the enum in the instrument:
            // eNO_USB_OUTPUT, eFILE_SIZE, eFILE_DATA, eFILE_NAME, eREALTIME_RAW, eREALTIME_POSTPROCESS, eXML_HEADER, eSINGLE_CHAN_POST, eCONDUCTIVITY_USB
            if (rbPostProcess.Checked)  //Post Porcess has been selected
            {
                command = "<USB_OUTPUT>5</USB_OUTPUT>";
                //If using the DLL, we assume that the command is actioned.
                if (cbUseDLL.Checked && EtherObj != null)
                    data_transfer_state = REALTIME_DATA_POSTPROCESS;

            }
            else if (rbRaw.Checked)  //Post Process has been selected
            {
                command = "<USB_OUTPUT>4</USB_OUTPUT>";
                //If using the DLL, we assume that the command is actioned.
                if (cbUseDLL.Checked && EtherObj != null)
                    data_transfer_state = REALTIME_DATA_RAW;
            }
            else if (rbNone.Checked)
            {
                command = "<USB_OUTPUT>0</USB_OUTPUT>"; //No data!
                //If using the DLL, we assume that the command is actioned.
                if (cbUseDLL.Checked && EtherObj != null)
                    data_transfer_state = NOTHING;

            }
            else if (rbSingleChan.Checked)
            {
                command = "<USB_OUTPUT>7</USB_OUTPUT>"; //Single channel, post processed data!
                //If using the DLL, we assume that the command is actioned.
                if (cbUseDLL.Checked && EtherObj != null)
                    data_transfer_state = SINGLE_CHAN_POST;
            }
            else if (rbNonRealtime.Checked)
            {
                command = "<USB_OUTPUT>8</USB_OUTPUT>"; //Non-Realtime, ie theta, radius, percentages
                //If using the DLL, we assume that the command is actioned.
                if (cbUseDLL.Checked && EtherObj != null)
                    data_transfer_state = NON_REALTIME;
            }

            WriteToInstrument(1, 0, command, current_instrument);

            //Change any required labels
            if (data_transfer_state == CONDUCTIVITY)
            {
                lbCh1.Text = "Conductivity";
                lbCh2.Text = "Lift-Off";
                tbC1Y.Visible = tbC2Y.Visible = tbCmixX.Visible = tbCmixY.Visible = lbMix.Visible = false;
                
            }
            else
            {
                lbCh1.Text = "Ch1";
                lbCh2.Text = "Ch2";
                tbC1Y.Visible = tbC2Y.Visible = tbCmixX.Visible = tbCmixY.Visible = lbMix.Visible = true;
            }
            if (data_transfer_state == NON_REALTIME)
                lbMix.Text = "Ch1 %";
            else if (data_transfer_state == SINGLE_CHAN_POST)
            {
                lbMix.Text = "Counter";
                tbCmixY.Text = tbTheta.Text = tbC2X.Text = tbC2Y.Text = "";
            }
            else
                lbMix.Text = "Mix";

            if (data_transfer_state == SINGLE_CHAN_POST)
                label12.Text = "Encoder";
            else
                label12.Text = "r-Theta";
        }
        /// <summary>
        /// The LOGGING button has been clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btLogging_Click(object sender, EventArgs e)
        {
            if (btLogging.BackColor == Color.Red)    //We weren't logging, so try too....
            {
                if (tbLoggingToFile.Text == "")
                {
                    MessageBox.Show("Must select log file first.");
                    return;
                }
                if (btConnectUSB.Text == "Connect")
                {
                    MessageBox.Show("Not Connected to an instrument!");
                    return;
                }
                //This will instruct the intstrument to resend the header for the data output.
                btLogging.BackColor = Color.Green;
                try
                {
                    fs = new FileStream(tbLoggingToFile.Text, System.IO.FileMode.Create);
                    //By telling the connected instrument what type of data we want, it will resend its XML header which we store in the log file :-)
                    ChangeUSBInstrumentOutput();
                    rbLoggingClickToSave.Enabled = false;
                    rbLoggingRealtime.Enabled = false;
                    //BUT, this isn't working for the C_Scan stuff (I think because of the slow poll-rate), so just start saving the data now.
                    if (data_transfer_state == CSCAN_ENC_TRIGGERED)
                        logging_realtime_data = true;
                }
                catch   //If we fail to open the ifle, set the button back to a RED background.
                {
                    fs = null;
                    btLogging.Invoke(new Action(() => btLogging.BackColor = Color.Red));
                }
                //By telling the connected instrument what type of data we want, it will resend its XML header which we store in the log file :-)
                ChangeUSBInstrumentOutput();
            }
            else // Stop logging!
            {
                StopLogging();
            }
        }

        private void btScanFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "ETherNDE data files (*.etd;*.dat;*.txt)|*.etd;*.dat;*.txt|All files (*.*)|*.*";
            if (ofd.ShowDialog() != DialogResult.OK || ofd.SafeFileNames.Length != 1)
                return;

            FileStream fs_scan = new FileStream(ofd.FileName, FileMode.Open);
            string tag = "";
            byte read_byte = 0;
            int prev_x = 0;
            UInt32 pos = 0; //Keep a rough indication as to how much we've processed.

            tbConsoleFileScan.Text = "";

            while (tag != "/RAW_DATA>")
            {
                tag = "";
                while (fs_scan.ReadByte() != '<')
                    pos++;
                do
                {
                    read_byte = (byte)fs_scan.ReadByte();
                    pos++;
                    tag += (char)read_byte;
                }
                while (read_byte != '>');
            }

            //Read the CR and NULL
            read_byte = (byte)fs_scan.ReadByte();
            read_byte = (byte)fs_scan.ReadByte();

            pos += 2;

            do
            {
                try
                {
                    //We should have been at 112, so search until we are
                    do
                    {
                        read_byte = (byte)fs_scan.ReadByte();
                        pos++;
                    }
                    while (read_byte != 112 && pos < fs_scan.Length);

                    read_byte = (byte)fs_scan.ReadByte();
                    if (read_byte != 143)
                        throw new SystemException("Error - Inverse Status");
                    //Read the 4 X values
                    int value_x = fs_scan.ReadByte();
                    value_x += (fs_scan.ReadByte() * 256);
                    value_x += (fs_scan.ReadByte() * 256 * 256);
                    //     value_x += (fs_scan.ReadByte() * 256 * 256 * 256);

                    if (prev_x + 1 != value_x)
                        tbConsoleFileScan.AppendText(value_x.ToString() + "Err"+Environment.NewLine);
                    else
                        tbConsoleFileScan.AppendText(value_x.ToString() + Environment.NewLine);
                    prev_x = values[0];

                    int value_y = fs_scan.ReadByte();
                    value_y += (fs_scan.ReadByte() * 256);
                    value_y += (fs_scan.ReadByte() * 256 * 256);
                    //       value_y += (fs_scan.ReadByte() * 256 * 256 * 256);
                }
                catch (SystemException ex)
                {
                    tbConsoleFileScan.AppendText(ex.Message);
                }
            } while ((pos += 8) < fs_scan.Length);

            fs_scan.Close();
        }
        /// <summary>
        /// Sending a 17 to the instrument toggles it's transmission status. If debug data then the X & Y values are simply an incrementing value!
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbDebugData_CheckedChanged(object sender, EventArgs e)
        {
            byte[] bt = { 1, 0, 17 };    //Send an unused number to the instrument so that I can break on it and take a look at the buffers!!
            WriteToInstrument(bt, current_instrument);

            if (!serialPortUSB.IsOpen)
            {
                this.cbInstrumentRealtime.CheckedChanged -= this.cbDebugData_CheckedChanged;
                cbInstrumentRealtime.Checked = !cbInstrumentRealtime.Checked;
                this.cbInstrumentRealtime.CheckedChanged += new System.EventHandler(this.cbDebugData_CheckedChanged);
            }
        }
        /// <summary>
        /// Take all the data in the sample buffer and add them to the PhasePlane or Timebase graph
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timerPhasePlane_Tick(object sender, EventArgs e)
        {
            int x = 0;

            if (tabControl1.SelectedTab.Name == "tpDataFormat")
            {
                if (cbUseDLL.Checked)
                {
                    if (data_transfer_state == CONDUCTIVITY)
                    {
                        tbC1X.Text = ((decimal)realtime_values[4] / 1000).ToString();
                        tbC2X.Text = ((decimal)realtime_values[5] / 1000).ToString();
                        tbTheta.Text = ((decimal)realtime_values[6] / 100000).ToString();
                        tbRadius.Text = realtime_values[7].ToString();
                    }
                    else if (data_transfer_state == REALTIME_DATA_RAW)
                    {
                        tbC1X.Text = (realtime_values[0] / 256).ToString();
                        tbC1Y.Text = (realtime_values[1] / 256).ToString();
                        tbC2X.Text = (realtime_values[2] / 256).ToString();
                        tbC2Y.Text = (realtime_values[3] / 256).ToString();
                    }
                    else if (data_transfer_state == SINGLE_CHAN_POST)
                    {
                        tbC1X.Text = realtime_values[0].ToString();
                        tbC1Y.Text = realtime_values[1].ToString();
                        tbCmixX.Text = realtime_values[2].ToString();   //Counter
                        tbRadius.Text = realtime_values[3].ToString();  //Encoder
                    }
                    else
                    {
                        tbC1X.Text = realtime_values[0].ToString();
                        tbC1Y.Text = realtime_values[1].ToString();
                        tbC2X.Text = realtime_values[2].ToString();
                        tbC2Y.Text = realtime_values[3].ToString();
                        tbCmixX.Text = realtime_values[4].ToString();
                        tbCmixY.Text = realtime_values[5].ToString();
                        tbTheta.Text = realtime_values[6].ToString();
                        tbRadius.Text = realtime_values[7].ToString();
                    }
                }
                else //Not using the DLL
                {
                    if (data_transfer_state == SINGLE_CHAN_POST)
                    {
                        tbC1X.Text = realtime_values[0].ToString();
                        tbC1Y.Text = realtime_values[1].ToString();
                        tbC2X.Text = realtime_values[2].ToString();
                        tbC2Y.Text = realtime_values[3].ToString();
                    }
                    else if (data_transfer_state == CONDUCTIVITY)
                    {
                        tbC1X.Text = ((decimal)realtime_values[0] / 1000).ToString();
                        tbC2X.Text = ((decimal)realtime_values[1] / 1000).ToString();
                        tbTheta.Text = ((decimal)realtime_values[2] / 100000).ToString();
                        tbRadius.Text = realtime_values[3].ToString();
                    }
                    else if (data_transfer_state == REALTIME_DATA_POSTPROCESS)
                    {
                        tbC1X.Text = realtime_values[0].ToString();
                        if (rbEmbedEC.Checked)
                        {
                            tbC1Y.Text = realtime_values[1].ToString();
                        }
                        else
                        {
                            tbC2X.Text = realtime_values[1].ToString();
                            tbCmixX.Text = realtime_values[2].ToString();
                            tbC1Y.Text = realtime_values[3].ToString();
                            tbC2Y.Text = realtime_values[4].ToString();
                            tbCmixY.Text = realtime_values[5].ToString();
                        }
                    }
                    else if (data_transfer_state == CSCAN_ENC_TRIGGERED)
                    {
                        tbC1X.Text = realtime_values[0].ToString();
                        tbC2X.Text = realtime_values[1].ToString();
                        tbCmixX.Text = realtime_values[2].ToString();
                        tbC1Y.Text = realtime_values[3].ToString();
                        tbC2Y.Text = realtime_values[4].ToString();
                        tbCmixY.Text = realtime_values[5].ToString();
                        tbTheta.Text = realtime_values[6].ToString();   //Counter
                        tbRadius.Text = realtime_values[7].ToString();  //Encoder
                    }
                    else if (data_transfer_state == REALTIME_DATA_RAW)
                    {
                        tbC1X.Text = (realtime_values[0] / 256).ToString();
                        tbC1Y.Text = (realtime_values[1] / 256).ToString();
                        tbC2X.Text = (realtime_values[2] / 256).ToString();
                        tbC2Y.Text = (realtime_values[3] / 256).ToString();
                    }
                    else if (data_transfer_state == NON_REALTIME)
                    {
                        tbC1X.Text = realtime_values[0].ToString();
                        tbC1Y.Text = realtime_values[1].ToString();
                        tbC2X.Text = realtime_values[2].ToString();
                        tbC2Y.Text = realtime_values[3].ToString();
                        tbTheta.Text = ((Int16)realtime_values[6]).ToString();
                        tbRadius.Text = ((Int16)realtime_values[7]).ToString();
                        tbCmixX.Text = realtime_values[4].ToString();
                        tbCmixY.Text = realtime_values[5].ToString();
                    }
                }
            }
            else if (tabControl1.SelectedTab.Name == "tpPhaseplane")
            {
                int display_buf = buf_num;  //We'll now display from the buffer that we were writing too..
                int draw_position = 0;

                if (buf_num == 0)
                {
                    buf_num = 1;    //As 0 & 1 are for Channels 1 & 2, the 2nd buffers are 2 & 3
                    write_position[buf_num] = 0;
                    draw_position = write_position[0];
                }
                else
                {
                    buf_num = 0;
                    write_position[buf_num] = 0;
                    draw_position = write_position[1];
                }
                Series temp_series;
                int y_val, x_val;

                for (int series_count = 0; series_count < 2; series_count++)
                {
                    if (series_count == 0)
                        temp_series = S;
                    else
                        temp_series = S2;

                    if (temp_series.Tag == null || (int)temp_series.Tag == -99)  //-99 is OFF.
                        continue;

					for (int y = (int)ctTimebase.ChartAreas[0].AxisX.Maximum; y < temp_series.Points.Count; y++)
						temp_series.Points.RemoveAt(0);

                    for (x = 0; x < draw_position; x++)
                    {
                        if ((int)temp_series.Tag == -1) //Chan 1 X
                        {
							temp_series.Points.AddXY(0, graph_points[CHAN1_BUFFER + display_buf, x].X);
                        }
                        else if ((int)temp_series.Tag == -2) //Chan 1 Y
                        {
                            temp_series.Points.AddXY(0, graph_points[CHAN1_BUFFER + display_buf, x].Y);
                        }
                        else if ((int)temp_series.Tag == -3) //Chan 2 X
                        {
                            temp_series.Points.AddXY(0, graph_points[CHAN2_BUFFER + display_buf, x].X);
                        }
                        else if ((int)temp_series.Tag == -4) //Chan 2 Y
                        {
                            temp_series.Points.AddXY(0, graph_points[CHAN2_BUFFER + display_buf, x].Y);
                        }
                        else if ((int)temp_series.Tag == -5) //Chan Mix X
                        {
                            temp_series.Points.AddXY(0, graph_points[MIX_BUFFER + display_buf, x].X);
                        }
                        else if ((int)temp_series.Tag == -6) //Chan Mix Y
                        {
                            temp_series.Points.AddXY(0, graph_points[MIX_BUFFER + display_buf, x].Y);
                        }
                        else if ((int)temp_series.Tag == -7) //Encoder data
                        {
                            temp_series.Points.AddXY(0, graph_points[ENCODER_BUFFER + display_buf, x].Y);
                        }
                        else if ((int)temp_series.Tag == -8) //Counter data
                        {
                            temp_series.Points.AddXY(0, graph_points[COUNTER_BUFFER + display_buf, x].Y);
                        }
                        else if ((int)temp_series.Tag == -9)  //Status data
                        {
                            temp_series.Points.AddXY(0, graph_points[STATUS_BUFFER + display_buf, x].X);
                        }
                        else if ((int)temp_series.Tag == -10)  //Theta
                        {
                            temp_series.Points.AddXY(0, graph_points[ANGLE_VECTOR_BUFFER + display_buf, x].X);
                        }
                        else if ((int)temp_series.Tag == -11)  //Vector/Radius
                        {
                            temp_series.Points.AddXY(0, graph_points[ANGLE_VECTOR_BUFFER + display_buf, x].Y);
                        }
                        else if ((int)temp_series.Tag == -12)  //Conductivity
                        {
                            temp_series.Points.AddXY(0, graph_points[CONDUCTIVITY_BUFFER + display_buf, x].X);
                        }
                        else if ((int)temp_series.Tag == -13)  //Lift-Off
                        {
                            temp_series.Points.AddXY(0, graph_points[CONDUCTIVITY_BUFFER + display_buf, x].Y);
                        }
                    }
                    //Phaseplane!
                    if (series_count == 0)
                    {
                        temp_series = Spp;
                    }
                    else
                    {
                        temp_series = Spp2;
                    }
                    if (temp_series.Tag == null)
                        continue;
                    
					//When decreasing the Persistence, points must be removed!z
					for (int y = (int)(nudPersistence.Value * last_seconds_points_count / (points_to_skip + 1)); y < temp_series.Points.Count - 1; y++)
						temp_series.Points.RemoveAt(0);


                    for (x = 0; x < draw_position; x++)
                    {
                        if ((int)temp_series.Tag == -5)
                        {
                            x_val = graph_points[MIX_BUFFER + display_buf, x].X;
                            y_val = graph_points[MIX_BUFFER + display_buf, x].Y;
                        }
                        else if ((int)temp_series.Tag == -3)
                        {
                            x_val = graph_points[CHAN2_BUFFER + display_buf, x].X;
                            y_val = graph_points[CHAN2_BUFFER + display_buf, x].Y;
                        }
                        else //Default to Chan 1 on the phase plane
                        {
                            x_val = graph_points[CHAN1_BUFFER + display_buf, x].X;
                            y_val = graph_points[CHAN1_BUFFER + display_buf, x].Y;
                        }
                        if (x_val == 0)
                            x_val = 1;
                        if (y_val == 0)
                            y_val = 1;

                        //Add the points!
                        temp_series.Points.AddXY(x_val, y_val);
                    }
                }//end of Series loop
            }
        }
        /// <summary>
        /// We only clear the Phase Plane, as the Timebase is generally OK.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btClearPhasePlane_Click(object sender, EventArgs e)
        {
            //Spp2.Points.Clear();
			for (Int32 ind = Spp2.Points.Count - 1; ind >= 0; ind--)
				Spp2.Points.RemoveAt(ind);	//Remove from the end.
            //Spp.Points.Clear();
			for (Int32 ind = Spp.Points.Count - 1; ind >= 0; ind--)
				Spp.Points.RemoveAt(ind);	//Remove from the end.
			//S.Points.Clear();
			for (Int32 ind = S.Points.Count - 1; ind >= 0; ind--)
				S.Points.RemoveAt(ind);	//Remove from the end.
			//S2.Points.Clear();
			for (Int32 ind = S2.Points.Count - 1; ind >= 0; ind--)
				S2.Points.RemoveAt(ind);	//Remove from the end.
        }

        private void nudPersistence_ValueChanged(object sender, EventArgs e)
        {
            //Populate the value of the large NUD
            //tbNudValue.Text = ((NumericUpDown)sender).Value.ToString();
            //tbNudNameText.Text = "Persistence";

            if (big_NumericUpDown_parent == nudPersistence)
                tbNudValue.Text = nudPersistence.Text;

            int max_points = (int)(nudPersistence.Value * (last_seconds_points_count / (points_to_skip + 1)));
            //When decreasing the Persistence, points must be removed!
            if (Spp.Points.Count > max_points)
            {
                for (int x = max_points; x < Spp.Points.Count; x++)
                    Spp.Points.RemoveAt(0);
            }
            if (ctPhase.Series.Count == 2)
            {
                if (Spp.Points.Count > max_points)
                {
                    for (int x = max_points; x < Spp.Points.Count; x++)
                        Spp.Points.RemoveAt(0);
                }
            }
        }

         /// <summary>
        /// This command causes the instrument to reprocess its internal Master Settings values. This is required if a value has been changed by sending an XML command.
        /// ie Changing the frequency via USB XML will change the value stored in the instrument but until reprocessed the actual frequency won't change.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btReprocessmasterSettings_Click(object sender, EventArgs e)
        {
            byte[] bt = { 1, 10 };
            WriteToInstrument(bt, current_instrument);
        }
        /// <summary>
        /// The instrument will reload the ms.xml and favourites.xml files from the uSD card. This is required if either of these files have been sent and values from the new files should be used.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btReloadMasterSettings_Click(object sender, EventArgs e)
        {
            byte[] bt = { 1, 11 };
            WriteToInstrument(bt, current_instrument);
        }
        /// <summary>
        /// The instrument will save all of it's master settings to the file ms.xml.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btSaveMasterSettings_Click(object sender, EventArgs e)
        {
            byte[] bt = { 1, 12 };
            WriteToInstrument(bt, current_instrument);
        }

        private void rbDataFormat_Click(object sender, EventArgs e)
        {
            //We just want to process a button that is CHECKED, not un-checked.
            if (!((RadioButton)sender).Checked)
                return;

            ChangeUSBInstrumentOutput();
        }

        private void timerRefreshPorts_Tick(object sender, EventArgs e)
        {
            RefreshVCPPorts();
        }

        private void btRefreshPorts_Click(object sender, EventArgs e)
        {
            timerRefreshPorts.Start();
        }
 
        private void nudDriveGain_ValueChanged(object sender, EventArgs e)
        {
            ((nudWithXML)sender).modified = true;
            //The only permissable values for Drive Gain are 0, 6 & 10.
            // 4 Occurs if decreasing from 10 as the step size is 6.
            if (nudDriveGain.Value == 4)
                nudDriveGain.Value = 6;
			//When connected to an EmbedEC, the Drive gain can only be 0 or 6dB, which is sent to the instrument as 0 or 1.
			if (rbEmbedEC.Checked && nudDriveGain.Value > 6)
				nudDriveGain.Value = 6;
        }

        private void nudPhase_ValueChanged(object sender, EventArgs e)
        {
            ((nudWithXML)sender).modified = true;

            if (((nudWithXML)sender).Value >= 360.0M)
                ((nudWithXML)sender).Value = 0;
            else if (((nudWithXML)sender).Value <= -0.1M)
                ((nudWithXML)sender).Value = 359.9M;
        }

        private void nudFrequency_ValueChanged(object sender, EventArgs e)
        {

            Label units_label = lbFreqUnitsCh1;
            if (sender == nudFrequencyCh2)
                units_label = lbFreqUnitsCh2;

            ControlValue ct_val = instrument_settings.GetControlValueFromTagAndPath(((nudWithXML)sender).Tag.ToString());

            if (ct_val == null)
                return;

            //Set the controls Modified flag to TRUE!
            ((nudWithXML)sender).modified = true;
            //Set up the correct text in the label, although it may change...
            if (((nudWithXML)sender).multiplier == 1)
                units_label.Text = "Hz";
            else if (((nudWithXML)sender).multiplier == 1000)
                units_label.Text = "kHz";
            else
                units_label.Text = "MHz";

            if (((nudWithXML)sender).Value < 10 && ((nudWithXML)sender).multiplier == 1)
                ((nudWithXML)sender).Value = 10;
            else if (((nudWithXML)sender).Value > 12.8M && ((nudWithXML)sender).multiplier == 1000000)
                ((nudWithXML)sender).Value = 12.8M;
            else if (((nudWithXML)sender).Value >= 1000M)
            {
                ((nudWithXML)sender).ValueChanged -= nudFrequency_ValueChanged;
                ((nudWithXML)sender).Value = 0.9M;
                ((nudWithXML)sender).ValueChanged += nudFrequency_ValueChanged;
                if (((nudWithXML)sender).multiplier == 1000)
                {
                    units_label.Text = "MHz";
                    ((nudWithXML)sender).multiplier = 1000000;
                    ct_val.multiplier = 1000000;
                }
                else
                {
                    units_label.Text = "kHz";
                    ((nudWithXML)sender).multiplier = 1000;
                    ct_val.multiplier = 1000;
                }
            }
            else if (((nudWithXML)sender).Value < 1M)
            {
                ((nudWithXML)sender).ValueChanged -= nudFrequency_ValueChanged;
                ((nudWithXML)sender).Value = 990M;
                ((nudWithXML)sender).ValueChanged += nudFrequency_ValueChanged;
                if (((nudWithXML)sender).multiplier == 1000)
                {
                    units_label.Text = "Hz";
                    ((nudWithXML)sender).multiplier = 1;
                    ct_val.multiplier = 1;
                }
                else
                {
                    units_label.Text = "kHz";
                    ((nudWithXML)sender).multiplier = 1000;
                    ct_val.multiplier = 1000;
                }
            }

            //Check the increment
            if (((nudWithXML)sender).Value > 100)
            {
                ((nudWithXML)sender).Increment = 10;
                ((nudWithXML)sender).ValueChanged -= nudFrequency_ValueChanged;
                ((nudWithXML)sender).Value = Math.Round(((nudWithXML)sender).Value / 10) * 10;
                ((nudWithXML)sender).ValueChanged += nudFrequency_ValueChanged;
            }
            else if (((nudWithXML)sender).Value > 10)
            {
                ((nudWithXML)sender).Increment = 1;
                ((nudWithXML)sender).ValueChanged -= nudFrequency_ValueChanged;
                ((nudWithXML)sender).Value = Math.Round(((nudWithXML)sender).Value);
                ((nudWithXML)sender).ValueChanged += nudFrequency_ValueChanged;
            }
            else// if (((nudWithXML)sender).Value > 1)
                ((nudWithXML)sender).Increment = 0.1M;
        }

        private void cbProbeTypeCh1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //Do all necessary checks assuming Probe1 to be correct as it's just changed.
            if (rbVictor22D.Checked)
                SetProbeTypesComponentCheck(0);
            else
                SetProbeTypesAeroCheckPlus(0);
        }
        private void cbProbeTypeCh2_SelectedIndexChanged(object sender, EventArgs e)
        {
            //Do all necessary checks assuming Probe1 to be correct as it's just changed.
            if (rbVictor22D.Checked)
                SetProbeTypesComponentCheck(1);
            else
                MessageBox.Show("Probe 2 not selectable on AeroCheck+");
        }
        /// <summary>
        /// The Connected instrument is thought to be a Component Check, so populate the relevant controls accordingly
        /// This is called when the button is selected or UN-selected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void rbComponentCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (rbVictor22D.Checked)   //The CC has just been selected:
            {
                cbProbeTypeCh1.Items.Clear();
                cbProbeTypeCh1.Items.Add("Abs-12");
                cbProbeTypeCh1.Items.Add("Abs-00");
                cbProbeTypeCh1.Items.Add("Bridge");
                cbProbeTypeCh1.Items.Add("Reflection");
                cbProbeTypeCh1.Items.Add("Rotary");
                cbProbeTypeCh2.Visible = true;
                cbProbeTypeCh2.Items.Clear();
                cbProbeTypeCh2.Text = "";
                cbProbeTypeCh2.Items.Add("Bridge");
                cbProbeTypeCh2.Items.Add("Reflection");
                cbProbeTypeCh2.Items.Add("Diff, ext Load");
                cbProbeTypeCh2.Items.Add("Diff, int Load");

                cbProbeTypeCh1.SelectedIndex = 0;
                cbProbeTypeCh2.SelectedIndex = -1;
            }
        }
        /// <summary>
        /// The Connected instrument is thought to be an AeroCheck+, so populate the relevant controls accordingly
        /// This is called when the button is selected or UN-selected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void rbCAeroCheckPlus_CheckedChanged(object sender, EventArgs e)
        {
            if (rbAeroCheckPlus.Checked)    //The AC+ has just been selected:
            {
                cbProbeTypeCh1.Items.Clear();
                cbProbeTypeCh1.Items.Add("Abs-12");
                cbProbeTypeCh1.Items.Add("Abs-00");
                cbProbeTypeCh1.Items.Add("Bridge");
                cbProbeTypeCh1.Items.Add("Reflection");
                //only add the Rotary probe if we have an Aero rather than a Weld instrument.
                if (instrument_settings != null)
                {
                    ControlValue ct_val = instrument_settings.GetControlValueFromTagAndPath("INSTRUMENT");
                    if (ct_val != null)
                    {
                        if (ct_val.display_value.StartsWith("Aero"))
                            cbProbeTypeCh1.Items.Add("Rotary");
                    }
                }

                cbProbeTypeCh1.Items.Add("Abs1/Diff2");
                cbProbeTypeCh1.Items.Add("Abs1/Refl2");
                cbProbeTypeCh1.Items.Add("Abs&Diff IntL");
                cbProbeTypeCh1.Items.Add("Abs&Diff ExtL");
                cbProbeTypeCh2.Items.Clear();
                cbProbeTypeCh2.Visible = false;

                cbProbeTypeCh1.SelectedIndex = 0;
            }
        }
        private void rbAeroCheck2_CheckedChanged(object sender, EventArgs e)
        {
            if (rbAeroCheck2.Checked)    //The AC2 has just been selected:
            {
                cbProbeTypeCh1.Items.Clear();
                cbProbeTypeCh1.Items.Add("Abs-12");
                cbProbeTypeCh1.Items.Add("Abs-00");
                cbProbeTypeCh1.Items.Add("Bridge");
                cbProbeTypeCh1.Items.Add("Reflection");
                //only add the Rotary probe if we have an Aero rather than a Weld instrument.
                if (instrument_settings != null)
                {
                    ControlValue ct_val = instrument_settings.GetControlValueFromTagAndPath("INSTRUMENT");
                    if (ct_val != null)
                    {
                        if (ct_val.display_value.StartsWith("Aero"))
                            cbProbeTypeCh1.Items.Add("Rotary");
                    }
                }
                cbProbeTypeCh2.Items.Clear();
                cbProbeTypeCh2.Visible = false;

                cbProbeTypeCh1.SelectedIndex = 0;
            }
        }
        /// <summary>
        /// The Connected instrument is thought to be an EmbedEC, so populate the relevant controls accordingly
        /// This is called when the button is selected or UN-selected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void rbEmbedEC_CheckedChanged(object sender, EventArgs e)
        {
            bool show_controls = !(rbEmbedEC.Checked || rbETI300.Checked);

            nudGainXch2.Visible = show_controls;
            nudGainYch2.Visible = show_controls;
            nudGainXchMix.Visible = show_controls;
            nudGainYchMix.Visible = show_controls;
            nudInputGainCh1.Visible = show_controls;
            nudInputGainCh2.Visible = show_controls;
            lbChannel2.Visible = show_controls;
            lbChannelMix.Visible = show_controls;
            nudFrequencyCh2.Visible = show_controls;
            nudPhaseCh2.Visible = show_controls;
            nudPhaseChMix.Visible = show_controls;
            lbFreqUnitsCh2.Visible = show_controls;
            lbRPM.Visible = show_controls;
            lbInputGain.Visible = show_controls;
            lbProbeType.Visible = show_controls;
            cbProbeTypeCh1.Visible = show_controls;
            cbProbeTypeCh2.Visible = show_controls;
            cbRotaryType.Visible = show_controls;
            gbAlarms.Visible = show_controls;
            nudLowPassFilterCh2.Visible = show_controls;
            nudHighPassFilterCh2.Visible = show_controls;
            btReprocessmasterSettings.Visible = show_controls;
            //Move the Drive Gain NUD
            nudDriveGain.Location = new Point(show_controls ? 181 : 106, nudDriveGain.Location.Y);
            //Move the Settings XML box
            rtbXMLSettings.Left = lbXMLFileName.Left = show_controls ? 546 : 350;
            rtbXMLSettings.Width = lbXMLFileName.Width = tabControl1.Width - 26 - rtbXMLSettings.Left;

        }
        /// <summary>
        /// We setup the Combo Box to only show the valid probes based on the provide probe number being correct.
        /// 
        /// </summary>
        /// <param name="correct_probe"></param>The probe (0 or 1) that is assumed to be set correctly.
        private void SetProbeTypesComponentCheck(int correct_probe)
        {
            //Remember what the OTHER probe is set too, so after we have changed it's options we can set the selected value back again.
            string other_probe_text = cbProbeTypeCh2.Text;

            cbProbeTypeCh1.modified = true;
            cbProbeTypeCh2.modified = true;

            if (correct_probe == 0) //Set up the available probes for Probe 2 based on probe 1s value.
            {
                //Hide/show the rotary settings
                if (!cbProbeTypeCh1.Text.StartsWith("Rotary"))
                {
                    cbProbeTypeCh2.Visible = true;
                    lbRPM.Visible = false;
                    nudRPM.Visible = false;
					btRotaryStart.Visible = btRotaryStop.Visible = false;
                    cbRotaryType.Visible = false;
                }

                cbProbeTypeCh2.Items.Clear();
                if (cbProbeTypeCh1.Text == "Abs-12")
                {
                    cbProbeTypeCh2.SelectedItem = -1;
                    cbProbeTypeCh2.Text = "";
                }
                else if (cbProbeTypeCh1.Text == "Reflection" || cbProbeTypeCh1.Text == "Bridge")
                {
                    cbProbeTypeCh2.Items.Add("Bridge");
                    cbProbeTypeCh2.Items.Add("Reflection");
                    if (other_probe_text != "Bridge" && other_probe_text != "Reflection")
                        cbProbeTypeCh2.Text = "";
                }
                else if (cbProbeTypeCh1.Text == "Abs-00")
                {
                    cbProbeTypeCh2.Items.Add("Reflection");
                    cbProbeTypeCh2.Items.Add("Bridge");
                    cbProbeTypeCh2.Items.Add("Diff, ext Load");
                    cbProbeTypeCh2.Items.Add("Diff, int Load");
                    if (other_probe_text != "Bridge" && other_probe_text != "Reflection" && other_probe_text != "Diff, ext Load" && other_probe_text != "Diff, int Load")
                        cbProbeTypeCh2.Text = "";
                }
                else if (cbProbeTypeCh1.Text.StartsWith("Rotary"))
                {
                    nudRPM.Top = cbProbeTypeCh2.Top;
                    nudRPM.Left = nudPhaseChMix.Left;
                    nudRPM.Visible = true;
					btRotaryStart.Visible = btRotaryStop.Visible = true;
                    cbProbeTypeCh2.Visible = false;
                    lbRPM.Visible = true;
                    cbRotaryType.Visible = true;
                    cbRotaryType.Location = cbProbeTypeCh2.Location;
                }
            }
            else    //Probe 2 is OK, set probe 1 possibilities
            {
                //We don't clear Probe 1s options, but we do insist it's set to a valid option
                if (((cbProbeTypeCh2.Text == "Bridge") &&
                        (cbProbeTypeCh1.Text != "Bridge" && cbProbeTypeCh1.Text != "Reflection")) ||
                ((cbProbeTypeCh2.Text == "Reflection") &&
                        (cbProbeTypeCh1.Text != "Bridge" && cbProbeTypeCh1.Text != "Reflection" && cbProbeTypeCh1.Text != "Abs-00")) ||

                ((cbProbeTypeCh2.Text == "Bridge" || cbProbeTypeCh2.Text == "Diff, ext Load" || cbProbeTypeCh2.Text == "Diff, int Load") &&
                        (cbProbeTypeCh1.Text != "Abs-00")))
                    cbProbeTypeCh1.SelectedItem = -1;

                /*
                        /// If the Combo box is holding Probe Type, it uses the following enum from the Code. ePT_OFF is for the use of Probe 2 only and is =1, same as ePT_ABSOLUTE_OO
                        ///         ePT_ABSOLUTE_12,
                        ///			ePT_ABSOLUTE_OO,
                        ///			ePT_OFF				= 1,
                        ///			ePT_BRIDGE,
                        ///			ePT_REFLECTION,
                        ///			ePT_ROTARY,
                        ///			ePT_ABS_DIF,
                        ///			ePT_ABS_REFL,
                        ///			ePT_DIFF_ABS_INTERNAL_LOAD,
                        ///			ePT_DIFF_ABS_EXTERNAL_LOAD,
                        ///			
                        /// The Items for a Combo Cox holding Probes are:
                            Abs-12
                            Abs-00
                            Bridge
                            Reflection
                            Rotary
                            Bridge
                            Reflection
                            Diff, ext Load
                            Diff, int Load
                  */
            }

            cbProbeTypeCh1.full_XML = "<PROBE_TYPE>";

            if (cbProbeTypeCh1.Text == "Abs-12")
                cbProbeTypeCh1.value_string = "0";
            else if (cbProbeTypeCh1.Text == "Abs-00")
                cbProbeTypeCh1.value_string = "1";
            else if (cbProbeTypeCh1.Text == "Bridge")
                cbProbeTypeCh1.value_string = "2";
            else if (cbProbeTypeCh1.Text == "Reflection")
                cbProbeTypeCh1.value_string = "3";
            else if (cbProbeTypeCh1.Text.StartsWith("Rotary"))
                cbProbeTypeCh1.value_string = "4";

            cbProbeTypeCh1.full_XML += cbProbeTypeCh1.value_string + "</PROBE_TYPE>\r";

            //Probe 2 can ONLY be set to:
            //	Bridge
            //	Refl
            //	OFF
            // UNLESS probe 1 is in Abs-OO when Probe 2 can also be:














































































































































            //	ePT_ABS_DIF,
            //	ePT_ABS_REFL,
            //	ePT_DIFF_ABS_INTERNAL_LOAD,
            //	ePT_DIFF_ABS_EXTERNAL_LOAD,
            //"Diff"
            //"Refl"
            //"Diff IntL"
            //"Diff ExtL"

            cbProbeTypeCh2.full_XML = "<DUAL_FREQUENCY>\r,<PROBE_TYPE>";

            if (cbProbeTypeCh2.Text == "" || cbProbeTypeCh1.Text == "Rotary")   //This is also OFF, so is valid.
                cbProbeTypeCh2.value_string = "1";
            else if (cbProbeTypeCh2.Text == "Bridge")
                cbProbeTypeCh2.value_string = "2";
            else if (cbProbeTypeCh2.Text == "Reflection")
                cbProbeTypeCh2.value_string = "3";
            else if (cbProbeTypeCh1.Text == "Abs-00" && cbProbeTypeCh2.Text == "Differential")
                cbProbeTypeCh2.value_string = "5";
            else if (cbProbeTypeCh1.Text == "Abs-00" && cbProbeTypeCh2.Text == "Reflection")
                cbProbeTypeCh2.value_string = "6";
            else if (cbProbeTypeCh1.Text == "Abs-00" && cbProbeTypeCh2.Text == "Diff, int Load")
                cbProbeTypeCh2.value_string = "7";
            else if (cbProbeTypeCh1.Text == "Abs-00" && cbProbeTypeCh2.Text == "Diff, ext Load")
                cbProbeTypeCh2.value_string = "8";
            else //Error, probe in an invalid setup.
                throw (new SystemException("Invalid probe combination"));

            cbProbeTypeCh2.full_XML += cbProbeTypeCh2.value_string + "</PROBE_TYPE>\r,</DUAL_FREQUENCY>\r";

        }
        /// <summary>
        /// We setup the Combo Box to only show the valid probes based on the provide probe number being correct.
        /// 
        /// </summary>
        /// <param name="correct_probe"></param>The probe (0 or 1) that is assumed to be set correctly.
        private void SetProbeTypesAeroCheckPlus(int correct_probe)
        {
            cbProbeTypeCh2.Text = "";
            cbProbeTypeCh2.SelectedItem = -1;

            cbProbeTypeCh1.modified = true;
            cbProbeTypeCh2.modified = true;

            if (cbProbeTypeCh1.Text.StartsWith("Rotary"))
            {
                nudRPM.Top = cbProbeTypeCh2.Top;
                nudRPM.Left = nudPhaseChMix.Left;
                nudRPM.Visible = true;
				btRotaryStart.Visible = btRotaryStop.Visible = true;
                cbProbeTypeCh2.Visible = false;
                lbRPM.Visible = true;
                cbRotaryType.Visible = true;
                cbRotaryType.Location = cbProbeTypeCh2.Location;
            }
            else
            {
                lbRPM.Visible = false;
                nudRPM.Visible = false;
				btRotaryStart.Visible = btRotaryStop.Visible = false;
                cbRotaryType.Visible = false;
            }

            cbProbeTypeCh1.full_XML = "<PROBE_TYPE>";

            if (cbProbeTypeCh1.Text == "Abs-12")
                cbProbeTypeCh1.value_string = "0";
            else if (cbProbeTypeCh1.Text == "Abs-00")
                cbProbeTypeCh1.value_string = "1";
            else if (cbProbeTypeCh1.Text == "Bridge")
                cbProbeTypeCh1.value_string = "2";
            else if (cbProbeTypeCh1.Text == "Reflection")
                cbProbeTypeCh1.value_string = "3";
            else if (cbProbeTypeCh1.Text.StartsWith("Rotary"))
                cbProbeTypeCh1.value_string = "4";
            else if (cbProbeTypeCh1.Text.StartsWith("Abs1/Diff2"))
                cbProbeTypeCh1.value_string = "5";
            else if (cbProbeTypeCh1.Text.StartsWith("Abs1/Refl2"))
                cbProbeTypeCh1.value_string = "6";
            else if (cbProbeTypeCh1.Text.StartsWith("Abs&Diff IntL"))
                cbProbeTypeCh1.value_string = "7";
            else if (cbProbeTypeCh1.Text.StartsWith("Abs&Diff ExtL"))
                cbProbeTypeCh1.value_string = "8";

            cbProbeTypeCh1.full_XML += cbProbeTypeCh1.value_string + "</PROBE_TYPE>";
        }
        /// <summary>
        /// Value of the High Pass Filter has changed so check and maybe alter:
        /// Increment of the NUD.
        /// Decimal Places of the NUD.
        /// Value of the NUD.
        /// Label that is displaying the units.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void nudHighPassFilter_ValueChanged(object sender, EventArgs e)
        {
            ((nudWithXML)sender).modified = true;

            if (((nudWithXML)sender).Value > 50)
            {
                ((nudWithXML)sender).DecimalPlaces = 0;
                ((nudWithXML)sender).Increment = 10;
            }
            else if (((nudWithXML)sender).Value > 1)
            {
                ((nudWithXML)sender).DecimalPlaces = 0;
                ((nudWithXML)sender).Increment = 1;
            }
            else if (((nudWithXML)sender).Value > 0.1M)
            {
                ((nudWithXML)sender).DecimalPlaces = 1;
                ((nudWithXML)sender).Increment = 0.1M;
            }
            else
            {
                ((nudWithXML)sender).DecimalPlaces = 2;
                ((nudWithXML)sender).Increment = 0.01M;
            }

            //Check decreasing values
            if (((nudWithXML)sender).Value == 0.9M)
                ((nudWithXML)sender).Value = 0.5M;
            else if (((nudWithXML)sender).Value == 0.4M)
                ((nudWithXML)sender).Value = 0.2M;
            else if (((nudWithXML)sender).Value == 0.09M)
                ((nudWithXML)sender).Value = 0.05M;
            else if (((nudWithXML)sender).Value == 0.04M)
                ((nudWithXML)sender).Value = 0.02M;
            //Check increasing values
            if (((nudWithXML)sender).Value == 1.1M)
                ((nudWithXML)sender).Value = 2M;
            else if (((nudWithXML)sender).Value == 0.6M)
                ((nudWithXML)sender).Value = 1M;
            else if (((nudWithXML)sender).Value == 0.3M)
                ((nudWithXML)sender).Value = 0.5M;
            else if (((nudWithXML)sender).Value == 0.11M)
                ((nudWithXML)sender).Value = 0.2M;
            else if (((nudWithXML)sender).Value == 0.06M)
                ((nudWithXML)sender).Value = 0.1M;
            else if (((nudWithXML)sender).Value == 0.03M)
                ((nudWithXML)sender).Value = 0.05M;

            //Check that the High Pass hasn't gone past the Low Pass!
            if (sender == nudHighPassFilterCh1)
            {
                if (nudLowPassFilterCh1.Value <= nudHighPassFilterCh1.Value)
                    nudLowPassFilterCh1.Value += nudLowPassFilterCh1.Increment;
            }
            else if (sender == nudHighPassFilterCh2)
            {
                if (nudLowPassFilterCh2.Value <= nudHighPassFilterCh2.Value)
                    nudLowPassFilterCh2.Value += nudLowPassFilterCh2.Increment;
            }
        }
        /// <summary>
        /// Value of the High Pass Filter has changed so check and maybe alter:
        /// Increment of the NUD.
        /// Decimal Places of the NUD.
        /// Value of the NUD.
        /// Label that is displaying the units.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void nudLowPassFilter_ValueChanged(object sender, EventArgs e)
        {
            ((nudWithXML)sender).modified = true;

            if (((nudWithXML)sender).Value > 50)
            {
                ((nudWithXML)sender).Increment = 10;
            }
            else
            {
                ((nudWithXML)sender).Increment = 1;
            }
            //Check that the High Pass hasn't gone past the Low Pass!
            if (sender == nudLowPassFilterCh1)
            {
                if (nudLowPassFilterCh1.Value <= nudHighPassFilterCh1.Value)
                    nudHighPassFilterCh1.Value -= nudHighPassFilterCh1.Increment;
            }
            else if (sender == nudLowPassFilterCh2)
            {
                if (nudLowPassFilterCh2.Value <= nudHighPassFilterCh2.Value)
                    nudHighPassFilterCh2.Value -= nudHighPassFilterCh2.Increment;
            }
        }
        /// <summary>
        /// The Alarm start and stop values are simply whole angles 0 to 359 degrees and can wrap round.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void nudAlarmStartStop_ValueChanged(object sender, EventArgs e)
        {
            ((nudWithXML)sender).modified = true;

            if (((nudWithXML)sender).Value == -1)
            {
                ((nudWithXML)sender).Value = 359;
            }
            else if (((nudWithXML)sender).Value == 360)
            {
                ((nudWithXML)sender).Value = 0;
            }
        }
        /// <summary>
        /// If the Alarm Outer is brought in check that it isn't too close to the Inner (gap of 5).
        /// If it is too close, move the Inner if possible, else leave the outer as it was.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void nudAlarmOuter_ValueChanged(object sender, EventArgs e)
        {
            ((nudWithXML)sender).modified = true;

            if (nudAlarmOuter.Value - 5 < nudAlarmInner.Value)
            {
                if (nudAlarmInner.Value > nudAlarmInner.Minimum)
                    nudAlarmInner.Value--;
                else
                    nudAlarmOuter.Value++;
            }
        }
        /// <summary>
        /// If the Alarm Inner is brought out check that it isn't too close to the Outer (gap of 5).
        /// If it is too close, move the Outer if possible, else leave the Inner as it was.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void nudAlarmInner_ValueChanged(object sender, EventArgs e)
        {
            ((nudWithXML)sender).modified = true;

            if (nudAlarmInner.Value + 5 > nudAlarmOuter.Value)
            {
                if (nudAlarmOuter.Value < nudAlarmOuter.Maximum)
                    nudAlarmOuter.Value++;
                else
                    nudAlarmInner.Value--;
            }
        }

        private void nudAlarmBottom_ValueChanged(object sender, EventArgs e)
        {
            ((nudWithXML)sender).modified = true;

            if (nudAlarmBottom.Value + 5 > nudAlarmTop.Value)
            {
                if (nudAlarmTop.Value < nudAlarmTop.Maximum)
                    nudAlarmTop.Value++;
                else
                    nudAlarmBottom.Value--;
            }
        }

        private void nudAlarmLeft_ValueChanged(object sender, EventArgs e)
        {
            ((nudWithXML)sender).modified = true;
            if (nudAlarmLeft.Value + 5 > nudAlarmRight.Value)
            {
                if (nudAlarmRight.Value < nudAlarmRight.Maximum)
                    nudAlarmRight.Value++;
                else
                    nudAlarmLeft.Value--;
            }
        }

        private void nudAlarmTop_ValueChanged(object sender, EventArgs e)
        {
            ((nudWithXML)sender).modified = true;
            if (nudAlarmTop.Value - 5 < nudAlarmBottom.Value)
            {
                if (nudAlarmBottom.Value > nudAlarmBottom.Minimum)
                    nudAlarmBottom.Value--;
                else
                    nudAlarmTop.Value++;
            }
        }

        private void nudAlarmRight_ValueChanged(object sender, EventArgs e)
        {
            ((nudWithXML)sender).modified = true;
            if (nudAlarmRight.Value - 5 < nudAlarmLeft.Value)
            {
                if (nudAlarmLeft.Value > nudAlarmLeft.Minimum)
                    nudAlarmLeft.Value--;
                else
                    nudAlarmRight.Value++;
            }
        }

        private void nudAlarmStretch_ValueChanged(object sender, EventArgs e)
        {
            ((nudWithXML)sender).modified = true;
            if (nudAlarmStretch.Value == 0.5M)
            {
                //nudAlarmStretch.Value = 0.5M;
                nudAlarmStretch.Increment = 0.5M;
            }
            else if (nudAlarmStretch.Value == 1)
            {
                nudAlarmStretch.Increment = 1M;
            }
            else if (nudAlarmStretch.Value == 3)
            {
                nudAlarmStretch.Value = 5M;
            }
            else if (nudAlarmStretch.Value == 6M)
            {
                nudAlarmStretch.Value = 10M;
            }
            else if (nudAlarmStretch.Value == 9)
            {
                nudAlarmStretch.Value = 5M;
            }
            else if (nudAlarmStretch.Value == 4)
            {
                nudAlarmStretch.Value = 2M;
            }

        }
        /// <summary>
        /// If a local file populated the settings window, then clicking on UPDATE will send the whole contents to the instrument, tag by tag in the correct order.
        /// Otherwise, the settings came from the instrument and we only send the modified one.
        /// Check controls that are of our types so see if they have their modified flag set.
        /// If they have been modified send the relevant XML command to the instrument.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btUpdateInstrument_Click(object sender, EventArgs e)
        {
            //Therefore the settings RTB holds a file, so all must be uploaded to the instrument.
            // The order can be important, plus we don't need to keep sending the Group tags.
            //Using the order that is in the RTB, just update the value with that of the associated control!
            if (lbXMLFileName.Text != "Live Data")
            {
                ScanAllControls(); //Take each string of XML and update the value within it and send to the instrument. TRUE = Update the instrument
                lbXMLFileName.Invoke(new Action(() => lbXMLFileName.Text = "Live Data"));   //Change the label above the RTB so it no longer indicates a File Name.
            }
            else
            {
                //Now search through our controls until we find one whose Tag matches our path and XML Tag.
                foreach (Control ctrl in this.tpXMLDisplay.Controls)
                {
                    if (ctrl.GetType() == typeof(System.Windows.Forms.GroupBox))
                    {
                        foreach (Control ctrl2 in ((GroupBox)ctrl).Controls)
                            CheckControlForModification(ctrl2);
                    }
                    else
                        CheckControlForModification(ctrl);
                }
            }
            //Send the reprocess command IF we are not connected to an EmbedEC, as that doesn't require it.
            if (!rbEmbedEC.Checked && !rbETI300.Checked)
                btReprocessmasterSettings_Click(sender, new EventArgs());
        }
        /// <summary>
        /// Given a Control, we check to see if it is one of our ones. If so, check to see if the modified flag is set,
        /// if so, send the XML to the instrument.
        /// </summary>
        /// <param name="ctrl"></param>
        private void CheckControlForModification(Control ctrl)
        {
            string html_command = ctrl.GetType().ToString();

            if (ctrl.GetType() == typeof(nudWithXML) && ((nudWithXML)ctrl).Modified())
            {
                ((nudWithXML)ctrl).modified = false;
                string XML_Command = ((nudWithXML)ctrl).GetXML();
                instrument_settings.ProcessXMLString(XML_Command);
                string[] commands = XML_Command.Split(',');
                foreach (string str in commands)
                    WriteToInstrument((byte)(serialPortRS232.IsOpen?(str.Length+2):1), 0, str, current_instrument);   //Write the string from the control preceeded with the control bytes 1,0 which inform the instrument this is an XML command.
            }
            else if (ctrl.GetType() == typeof(radbutWithXML) && ((radbutWithXML)ctrl).Modified())
            {
                ((radbutWithXML)ctrl).modified = false;
                string[] commands = ((radbutWithXML)ctrl).GetXML().Split(',');
                //If nothing returned, exit.
                if (commands.Length == 1 && commands[0] == "")
                    return;
                foreach (string str in commands)
                    WriteToInstrument((byte)(serialPortRS232.IsOpen?(str.Length+2):1), 0, str, current_instrument);   //Write the string from the control preceeded with the control bytes 1,0 which inform the instrument this is an XML command.
            }
            else if (ctrl.GetType() == typeof(comboWithXML) && ((comboWithXML)ctrl).Modified())
            {
                ((comboWithXML)ctrl).modified = false;
                string[] commands = ((comboWithXML)ctrl).GetXML().Split(',');
                foreach (string str in commands)
                    WriteToInstrument((byte)(serialPortRS232.IsOpen?(str.Length+2):1), 0, str, current_instrument);   //Write the string from the control preceeded with the control bytes 1,0 which inform the instrument this is an XML command.
            }
        }

        private void nudGain_ValueChanged(object sender, EventArgs e)
        {
            //Set the controls Modified flag to TRUE!
            ((nudWithXML)sender).modified = true;
        }

        /// <summary>
        /// Go through all of the lines of XML in the RTB and transmit it to the Instrument!
        /// Certain unnecessary tags will not be sent, like the INSTRUMENT tag etc.
        /// </summary>
        private void ScanAllControls()
        {
            string path = "";
            string[] xml_strings = rtbXMLSettings.Text.Split('\n');
            //Go through each string now and see if the XML tag is used by any of the controls.
            foreach (string str in xml_strings)
            {
                string temp = str.Trim();
                int first_close_bracket = temp.IndexOf('>');
                int last_open_bracket = temp.LastIndexOf('<');
                int first_end_sign = temp.IndexOf('/');
                //Do we have a block start tag?
                if (first_close_bracket > last_open_bracket)
                {
                    string value = temp.Substring(1, first_close_bracket - 1);
                    if (first_end_sign == -1)   //Starting group tag
                    {
                        if (value == "INSTRUMENT" || value == "RAW_DATA" || value == "SETTINGS")    //We don't add the main XML Group Tag "RAW_DATA"
                            continue;

                        WriteToInstrument(1, 0, "<" + value + ">\r", current_instrument);

                        path += value + "/";
                    }
                    else if (value.StartsWith("/")) //Are we a closing group Tag?
                    {
                        value = value.Substring(1); //Remove the preceeding '/'
                        if (path.EndsWith(value + "/"))
                        {
                            path = path.Replace(value + "/", "");
                            //Send the group tag.
                            WriteToInstrument(1, 0, "</" + value + ">\r", current_instrument);
                        }
                    }
                }
                else if (first_end_sign > first_close_bracket)
                {
                    //Get the value from the end of the string.
                    string tag = temp.Substring(first_end_sign + 1, temp.Length - first_end_sign - 2);
                    //Check that the string starts with the same
                    if (!temp.StartsWith("<" + tag))
                        continue;
                    string value = temp.Substring(first_close_bracket + 1, last_open_bracket - first_close_bracket - 1);

                    WriteToInstrument(1, 0, str.Trim(), current_instrument);
                }
            }
            //Send the reprocess command!
            byte[] bt = { 1, 10 };
            WriteToInstrument(bt, current_instrument);

        }
		/// <summary>
		/// Go through the XML, one line at a time. Find the corresponding control and update the XML with the value from the control.
		/// </summary>
		private void TakeXMLAndUpdateValue()
        {
			String new_xml = "";

            string path = "";
            string[] xml_strings = rtbXMLSettings.Text.Split('\n');
            //Go through each string now and see if the XML tag is used by any of the controls.
            foreach (string str in xml_strings)
            {
                string temp = str.Trim();
                int first_close_bracket = temp.IndexOf('>');
                int last_open_bracket = temp.LastIndexOf('<');
                int first_end_sign = temp.IndexOf('/');
                //Do we have a block start tag?
                if (first_close_bracket > last_open_bracket)
                {
                    string value = temp.Substring(1, first_close_bracket - 1);
                    if (first_end_sign == -1)   //Starting group tag
                    {
                        //if (value == "INSTRUMENT" || value == "RAW_DATA" || value == "SETTINGS")    //We don't add the main XML Group Tag "RAW_DATA"
                        //WriteToInstrument(1, 0, "<" + value + ">\r", current_instrument);
						new_xml += str + "\n";

                        path += value + "/";
						continue;
                    }
                    else if (value.StartsWith("/")) //Are we a closing group Tag?
                    {
                        value = value.Substring(1); //Remove the preceeding '/'
                        if (path.EndsWith(value + "/"))
                        {
                            path = path.Replace(value + "/", "");
                            //Write the group tag.
                           new_xml += str + "\n";
                        }
                    }
                }
                else if (first_end_sign > first_close_bracket)
                {
                    //Get the value from the end of the string.
                    string tag = temp.Substring(first_end_sign + 1, temp.Length - first_end_sign - 2);
                    //Check that the string starts with the same
                    if (!temp.StartsWith("<" + tag))
                        continue;
                    string value = temp.Substring(first_close_bracket + 1, last_open_bracket - first_close_bracket - 1);
					//If no control is found and "" is returned, add the original string.
					value = UpdateXML(path, tag);
					if (value == "")
						new_xml += str + "\n";
					else
					{
						//Keep the original number of TABS at the start of the string.
						int x = 0;
						while (str[x++] == '\t')
							new_xml += '\t';
						new_xml += value + "\n";
					}
                }
            }
			rtbXMLSettings.Text = new_xml;
        }
        /// <summary>
        /// We take an XML path and tag, find the associated control, we create the full XML string and populate the RichTextBox.
        /// </summary>
        /// <param name="ctrl"></param>
        /// <param name="path"></param>
        /// <param name="tag"></param>
        /// <returns>True if we have at least attempted to send the XML to the instrument.</returns>
        private string UpdateXML(string path, string tag)
        {
			Control ctrl_inner;
			int x = 0;
			//Go through all the controls and try to find the one related to the provided Path & tag.
			//foreach (Control ctrl in this.Controls)
			foreach (Control ctrl in tabControl1.TabPages["tpXMLDisplay"].Controls)
			{
				x = 0;
				do
				{
					if (ctrl.GetType() == typeof(Panel))
					{
						ctrl_inner = ((Panel)ctrl).Controls[x++];
					}
					else
						ctrl_inner = ctrl;

					if (ctrl_inner.Tag != null && ctrl_inner.Tag.ToString() == (path + tag).ToString())
					{
						if (ctrl_inner.GetType() != typeof(nudWithXML) && ctrl_inner.GetType() != typeof(comboWithXML) && ctrl_inner.GetType() != typeof(radbutWithXML))
							continue;

						string string_to_send = "";
						if (ctrl_inner.GetType() == typeof(nudWithXML))
							string_to_send = ((nudWithXML)ctrl_inner).GetXMLnoPath();
						else if (ctrl_inner.GetType() == typeof(comboWithXML))
							string_to_send = ((comboWithXML)ctrl_inner).GetXMLnoPath();
						else if (ctrl_inner.GetType() == typeof(radbutWithXML))
							string_to_send = ((radbutWithXML)ctrl_inner).GetXMLnoPath();

						//Write the XML to the instrument!!
						if (string_to_send != "")
						{
							//WriteToInstrument(1, 0, string_to_send, current_instrument);
							//rtbXMLSettings.AppendText(string_to_send);
							return string_to_send;
						}
					}
					//We keep going if we're going through the children of a Panel, otherwise, we're finished.
				} while (ctrl.GetType() == typeof(Panel) && x < ((Panel)ctrl).Controls.Count);
			}
            return "";   //We couldn't find a control that relates to this XML.
        }

        /// <summary>
        /// Load in an XML file from the PC, add it to the settings RichTextBox which in turn populates the controls.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btLoadXMLFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Title = "Select XML file";
            openFileDialog1.DefaultExt = "xml";
            openFileDialog1.Filter = "xml files (*.xml)|*.xml|hex files (*.hex)|*.hex|All files (*.*)|*.*";
            openFileDialog1.Multiselect = false;
            if (openFileDialog1.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            string file = openFileDialog1.FileName;
            lbXMLFileName.Text = openFileDialog1.SafeFileName;
            rtbXMLSettings.LoadFile(file, RichTextBoxStreamType.PlainText);

            bool xml_ok = false;
            StringBuilder str_bldr = new StringBuilder(rtbXMLSettings.Text);
            instrument_settings = new XML_Handler(str_bldr, out xml_ok);         //Extract all XML values in to the instrument_settnigs class.

            //If the XML was OK, trigger the refreshing of all controls. ELSE, request the data again!
            if (xml_ok)
                this.Invoke(new Action(() => RefreshSettingsControlsValues())); //Now refresh the dispay of the controls.
            else
                this.Invoke(new Action(() => btReloadSettings_Click(this, new EventArgs())));
        }

        private void cbRotaryType_SelectedIndexChanged(object sender, EventArgs e)
        {
            cbRotaryType.modified = true;

            //The ComboBoxes aren't straight forward in how the value displayed maps to the XML, so we set the Tag of the Control
            //Only 3 options, 0=ETher, 1=Rohmann, 2=Zetec
            if (cbRotaryType.Text == "ETher")
                cbRotaryType.value_string = "0";
            else if (cbRotaryType.Text == "Rohmann")
                cbRotaryType.value_string = "1";
            else if (cbRotaryType.Text == "Zetec")
                cbRotaryType.value_string = "2";
            cbRotaryType.full_XML = "<ROTARY_PROBE_TYPE>" + cbRotaryType.value_string + "</ROTARY_PROBE_TYPE>\r";
        }

        private void nudRPM_ValueChanged(object sender, EventArgs e)
        {
            nudRPM.modified = true;
        }

        private void cbAlarmSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbAlarmSource.Text == "Ch 1")
                cbAlarmSource.value_string = "0";
            else if (cbAlarmSource.Text == "Ch 2")
                cbAlarmSource.value_string = "1";
            else if (cbAlarmSource.Text == "Both")
                cbAlarmSource.value_string = "2";
            else
            {
                cbAlarmSource.full_XML = "";
                return;
            }
            cbAlarmSource.modified = true;
            cbAlarmSource.full_XML = "<ALARMS>\r,<ALARM>\r,<SOURCE>" + cbAlarmSource.value_string + "</SOURCE>\r,</ALARM>\r,</ALARMS>\r";
        }

        private void cbAlarmAction_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbAlarmAction.Text == "Freeze")
                cbAlarmAction.value_string = "0";
            else if (cbAlarmAction.Text == "Tone")
                cbAlarmAction.value_string = "1";
            else if (cbAlarmAction.Text == "Freeze & Tone")
                cbAlarmAction.value_string = "2";
            else if (cbAlarmAction.Text == "None")
                cbAlarmAction.value_string = "3";
            else
            {
                cbAlarmAction.full_XML = "";
                return;
            }
            cbAlarmAction.modified = true;
            cbAlarmAction.full_XML = "<ALARMS>\r,<ALARM>\r,<ACTION>" + cbAlarmAction.value_string + "</ACTION>\r,</ALARM>\r,</ALARMS>\r";
        }
        /// <summary>
        /// When the Reload Settings button is clicked we request the latest setting from the instrument.
        /// This is done by sending the USB_STATUS which will just set the data logging format which results in
        /// a new set of settings be sent.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btReloadSettings_Click(object sender, EventArgs e)
        {
            ChangeUSBInstrumentOutput();
        }
        /// <summary>
        /// If any of the radio buttons related to the Alarm change, setup their XML values accordingly.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void rbAlarm_CheckedChanged(object sender, EventArgs e)
        {
            if (((Control)sender).Name == "rbAlarmOff")
            {
                rbAlarmOff.value_string = rbAlarmOff.Checked ? "0" : "1"; //0=OFF, 1=ON
                rbAlarmOff.full_XML = "<ALARMS><ALARM><ENABLED>" + rbAlarmOff.value_string + "</ENABLED>\r</ALARMS></ALARM>";
                //When OFF is pressed, the modifed state of which ever alarm type (Box or Sector) that WAS selected is set to TRUE
                // as the radio button was turned OFF. If we send XML turning alarms OFF< we SHOULT NOT change the alarm type.
                rbAlarmBox.modified = false;
                rbAlarmSector.modified = false;
            }
            else if (((Control)sender).Name == "rbAlarmBox" && ((radbutWithXML)sender).Checked)
            {
                //rbAlarmSector.value_string = "1";   //Set rbAlarmSector to return the XML with the value of 1 = Box
                rbAlarmBox.value_string = "1";       //The rbAlarmSector Control returns the string, rbAlarmBox returns "" so the XML won't get sent twice.
                rbAlarmBox.full_XML = "<ALARMS><ALARM><TYPE>" + rbAlarmBox.value_string + "</TYPE>\r</ALARMS></ALARM>";
               // rbAlarmBox.full_XML = "";           //The rbAlarmSector Control returns the string, rbAlarmBox returns "" so the XML won't get sent twice.
                rbAlarmOff.Checked = false;
                rbAlarmSector.modified = false;
            }
            else if (((Control)sender).Name == "rbAlarmSector" && ((radbutWithXML)sender).Checked)
            {
                rbAlarmSector.value_string = "0";   //Set rbAlarmSector to return the XML with the value of 0 = Sector
                //rbAlarmBox.value_string = "";       //The rbAlarmSector Control returns the string, rbAlarmBox returns "" so the XML won't get sent twice.
                rbAlarmSector.full_XML = "<ALARMS><ALARM><TYPE>" + rbAlarmSector.value_string + "</TYPE>\r</ALARMS></ALARM>";
                //rbAlarmBox.full_XML = "";           //The rbAlarmSector Control returns the string, rbAlarmBox returns "" so the XML won't get sent twice.
                rbAlarmOff.Checked = false;
                rbAlarmBox.modified = false;
            }
        }
        /// <summary>
        /// To prevent too many commands getting sent too quickly to the instrument we only send a command on a Tick!
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timerWriteToInstrument_Tick(object sender, EventArgs e)
        {
            if (commands_to_send.Count == 0)
                return;

            Int32 char_pos = 0, max_len = 0;
            byte[] bytes = (byte[])commands_to_send[0];
            bool on_start_tri_bracket = false;

            //We need to check the NEXT command in the list. If it's the same (except the value) we use that instead.
            //That way we are sending the latest value to the instrument.
            if (rbEmbedEC.Checked || rbETI300.Checked)
            {
                for (int x=1; x<commands_to_send.Count; x++)
                {
                    /*if (commands_to_send.Count < 2)
                    {
                        bytes = (byte[])commands_to_send[0];
                    }
                    else*/
                    {
                        max_len = Math.Min(((byte[])commands_to_send[0]).Length, ((byte[])commands_to_send[x]).Length);
                        for(char_pos = 0; char_pos < max_len; char_pos++)
                        {
                            //Compare each character at a time until there is a difference. We then know if the difference was between < & >, different command.
                            // if the difference was NOT between < & >, same command but a different value!
                            if (((byte[])commands_to_send[0])[char_pos] == ((byte[])commands_to_send[x])[char_pos])
                            {
                                if (((byte[])commands_to_send[0])[char_pos] == '<')
                                    on_start_tri_bracket = true;
                                else if (((byte[])commands_to_send[0])[char_pos] == '>')
                                    on_start_tri_bracket = false;
                                continue;
                            }
                            //Difference found, jump out.
                            break;
                        }
                        if (on_start_tri_bracket)   //Command is different AFTER a start tag, so is a different command entirely
                        {
                            //bytes = (byte[])commands_to_send[0];
                            continue;
                        }
                        //2nd command is the same as the first, remove the first!
                        //commands_to_send.RemoveAt(0);

                        //2nd command is the same as the first, copy the latter to the first, then remove the latter. THen we can go through the list again!
                        commands_to_send[0] = commands_to_send[x];
                        commands_to_send.RemoveAt(x);
                        x--;        //Must now reduce X as we have just removed a command.
                    }
                }
                
            }
            bytes = (byte[])commands_to_send[0];
            commands_to_send.RemoveAt(0);

            WriteToInstrument(bytes, current_instrument);

            if (commands_to_send.Count == 0)    //No more commands, so stop the timer.
                timerWriteToInstrument.Stop();

        }

        /// <summary>
        /// The Zoom level has changed. Update the graph axis.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void nudZoom_ValueChanged(object sender, EventArgs e)
        {
            if (big_NumericUpDown_parent == nudZoom)
                tbNudValue.Text = nudZoom.Text;

            SetGraphAxisValues();
        }
        /// <summary>
        /// When selecting Project 29, it's our single channel embedded board, so only outputs 1 channel in Post or Pre processed data.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbProj29_Click(object sender, EventArgs e)
        {
            rbNonRealtime.Visible = !rbEmbedEC.Checked;
            rbSingleChan.Visible = !rbEmbedEC.Checked;
            rbNone.Visible = !rbEmbedEC.Checked;

            if (!rbEmbedEC.Checked && rbRaw.Checked == false && rbPostProcess.Checked == false)
                rbRaw.Checked = true;
        }

        private void tpPhaseplane_Resize(object sender, EventArgs e)
        {
            //Calculate the maximum size that each side of the Phase Plane chart can be.
            double max_x_size = lbSource.Left - ctPhase.Left;
            double max_y_size = Math.Min(ctTimebase.Top - ctPhase.Top, tpPhaseplane.Height*0.7);

            if (max_x_size * 0.75 > max_y_size) //Use the Y size and scale X accordingly
            {
                ctPhase.Height = (int)max_y_size;
                ctPhase.Width = (int)(max_y_size * 1.333);
            }
            else
            {
                ctPhase.Width = (int)max_x_size;
                ctPhase.Height = (int)(max_x_size * 0.75);
            }
			ctTimebase.Location = new Point(5, ctPhase.Bottom + 20);
			ctTimebase.Height = tpPhaseplane.Height - ctPhase.Height - 20;
        }
		/// <summary>
		/// The sweep step change needs to vary so it's 0.1 below 1 and 1 above 1.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        private void nudSweepTime_ValueChanged(object sender, EventArgs e)
        {
			decimal new_value = ((NumericUpDown)sender).Value;
			decimal old_value = Convert.ToDecimal(((NumericUpDown)sender).Text);
			decimal difference = new_value - old_value;
			
			//Increasing the value
			if (difference > 0)
			{
				if (new_value >= 1 && nudSweepTime.Increment <= 1.0M)
				{
					nudSweepTime.Increment = 1.0M;
					nudSweepTime.DecimalPlaces = 0;
					//((NumericUpDown)sender).Value = Math.Round(old_value) + 1.0M;
				}
				else if (new_value >= 0.1M && nudSweepTime.Increment <= 0.1M)
				{
					nudSweepTime.Increment = 0.1M;
					nudSweepTime.DecimalPlaces = 1;
					//((NumericUpDown)sender).Value = Math.Round(old_value) + 0.1M;
				}
				else if (new_value >= 0.01M && nudSweepTime.Increment <= 0.01M)
				{
					nudSweepTime.Increment = 0.01M;
					nudSweepTime.DecimalPlaces = 2;
					//((NumericUpDown)sender).Value = Math.Round(old_value) + 0.01M;
				}
			}
			//Decreasing the value
			else if (difference < 0)
			{
				if (old_value <= 1 && nudSweepTime.Increment > 0.1M)
				{
					nudSweepTime.Increment = 0.1M;
					((NumericUpDown)sender).Value = old_value - 0.1M;
					nudSweepTime.DecimalPlaces = 1;
				}
				else if (old_value <= 0.1M && nudSweepTime.Increment > 0.01M)
				{
					nudSweepTime.Increment = 0.01M;
					((NumericUpDown)sender).Value = old_value - 0.01M;
					nudSweepTime.DecimalPlaces = 2;
				}
				else if (old_value <= 0.01M && nudSweepTime.Increment > 0.001M)
				{
					nudSweepTime.Increment = 0.001M;
					((NumericUpDown)sender).Value = old_value - 0.001M;
					nudSweepTime.DecimalPlaces = 3;
				}
			}
            int max_points = (int)(nudSweepTime.Value * (8000 / (points_to_skip + 1)));
			
			if (data_transfer_state == CSCAN_ENC_TRIGGERED)
			{
				try { max_points = (int)(((NumericUpDown)sender).Value * 1000) * instrument_settings.GetControlValueFromTagAndPath("INSTRUMENT/CSCAN/CSCAN_X_TICKS_MM").integer_value; }
				catch
				{}
			}
            //Populate the value of the large NUD
            //tbNudValue.Text = ((NumericUpDown)sender).Value.ToString();
            //tbNudNameText.Text = "Sweep Time";

            //When decreasing the Persistence, points must be removed!
            if (S.Points.Count > max_points)
            {
				int x = S.Points.Count - max_points;
                while (x-- > 0)
                    S.Points.RemoveAt(0);
            }
            //We throw away half of the data before displaying, hence th"e"e 4000. (For the EmbedEC we throw away 3 quarters, but as the sample rate is 16000, still get 4000 points per second.
			ctTimebase.ChartAreas[0].AxisX.Maximum = max_points;// (double)(4000 * nudSweepTime.Value);//double)((8000 / (points_to_skip + 1)) * nudSweepTime.Value);

            if (big_NumericUpDown_parent == nudSweepTime)
                tbNudValue.Text = nudSweepTime.Text;
        }
        /// <summary>
        /// Go through all controls that are of our special type (nudWithXML, comboWithXML, radbutWithXML) and set the correct value.
        /// </summary>
        private void RefreshSettingsControlsValues()
        {
            if (instrument_settings == null)
                return;
            ControlValue ct_val = instrument_settings.GetControlValueFromTagAndPath("INSTRUMENT");
            if (ct_val == null)
                return;

			switch (ct_val.display_value)
			{
				case "RailCheck":
					connected_instrument = eInstrumentType.RAILCHECK;
					rbRailCheck.Checked = true;
					break;
				case "ETI300":
					connected_instrument = eInstrumentType.ETI300;
					rbETI300.Checked = true;
					break;
				case "EmbedEC":
					rbEmbedEC.Checked = true;
					connected_instrument = eInstrumentType.EMBEDEC;
					break;
				case "ViCTor2.2D":
					rbVictor22D.Checked = true;
					connected_instrument = eInstrumentType.VICTOR22D;
					break;
				case "AeroCheck+":
					rbAeroCheckPlus.Checked = true;
					connected_instrument = eInstrumentType.AEROCHECK_P;
					break;
				case "WeldCheck+":
					rbAeroCheckPlus.Checked = true;
					connected_instrument = eInstrumentType.WELDCHECK_P;
					break;
				case "WeldCheck3":
					rbAeroCheckPlus.Checked = true;
					connected_instrument = eInstrumentType.WELDCHECK3;
					break;
				case "AeroCheck2":
					rbAeroCheck2.Checked = true;
					connected_instrument = eInstrumentType.AEROCHECK;
					break;
				case "WeldCheck2":
					rbAeroCheck2.Checked = true;
					connected_instrument = eInstrumentType.WELDCHECK;
					break;
				default:
					MessageBox.Show("Unknown instrument or error in data. Try to reconnect.");
					break;

			}//End of Switch

			if (connected_instrument == eInstrumentType.ETI300 || connected_instrument == eInstrumentType.EMBEDEC)
			{
				panelKeypad.Visible = false;
				panelEmbedEC.Visible = true;

				if (current_EmbedEC == "")	//A new EmbedEC has been connected
				{
					current_EmbedEC = instrument_settings.GetControlValueFromTagAndPath("INSTRUMENT/INSTRUMENT_ID").display_value;

					if (current_EmbedEC == last_seen_EmbedEC)
					{
						if (MessageBox.Show("Send previous settings?", "", MessageBoxButtons.YesNo) == DialogResult.Yes)
						{
							//Send the last settings that we used for this machine!
							rtbXMLSettings.LoadFile(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\ETherRealTime" + "\\EmbedEC.xml", RichTextBoxStreamType.PlainText);

							bool xml_ok = false;
							StringBuilder str_bldr = new StringBuilder(rtbXMLSettings.Text);
							instrument_settings = new XML_Handler(str_bldr, out xml_ok);         //Extract all XML values in to the instrument_settnigs class.

							//If the XML was OK, trigger the refreshing of all controls. ELSE, request the data again!
							if (xml_ok)
							{
								this.Invoke(new Action(() => RefreshSettingsControlsValues())); //Now refresh the dispay of the controls.
								ScanAllControls(); //Take each string of XML and update the value within it and send to the instrument.
								lbXMLFileName.Invoke(new Action(() => lbXMLFileName.Text = "Live Data"));   //Change the label above the RTB so it no longer indicates a File Name.
							}
							else
								this.Invoke(new Action(() => btReloadSettings_Click(this, new EventArgs())));
						}
					}
				}
				SetDataSetValue("LAST_EMBEDEC", current_EmbedEC);
			}
			int pos = this.Text.IndexOf(" Connected");
			if (pos >= 0)
				this.Text = this.Text.Substring(0, pos);
			this.Text += " Connected to " + ct_val.display_value;

            //If an EmbedEC is connected, hide the Keypad and show the Balance, Phase and gain controls.
            btBeadSeat.Visible = btLoadVeeScanSettings.Visible = btAlarm.Visible = !(rbEmbedEC.Checked || rbETI300.Checked);
            tbTxString.Visible = panelEmbedEC.Visible = (rbEmbedEC.Checked || rbETI300.Checked);
            btAlarm2.Visible = rbVictor22D.Checked;

			//We don't draw/process every point we receive, unless in CScan triggered mode which we
            points_to_skip = (Int16)(rbEmbedEC.Checked || rbETI300.Checked ? 3 : 1);

            //Set up the correct text in the label, although it may change...
            if (rbEmbedEC.Checked || rbETI300.Checked)
            {
                ct_val = instrument_settings.GetControlValueFromTagAndPath("INSTRUMENT/SETTINGS/FREQUENCY");
                if (ct_val != null)
                {
                    nudFrequencyEmbedECOnly.multiplier = ct_val.multiplier;
                    if (ct_val.multiplier == 1)
                        lbFreqUnitsEmbedECOnly.Text = "Hz";
                    else if (ct_val.multiplier == 1000)
                        lbFreqUnitsEmbedECOnly.Text = "kHz";
                    else
                        lbFreqUnitsEmbedECOnly.Text = "MHz";
                }
            }
//            else
            {
                ct_val = instrument_settings.GetControlValueFromTagAndPath("INSTRUMENT/SETTINGS/FREQUENCY");
                if (ct_val != null)
                {
                    nudFrequencyCh1.multiplier = ct_val.multiplier;
                    if (ct_val.multiplier == 1)
                        lbFreqUnitsCh1.Text = "Hz";
                    else if (ct_val.multiplier == 1000)
                        lbFreqUnitsCh1.Text = "kHz";
                    else
                        lbFreqUnitsCh1.Text = "MHz";
                }
                ct_val = instrument_settings.GetControlValueFromTagAndPath("INSTRUMENT/SETTINGS/DUAL_FREQUENCY/FREQUENCY");
                if (ct_val != null)
                {
                    nudFrequencyCh2.multiplier = ct_val.multiplier;
                    if (ct_val.multiplier == 1)
                        lbFreqUnitsCh2.Text = "Hz";
                    else if (ct_val.multiplier == 1000)
                        lbFreqUnitsCh2.Text = "kHz";
                    else
                        lbFreqUnitsCh2.Text = "MHz";
                }
            }
            
            //Now search through our controls until we find one whose Tag matches our path and XML Tag.
            //We must set a flag to indicate that we DO NOT want the event of the controls value being changed to send the value
            // to the instrument, as the value has just come from the instrument anyway!
            do_not_send_value_to_instrument = true;

            foreach (Control ctrl in this.tpXMLDisplay.Controls)
            {
                if (ctrl.GetType() == typeof(System.Windows.Forms.GroupBox))
                {
                    foreach (Control ctrl2 in ((GroupBox)ctrl).Controls)
                    {
                        if (ctrl2.GetType() == typeof(nudWithXML) || ctrl2.GetType() == typeof(comboWithXML) || ctrl2.GetType() == typeof(radbutWithXML))
                            CheckControl(ctrl2);
                    }
                }
                else if (ctrl.GetType() == typeof(nudWithXML) || ctrl.GetType() == typeof(comboWithXML) || ctrl.GetType() == typeof(radbutWithXML))
                    CheckControl(ctrl);
            }
            foreach (Control ctrl in this.Controls)
            {
                if (ctrl.GetType() == typeof(nudWithXML) || ctrl.GetType() == typeof(comboWithXML) || ctrl.GetType() == typeof(radbutWithXML))
                    CheckControl(ctrl);
                else if (ctrl.GetType() == typeof(System.Windows.Forms.Panel))
                {
                    foreach (Control ctrl2 in ((Panel)ctrl).Controls)
                    {
                        if (ctrl2.GetType() == typeof(nudWithXML) || ctrl2.GetType() == typeof(comboWithXML) || ctrl2.GetType() == typeof(radbutWithXML))
                            CheckControl(ctrl2);
                    }
                }
            }
			foreach (Control ctrl in this.tpIO.Controls)
            {
                if (ctrl.GetType() == typeof(nudWithXML) || ctrl.GetType() == typeof(comboWithXML) || ctrl.GetType() == typeof(radbutWithXML))
                    CheckControl(ctrl);
                else if (ctrl.GetType() == typeof(System.Windows.Forms.Panel))
                {
                    foreach (Control ctrl2 in ((Panel)ctrl).Controls)
                    {
                        if (ctrl2.GetType() == typeof(nudWithXML) || ctrl2.GetType() == typeof(comboWithXML) || ctrl2.GetType() == typeof(radbutWithXML))
                            CheckControl(ctrl2);
                    }
                }
            }
            //Some special cases! IJD
            /*if (rbAlarmBox.value_string == "1")
                rbAlarmBox.Checked*/
            do_not_send_value_to_instrument = false;
        }

        /// <summary>
        /// Part of populating the controls with values from an XML file within the rich text box.
        /// Given a control, then the XML details of Value, Tag and Path (heirachy of tags) we load the
        /// value in to the control. There are various things that need to be taken in to consideration,
        /// ie any multipliers as phase of 245000 is 245 degrees.
        /// </summary>
        /// <param name="ctrl"></param>
        /// <param name="value"></param>
        /// <param name="path"></param>
        /// <param name="tag"></param>
        public void CheckControl(Control ctrl)
        {
            //string tag = ((IGetXML)ctrl).GetXMLnoPath();
            //string path = ((IGetXML)ctrl).GetXML();
            //Find a control with the correct Tag and Path.
            ControlValue ctrl_var = instrument_settings.GetControlValueFromTagAndPath(ctrl.Tag.ToString());

            if (ctrl_var == null)
                return;

            if (ctrl.GetType() == typeof(nudWithXML))
            {
                try
                {
                    ((nudWithXML)ctrl).Value = ctrl_var.decimal_display_value;
                    ((nudWithXML)ctrl).modified = false;    //This is ONLY set true when being modified by a user, not when being loaded in.
                    ((nudWithXML)ctrl).multiplier = ctrl_var.multiplier;
                }
                catch(SystemException e)
                {
					MessageBox.Show(e.Message);
                }
            }
            else if (ctrl.GetType() == typeof(comboWithXML))
            {
                if (ctrl.Name == "cbAlarmAction" || ctrl.Name == "cbAlarmSource")
                {
                    ((comboWithXML)ctrl).SelectedIndex = ctrl_var.integer_value;
                }
                else
                {
                    //Run through the items of the control and set the correct one:
                    for (int x = 0; x < ((comboWithXML)ctrl).Items.Count; x++)
                    {
                        if (((comboWithXML)ctrl).Items[x].ToString() == ctrl_var.display_value)
                            ((comboWithXML)ctrl).SelectedIndex = x;
                    }
                }
                ((comboWithXML)ctrl).modified = false;    //This is ONLY set true when being modified by a user, not when being loaded in.
            }
            else if (ctrl.GetType() == typeof(radbutWithXML))
            {
                //We have a strange issue where one tag is represented by 2 rad buttons, so need to cope with that.
                if (ctrl.Tag.ToString() == "INSTRUMENT/ALARMS/ALARM/TYPE")
                {
                    if (ctrl_var.display_value == "Box")
                    {
                        rbAlarmBox.Checked = true;
                        rbAlarmSector.Checked = false;
                    }
                    else
                    {
                        rbAlarmBox.Checked = false;
                        rbAlarmSector.Checked = true;
                    }
                }
				else if (ctrl.Tag.ToString().StartsWith("INSTRUMENT/SETTINGS/IO_PORT_CONFIG"))
				{
					if  (((radbutWithXML)ctrl).Name == "rbIOOff" && ctrl_var.display_value == "IO_OFF")
						((radbutWithXML)ctrl).Checked = true;
					else if  (((radbutWithXML)ctrl).Name == "rbEnc1" && ctrl_var.display_value == "ENCODER_1")
						((radbutWithXML)ctrl).Checked = true;
					else if  (((radbutWithXML)ctrl).Name == "rbEnc2" && ctrl_var.display_value == "ENCODER_2")
						((radbutWithXML)ctrl).Checked = true;
					else if  (((radbutWithXML)ctrl).Name == "rbEnc1Enc2" && ctrl_var.display_value == "ENCODER_1_AND_2")
						((radbutWithXML)ctrl).Checked = true;
					else if  (((radbutWithXML)ctrl).Name == "rbEnc1SPI" && ctrl_var.display_value == "ENCODER_1_SPI")
						((radbutWithXML)ctrl).Checked = true;
					else if  (((radbutWithXML)ctrl).Name == "rbEnc1DigIO" && ctrl_var.display_value == "ENCODER_1_AND_DO")
						((radbutWithXML)ctrl).Checked = true;
					else if  (((radbutWithXML)ctrl).Name == "rbDigIO" && ctrl_var.display_value == "DIGITAL_OUT")
						((radbutWithXML)ctrl).Checked = true;
					else
						((radbutWithXML)ctrl).Checked = false;

					((radbutWithXML)ctrl).modified = false;    //This is ONLY set true when being modified by a user, not when being loaded in.
				}
            }
        }
        /// <summary>
        /// Scan a previous LOG file or ETD file taken from an Instrument, past the header then display the data and then ask whether to display on the Graph.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btScanLogFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "ETherNDE data files (*.etd;*.dat;*.txt)|*.etd;*.dat;*.txt|All files (*.*)|*.*";
            if (ofd.ShowDialog() != DialogResult.OK || ofd.SafeFileNames.Length != 1)
                return;

            Int32 pos = 0; //Keep a rough indication as to how much we've processed.
            bool xml_ok = false;
            bool ether_save_data = false;   //We store whether the data is ETher RAW_DATA, ie recorded data from an instrument. This data has no status bytes etc in it.
            bool sync_pulse = false;
            StreamReader str = new StreamReader(ofd.FileName);
            String data = str.ReadToEnd();
            loaded_file_header_info = new XML_Handler(data, out xml_ok, ofd.FileName);
            int bytes_to_expect = 0;
            UInt32 data_sets = 0;
            UInt16 data_type = 0;
            String message_text = "";   //After the file is processed, we display a message to the user.

            //If we have a RAW_DATA XML tag, this is a raw data file!
            ControlValue cv = loaded_file_header_info.GetControlValueFromTagAndPath("RAW_DATA");
            if (cv != null)
            {
                ether_save_data = true;
                cv = loaded_file_header_info.GetControlValueFromTagAndPath("RAW_DATA/FREQUENCIES");

                if (cv == null)
                {
                    //AeroChecks (NOT the PLUS) may just have a FREQUENCY tag, so check for that!
                    cv = loaded_file_header_info.GetControlValueFromTagAndPath("RAW_DATA/FREQUENCY");

                    if (cv == null)
                    {
						cv = loaded_file_header_info.GetControlValueFromTagAndPath("RAW_DATA/CHANNELS");
						if (cv == null)
						{
							MessageBox.Show("File Error, tag FREQUENCIES or FREQUENCY not found." + Environment.NewLine);
							str.Close();
							return;
						}
                    }
                    else //Assume a single frequency
                        cv.integer_value = 1;
                }
                if (cv.integer_value == 2)  //Meaning it was a dual frequency recording!
                {
                    data_type = DUAL_CHANNEL_AERO_FROM_ETD_FILE;
                    bytes_to_expect = 16;
                }
                else if (cv.integer_value == 1)  //Meaning it was a dual frequency recording!
                {
                    data_type = SINGLE_CHANNEL_WELD_AERO_FROM_ETD_FILE;
                    bytes_to_expect = 8;
                }
            }
            pos = data.IndexOf((char)0);  //Find the first NULL, the start of the data!

            str.Close();

            byte[] raw_data = File.ReadAllBytes(ofd.FileName);

            try
            {
                if (!ether_save_data)   //Data is NOT data that has been recorded by and on an instrument.
                {
                    pos++;

                    cv = loaded_file_header_info.GetControlValueFromTagAndPath("INSTRUMENT/SETTINGS/USB_OUTPUT");
                    if (cv == null)
                    {
                        MessageBox.Show("File Error, Data Format not found"+Environment.NewLine);
                        return;
                    }
                    //The value from this tag is from the eNum within the instrument:
                    //eNO_USB_OUTPUT, eFILE_SIZE, eFILE_DATA, eFILE_NAME, eREALTIME_RAW, eREALTIME_POSTPROCESS, eXML_HEADER, eSINGLE_CHAN_POST, eNON_REALTIME, eCONDUCTIVITY_USB, NUMBER_OF_USB_OUTPUTS
                    //It should only be:
                    // 0 - eNO_USB_OUTPUT
                    // 4 - eREALTIME_RAW
                    // 5 - eREALTIME_POSTPROCESS
                    // 7 - eSINGLE_CHAN_POST
                    // 8 - eNON_REALTIME
                    // 9 - eCONDUCTIVITY_USB
                    switch (cv.integer_value)
                    {
                        case 4:
                            data_type = REALTIME_DATA_RAW;
                            bytes_to_expect = 16;
                            pos--;
                            ether_save_data = true; //Set to TRUE for Realtime as it is read in the same, hence the RAW!
                            break;
                        case 5:
                            data_type = REALTIME_DATA_POSTPROCESS;
                            bytes_to_expect = 24;
                            break;
                        case 7:
                            data_type = SINGLE_CHAN_POST;
                            bytes_to_expect = 16;
                            break;
                        case 8:
                            data_type = NON_REALTIME;
                            bytes_to_expect = 24;
                            break;
                        case 9:
                            data_type = CONDUCTIVITY;
                            bytes_to_expect = 16;
                            break;
                        case 11:
                            data_type = CSCAN_ENC_TRIGGERED;
                            bytes_to_expect = 32;   //X, Y, X1, Y1, Xmix, Ymix, Counter, Encoder
                            break;
                        default:
                            return;
                    }
                }
                if (ether_save_data) //From the file length and the number of bytes per reading, we calculate the number of Data Sets:
                {
                    data_sets = (UInt32)((raw_data.Length - (pos + 1)) / bytes_to_expect);
                    pos++;  //Skips the NULL in the file.
                }
                else
                {
                    //Now we count the number of Carriage Returns to get the number of Data Sets in the file:
                    for (Int64 x = pos; x < raw_data.Length; x++)
                    {
                        if (raw_data[x] == '\n')
                            data_sets++;
                    }
                }
                //Now we have the status byte, we know how many "sets" of 4 bytes (U32s) to extract.
                switch (data_type)
                {
                    case SINGLE_CHANNEL_WELD_AERO_FROM_ETD_FILE:
                        message_text = "ETD file loaded with 1 Channel of data.\n";
                        //How many data sets are in the file?
                        FileData = new GraphData[3];
                        FileData[0] = new GraphData("X - Chan 1", data_sets, 4, SINGLE_CHANNEL_WELD_AERO_FROM_ETD_FILE);
                        FileData[1] = new GraphData("Y - Chan 1", data_sets, 4, SINGLE_CHANNEL_WELD_AERO_FROM_ETD_FILE);
                        FileData[2] = new GraphData("Status - File", data_sets, 2, STATUS);
                        break;
                    
                    case REALTIME_DATA_POSTPROCESS:
                        message_text = "Recorded Data values loaded with 3 Channels of Post-Processed data.\n";
                        //How many data sets are in the file?
                        FileData = new GraphData[7];
                        FileData[0] = new GraphData("X - Chan 1", data_sets, 4, REALTIME_DATA_POSTPROCESS);
                        FileData[1] = new GraphData("Y - Chan 1", data_sets, 4, REALTIME_DATA_POSTPROCESS);
                        FileData[2] = new GraphData("X - Chan 2", data_sets, 4, REALTIME_DATA_POSTPROCESS);
                        FileData[3] = new GraphData("Y - Chan 2", data_sets, 4, REALTIME_DATA_POSTPROCESS);
                        FileData[4] = new GraphData("X - Chan Mix", data_sets, 4, REALTIME_DATA_POSTPROCESS);
                        FileData[5] = new GraphData("Y - Chan Mix", data_sets, 4, REALTIME_DATA_POSTPROCESS);
                        FileData[6] = new GraphData("Status - File", data_sets, 2, STATUS);
                        break;
                    case CONDUCTIVITY:             // 0x90, 16 bytes, 4 for CONDUCTIVITY, 4 for thickness, 4 for angle, 4 for vector.
                        message_text = "Recorded Conductivity data loaded.\n";
                        //How many data sets are in the file?
                        FileData = new GraphData[4];
                        FileData[0] = new GraphData("Conductivity", data_sets, 4, CONDUCTIVITY);
                        FileData[1] = new GraphData("Lift-Off", data_sets, 4, CONDUCTIVITY);
                        FileData[2] = new GraphData("Angle", data_sets, 4, CONDUCTIVITY);
                        FileData[3] = new GraphData("Vector", data_sets, 4, CONDUCTIVITY);
                        break;
                    case REALTIME_DATA_RAW:        //  0x40, 16 bytes, 4 for X1, 4 for Y1, 4 for X2, 4 for Y2
                        message_text = "Recorded Raw Data values loaded with 2 Channels of data.\n";
                        FileData = new GraphData[5];
                        FileData[0] = new GraphData("X - Chan 1", data_sets, 4, REALTIME_DATA_RAW);
                        FileData[1] = new GraphData("Y - Chan 1", data_sets, 4, REALTIME_DATA_RAW);
                        FileData[2] = new GraphData("X - Chan 2", data_sets, 4, REALTIME_DATA_RAW);
                        FileData[3] = new GraphData("Y - Chan 2", data_sets, 4, REALTIME_DATA_RAW);
                        FileData[4] = new GraphData("Status - File", data_sets, 2, STATUS);
                        break;
                    case DUAL_CHANNEL_AERO_FROM_ETD_FILE:
                        message_text = "ETD file loaded with 2 Channels of data.\n";
                        //How many data sets are in the file?
                        FileData = new GraphData[5];
                        FileData[0] = new GraphData("X - Chan 1", data_sets, 4, REALTIME_DATA_RAW);
                        FileData[1] = new GraphData("Y - Chan 1", data_sets, 4, REALTIME_DATA_RAW);
                        FileData[2] = new GraphData("X - Chan 2", data_sets, 4, REALTIME_DATA_RAW);
                        FileData[3] = new GraphData("Y - Chan 2", data_sets, 4, REALTIME_DATA_RAW);
                        FileData[4] = new GraphData("Status - File", data_sets, 2, STATUS);
                        break;
                    case SINGLE_CHAN_POST:         // 0x70, 16 bytes, 4 for X, 4 for Y, 4 for counter, 4 for Encoder.
                        message_text = "Recorded Screen Coordinate, processed Data values loaded with 1 Channel of data and Counter and Encoder data.\n";
                        //How many data sets are in the file?
                        FileData = new GraphData[5];
                        FileData[0] = new GraphData("X - Chan 1", data_sets, 4, SINGLE_CHAN_POST);
                        FileData[1] = new GraphData("Y - Chan 1", data_sets, 4, SINGLE_CHAN_POST);
                        FileData[2] = new GraphData("Counter - File", data_sets, 4, COUNTER);
                        FileData[3] = new GraphData("Encoder - File", data_sets, 4, ENCODER);
                        FileData[4] = new GraphData("Status - File", data_sets, 2, STATUS);
                        break;
                    case NON_REALTIME:             // 0x80, 24 bytes, 4 for X1, 4 for Y1, 4 for X2, 4 for Y2, 2 for each of: Theta, Radius, X percent, Y percent.
                        message_text = "Recorded Non-Realtime data loaded with 2 Channels of data and Theta, Radius, X%, Y% data.\n";
                        //How many data sets are in the file?
                        FileData = new GraphData[9];
                        FileData[0] = new GraphData("X - Chan 1", data_sets, 4, NON_REALTIME);
                        FileData[1] = new GraphData("Y - Chan 1", data_sets, 4, NON_REALTIME);
                        FileData[2] = new GraphData("X - Chan 2", data_sets, 4, NON_REALTIME);
                        FileData[3] = new GraphData("Y - Chan 2", data_sets, 4, NON_REALTIME);
                        FileData[4] = new GraphData("Theta", data_sets, 2, NON_REALTIME);
                        FileData[5] = new GraphData("Radius", data_sets, 2, NON_REALTIME);
                        FileData[6] = new GraphData("X Percent", data_sets, 2, NON_REALTIME);
                        FileData[7] = new GraphData("Y Percent", data_sets, 2, NON_REALTIME);
                        FileData[8] = new GraphData("Status - File", data_sets, 2, STATUS);
                        break;
                    case CSCAN_ENC_TRIGGERED:             // 0xB0, 32 bytes, 4 for X1, 4 for Y1, 4 for X2, 4 for Y2, 4 for Xmix, 4 for Ymix, 4 for Counter, 4 for Encoder.
                        message_text = "Recorded C-Scan data. All 3 channels and 2 positional channels\n";
                        //How many data sets are in the file?
                        FileData = new GraphData[9];
                        FileData[0] = new GraphData("X - Chan 1", data_sets, 4, NON_REALTIME);
                        FileData[1] = new GraphData("Y - Chan 1", data_sets, 4, NON_REALTIME);
                        FileData[2] = new GraphData("X - Chan 2", data_sets, 4, NON_REALTIME);
                        FileData[3] = new GraphData("Y - Chan 2", data_sets, 4, NON_REALTIME);
                        FileData[4] = new GraphData("X - Chan Mix", data_sets, 4, NON_REALTIME);
                        FileData[5] = new GraphData("Y - Chan Mix", data_sets, 4, NON_REALTIME);
                        FileData[6] = new GraphData("Counter - File", data_sets, 4, COUNTER);
                        FileData[7] = new GraphData("Encoder - File", data_sets, 4, ENCODER);
						//if (ether_save_data)	//Only with real instrument data do we have the status byte
							FileData[8] = new GraphData("Status - File", data_sets, 2, STATUS);
                        break;
                    default:
                        MessageBox.Show("Unknown Data format.");
                        return;
                }
				//We store the first point as a balance value for realtime
				bool first_point = true;
				int bal_x = 0, bal_y = 0;
				//Only balance this type. Adding in for wagner to view his files 29/7/2022
					if (data_type != SINGLE_CHANNEL_WELD_AERO_FROM_ETD_FILE)
						first_point = false;
                do
                {
                    if (FileData == null)
                        return;

					

                    //Loop through each of the expected data fields in each packet
                    foreach (GraphData gd in FileData)
                    {
                        Int32 value = 0;
						UInt32 tmp_u32 = 0;
                        if (!ether_save_data)  //Data was recorded on a PC, probably by ETherRealtime and is a CSV file
                        {
                            StringBuilder val = new StringBuilder();
                            while (raw_data[pos] != ',' && raw_data[pos] != '\n')
                            {
                                val.Append((char)raw_data[pos++]);
                            }
                            value = Convert.ToInt32(val.ToString());
                            if (data_type == REALTIME_DATA_POSTPROCESS && gd.Description().StartsWith("Y"))
                                value = -value;
                            else if ((data_type == SINGLE_CHAN_POST || data_type == CSCAN_ENC_TRIGGERED) && gd.Description().StartsWith("Y"))
                                value = -value + 480;
                            pos++;  //Skip past the ',' or New Line
                        }
                        else //ether_save_data is a file recorded on and by an instrument.
                        {
                            if (gd.data_type == STATUS) //The STATUS channel isn't read from the etd file, we create it from the status bit (LSb) of the Checksum byte (LSB).
                            {
                                if (sync_pulse)
                                    value = 1;
                                sync_pulse = false;
                            }
                            else
                            {
                                if ((raw_data[pos] & 0x01) == 1)    //Status bit for Sync Pulse!
                                {
                                    sync_pulse = true;
                                }
								

                                for (UInt16 x = 0; x < gd.ByteSize(); x++)
                                    tmp_u32 += (raw_data[pos + x] * (UInt32)Math.Pow(256, x));
                                //Now we've converted to signed, remove the least sig 8 bits
                                //value = ConvertUIntToInt((UInt32)value) / 256;
								value = ((Int32)tmp_u32)/256;

								if (gd.Description().StartsWith("X - Chan"))
								{
									if (first_point)
										bal_x = value;
									//balance the x value
									value -= bal_x;
								}
								else if (gd.Description().StartsWith("Y - Chan"))
								{
									value = -value;
									if (first_point)
									{
										bal_y = value;
										first_point = false;	//Clear flag now we have a balance value.
									}
									//balance the y value
									value -= bal_y;
								}
                                pos += gd.ByteSize();
                            }
                        }
                        //Add the point to the Objects array. If this fails, it's full, so break.
                        if (!gd.AddPoint(value))
                            break;
                    }
                } while (pos < raw_data.Length);

				PopulateSourceComboBoxes();
            }
            catch (SystemException ex)
            {
                tbConsoleFileScan.AppendText(ex.Message);
            }

            message_text += FileData[0].data.Length.ToString() + " points per Channel.\n" + FileData.Length.ToString() + " Channels of data:\n";

            foreach (GraphData gd in FileData)
            {
                message_text += "  " + gd.Description() + "\n";
            }
            tabControl1.SelectedTab = tabControl1.TabPages["tpPhaseplane"];

            //Now we'll set the Combo Boxes of Source data of the Strip Charts to the X & Y data just loaded in.
            int y = 0;
            foreach (string src in cbSource.Items)
            {
                if (src == "X - Chan 1")
                {
                    cbSource.SelectedIndex = y;
                    break;
                }
                y++;
            }
            y = 0;
            foreach (string src in cbSource2.Items)
            {
                if (src == "Y - Chan 1")
                {
                    cbSource2.SelectedIndex = y;
                    //Trigger an axis change of the Timebase which will then draw the Phaseplane :-) Lovely Jubley
                    ctTimebase_AxisViewChanged(this, new ViewEventArgs(ctTimebase.ChartAreas[0].AxisX, 0));
                    break;
                }
                y++;
            }
			//Display which file we're viewing
			tbCurrentFile.Text = ofd.FileName;
        }

		void PopulateSourceComboBoxes()
		{
			cbSource.Items.Clear();
			cbSource2.Items.Clear();

			previous_source1 = "";
			previous_source2 = "";

			//First, add the stuff that will always be there:
			if (serialPortUSB.IsOpen || (cbUseDLL.Checked && EtherObj != null))
			{
				cbSource.Items.Add("Ch1-X");
				cbSource.Items.Add("Ch1-Y");
				cbSource2.Items.Add("Off");
				cbSource2.Items.Add("Ch1-X");
				cbSource2.Items.Add("Ch1-Y");

				if (rbPostProcess.Checked)  //Post Porcess has been selected
				{
					cbSource.Items.Add("Ch2-X");
					cbSource.Items.Add("Ch2-Y");
					cbSource.Items.Add("Mix-X");
					cbSource.Items.Add("Mix-Y");
					cbSource.Items.Add("Status");
					cbSource2.Items.Add("Ch2-X");
					cbSource2.Items.Add("Ch2-Y");
					cbSource2.Items.Add("Mix-X");
					cbSource2.Items.Add("Mix-Y");
					cbSource2.Items.Add("Status");
				}
				else if (rbRaw.Checked)  //Post Process has been selected
				{
					cbSource.Items.Add("Ch2-X");
					cbSource.Items.Add("Ch2-Y");
					cbSource2.Items.Add("Ch2-X");
					cbSource2.Items.Add("Ch2-Y");
				}
				else if (rbNone.Checked)
				{
					cbSource.Items.Clear();
					cbSource2.Items.Clear();
				}
				else if (rbSingleChan.Checked)
				{
					cbSource.Items.Add("Encoder");
					cbSource.Items.Add("Counter");
					cbSource.Items.Add("Status");
					cbSource2.Items.Add("Encoder");
					cbSource2.Items.Add("Counter");
					cbSource2.Items.Add("Status");
				}
				else if (rbNonRealtime.Checked)
				{
					cbSource.Items.Add("Ch2-X");
					cbSource.Items.Add("Ch2-Y");
					cbSource.Items.Add("Theta");
					cbSource.Items.Add("Radius");
					cbSource.Items.Add("X%");
					cbSource.Items.Add("Y%");
					cbSource.Items.Add("Status");

					cbSource2.Items.Add("Ch2-X");
					cbSource2.Items.Add("Ch2-Y");
					cbSource2.Items.Add("Theta");
					cbSource2.Items.Add("Radius");
					cbSource2.Items.Add("X%");
					cbSource2.Items.Add("Y%");
					cbSource2.Items.Add("Status");
				}
			}//End of if we're connected to an instrument

			//Now add these new GraphDatas to the combo boxes:
			if (FileData != null)
			{
				foreach (GraphData gd in FileData)
				{
					cbSource.Items.Add(gd.Description());
					cbSource2.Items.Add(gd.Description());
				}
			}
		}
        /// <summary>
        /// The source data for the graphs has changed. There are usually Realtime, Realtime Status data, then any other loaded in data files.
        /// Configure the graph sizes etc.
        /// The Tags of the graph axis are set to the index in to the FileData[] array if NOT realtime.
        /// If realtime data, the tags are set as follows:
        /// "Ch1-X"         -1
        /// "Ch1-Y"         -2
        /// "Ch2-X"         -3
        /// "Ch2-Y"         -4
        /// "Mix-X"         -5
        /// "Mix-Y"         -6
        /// "Encoder"       -7
        /// "Counter"       -8
        /// "Status"        -9
        /// "Theta"         -10
        /// "Radius"        -11
        /// "X%"
        /// "Y%"
        /// "Conductivity"  -12
        /// "Lift-Off"      -13
        /// Off            -99
        /// 
        /// Off is valid for Realtime & NON-Realtime, but only for Channel 2.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbSource_SelectedIndexChanged(object sender, EventArgs e)
        {
			ProcessGraphs();
		}
		private void cbSource2_SelectedIndexChanged(object sender, EventArgs e)
        {
			ProcessGraphs();
		}
		private void ProcessGraphs()
		{
            int x = 0;
			int last_encoder_value = 0;	//We remember the last encoder value so we can remove reverse readings.
            string selection_text;
            Series temp_series;
			int[] source_channel = new int[2]{-1, -1};	//Which channel is each Combo Box looking at? -1 is NOWT!
            timerPhasePlane.Enabled = false;    //Turn it off here, turn it on below if Realtime data is detected!
            //Loop through the 2 Source Combo-Boxes
            for (x = 0; x < 2; x++)
            {
                if (x == 0)
                {
					/*if (previous_source1 == cbSource.SelectedItem.ToString())
					{
						//If nothign has changed, BUT we are a valid setting, start the timer
						if (previous_source1 != "" && previous_source1 != "Off")
							timerPhasePlane.Enabled = true;
						continue;
					}*/
                    selection_text = previous_source1 = cbSource.SelectedItem.ToString();
                    temp_series = S;
                }
                else
                {
					/*if (previous_source2 == cbSource2.SelectedItem.ToString())
					{
						//If nothign has changed, BUT we are a valid setting, start the timer
						if (previous_source2 != "" && previous_source2 != "Off")
							timerPhasePlane.Enabled = true;
						continue;
					}*/
                    temp_series = S2;
                    if (cbSource2.SelectedItem == null)
                    {
                        temp_series.Tag = -1;   //Give it some value!
                        continue;
                    }
                    selection_text = previous_source2 = cbSource2.SelectedItem.ToString();
                }

                switch (selection_text)
                {
                    case "Ch1-X":
                        temp_series.Tag = -1;
						source_channel[x] = 1;	//Viewing Channel 1
                        break;
                    case "Ch1-Y":
                        temp_series.Tag = -2;
						source_channel[x] = 1;	//Viewing Channel 1
                        break;
                    case "Ch2-X":
                        temp_series.Tag = -3;
						source_channel[x] = 2;	//Viewing Channel 2
                        break;
                    case "Ch2-Y":
                        temp_series.Tag = -4;
						source_channel[x] = 2;	//Viewing Channel 1
                        break;
                    case "Mix-X":
                        temp_series.Tag = -5;
						source_channel[x] = 3;	//Viewing Mix Channel (3)
                        break;
                    case "Mix-Y":
                        temp_series.Tag = -6;
						source_channel[x] = 3;	//Viewing Mix Channel (3)
                        break;
                    case "Encoder":
                        temp_series.Tag = -7;
                        break;
                    case "Counter":
                        temp_series.Tag = -8;
                        break;
                    case "Status":
                        temp_series.Tag = -9;
                        break;
                    /*case "Status 2":    //Not currently used, but there status info in the data coming back from the instrument
                        temp_series.Tag = -6;
                        break;*/
                    case "Theta":
                    case "Angle":
                        temp_series.Tag = -10;
                        break;
                    case "Vector":
                    case "Radius":
                        temp_series.Tag = -11;
                        break;
                    case "Conductivity":
                        temp_series.Tag = -12;
                        break;
                    case "Lift-Off":
                        temp_series.Tag = -13;
                        break;
                    case "Off":
                        temp_series.Tag = -99;
                        for (Int32 ind = temp_series.Points.Count - 1; ind >= 0; ind--)
							temp_series.Points.RemoveAt(ind);	//Remove from the end.
                        break;
                    default:
                        if (FileData == null)
                            return;
                        int y;
                        //Find the index of the chosen data.
                        for (y = 0; y < FileData.Length; y++)
                        {
                            if (FileData[y].Description() == selection_text) //Both cbSource and cbSource2 have the same contents and in the same order...
                                break;
                        }
                        //Was the data found?
                        if (y == FileData.Length)
                        {
                            MessageBox.Show("Graph Data not found.");
                            return;
                        }
                        //If the selection hasn't changed and the number of points hasn't changed, lets leave it alone!
                        if (temp_series.Points.Count == FileData[y].data.Length && (int)temp_series.Tag == y && !reverse_refresh)
                            continue;

                        temp_series.Tag = y;
                        //temp_series.Points.Clear();	//Took 4mins on 597,000 points
						//Looping round took about a second!!!!!
						//for (Int32 ind = temp_series.Points.Count - 1; ind >= 0; ind--)
						//	temp_series.Points.RemoveAt(ind);	//Remove from the end.

						bool encoder_reverse_checked = false;
						if (cbRemoveReverse.Checked && FileData.Length > 5 && FileData[5].Description() == "Encoder1 - File")
						{
							encoder_reverse_checked = true;
							last_encoder_value = FileData[5].data[0] - 1;	//Ensures we read the first encoder value.
						}
						int j = 0;
						double val = 0;
						// We need to add all the data, so loop through it
						for (; j < FileData[y].data.Length; j++)
						{
							val = FileData[y].data[j];
							//If we had encoder data and we want to ignore repeat data (from goinf back and forth) skip data
							if (encoder_reverse_checked)
							{
								if (last_encoder_value >= FileData[5].data[j])
									continue;
								last_encoder_value = FileData[5].data[j];
							}
							//Change as many of the existing points as possible, then Add the rest, OR Remove the extra existing
							if (j < temp_series.Points.Count)	//Then there is space to simply change the existing data
							{
								temp_series.Points[j].XValue = 0;//(0, FileData[y].data[j]);
								temp_series.Points[j].YValues[0] = val;
							}
							else
							{
								temp_series.Points.AddXY(0, val);
							}
						}
						//If j is still less than the number of points in the graph, delete the rest
						//Deleting from the middle is TOO slow.
						//Deleting from the end..
						int z = temp_series.Points.Count;
						while (j < z--)
							temp_series.Points.RemoveAt(z);	//Remove from the end.
						break;
                }//End of Switch

				//THis is LIVE data, so start the timer.
				if ((int)temp_series.Tag < 0)
				{
					timerPhasePlane.Enabled = true;
				}
                //The Axis are always tied to what the 1st Source Combo Box is set to
                if (x == 0)
                {
                    ctTimebase.ChartAreas[0].AxisY.Tag = S.Tag;
                    ctTimebase.ChartAreas[0].AxisX.Tag = S.Tag;
                }

                if (ctTimebase.Series.Count < 2)
                {
                    ctTimebase.Series.Add(S2);
                    //  ctPhase.Series.Add(Spp2); This is commented out as it was simply adding an identical Series of Data to the PhasePlane.
                }

                //If realtime data is being displayed, enable the timer.
                if ((int)temp_series.Tag < 0 && (int)temp_series.Tag != -99)
                    timerPhasePlane.Enabled = true;

            }//End of Loop for the cbSource's

            //Check to see of the Phaseplane should be visible!
            if ((int)S.Tag == -1 || (int)S.Tag == -2)  //Series 1 on the Timebase is looking at Channel 1
                Spp.Tag = -1;
            else if ((int)S.Tag == -3 || (int)S.Tag == -4)  //Series 1 on the Timebase is looking at Channel 1
                Spp.Tag = -3;
            else if ((int)S.Tag == -5 || (int)S.Tag == -6)  //Series 1 on the Timebase is looking at Channel 1
                Spp.Tag = -5;

            //Check to see if the Phaseplane should be visible!
			if ((source_channel[0] != source_channel[1]) && source_channel[0] > 0 && source_channel[1] > 0) //There is more than 1 channel visible on the PhasePlane
			{
				if ((int)S2.Tag == -1 || (int)S2.Tag == -2)  //Series 1 on the Timebase is looking at Channel 1
					Spp2.Tag = -1;
				else if ((int)S2.Tag == -3 || (int)S2.Tag == -4)  //Series 1 on the Timebase is looking at Channel 1
					Spp2.Tag = -3;
				else if ((int)S2.Tag == -5 || (int)S2.Tag == -6)  //Series 1 on the Timebase is looking at Channel 1
					Spp2.Tag = -5;
			}
			else
				Spp2.Tag = -99;

            //Configure the axis Min and Max values:
            SetGraphAxisValues();
        }
 
        /// <summary>
        /// Each axis of the graphs has its TAG set to the indexer of the FileData[] array of GraphData.
        /// The Min & Max values of each axis can therefore be configured with the Zoom level also taken in to account.
        /// </summary>
        private void SetGraphAxisValues()
        {
            //Check for real-time data first. If one axis is set to Realtime, they all will be:
            if ((int)ctTimebase.ChartAreas[0].AxisX.Tag < 0)
            {
				ctTimebase.ChartAreas[0].AxisX.CustomLabels.Clear();
				ctTimebase.ChartAreas[0].AxisX.Interval = 0.0;

				if (data_transfer_state == CSCAN_ENC_TRIGGERED)
				{
					double ticks = 1;
					try
					{
						ControlValue ctv = instrument_settings.GetControlValueFromTagAndPath("INSTRUMENT/CSCAN/CSCAN_X_TICKS_MM");
						ticks = (double)ctv.integer_value / 1000;
					}
					catch
					{
					}
					//We throw away half of the data before displaying, hence the 4000. (For the EmbedEC we throw away 3 quarters, but as the sample rate is 16000, still get 4000 points per second.

					ctTimebase.ChartAreas[0].AxisX.Maximum = (double)(nudSweepTime.Value * 1000000) / ticks;// *1000 converting mm to km
				}
				else
					ctTimebase.ChartAreas[0].AxisX.Maximum = (double)((8000 / (points_to_skip + 1)) * nudSweepTime.Value);// (double)((8000 / (points_to_skip + 1)) * nudSweepTime.Value);
				
                ctPhase.ChartAreas[0].AxisY.Minimum = ctTimebase.ChartAreas[0].AxisY.Minimum = -240 * (double)nudZoom.Value;
                ctPhase.ChartAreas[0].AxisY.Maximum = ctTimebase.ChartAreas[0].AxisY.Maximum = 240 * (double)nudZoom.Value;
                ctPhase.ChartAreas[0].AxisX.Minimum = -320 * (double)nudZoom.Value;
                ctPhase.ChartAreas[0].AxisX.Maximum = 320 * (double)nudZoom.Value;

                if (rbSingleChan.Checked)   //Need to Offset the Min and Max by the centre of the instruments screen.
                {
                    ctTimebase.ChartAreas[0].AxisY.Minimum = ctPhase.ChartAreas[0].AxisY.Minimum = 240 - (240 * (double)nudZoom.Value);
                    ctTimebase.ChartAreas[0].AxisY.Maximum = ctPhase.ChartAreas[0].AxisY.Maximum = 240 + (240 * (double)nudZoom.Value);

                    ctPhase.ChartAreas[0].AxisX.Minimum = 320 - (320 * (double)nudZoom.Value);
                    ctPhase.ChartAreas[0].AxisX.Maximum = 320 + (320 * (double)nudZoom.Value);
                }

                if ((int)ctTimebase.ChartAreas[0].AxisX.Tag > -7)  //Realtime XY data so we can leave
                {
                    return;
                }
                else if ((int)ctTimebase.ChartAreas[0].AxisX.Tag == -7) //Encoder
                {
                    ctTimebase.ChartAreas[0].AxisY.Minimum = 0;
                    ctTimebase.ChartAreas[0].AxisY.Maximum = 100 * (double)nudZoom.Value;
                    return;
                }
                else if ((int)ctTimebase.ChartAreas[0].AxisX.Tag == -8) //Counter
                {
                    ctTimebase.ChartAreas[0].AxisY.Minimum = 0;
                    ctTimebase.ChartAreas[0].AxisY.Maximum = 1000 * (double)nudZoom.Value;
                    return;
                }
                else if ((int)ctTimebase.ChartAreas[0].AxisX.Tag == -9) //Status
                {
                    ctTimebase.ChartAreas[0].AxisY.Minimum = 0;
                    ctTimebase.ChartAreas[0].AxisY.Maximum = 5 * (double)nudZoom.Value;
                    return;
                }
                else if ((int)ctTimebase.ChartAreas[0].AxisX.Tag == -10) //Angel/Theta
                {
                    ctTimebase.ChartAreas[0].AxisY.Minimum = 0;
                    ctTimebase.ChartAreas[0].AxisY.Maximum = 360 * (double)nudZoom.Value;
                    return;
                }
                else if ((int)ctTimebase.ChartAreas[0].AxisX.Tag == -11) //Radius
                {
                    ctTimebase.ChartAreas[0].AxisY.Minimum = 0;
                    ctTimebase.ChartAreas[0].AxisY.Maximum = 640 * (double)nudZoom.Value;
                    return;
                }
                else if ((int)ctTimebase.ChartAreas[0].AxisX.Tag == -12) //Conductivity
                {
                    ctTimebase.ChartAreas[0].AxisY.Minimum = 0;
                    ctTimebase.ChartAreas[0].AxisY.Maximum = 110 * (double)nudZoom.Value;
                    return;
                }
                else if ((int)ctTimebase.ChartAreas[0].AxisX.Tag == -13) //Lift-Off
                {
                    ctTimebase.ChartAreas[0].AxisY.Minimum = 0;
                    ctTimebase.ChartAreas[0].AxisY.Maximum = 1 * (double)nudZoom.Value;
                    return;
                }
            }//End of Real-time stuff

            //If file data is present set the size of the Timebase X azis so it will fit all points, no Zoom.
            ctTimebase.ChartAreas[0].AxisX.Maximum = FileData[(int)ctTimebase.ChartAreas[0].AxisX.Tag].data.Length;

            //Now for all other axis! Loop through 4 axis (2 on each graph). Starting with Timebase Y axis
            Axis current_axis = ctTimebase.ChartAreas[0].AxisY;
            for (int x = 0; x < 3; x++)
            {
				if (x == 1)
				{
					if (cbLockAxis.Checked)	//Don't fiddle with the axis of the PhasePlane if the checkbox is checked!
						break;
					current_axis = ctPhase.ChartAreas[0].AxisX;
				}
                if (x == 2)
                    current_axis = ctPhase.ChartAreas[0].AxisY;

                //PhasePlane axis can be -1 (ch1 Realtime) until the Timebase is Zoomed in to, when it's true axis can be configured.
                if ((int)current_axis.Tag == -1)
                    continue;

                //Get the type of data the axis is related too:
                UInt16 graph_data_type = (UInt16)FileData[(int)current_axis.Tag].data_type;
                if (graph_data_type == REALTIME_DATA_RAW || graph_data_type == SINGLE_CHANNEL_WELD_AERO_FROM_ETD_FILE || graph_data_type == DUAL_CHANNEL_AERO_FROM_ETD_FILE || graph_data_type == STATUS || graph_data_type == COUNTER || graph_data_type == ENCODER)
                {
					//Get the min and max vallues for the axis from what is chosen by cbSource OR the min & max of both if check box checked.
					int max = int.MinValue;
					int min = int.MaxValue;
					foreach (GraphData gd in FileData)
					{
						if (gd.Description() == cbSource.Text && cbFitSC1onAxis.Checked)
						{
							max = Math.Max(max, gd.max);
							min = Math.Min(min, gd.min);
						}
						else if (gd.Description() == cbSource2.Text && cbFitSC2onAxis.Checked)
						{
							max = Math.Max(max, gd.max);
							min = Math.Min(min, gd.min);
						}
					}
					//If we found no min & max values, set something nice
					if (min == int.MaxValue)
					{
						min = -100;
						max = 100;
					}
					Int64 range = max - min;
					Int64 mid_point = (max + min)/2;

					//Testing the zoom!
					range = (Int64)(range * nudZoom.Value);
					current_axis.Maximum = mid_point + (range / 2);
					current_axis.Minimum = mid_point - (range / 2);

                 //   current_axis.Maximum = FileData[(int)current_axis.Tag].max;
                 //   current_axis.Minimum = FileData[(int)current_axis.Tag].min;
                }
                else if (graph_data_type == REALTIME_DATA_POSTPROCESS)
                {
                    //if (nudZoom.Value > 1)
                    {
                        if (FileData[(int)current_axis.Tag].Description().StartsWith("X - "))
                        {
                            current_axis.Maximum = 320 * (double)nudZoom.Value;
                            current_axis.Minimum = -320 * (double)nudZoom.Value;
                        }
                        else
                        {
                            current_axis.Maximum = 240 * (double)nudZoom.Value;
                            current_axis.Minimum = -240 * (double)nudZoom.Value;
                        }
                    }
                }
                else// if (graph_data_type == SINGLE_CHAN_POST)   //Data is just slightly OFFSET
                {
                    if (FileData[(int)current_axis.Tag].Description().StartsWith("X - "))
                    {
                        current_axis.Maximum = 320 + (320 * (double)nudZoom.Value);
                        current_axis.Minimum = 320 - (320 * (double)nudZoom.Value);
                    }
                    else
                    {
                        current_axis.Maximum = 240 + (240 * (double)nudZoom.Value);
                        current_axis.Minimum = 240 - (240 * (double)nudZoom.Value);
                    }
                }
                /*else
                {
                    MessageBox.Show("Couldn't find Axis values.");
                    return;
                }*/
            }//End of looping through the remaining 3 axis

            //Check that the Min and Max aren't the same, otherwise it crashes!
            if (ctTimebase.ChartAreas[0].AxisX.Minimum == ctTimebase.ChartAreas[0].AxisY.Maximum)
                ctTimebase.ChartAreas[0].AxisY.Maximum++;
        }
        /// <summary>
        /// Zooming may have taken place on the Timebase graph.
        /// SO we now see if there is the opposing Axis data available and if so, display the selected data on the Phaseplane.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ctTimebase_AxisViewChanged(object sender, ViewEventArgs e)
        {
            String x_axis, y_axis, axis_letter, selection_text;
            int x, y_axis_graph_index = -1, x_axis_graph_index = -1;
            Series temp_series;

			if (e.Axis == ctTimebase.ChartAreas[0].AxisY && cbLockAxis.Checked)
			{
				//If the Checkbox is set then we scale the Phaseplane axis size to that of the Y axis of the Timebase
				ctPhase.ChartAreas[0].AxisY.Minimum = ctPhase.ChartAreas[0].AxisX.Minimum = e.Axis.ScaleView.ViewMinimum;
				ctPhase.ChartAreas[0].AxisY.Maximum = ctPhase.ChartAreas[0].AxisX.Maximum = e.Axis.ScaleView.ViewMaximum;
				return;
			}

			//We only allow zooming on the Timebase graph, otherwise exit.
            if (e.Axis != ctTimebase.ChartAreas[0].AxisX)
                return;

            for (x = 0; x < 2; x++)
            {
                if (x == 0)
                {
                    temp_series = Spp;
                    axis_letter = cbSource.SelectedItem.ToString().Substring(0, 1);
                    selection_text = cbSource.SelectedItem.ToString();
                }
                else
                {
					if ((int)Spp2.Tag == -99)
						break;
                    temp_series = Spp2;
                    if (cbSource2.SelectedItem == null)
                        continue;
                    axis_letter = cbSource2.SelectedItem.ToString().Substring(0, 1);
                    selection_text = cbSource2.SelectedItem.ToString();
                }

                //Swap over the first letter from X to Y or vice-versa
                if (axis_letter == "X")
                {
                    x_axis = selection_text;
                    y_axis = "Y" + x_axis.Substring(1);
                }
                else if (axis_letter == "Y")
                {
                    y_axis = selection_text;
                    x_axis = "X" + y_axis.Substring(1);
                }
                else //if ((int)ctTimebase.ChartAreas[0].AxisX.Tag < -1)
                {
                    SetGraphAxisValues();
                    return;
                }
                //We now have the Name of both axis, just need to find the FileData[] index for that name
                for (int y = 0; y < FileData.Length; y++)
                {
                    if (FileData[y].Description() == x_axis)
                        x_axis_graph_index = y;
                    else if (FileData[y].Description() == y_axis)
                        y_axis_graph_index = y;
                }
                //Did we find the other axis?
                if (x_axis_graph_index == -1 || y_axis_graph_index == -1)
                {
                    return;
                }
                if (x == 0) //Axis tag is always tied to Source 1.
                {
                    ctPhase.ChartAreas[0].AxisX.Tag = x_axis_graph_index;
                    ctPhase.ChartAreas[0].AxisY.Tag = y_axis_graph_index;
                    Spp.Tag = x_axis_graph_index;
                }
                else
                    Spp2.Tag = x_axis_graph_index;

                //Now add points to the Phase Plane!

				//Check that the ScaleView is set correctly. When loading in a file it can be left at a much higher position.
				if (e.Axis.ScaleView.ViewMaximum > FileData[y_axis_graph_index].data.Length)
				{
					e.Axis.ScaleView.Size = FileData[y_axis_graph_index].data.Length;
					e.Axis.ScaleView.Position = 0;
				}

		//		Int32 last_encoder_value = FileData[5].data[(UInt32)e.Axis.ScaleView.ViewMinimum] - 1;	//Ensures we read the first encoder value.
                int last_encoder_value = 0;	//We remember the last encoder value so we can remove reverse readings.
				{
					bool encoder_reverse_checked = false;
					if (cbRemoveReverse.Checked && FileData.Length > 5 && FileData[5].Description() == "Encoder1 - File")
					{
						encoder_reverse_checked = true;
						last_encoder_value = FileData[5].data[0] - 1;	//Ensures we read the first encoder value.
					}
					int j = 0;
					//double val = 0;
					// We need to add all the data, so loop through it
					//for (; j < FileData[x_axis_graph_index].data.Length; j++)
					int z = (int)e.Axis.ScaleView.ViewMinimum;
					for (; z < (UInt32)e.Axis.ScaleView.ViewMaximum; z++, j++)	//We read from index z and write to index j
					{
						//val = FileData[y].data[j];
						//If we had encoder data and we want to ignore repeat data (from goinf back and forth) skip data
						if (encoder_reverse_checked)
						{
							if (last_encoder_value >= FileData[5].data[z])
								continue;
							last_encoder_value = FileData[5].data[z];
						}
						//Change as many of the existing points as possible, then Add the rest, OR Remove the extra existing
						if (j < temp_series.Points.Count)	//Then there is space to simply change the existing data
						{
							temp_series.Points[j].XValue = FileData[x_axis_graph_index].data[z];
							temp_series.Points[j].YValues[0] = FileData[y_axis_graph_index].data[z];
						}
						else
						{
							temp_series.Points.AddXY(FileData[x_axis_graph_index].data[z], FileData[y_axis_graph_index].data[z]);
						}
					}
					//If j is still less than the number of points in the graph, delete the rest
					//Deleting from the middle is TOO slow.
					//Deleting from the end..
					z = temp_series.Points.Count;
					while (j < z--)
						temp_series.Points.RemoveAt(z);	//Remove from the end.
				}
            }
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Set the new Min & Max values of the graphs.
            //Only call if the Zoom level has changed, otherwise we are only scrolling and Min & Max axis values don't need to change!
            if (previous_timebase_zoom_value != e.NewSize) //The size of the zoomed area has changed
            {
                SetGraphAxisValues();
            }
            previous_timebase_zoom_value = e.NewSize;
        }
        /// <summary>
        /// This timer is used to request the XML from the instrument 0.5s after it is started.
        /// Requesting it too quickly causes it to become a bit overworked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timerRequestXML_Tick(object sender, EventArgs e)
        {
            timerRequestXML.Stop();
            this.Invoke(new Action(() => btReloadSettings_Click(this, new EventArgs())));
        }

        private void nudSpotSize_ValueChanged(object sender, EventArgs e)
        {
            Spp.BorderWidth = Spp2.BorderWidth = S.BorderWidth = S2.BorderWidth = S.MarkerSize = S2.MarkerSize = Spp.MarkerSize = Spp2.MarkerSize = (int)nudSpotSize.Value;
            //Populate the value of the large NUD
            //tbNudValue.Text = ((NumericUpDown)sender).Value.ToString();
            //tbNudNameText.Text = "Spot Size";

            if (big_NumericUpDown_parent == nudSpotSize)
                tbNudValue.Text = nudSpotSize.Text;
        }

        /// <summary>
        /// There are 2 methods of connecting to the DLL:
        /// 1. We create the arrays in memory and pass a reference to them to the DLL which will populate them in real-time.
        /// 2. The DLL allocates the memory. When we want to get data we pass in an array which the DLL copies the latest set of values in to.
        /// Ideally, use the first method. It appears that the 2nd approach is what LabView requires.
        /// </summary>
        private void ConnectToDLL()
        {
            if (EtherObj != null)
                EtherObj = null;

            
            EtherObj = new ETherRealtimeClass(0, GetDataTypeFromRadioButtons());
            EtherObj.RegisterDataCallback(NewRealtimeData);
            EtherObj.RegisterDataCallbackNonRealtime(NewNonRealtimeData);
            EtherObj.RegisterFileCallback(NewFileData);
            EtherObj.RegisterProgressCallback(ProgressBarValue);
            EtherObj.RegisterECResponseCallback(NewHardwareResponse);
            
            if (EtherObj == null)
            {
                tbConsole.AppendText("DLL - Connection FAILED"+Environment.NewLine);
                btConnectUSB.Text = "Connect";
                timerPhasePlane.Enabled = false;
                serialPortUSB.Close();
                rbUSBTransmit.Enabled = false;
				int pos = this.Text.IndexOf(" Connected");
				if (pos >= 0)
					this.Text = this.Text.Substring(0, pos);

                return;
            }

            tbConsole.AppendText("DLL - Version: " + EtherObj.GetVersion() + Environment.NewLine);
            /*if (cbDllMemory.Checked)    //We let the DLL handle the memory for us :-)
            {
            EtherObj = new ETherRealtimeClass(1500, GetDataTypeFromRadioButtons());
            //EtherObj.GetBufferPointers(ref x_data1, ref y_data1, ref x_data2, ref y_data2, ref x_dataMix, ref y_dataMix);
            }
            else
            {
            //We are only using channel 1 in this example. Passing NULL as the other variables in the DLL Constructor means they won't be used.
            x_data1 = new Int32[5000];
            y_data1 = new Int32[5000];
            x_data2 = new Int32[5000];
            y_data2 = new Int32[5000];

            //EtherObj = new ETherRealtimeClass(x_data1, y_data1, x_data2, y_data2, x_dataMix, y_dataMix, 5000, GetDataTypeFromRadioButtons());
            }*/

        }
        private Int32 GetDataTypeFromRadioButtons()
        {
            // eNO_USB_OUTPUT, eFILE_SIZE, eFILE_DATA, eFILE_NAME, eREALTIME_RAW, eREALTIME_POSTPROCESS, eXML_HEADER, eSINGLE_CHAN_POST, eCONDUCTIVITY_USB
            if (rbPostProcess.Checked)  //Post Porcess has been selected
                return REALTIME_DATA_POSTPROCESS;
            //command = "<USB_OUTPUT>5</USB_OUTPUT>";
            else if (rbRaw.Checked)  //RAW has been selected
                return REALTIME_DATA_RAW;
            //command = "<USB_OUTPUT>4</USB_OUTPUT>";
            else if (rbSingleChan.Checked)
                return SINGLE_CHAN_POST;
            //command = "<USB_OUTPUT>7</USB_OUTPUT>"; //No data!
            //command = "<USB_OUTPUT>9</USB_OUTPUT>"; //Non-Realtime, ie theta, radius, percentages
            return NOTHING;
        }
        /// <summary>
        /// CALLBACK from DLL when a New file has been transmitted from the instrument.
        /// </summary>
        /// <param name="full_filename">Full path & filename of where the new file can be found.</param>
        public void NewFileData(string full_filename)
        {
            this.Invoke(new Action(() => HandleFileReceived(full_filename)));
        }
        /// <summary>
        /// CALLBACK from DLL when the progress bar should be set to the provided percentage.
        /// </summary>
        /// <param name="bar_percentage">0 to 1, ie 27% is 0.27</param>
        public void ProgressBarValue(double bar_percentage)
        {
            try
            {
                int val = (int)((double)progressBar1.Maximum * bar_percentage);
                //progressBar1.Invoke(new Action(() => progressBar1.Value = val ));
                //progressBar1.Value = val;
                this.Invoke(new Action(() => progressBar1.Value = val));
            }
            catch (SystemException)
            {
            }
        }
        /// <summary>
        /// CALLBACK from DLL when new realtime data has been received.
        /// </summary>
        /// <param name="X1"></param>
        /// <param name="Y1"></param>
        /// <param name="X2"></param>
        /// <param name="Y2"></param>
        /// <param name="Xmix"></param>
        /// <param name="Ymix"></param>
        /// <param name="Theta"></param>
        /// <param name="Radius"></param>
        /// If viewing on a Graph, we populate the respective buffers:
        /// CHAN1_BUFFER
        /// CHAN2_BUFFER
        /// STATUS_BUFFER
        /// COUNTER_BUFFER
        /// ENCODER_BUFFER
        /// CONDUCTIVITY_BUFFER
        /// ANGLE_VECTOR_BUFFER
        public void NewRealtimeData(Int32 X1, Int32 Y1, Int32 X2, Int32 Y2, Int32 Xmix_or_percent, Int32 Ymix_or_percent, Int32 Theta_or_status_X, Int32 Radius_or_status_Y)
        {
            byte[] temp_bytes = new byte[4];
            ByteArray conversion;
			int status_data = 0;

            data_point_count++;    //Keep a count of packets per second!

            if (data_transfer_state == REALTIME_DATA_POSTPROCESS || data_transfer_state == SINGLE_CHAN_POST || data_transfer_state == REALTIME_DATA_RAW || data_transfer_state == CSCAN_ENC_TRIGGERED)    //In Non-realtime mode, Theta and Radius are sent. Otherwise, these are the status bytes!
            {
				
				//For the C-Scan data we encode the Status byte in to the top 4 bits of Xmix_or_percent, which in this situation is the X Mix value, which doesn't need 32bits, 28 is fine.
				if (data_transfer_state == CSCAN_ENC_TRIGGERED)
				{
					status_data = Xmix_or_percent >> 28;				//In C-Scan mode, the status data is in the top 4 bits.
					Xmix_or_percent &= 0xFFFFFFF;	//Remove the top 4 bits
				}
				else
					status_data = Theta_or_status_X;

                if ((status_data & ALARM_BIT1) > 0 && status_alarm_was_off[0])
                {
                    btAlarm.Invoke(new Action(() => btAlarm.BackColor = Color.Red));
                    status_alarm_was_off[0] = false;
                }
                else if ((status_data & ALARM_BIT1) == 0 && !status_alarm_was_off[0])
                {
                    btAlarm.Invoke(new Action(() => btAlarm.BackColor = Color.LightGreen));
                    status_alarm_was_off[0] = true;
                }
                if ((status_data & ALARM_BIT2) > 0 && status_alarm_was_off[1])
                {
                    btAlarm2.Invoke(new Action(() => btAlarm2.BackColor = Color.Green));
                    status_alarm_was_off[1] = false;
                }
                else if ((status_data & ALARM_BIT2) == 0 && !status_alarm_was_off[1])
                {
                    btAlarm2.Invoke(new Action(() => btAlarm2.BackColor = Color.LightGreen));
                    status_alarm_was_off[1] = true;
                }
            }
            if (skip_counter++ >= points_to_skip)    //Skip every other(=1) or every 2 out of 3 (=2)
            {
                skip_counter = 0;

                //Lets log data!
                if (logging_realtime_data)
                {
                    //If user only wants to log on click, the click sets realtime logging on, then it's turned OFF here.
                    if (rbLoggingClickToSave.Checked)
                        logging_realtime_data = false;

                    if (data_transfer_state == REALTIME_DATA_RAW)
                    {
                        conversion.Bt1 = conversion.Bt2 = conversion.Bt3 = conversion.Bt4 = 0;
                        conversion.Int1 = X1;
                        fs.WriteByte(conversion.Bt1);
                        fs.WriteByte(conversion.Bt2);
                        fs.WriteByte(conversion.Bt3);
                        fs.WriteByte(conversion.Bt4);

                        conversion.Int1 = Y1;
                        fs.WriteByte(conversion.Bt1);
                        fs.WriteByte(conversion.Bt2);
                        fs.WriteByte(conversion.Bt3);
                        fs.WriteByte(conversion.Bt4);

                        conversion.Int1 = X2;
                        fs.WriteByte(conversion.Bt1);
                        fs.WriteByte(conversion.Bt2);
                        fs.WriteByte(conversion.Bt3);
                        fs.WriteByte(conversion.Bt4);

                        conversion.Int1 = Y2;
                        fs.WriteByte(conversion.Bt1);
                        fs.WriteByte(conversion.Bt2);
                        fs.WriteByte(conversion.Bt3);
                        fs.WriteByte(conversion.Bt4);
                    }
                    else if (data_transfer_state == REALTIME_DATA_POSTPROCESS || data_transfer_state == SINGLE_CHAN_POST || data_transfer_state == NON_REALTIME || data_transfer_state == CSCAN_ENC_TRIGGERED)
                    {
                        foreach (byte bt in X1.ToString())
                            fs.WriteByte(bt);
                        fs.WriteByte((byte)',');
                        foreach (byte bt in Y1.ToString())
                            fs.WriteByte(bt);
                        fs.WriteByte((byte)',');
                        foreach (byte bt in X2.ToString())
                            fs.WriteByte(bt);
                        fs.WriteByte((byte)',');
                        foreach (byte bt in Y2.ToString())
                            fs.WriteByte(bt);
                        if (data_transfer_state != SINGLE_CHAN_POST/* && data_transfer_state != CSCAN_ENC_TRIGGERED*/)
                        {
                            fs.WriteByte((byte)',');
                            foreach (byte bt in Xmix_or_percent.ToString())
                                fs.WriteByte(bt);
                            fs.WriteByte((byte)',');
                            foreach (byte bt in Ymix_or_percent.ToString())
                                fs.WriteByte(bt);
                        }
                        fs.WriteByte((byte)',');
                        //Write the status byte
                        foreach (byte bt in Theta_or_status_X.ToString())
                            fs.WriteByte(bt);
                        //We only add the last value for Non Realtime for the Radius.
                        // we're currently not using the 2nd status byte.
                        if (data_transfer_state != NON_REALTIME && data_transfer_state != CSCAN_ENC_TRIGGERED)	//Non realtime and C-Scan have the encoder values included
                        {
                            fs.WriteByte((byte)'\n');
                        }
                        else
                        {
                            fs.WriteByte((byte)',');
                            foreach (byte bt in Radius_or_status_Y.ToString())
                                fs.WriteByte(bt);
							if (data_transfer_state == CSCAN_ENC_TRIGGERED)
							{
								fs.WriteByte((byte)',');
								foreach (byte bt in status_data.ToString())
									fs.WriteByte(bt);
							}
                            fs.WriteByte((byte)'\n');
                        }
                    }
                }   //End of if Logging_realtime_data

                //Store the data in the realtime_values array so it can later be displayed on the screen.
                realtime_values[0] = X1;
                realtime_values[1] = Y1;
                realtime_values[2] = X2;
                realtime_values[3] = Y2;
                realtime_values[4] = Xmix_or_percent;
                realtime_values[5] = Ymix_or_percent;
                realtime_values[6] = Theta_or_status_X;
                realtime_values[7] = Radius_or_status_Y;

                //If the Graph is PAUSED, don't store the data in the Graph arrays.
                if (phaseplane_paused)
                    return;

                if (data_transfer_state == REALTIME_DATA_RAW)
                {
                    graph_points[CHAN1_BUFFER + buf_num, write_position[buf_num]].X = X1;
                    graph_points[CHAN1_BUFFER + buf_num, write_position[buf_num]].Y = Y1;
                    graph_points[CHAN2_BUFFER + buf_num, write_position[buf_num]].X = X2;
                    graph_points[CHAN2_BUFFER + buf_num, write_position[buf_num]].Y = Y2;

                    //Flip this value ONLY in REALTIME_DATA_POSTPROCESS
                    Ymix_or_percent = -Ymix_or_percent;
                }
                else if (data_transfer_state == REALTIME_DATA_POSTPROCESS)
                {
                    graph_points[CHAN1_BUFFER + buf_num, write_position[buf_num]].X = X1;
                    graph_points[CHAN1_BUFFER + buf_num, write_position[buf_num]].Y = -Y1;
                    graph_points[CHAN2_BUFFER + buf_num, write_position[buf_num]].X = X2;
                    graph_points[CHAN2_BUFFER + buf_num, write_position[buf_num]].Y = -Y2;
                    graph_points[MIX_BUFFER + buf_num, write_position[buf_num]].X = Xmix_or_percent;
                    graph_points[MIX_BUFFER + buf_num, write_position[buf_num]].Y = -Ymix_or_percent;
                    graph_points[STATUS_BUFFER + buf_num, write_position[buf_num]].X = Theta_or_status_X;
                    graph_points[STATUS_BUFFER + buf_num, write_position[buf_num]].Y = Radius_or_status_Y;
                    //Flip this value ONLY in REALTIME_DATA_POSTPROCESS
                    //Ymix_or_percent = -Ymix_or_percent;
                }
                else if (data_transfer_state == SINGLE_CHAN_POST || data_transfer_state == CSCAN_ENC_TRIGGERED)    //Need to flip the X coord as our graphs WONT allow the TOP of the axis to be a lower number than the bottom, which in screen coords is what we would like.
                {
                    graph_points[CHAN1_BUFFER + buf_num, write_position[buf_num]].Y = 480 - Y1;
                    graph_points[CHAN1_BUFFER + buf_num, write_position[buf_num]].X = X1;
                    graph_points[COUNTER_BUFFER + buf_num, write_position[buf_num]].Y = X2;
                    graph_points[ENCODER_BUFFER + buf_num, write_position[buf_num]].Y = Y2;
                    graph_points[STATUS_BUFFER + buf_num, write_position[buf_num]].X = Theta_or_status_X;
                    graph_points[STATUS_BUFFER + buf_num, write_position[buf_num]].Y = Radius_or_status_Y;
                }
                else if (data_transfer_state == NON_REALTIME)
                {
                    graph_points[CHAN1_BUFFER + buf_num, write_position[buf_num]].X = X1;
                    graph_points[CHAN1_BUFFER + buf_num, write_position[buf_num]].Y = -Y1;
                    graph_points[CHAN2_BUFFER + buf_num, write_position[buf_num]].X = X2;
                    graph_points[CHAN2_BUFFER + buf_num, write_position[buf_num]].Y = -Y2;
                    graph_points[MIX_BUFFER + buf_num, write_position[buf_num]].X = Xmix_or_percent;
                    graph_points[MIX_BUFFER + buf_num, write_position[buf_num]].Y = -Ymix_or_percent;
                    graph_points[ANGLE_VECTOR_BUFFER + buf_num, write_position[buf_num]].X = Theta_or_status_X;
                    graph_points[ANGLE_VECTOR_BUFFER + buf_num, write_position[buf_num]].Y = Radius_or_status_Y;
                }
                //Check for the buffer being full, jump back to start if it is.
                if (++write_position[buf_num] >= SAMPLE_BUF_SIZE)
                    write_position[buf_num] = 0;
            }
        }

        /// <summary>
        /// CALLBACK from DLL when new realtime data has been received.
        /// </summary>
        /// <param name="X1"></param>
        /// <param name="Y1"></param>
        /// <param name="X2"></param>
        /// <param name="Y2"></param>
        /// <param name="Xmix"></param>
        /// <param name="Ymix"></param>
        /// <param name="Theta"></param>
        /// <param name="Radius"></param>
        /// If the data format is set to NON_REALTIME, the returngin data is:
		/// For AC+:	 X1, Y1, X2, Y2, Theta, Radius,
		/// For 
        /// X1, Y1, X2, Y2, X%, Y%, Theta, Radius, Theta2, Radius2, BLANK1, BLANK2
        /// If the data format is set to CSCAN_ENC_TRIGGERED, the returngin data is:
        /// X1, Y1, X2, Y2, Xmix, Ymix, User Counter (will be used as a sort of encoder), Encoder.

        public void NewNonRealtimeData(Int32 X1, Int32 Y1, Int32 X2, Int32 Y2, Int32 X_Mix_or_Percent, Int32 Y_Mix_or_Percent, Int32 Counter_or_Theta, Int32 Encoder_or_Radius, Int32 Theta2, Int32 Radius2, Int32 blank1, Int32 blank2)
        {
            byte[] temp_bytes = new byte[4];
            int sync_pulse_seen = 0;

            data_point_count++;    //Keep a count of packets per second!

			//The data coming back is in a different position depending on the instrument.
			if (connected_instrument == eInstrumentType.AEROCHECK_P || connected_instrument == eInstrumentType.WELDCHECK_P || connected_instrument == eInstrumentType.WELDCHECK3)
			{
				Counter_or_Theta = X_Mix_or_Percent;
				Encoder_or_Radius=  Y_Mix_or_Percent;
				X_Mix_or_Percent = Counter_or_Theta;
				Y_Mix_or_Percent = Encoder_or_Radius;
			}
            //Lets log data!
            if (logging_realtime_data)
            {
                //If user only wants to log on click, the click sets realtime logging on, then it's turned OFF here.
                if (rbLoggingClickToSave.Checked)
                    logging_realtime_data = false;

                if (data_transfer_state == CSCAN_ENC_TRIGGERED || data_transfer_state == SINGLE_CHAN_POST)
                {
                    foreach (byte bt in X1.ToString())
                        fs.WriteByte(bt);
                    fs.WriteByte((byte)',');
                    foreach (byte bt in Y1.ToString())
                        fs.WriteByte(bt);
                    fs.WriteByte((byte)',');
                    foreach (byte bt in X2.ToString())
                        fs.WriteByte(bt);
                    fs.WriteByte((byte)',');
                    foreach (byte bt in Y2.ToString())
                        fs.WriteByte(bt);
                    fs.WriteByte((byte)',');
                    foreach (byte bt in X_Mix_or_Percent.ToString())
                        fs.WriteByte(bt);
                    fs.WriteByte((byte)',');
                    foreach (byte bt in Y_Mix_or_Percent.ToString())
                        fs.WriteByte(bt);
                    fs.WriteByte((byte)',');
                    //Write the status byte
                    foreach (byte bt in Counter_or_Theta.ToString())
                        fs.WriteByte(bt);
                    fs.WriteByte((byte)',');
                    foreach (byte bt in Encoder_or_Radius.ToString())
                        fs.WriteByte(bt);
                    fs.WriteByte((byte)',');
                    foreach (byte bt in Theta2.ToString())
                        fs.WriteByte(bt);
                    fs.WriteByte((byte)',');
                    foreach (byte bt in Radius2.ToString())
                        fs.WriteByte(bt);
                    fs.WriteByte((byte)',');
                    foreach (byte bt in blank1.ToString())
                        fs.WriteByte(bt);
                    fs.WriteByte((byte)',');
                    foreach (byte bt in blank2.ToString())
                        fs.WriteByte(bt);
                    fs.WriteByte((byte)'\n');

                }
            }   //End of if Logging_realtime_data

            //Store the data in the realtime_values array so it can later be displayed on the screen.
            realtime_values[0] = X1;
            realtime_values[1] = Y1;
            realtime_values[2] = X2;
            realtime_values[3] = Y2;
            realtime_values[4] = X_Mix_or_Percent;
            realtime_values[5] = Y_Mix_or_Percent;
            realtime_values[6] = Counter_or_Theta;
            realtime_values[7] = Encoder_or_Radius;

            //If the Graph is PAUSED, don't store the data in the Graph arrays.
            if (phaseplane_paused)
                return;
        }
        private void cbInterpolate_CheckedChanged(object sender, EventArgs e)
        {
            if (cbInterpolate.Checked)
                Spp2.ChartType = Spp.ChartType = SeriesChartType.FastLine;
            else
                Spp2.ChartType = Spp.ChartType = SeriesChartType.FastPoint;
        }

        private void nudPhaseEmbedECOnly_ValueChanged(object sender, EventArgs e)
        {
            if (((nudWithXML)sender).Value >= 360.0M)
                ((nudWithXML)sender).Value = 0;
            else if (((nudWithXML)sender).Value <= -0.1M)
                ((nudWithXML)sender).Value = 359.9M;

            string XML_Command = ((nudWithXML)sender).GetXML();
            instrument_settings.ProcessXMLString(XML_Command);

            if (big_NumericUpDown_parent == nudFrequencyEmbedECOnly)
                tbNudValue.Text = nudPhaseEmbedECOnly.Text;
            //Only send to the instrument if the user changed the value!
            if (do_not_send_value_to_instrument)
                return;

			if (serialPortRS232.IsOpen)
			{
				UInt32 data_to_send = (UInt32)(nudPhaseEmbedECOnly.Value * 1000);	//10ths of a Degree accuracy, but we store in 1000ths of a degree
				byte channel = (byte)(rbVictorCh2.Checked ? 1 : 0);					//Filters can only be for Channel 1 or 2.
				//byte0: Length. Byte1=Parameter ID, byte2=data MSB, byte3=data, byte4=data LSB, byte5=channel (0,1,2).
				byte[] bt = new byte[] { 6, 0x50, (byte)(data_to_send>>16 & 0xFF), (byte)(data_to_send>>8 & 0xFF), (byte)(data_to_send & 0xFF), channel };
				WriteToInstrument(bt, current_instrument);   //Write the string from the control preceeded with the control bytes 1,0 which inform the instrument this is an XML command.
				return;
			}

            //Now just get the SHORT version 
            XML_Command = ((nudWithXML)sender).GetXMLnoPath();
            string[] commands = XML_Command.Split(',');
			//If RS232 coms we put the string length in the first byte
            foreach (string str in commands)
                WriteToInstrument((byte)(serialPortRS232.IsOpen?(str.Length+2):1), 0, str, current_instrument);   //Write the string from the control preceeded with the control bytes 1,0 which inform the instrument this is an XML command.
        }

        private void nudGainXEmbedECOnly_ValueChanged(object sender, EventArgs e)
        {
            //First, lets check to see if the OTHER gain needs to be changed due to a Gain Lock.
            //We do this first as the lock state may mean that we can't actually even change THIS gain!

            //Now check the state of any Gain Locks!
            if (rbGainLockXequalY.Checked)
            {
                    nudGainYEmbedECOnly.Value = nudGainXEmbedECOnly.Value;
            }
            else if (rbGainLockFixed.Checked)
            {
                if (nudGainXEmbedECOnly.Value - gain_ratio_value <= nudGainYEmbedECOnly.Maximum && nudGainXEmbedECOnly.Value - gain_ratio_value >= nudGainYEmbedECOnly.Minimum) //The Gain Lock will cause the OTHER gain value to above its maximum, so DON'T increase!
                    nudGainYEmbedECOnly.Value = nudGainXEmbedECOnly.Value - gain_ratio_value;
                else //We put our Gain value BACK as it would cause the other to go past it's limit.
                {   //We don't need to recall this handler as we are only returning the value back to it's previous value, so cancel the handler.
                    this.nudGainXEmbedECOnly.ValueChanged -= new System.EventHandler(this.nudGainXEmbedECOnly_ValueChanged);
                    nudGainXEmbedECOnly.Value = nudGainYEmbedECOnly.Value + gain_ratio_value;
                    this.nudGainXEmbedECOnly.ValueChanged += new System.EventHandler(this.nudGainXEmbedECOnly_ValueChanged);
                    return;
                }
            }

            string XML_Command = ((nudWithXML)sender).GetXML();
            instrument_settings.ProcessXMLString(XML_Command);

            if (big_NumericUpDown_parent == (Control)sender)
                tbNudValue.Text = ((nudWithXML)sender).Value.ToString();

            //Only send to the instrument if the user changed the value!
            if (do_not_send_value_to_instrument)
                return;

			if (serialPortRS232.IsOpen)
			{
				UInt32 data_to_send = (UInt32)(nudGainXEmbedECOnly.Value * 10);	//10ths of a dB
				byte channel = (byte)(rbVictorCh3.Checked?2:rbVictorCh2.Checked ? 1 : 0);					//Gain can only be for all channels
				//byte0: Length. Byte1=Parameter ID, byte2=data MSB, byte3=data, byte4=data LSB, byte5=channel (0,1,2).
				byte[] bt = new byte[] { 6, 0x58, (byte)(data_to_send>>16 & 0xFF), (byte)(data_to_send>>8 & 0xFF), (byte)(data_to_send & 0xFF), channel };
				WriteToInstrument(bt, current_instrument);   //Write the string from the control preceeded with the control bytes 1,0 which inform the instrument this is an XML command.
				return;
			}

            //Now just get the SHORT version 
            XML_Command = ((nudWithXML)sender).GetXMLnoPath();
            string[] commands = XML_Command.Split(',');
            foreach (string str in commands)
                WriteToInstrument((byte)(serialPortRS232.IsOpen?(str.Length+2):1), 0, str, current_instrument);   //Write the string from the control preceeded with the control bytes 1,0 which inform the instrument this is an XML command.
        }
        private void nudGainYEmbedECOnly_ValueChanged(object sender, EventArgs e)
        {
            //First, lets check to see if the OTHER gain needs to be changed due to a Gain Lock.
            //We do this first as the lock state may mean that we can't actually even change THIS gain!

            //Now check the state of any Gain Locks!
            if (rbGainLockXequalY.Checked)
            {
                nudGainXEmbedECOnly.Value = nudGainYEmbedECOnly.Value;
            }
            else if (rbGainLockFixed.Checked)
            {
                if (nudGainYEmbedECOnly.Value + gain_ratio_value <= nudGainXEmbedECOnly.Maximum && nudGainYEmbedECOnly.Value + gain_ratio_value >= nudGainXEmbedECOnly.Minimum) //The Gain Lock will cause the OTHER gain value to above its maximum, so DON'T increase!
                    nudGainXEmbedECOnly.Value = nudGainYEmbedECOnly.Value + gain_ratio_value;
                else //We put our Gain value BACK as it would cause the other to go past it's limit.
                {   //We don't need to recall this handler as we are only returning the value back to it's previous value, so cancel the handler.
                    this.nudGainYEmbedECOnly.ValueChanged -= new System.EventHandler(this.nudGainYEmbedECOnly_ValueChanged);
                    nudGainYEmbedECOnly.Value = nudGainXEmbedECOnly.Value - gain_ratio_value;
                    this.nudGainYEmbedECOnly.ValueChanged += new System.EventHandler(this.nudGainYEmbedECOnly_ValueChanged);
                    return;
                }
            }

            string XML_Command = ((nudWithXML)sender).GetXML();
            instrument_settings.ProcessXMLString(XML_Command);

            if (big_NumericUpDown_parent == (Control)sender)
                tbNudValue.Text = ((nudWithXML)sender).Value.ToString();

            //Only send to the instrument if the user changed the value!
            if (do_not_send_value_to_instrument)
                return;

			if (serialPortRS232.IsOpen)
			{
				UInt32 data_to_send = (UInt32)(nudGainYEmbedECOnly.Value * 10);	//10ths of a dB
				byte channel = (byte)(rbVictorCh3.Checked?2:rbVictorCh2.Checked ? 1 : 0);					//Gain can only be for all channels
				//byte0: Length. Byte1=Parameter ID, byte2=data MSB, byte3=data, byte4=data LSB, byte5=channel (0,1,2).
				byte[] bt = new byte[] { 6, 0x59, (byte)(data_to_send>>16 & 0xFF), (byte)(data_to_send>>8 & 0xFF), (byte)(data_to_send & 0xFF), channel };
				WriteToInstrument(bt, current_instrument);   //Write the string from the control preceeded with the control bytes 1,0 which inform the instrument this is an XML command.
				return;
			}

            //Now just get the SHORT version 
            XML_Command = ((nudWithXML)sender).GetXMLnoPath();
            string[] commands = XML_Command.Split(',');
            foreach (string str in commands)
                WriteToInstrument((byte)(serialPortRS232.IsOpen?(str.Length+2):1), 0, str, current_instrument);   //Write the string from the control preceeded with the control bytes 1,0 which inform the instrument this is an XML command.
        }
        private void nudFiltLPEmbedECOnly_ValueChanged(object sender, EventArgs e)
        {
            if (((nudWithXML)sender).Value > 50)
            {
                ((nudWithXML)sender).Increment = 10;
            }
            else
            {
                ((nudWithXML)sender).Increment = 1;
            }
            //Check that the High Pass hasn't gone past the Low Pass!
            if (nudFiltLPEmbedECOnly.Value <= nudFiltHPEmbedECOnly.Value)
                nudFiltHPEmbedECOnly.Value -= nudFiltHPEmbedECOnly.Increment;

            if (big_NumericUpDown_parent == nudFiltLPEmbedECOnly)
                tbNudValue.Text = nudFiltLPEmbedECOnly.Text;
            //Only send to the instrument if the user changed the value!
            if (do_not_send_value_to_instrument)
                return;

			if (serialPortRS232.IsOpen)
			{
				UInt32 data_to_send = (UInt32)(nudFiltLPEmbedECOnly.Value * 100);	//100ths of a Hz
				byte channel = (byte)(rbVictorCh2.Checked ? 1 : 0);					//Filters can only be for Channel 1 or 2.
				//byte0: Length. Byte1=Parameter ID, byte2=data MSB, byte3=data, byte4=data LSB, byte5=channel (0,1,2).
				byte[] bt = new byte[] { 6, 0x4C, (byte)(data_to_send>>16 & 0xFF), (byte)(data_to_send>>8 & 0xFF), (byte)(data_to_send & 0xFF), channel };
				WriteToInstrument(bt, current_instrument);   //Write the string from the control preceeded with the control bytes 1,0 which inform the instrument this is an XML command.
				return;
			}
            string XML_Command = ((nudWithXML)sender).GetXML();
            instrument_settings.ProcessXMLString(XML_Command);
            //Now just get the SHORT version 
            XML_Command = ((nudWithXML)sender).GetXMLnoPath();
            string[] commands = XML_Command.Split(',');
            foreach (string str in commands)
                WriteToInstrument((byte)(serialPortRS232.IsOpen?(str.Length+2):1), 0, str, current_instrument);   //Write the string from the control preceeded with the control bytes 1,0 which inform the instrument this is an XML command.

        }

        private void nudFiltHPEmbedECOnly_ValueChanged(object sender, EventArgs e)
        {
            if (((nudWithXML)sender).Value > 50)
            {
                ((nudWithXML)sender).DecimalPlaces = 0;
                ((nudWithXML)sender).Increment = 10;
            }
            else if (((nudWithXML)sender).Value > 1)
            {
                ((nudWithXML)sender).DecimalPlaces = 0;
                ((nudWithXML)sender).Increment = 1;
            }
            else if (((nudWithXML)sender).Value > 0.1M)
            {
                ((nudWithXML)sender).DecimalPlaces = 1;
                ((nudWithXML)sender).Increment = 0.1M;
            }
            else
            {
                ((nudWithXML)sender).DecimalPlaces = 2;
                ((nudWithXML)sender).Increment = 0.01M;
            }

            //Check decreasing values
            if (((nudWithXML)sender).Value == 0.9M)
                ((nudWithXML)sender).Value = 0.5M;
            else if (((nudWithXML)sender).Value == 0.4M)
                ((nudWithXML)sender).Value = 0.2M;
            else if (((nudWithXML)sender).Value == 0.09M)
                ((nudWithXML)sender).Value = 0.05M;
            else if (((nudWithXML)sender).Value == 0.04M)
                ((nudWithXML)sender).Value = 0.02M;
            //Check increasing values
            if (((nudWithXML)sender).Value == 1.1M)
                ((nudWithXML)sender).Value = 2M;
            else if (((nudWithXML)sender).Value == 0.6M)
                ((nudWithXML)sender).Value = 1M;
            else if (((nudWithXML)sender).Value == 0.3M)
                ((nudWithXML)sender).Value = 0.5M;
            else if (((nudWithXML)sender).Value == 0.11M)
                ((nudWithXML)sender).Value = 0.2M;
            else if (((nudWithXML)sender).Value == 0.06M)
                ((nudWithXML)sender).Value = 0.1M;
            else if (((nudWithXML)sender).Value == 0.03M)
                ((nudWithXML)sender).Value = 0.05M;

            //Check that the High Pass hasn't gone past the Low Pass!
            if (nudFiltLPEmbedECOnly.Value <= nudFiltHPEmbedECOnly.Value)
                nudFiltLPEmbedECOnly.Value += nudFiltLPEmbedECOnly.Increment;

            if (big_NumericUpDown_parent == nudFiltHPEmbedECOnly)
            {
                tbNudValue.Text = nudFiltHPEmbedECOnly.Text;
            }
            //Only send to the instrument if the user changed the value!
            if (do_not_send_value_to_instrument)
                return;

			if (serialPortRS232.IsOpen)
			{
				UInt32 data_to_send = (UInt32)(nudFiltHPEmbedECOnly.Value * 100);	//100ths of a Hz
				byte channel = (byte)(rbVictorCh2.Checked ? 1 : 0);					//Filters can only be for Channel 1 or 2.
				//byte0: Length. Byte1=Parameter ID, byte2=data MSB, byte3=data, byte4=data LSB, byte5=channel (0,1,2).
				byte[] bt = new byte[] { 6, 0x48, (byte)(data_to_send>>16 & 0xFF), (byte)(data_to_send>>8 & 0xFF), (byte)(data_to_send & 0xFF), channel };
				WriteToInstrument(bt, current_instrument);   //Write the string from the control preceeded with the control bytes 1,0 which inform the instrument this is an XML command.
				return;
			}

            string XML_Command = ((nudWithXML)sender).GetXML();
            instrument_settings.ProcessXMLString(XML_Command);
            //Now just get the SHORT version 
            XML_Command = ((nudWithXML)sender).GetXMLnoPath();
            string[] commands = XML_Command.Split(',');
            foreach (string str in commands)
                WriteToInstrument((byte)(serialPortRS232.IsOpen?(str.Length+2):1), 0, str, current_instrument);   //Write the string from the control preceeded with the control bytes 1,0 which inform the instrument this is an XML command.
        }
        /// <summary>
        /// The Frequency for the EmbedEC has changed.
        /// We change the value and possibly the multiplier here, so must then update the control_valus in the InstrumentSettings
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void nudFrequencyEmbedECOnly_ValueChanged(object sender, EventArgs e)
        {
            Label units_label = lbFreqUnitsEmbedECOnly;
            ControlValue ct_val = instrument_settings.GetControlValueFromTagAndPath("INSTRUMENT/SETTINGS/FREQUENCY");

            //Set up the correct text in the label, although it may change...
            if (((nudWithXML)sender).multiplier == 1)
                units_label.Text = "Hz";
            else if (((nudWithXML)sender).multiplier == 1000)
                units_label.Text = "kHz";
            else
                units_label.Text = "MHz";

            if (((nudWithXML)sender).Value < 10 && ((nudWithXML)sender).multiplier == 1)
                ((nudWithXML)sender).Value = 10;
            else if (((nudWithXML)sender).Value > 12.8M && ((nudWithXML)sender).multiplier == 1000000)
                ((nudWithXML)sender).Value = 12.8M;
            else if (((nudWithXML)sender).Value >= 1000M)
            {
                ((nudWithXML)sender).ValueChanged -= nudFrequencyEmbedECOnly_ValueChanged;
                ((nudWithXML)sender).Value = 0.9M;
                ((nudWithXML)sender).ValueChanged += nudFrequencyEmbedECOnly_ValueChanged;
                if (((nudWithXML)sender).multiplier == 1000)
                {
                    units_label.Text = "MHz";
                    ((nudWithXML)sender).multiplier = 1000000;
					if (ct_val != null)
						ct_val.multiplier = 1000000;
                }
                else
                {
                    units_label.Text = "kHz";
                    ((nudWithXML)sender).multiplier = 1000;
					if (ct_val != null)
						ct_val.multiplier = 1000;
                }
            }
            else if (((nudWithXML)sender).Value < 1M)
            {
                ((nudWithXML)sender).ValueChanged -= nudFrequencyEmbedECOnly_ValueChanged;
                ((nudWithXML)sender).Value = 990M;
                ((nudWithXML)sender).ValueChanged += nudFrequencyEmbedECOnly_ValueChanged;
                if (((nudWithXML)sender).multiplier == 1000)
                {
                    units_label.Text = "Hz";
                    ((nudWithXML)sender).multiplier = 1;
					if (ct_val != null)
						ct_val.multiplier = 1;
                }
                else
                {
                    units_label.Text = "kHz";
                    ((nudWithXML)sender).multiplier = 1000;
					if (ct_val != null)
						ct_val.multiplier = 1000;
                }
            }

            //Check the increment
            if (((nudWithXML)sender).Value > 100)
            {
                ((nudWithXML)sender).Increment = 10;
                ((nudWithXML)sender).ValueChanged -= nudFrequencyEmbedECOnly_ValueChanged;
                ((nudWithXML)sender).Value = Math.Round(((nudWithXML)sender).Value / 10) * 10;
                ((nudWithXML)sender).ValueChanged += nudFrequencyEmbedECOnly_ValueChanged;
            }
            else if (((nudWithXML)sender).Value > 10)
            {
                ((nudWithXML)sender).Increment = 1;
                ((nudWithXML)sender).ValueChanged -= nudFrequencyEmbedECOnly_ValueChanged;
                ((nudWithXML)sender).Value = Math.Round(((nudWithXML)sender).Value);
                ((nudWithXML)sender).ValueChanged += nudFrequencyEmbedECOnly_ValueChanged;
            }
            else// if (((nudWithXML)sender).Value > 1)
                ((nudWithXML)sender).Increment = 0.1M;

            //Only send to the instrument if the user changed the value!
            if (do_not_send_value_to_instrument)
                return;

			if (serialPortRS232.IsOpen)
			{
				UInt32 data_to_send = (UInt32)(nudFrequencyEmbedECOnly.Value*((nudWithXML)sender).multiplier);	//Hz
				byte channel = (byte)(rbVictorCh2.Checked ? 1 : 0);				//Filters can only be for Channel 1 or 2.
				//byte0: Length. Byte1=Parameter ID, byte2=data MSB, byte3=data, byte4=data LSB, byte5=channel (0,1,2).
				byte[] bt = new byte[] { 6, 0x46, (byte)(data_to_send>>16 & 0xFF), (byte)(data_to_send>>8 & 0xFF), (byte)(data_to_send & 0xFF), channel };
				WriteToInstrument(bt, current_instrument);   //Write the string from the control preceeded with the control bytes 1,0 which inform the instrument this is an XML command.
				return;
			}

            string XML_Command = ((nudWithXML)sender).GetXML();
            instrument_settings.ProcessXMLString(XML_Command);
            //Now just get the SHORT version 
            XML_Command = ((nudWithXML)sender).GetXMLnoPath();
            string[] commands = XML_Command.Split(',');
            foreach (string str in commands)
                WriteToInstrument((byte)(serialPortRS232.IsOpen?(str.Length+2):1), 0, str, current_instrument);   //Write the string from the control preceeded with the control bytes 1,0 which inform the instrument this is an XML command.
        }
        /// <summary>
        /// User wants the Y gain to be the same as the X, so set the Y to be the same as the X!
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void rbGainLockXequalY_CheckedChanged(object sender, EventArgs e)
        {
            if (rbGainLockXequalY.Checked)
                nudGainYEmbedECOnly.Value = nudGainXEmbedECOnly.Value;
        }
        /// <summary>
        /// Any change in gain (For EmbedEc) will result in the OTHER gain (x or Y) being changed also.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void rbGainLockFixed_CheckedChanged(object sender, EventArgs e)
        {
            if (rbGainLockFixed.Checked)
                gain_ratio_value = nudGainXEmbedECOnly.Value - nudGainYEmbedECOnly.Value;
        }

        private void btPause_Click(object sender, EventArgs e)
        {
            if (phaseplane_paused)
                this.btPause.BackgroundImage = global::ETherRealTime.Properties.Resources.pause;
            else
                this.btPause.BackgroundImage = global::ETherRealTime.Properties.Resources.play;
            phaseplane_paused = !phaseplane_paused;
        }

        private void ctPhase_MouseClick(object sender, MouseEventArgs e)
        {
            if (cbAlarmDraw.Checked)
            {
                //Lets store the corods of the click
                ctPhase.ChartAreas[0].BackImageAlignment = ChartImageAlignmentStyle.TopRight;
                //ctPhase.ChartAreas[0].BackImageWrapMode = ChartImageWrapMode.Unscaled;
                ctPhase.ChartAreas[0].BackImageWrapMode = ChartImageWrapMode.Scaled;
                ctPhase.ChartAreas[0].BackImage = "phaseplane.bmp";
            }


     /*       Graphics gp = ctPhase.CreateGraphics();
            gp.DrawRectangle(new Pen(Color.DarkGreen, 3), new Rectangle(10, 10, 100, 100));
      * */
        }

        private void PhaseEmbedECOnly_Click(object sender, EventArgs e)
        {
            //Populate the value of the large NUD
            tbNudValue.Text = nudPhaseEmbedECOnly.Text;
            tbNudNameText.Text = "Phase";
            lbBIGNudFreq.Text = "deg.";

            big_NumericUpDown_parent = nudPhaseEmbedECOnly;
			big_NumericUpDown_brother = nudPhaseCh1;

			byte channel = 0;

			if (rbVictorCh2.Checked)
				channel = 1;
			else if (rbVictorCh3.Checked)
				channel = 2;

			byte[] bt = new byte[] { 4, 0x3f, 0x50, channel };	//2=Lemgth, 0x3f='?', 0x50 = P, ie What is the Phase?
			WriteToInstrument(bt, current_instrument);

        }

        private void GainXEmbedEC_Click(object sender, EventArgs e)
        {
            //Populate the value of the large NUD
            tbNudValue.Text = nudGainXEmbedECOnly.Text;
            tbNudNameText.Text = "Gain X";
            lbBIGNudFreq.Text = "dB";
            big_NumericUpDown_parent = nudGainXEmbedECOnly;
			big_NumericUpDown_brother = nudGainXch1;

			byte channel = 0;

			if (rbVictorCh2.Checked)
				channel = 1;
			else if (rbVictorCh3.Checked)
				channel = 2;

			byte[] bt = new byte[] { 4, 0x3f, 0x58, channel };	//2=Lemgth, 0x3f='?', 0x58 = X, ie What is the X Gain?
			WriteToInstrument(bt, current_instrument);
        }

        private void GainYEmbedEC_Click(object sender, EventArgs e)
        {
            //Populate the value of the large NUD
            tbNudValue.Text = nudGainYEmbedECOnly.Text;
            tbNudNameText.Text = "Gain Y";
            lbBIGNudFreq.Text = "dB";
            big_NumericUpDown_parent = nudGainYEmbedECOnly;
			big_NumericUpDown_brother = nudGainYch1;

			byte channel = 0;

			if (rbVictorCh2.Checked)
				channel = 1;
			else if (rbVictorCh3.Checked)
				channel = 2;

			byte[] bt = new byte[] { 4, 0x3f, 0x59, channel };	//4=Lemgth, 0x3f='?', 0x59 = X, ie What is the X Gain?
			WriteToInstrument(bt, current_instrument);
        }

        private void FiltLPEmbedECOnly_Click(object sender, EventArgs e)
        {
            //Populate the value of the large NUD
            tbNudValue.Text = nudFiltLPEmbedECOnly.Text;
            tbNudNameText.Text = "Low Pass";
            lbBIGNudFreq.Text = "Hz";
            big_NumericUpDown_parent = (Control)nudFiltLPEmbedECOnly;
			big_NumericUpDown_brother = nudLowPassFilterCh1;

			byte channel = 0;

			if (rbVictorCh2.Checked)
				channel = 1;
			else if (rbVictorCh3.Checked)
				channel = 2;
			byte[] bt = new byte[] { 4, 0x3f, 0x4C, channel };	//4=Lemgth, 0x3f='?', 0x4C = L, ie What is the Low Pass Filter?
			WriteToInstrument(bt, current_instrument);
        }
        private void FiltHPEmbedECOnly_Click(object sender, EventArgs e)
        {
            //Populate the value of the large NUD
            tbNudValue.Text = nudFiltHPEmbedECOnly.Text;
            tbNudNameText.Text = "High Pass";
            lbBIGNudFreq.Text = "Hz";
            big_NumericUpDown_parent = (Control)nudFiltHPEmbedECOnly;
			big_NumericUpDown_brother = nudHighPassFilterCh1;

			byte channel = 0;

			if (rbVictorCh2.Checked)
				channel = 1;
			else if (rbVictorCh3.Checked)
				channel = 2;

			byte[] bt = new byte[] { 4, 0x3f, 0x48, channel };	//4=Lemgth, 0x3f='?', 0x48 = H, ie What is the High pass Filter?
			WriteToInstrument(bt, current_instrument);
        }

        private void FrequencyEmbedECOnly_Click(object sender, EventArgs e)
        {
            //Populate the value of the large NUD
            tbNudValue.Text = nudFrequencyEmbedECOnly.Text;
            tbNudNameText.Text = "Frequency";

            big_NumericUpDown_parent = (Control)nudFrequencyEmbedECOnly;
			big_NumericUpDown_brother = nudFrequencyCh1;

			byte channel = 0;

			if (rbVictorCh2.Checked)
				channel = 1;
			else if (rbVictorCh3.Checked)
				channel = 2;

			byte[] bt = new byte[] { 4, 0x3f, 0x46, channel };	//4=Lemgth, 0x3f='?', 0x46 = F, ie What is the Frequency?
			WriteToInstrument(bt, current_instrument);
        }

        private void Persistence_Click(object sender, EventArgs e)
        {
            //Populate the value of the large NUD
            tbNudValue.Text = nudPersistence.Text;
            tbNudNameText.Text = "Persistence";
            lbBIGNudFreq.Text = "s";
            big_NumericUpDown_parent = (Control)nudPersistence;
        }

        private void Zoom_Click(object sender, EventArgs e)
        {
            //Populate the value of the large NUD
            tbNudValue.Text = nudZoom.Text;
            tbNudNameText.Text = "Zoom";
            lbBIGNudFreq.Text = "";
            big_NumericUpDown_parent = (Control)nudZoom;
        }

        private void SpotSize_Click(object sender, EventArgs e)
        {
            //Populate the value of the large NUD
            tbNudValue.Text = nudSpotSize.Text;
            tbNudNameText.Text = "Spot Size";
            lbBIGNudFreq.Text = "pix.";
            big_NumericUpDown_parent = (Control)nudSpotSize;
        }

        private void nudSweepTime_Click(object sender, EventArgs e)
        {
            //Populate the value of the large NUD
            tbNudValue.Text = nudSweepTime.Text;
            tbNudNameText.Text = "Sweep Time";
            lbBIGNudFreq.Text = "s";
            big_NumericUpDown_parent = (Control)nudSweepTime;
        }

        private void btNudUp_Click(object sender, EventArgs e)
        {
            if (big_NumericUpDown_parent != null)
				((NumericUpDown)big_NumericUpDown_parent).UpButton();
			if (big_NumericUpDown_brother != null)
				((NumericUpDown)big_NumericUpDown_brother).UpButton();

        }

        private void btNudDown_Click(object sender, EventArgs e)
        {
            if (big_NumericUpDown_parent != null)
	            ((NumericUpDown)big_NumericUpDown_parent).DownButton();
			if (big_NumericUpDown_brother != null)
				((NumericUpDown)big_NumericUpDown_brother).DownButton();
        }

        private void nudEmbedECOnly_TextChanged(object sender, EventArgs e)
        {
            if (big_NumericUpDown_parent == (Control)sender)
                tbNudValue.Text = ((NumericUpDown)sender).Text;
			if (big_NumericUpDown_brother == (Control)sender)
				tbNudValue.Text = ((NumericUpDown)sender).Text;
        }
        /// <summary>
        /// Used for the BIG Up Down keys for the EmbedEC.
        /// If OFF, we assume it's just starting and set to 1s
        /// If the button is still pressed after 1s, increase timer to 0.1s
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timerKeyRepeat_Tick(object sender, EventArgs e)
        {
            //Shouldn't ever happen, but better be safe rather than sorry!
            if (timerKeyRepeat.Tag == null)
            {
                timerKeyRepeat.Stop();
                timerKeyRepeat.Interval = 1000;
                return;
            }
            //Send a button press
            if ((Control)timerKeyRepeat.Tag == btNudDown)
                btNudDown_Click(this, new EventArgs());
            else if ((Control)timerKeyRepeat.Tag == btNudUp)
                btNudUp_Click(this, new EventArgs());

            //The interval should now be 0.1s
            if (timerKeyRepeat.Interval == 1000)
                timerKeyRepeat.Interval = 100;
            else if (timerKeyRepeat.Interval >= 30)
                timerKeyRepeat.Interval -= 10;
        }
        /// <summary>
        /// The Touch Screen tablet generates a CLICK message before a MOUSE DOWN.
        /// When using a mouse, a MOUSE DOWN event is generated before a CLICK.
        /// SO, when a CLICK is generated first, Block the MOUSE DOWN event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btNudUpDown_MouseDown(object sender, MouseEventArgs e)
        {
            if (!block_mouse_down)
            {
                tbConsoleFileScan.AppendText("Timer Start on MOUSEDOWN"+Environment.NewLine);//Set which ever button has been pressed to the the tag of the timer.
                timerKeyRepeat.Tag = sender;
                timerKeyRepeat.Start();
            }
            block_mouse_down = !block_mouse_down;
        }

        private void btNudUpDown_MouseUp(object sender, MouseEventArgs e)
        {
            block_mouse_down = false;
            timerKeyRepeat.Tag = null;

            timerKeyRepeat.Stop();          //Stop the timer..

            //If the timer is still set to 1s, no button presses have been sent yet, so send one now.
            if (timerKeyRepeat.Interval == 1000)
            {
                if ((Control)sender == btNudDown)
                    btNudDown_Click(sender, new EventArgs());
                else if ((Control)sender == btNudUp)
                    btNudUp_Click(sender, new EventArgs());
            }

            timerKeyRepeat.Interval = 1000; //Reset the interval.
        }
		/// <summary>
        /// Get a Value from the provided Key.
        /// </summary>
        /// <param name="tag">The name of a Tag.</param>
        /// <returns>String. "" Will be returned if the Tag couldn't be found, obviously this could also be the value if the Key was found!</returns>
        private string GetDataSetValue(string tag)
        {
            DataRow row = dataGeneral.Tables["General"].Rows.Find(tag);
            if (row == null)
                return "";

            return row["Value"].ToString();
        }
        /// <summary>
        /// Set a value that will be stored in the dataSet. If the tag already exists its value will be updated, if not a new entry will be made.
        /// </summary>
        /// <param name="tag">The name of the variable, ie "StartingLane"</param>
        /// <param name="value">The value in string form, ie for the StartingLane, "19"</param>
        public void SetDataSetValue(string tag, string value)
        {
            DataRow row = dataGeneral.Tables["General"].Rows.Find(tag);
            if (row == null)
            {
                row = dataGeneral.Tables["General"].NewRow();
                row[0] = tag;
                row[1] = value;
                dataGeneral.Tables["General"].Rows.Add(row);
            }
            else
            {
                row[1] = value;
            }
            dataGeneral.AcceptChanges();
        }
        /// <summary>
        /// The Frequency Units of the list of variabels has changed, so update the equivalent label next to the BIG Up/Down buttons.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lbFreqUnitsEmbedECOnly_TextChanged(object sender, EventArgs e)
        {
            lbBIGNudFreq.Text = lbFreqUnitsEmbedECOnly.Text;
        }
        /// <summary>
        /// Only display the units Label (Hz, kHz, MHz) if the BIG Up/Down is editing frequency.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbNudNameText_TextChanged(object sender, EventArgs e)
        {
            if (tbNudNameText.Text == "Frequency")
                lbBIGNudFreq.Text = lbFreqUnitsEmbedECOnly.Text;
        }

        private void button1_MouseDown(object sender, MouseEventArgs e)
        {
            tbConsoleFileScan.AppendText("Mouse Down"+Environment.NewLine);
        }

        private void button1_MouseEnter(object sender, EventArgs e)
        {
            tbConsoleFileScan.AppendText("Mouse Enter"+Environment.NewLine);
        }

        private void button1_MouseHover(object sender, EventArgs e)
        {
            tbConsoleFileScan.AppendText("Mouse Hover"+Environment.NewLine);
        }

        private void button1_MouseLeave(object sender, EventArgs e)
        {
            tbConsoleFileScan.AppendText("Mouse Leave"+Environment.NewLine);
        }

        private void button1_MouseMove(object sender, MouseEventArgs e)
        {
            //tbConsoleFileScan.AppendText("Mouse Move\n");
        }

        private void button1_MouseUp(object sender, MouseEventArgs e)
        {
            tbConsoleFileScan.AppendText("Mouse Up"+Environment.NewLine);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            tbConsoleFileScan.AppendText("Click"+Environment.NewLine);
        }

        private void button1_MouseCaptureChanged(object sender, EventArgs e)
        {
            tbConsoleFileScan.AppendText("Mouse Capture Changed"+Environment.NewLine);
        }

        private void button1_MouseClick(object sender, MouseEventArgs e)
        {
            tbConsoleFileScan.AppendText("Mouse Click"+Environment.NewLine);
        }

        private void button1_Enter(object sender, EventArgs e)
        {
            tbConsoleFileScan.AppendText("Enter"+Environment.NewLine);
        }

        private void button1_Leave(object sender, EventArgs e)
        {
            tbConsoleFileScan.AppendText("Leave"+Environment.NewLine);
        }

        private void button1_KeyDown(object sender, KeyEventArgs e)
        {
            tbConsoleFileScan.AppendText("Key Down"+Environment.NewLine);
        }

        private void button1_KeyPress(object sender, KeyPressEventArgs e)
        {
            tbConsoleFileScan.AppendText("Key Press"+Environment.NewLine);
        }
        /// <summary>
        /// The Touch Screen tablet generates a CLICK message before a MOUSE DOWN.
        /// When using a mouse, a MOUSE DOWN event is generated before a CLICK.
        /// SO, when a CLICK is generated first, Block the MOUSE DOWN event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btNudUpDown_Click_1(object sender, EventArgs e)
        {
            if (!block_mouse_down)
            {
                tbConsoleFileScan.AppendText("Timer Start on CLICK"+Environment.NewLine);//Set which ever button has been pressed to the the tag of the timer.
                timerKeyRepeat.Tag = sender;
                timerKeyRepeat.Start();
            }
            block_mouse_down = !block_mouse_down;
            //tbConsoleFileScan.AppendText("Click_1\n");
        }

        private void btHelp_Click(object sender, EventArgs e)
        {
            new Help().Show();
            
        }

        /// <summary>
        /// Assuming realtime logging is configured, log 1 set of data.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btLOG_Click(object sender, EventArgs e)
        {
            //Set this TRUE, then the next set of incomming data is logged and the below variable set to FALSE once again.
            logging_realtime_data = true;
        }


     
        /// <summary>
        /// We check the dragdrop item for it's filename end ensure that it is a bmp OR XML file.
        /// </summary>
        /// <param name="filename"></param>
        /// The filename of the item is sent back
        /// <param name="e"></param>
        /// Return 
        /// <returns></returns>
        protected Int16 GetFilename(DragEventArgs e)
        {
            if (((e.AllowedEffect & DragDropEffects.Copy) == DragDropEffects.Copy) && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];

                if (files != null)
                {
                    foreach (string st in files)
                    {
                        drag_items.Add(st);
                    }//End of foreach strign that has been dragged in
                }//End of if the draged in data was valid.
            }
            return 1;
        }

		private void tbTxString_Validated(object sender, EventArgs e)
		{
			String command = tbTxString.Text;
			WriteToInstrument(1, 0, command, current_instrument);
		}
		/// <summary>
		/// The extra IO connector on the EmbedEC (maybe later the AeroCheck for rail inspection)
		/// can be configured in the following ways:
		/// IO_OFF, ENCODER_1, ENCODER_2, ENCODER_1_AND_2, ENCODER_1_SPI, ENCODER_1_AND_DO, DIGITAL_OUT
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void rbIO_Mode_CheckedChanged(object sender, EventArgs e)
		{
			string command = "IO_OFF";

			if (((RadioButton)sender).Name == "rbEnc1SPI" || ((RadioButton)sender).Name == "rbEnc1DigIO" || ((RadioButton)sender).Name == "rbDigIO")
			{
				nudSPI_DIO.Enabled = btTxIO.Enabled = true;
				if (((RadioButton)sender).Name == "rbEnc1SPI")
				{
					command = "ENCODER_1_SPI";
					btTxIO.Text = "Tx SPI";
					nudSPI_DIO.Maximum = 255;
				}
				else // DIgitla IO of one kind...
				{
					
					btTxIO.Text = "Tx Digital";
					if (((RadioButton)sender).Name == "rbDigIO")
					{
						command = "DIGITAL_OUT";
						nudSPI_DIO.Maximum = 31;	// 5 bit digital number (0-31)
					}
					else
					{
						command = "ENCODER_1_AND_DO";
						nudSPI_DIO.Maximum = 7;		//3 bit digital number (0-7)
					}
				}
			}
			else
			{
				nudSPI_DIO.Enabled = btTxIO.Enabled = false;
				if (((RadioButton)sender).Name == "rbEnc1")
					command = "ENCODER_1";
				else if (((RadioButton)sender).Name == "rbEnc2")
					command = "ENCODER_2";
				else //if (((RadioButton)sender).Name == "rbEnc1Enc2")
					command = "ENCODER_1_AND_2";
			}
			WriteToInstrument(1, 0, "<IO_PORT_CONFIG>" + command + "</IO_PORT_CONFIG>", current_instrument);
		}
		/// <summary>
		/// Transmit a number via the EMbedEC ourput connector on it's SPI bus OR standard digital IO lines
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btTxIO_Click(object sender, EventArgs e)
		{
			if (tbByteSequence.Text.Length == 0)
				WriteToInstrument(1, 0, "<DIGITAL_OUT>" + nudSPI_DIO.Value + "</DIGITAL_OUT>", current_instrument);
			else
			{
				D_IO_Seq = new DigitalIOSequence(tbByteSequence.Text.Split(','), cbLoop.Checked);
				if (nudBytesPerSecond.Value > 20)
				{
					if (previous_Dig_IO_rate <= 20)
						MessageBox.Show("Byte rates above 20 must be controlled directly by the EmbedEC, not ETherRealtime.\n Command is being sent...");
					if (nudBytesPerSecond.Value < 640)
					{
						MessageBox.Show("Rates below 640 WILL NOT WORK on the Embed EC due to clock ranges!!");
						return;
					}

					//The sequence is added in to the command followed by the number of mS wait between each byte.
					WriteToInstrument(1, 0, "<DIG_SEQ>" + D_IO_Seq.GetWholeSequenceString() + "," + ((int)(nudBytesPerSecond.Value / 256)).ToString() + "," + (nudBytesPerSecond.Value % 256).ToString() + "</DIG_SEQ>", current_instrument);
					timerDigitalIOSequence.Enabled = false;
				}
				else
				{
					if (previous_Dig_IO_rate > 20)
						MessageBox.Show("Byte rates below 20 are sent at the specified time interval by ETherRealtime, NOT by the EmbedECs timer");
					timerDigitalIOSequence.Interval = (int)(1000 / nudBytesPerSecond.Value);
					timerDigitalIOSequence.Enabled = true;
				}
			}
			previous_Dig_IO_rate = nudBytesPerSecond.Value;
		}
		/// <summary>
		/// Attempt to save the ETherRealTime settings as we're closing.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void ETherRealTime_FormClosing(object sender, FormClosingEventArgs e)
		{
			dataGeneral.AcceptChanges();
			string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\ETherRealTime";
            Directory.CreateDirectory(path);
			StreamWriter stw = new StreamWriter(path + "\\config.txt");
			//Lets save some of the data so that we can reconfigure ourselves when we next open!
            try
            {
                //stw.WriteLine("folder:" + tbTestFolder.Text);
                text_box_strings.Clear();   //Clear out the array list where we save all the contents of all text boxes.

                GetControlText(this);       //Asks all controls that are of type TextBoxSaveLoad to return their contents as an XML formatted string
                foreach (string st in text_box_strings)
                {
                    stw.WriteLine(st);
                }
            }
            catch
            {
            }
            finally
            {
                stw.Close();
            }
			try
			{
				
				dataGeneral.WriteXml(path + "\\General.xml");
			}
			catch
			{
				MessageBox.Show("Couldn't save ETherRealTime settings to the following folder:\n" + path + "\n" + "Consider creating the folder.");
			}
		}
		        /// <summary>
        /// Search through all child controls of the provided control for any of type TextBoxSaveLoad.
        /// If any have the provided name, set its text to the provided text in the XML.
        /// The XML string contains the control name to look for and the value to set its text to in the format:
        /// "<TEXTBOX Name="tbTestedBy" Contents="Ian Drew">"
        /// </summary>
        /// <param name="ctrl"></param>
        /// <param name="xml"></param>
        void SetControlText(Control ctrl, String xml)
        {
            string[] junk = xml.Split('\"');

            if (junk.Length < 4)
                return;

            foreach (Control child_ctrl in ctrl.Controls)
            {
                if (child_ctrl.GetType() == typeof(TextBoxLoadSave) && child_ctrl.Name == junk[1])   //Save the text if it's a text box and if it has any text!
                {
                    child_ctrl.Text = junk[3];
                    return;
                }
                else if (child_ctrl.HasChildren)
                {
                    SetControlText(child_ctrl, xml);
                }
            }
        }
		/// <summary>
        /// Asks all controls that are of type TextBoxSaveLoad to return their contents as an XML formatted string
        /// </summary>
        /// <param name="ctrl"></param>
        void GetControlText(Control ctrl)
        {
            foreach (Control child_ctrl in ctrl.Controls)
            {
                if (child_ctrl.GetType() == typeof(TextBoxLoadSave) && child_ctrl.Text.Length > 0)   //Save the text if it's a text box and if it has any text!
                {
                    text_box_strings.Add(((TextBoxLoadSave)child_ctrl).GetContentsInXML());
                }
                else if (child_ctrl.HasChildren)
                {
                    GetControlText(child_ctrl);
                }
            }
        }
		/// <summary>
		/// User is saving the XML file present in the Rich Text Box. First though, we update the XML with the latest values from the controls.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btSaveXMLFile_Click(object sender, EventArgs e)
		{
			SaveFileDialog sfd = new SaveFileDialog();

			//Update the XML in the rtb with the correct value from each control.
			TakeXMLAndUpdateValue();
			//Save the XML file
			string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			sfd.Title = "Select Save File";
			sfd.InitialDirectory = path;
			sfd.DefaultExt = "xml";
            sfd.Filter = "xml files (*.xml)|*.xml|All files (*.*)|*.*";
			if (sfd.ShowDialog() != DialogResult.OK)
				return;
			//Lets save the file!
			rtbXMLSettings.SaveFile(sfd.FileName, RichTextBoxStreamType.PlainText);	
		}

		private void tbByteSequence_TextChanged(object sender, EventArgs e)
		{
			if (tbByteSequence.Text.Length > 0)
			{
				nudSPI_DIO.Enabled = false;
				nudBytesPerSecond.Enabled = cbLoop.Enabled = true;
			}
			else
			{
				nudSPI_DIO.Enabled = true;
				nudBytesPerSecond.Enabled = cbLoop.Enabled = false;
			}
		}

		private void timerDigitalIOSequence_Tick(object sender, EventArgs e)
		{
			string number_to_send = D_IO_Seq.GetNextValue();
			if (number_to_send == "")
			{
				timerDigitalIOSequence.Enabled = false;
				return;
			}
			//WriteToInstrument(1, 0, "<DIGITAL_OUT>" + number_to_send + "</DIGITAL_OUT>", current_instrument);
			string data = "<DIGITAL_OUT>" + number_to_send + "</DIGITAL_OUT>";
			byte[] bt_data = new byte[data.Length + 2];
			int x = 0;
			bt_data[0] = 1;
			bt_data[1] = 0;
			for (; x < data.Length; x++)
			{
				bt_data[x+2] = (byte)data[x];
			}
			WriteToInstrument(bt_data, current_instrument);

		}
		/// <summary>
		/// The RailCheck instrument streams its data to the microSD card in the form of a CSV file.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btViewCSV_Click(object sender, EventArgs e)
		{
			OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "RailCheck csv files (*.csv)|*.csv|All files (*.*)|*.*";
            if (ofd.ShowDialog() != DialogResult.OK || ofd.SafeFileNames.Length != 1)
                return;

			LoadRailCheckCSVFile(ofd.FileName);
			//Display which file we're viewing
			tbCurrentFile.Text = ofd.FileName;
		}
		public void LoadRailCheckCSVFile(String filename)
		{
            Int32 pos = 0; //Keep a rough indication as to how much we've processed.
			bool sync_pulse = false;
			bool xml_ok = false;

			StreamReader str = null;
			StreamReader str_xml = null;	//The csv file often has an accompanied XML file containing the instrument settings, so lets get it!
			String data = "";
			String data_xml = "";
			XML_Handler loaded_file_header_info = null;
			try
			{
				str = new StreamReader(filename);
				str_xml = null;	//The csv file often has an accompanied XML file containing the instrument settings, so lets get it!
				data = str.ReadToEnd();
			
				str_xml = new StreamReader(filename + ".xml");
				data_xml = str_xml.ReadToEnd();
				loaded_file_header_info = new XML_Handler(data_xml, out xml_ok, filename + ".xml");
				str_xml.Close();
			}
			catch
			{
				MessageBox.Show("Settings File NOT Present, data can still be displayed.");
			}
            UInt32 data_sets = 0;
            String message_text = "";   //After the file is processed, we display a message to the user.

            byte[] raw_data = File.ReadAllBytes(filename);
			str.Close();

            try
            {
                
                //Now we count the number of Carriage Returns to get the number of Data Sets in the file:
                for (Int64 x = pos; x < raw_data.Length; x++)
                {
                    if (raw_data[x] == '\n')
                        data_sets++;
                }                
                //case SINGLE_CHAN_POST:         // 0x70, 16 bytes, 4 for X, 4 for Y, 4 for counter, 4 for Encoder.
                message_text = "RailCheck data of X & Y for 2 channels, 2 Encoders and a status byte.\n";
                //How many data sets are in the file?
                FileData = new GraphData[7];
                FileData[0] = new GraphData("X - Chan 1", data_sets, 4, REALTIME_DATA_POSTPROCESS);
                FileData[1] = new GraphData("Y - Chan 1", data_sets, 4, REALTIME_DATA_POSTPROCESS);
				FileData[2] = new GraphData("X - Chan 2", data_sets, 4, REALTIME_DATA_POSTPROCESS);
                FileData[3] = new GraphData("Y - Chan 2", data_sets, 4, REALTIME_DATA_POSTPROCESS);
                FileData[4] = new GraphData("Encoder2 - File", data_sets, 4, ENCODER);
                FileData[5] = new GraphData("Encoder1 - File", data_sets, 4, ENCODER);
                FileData[6] = new GraphData("Status - File", data_sets, 2, STATUS);
                
                do
                {
                    if (FileData == null)
                        return;
                    //Loop through each of the expected data fields in each packet
                    foreach (GraphData gd in FileData)
                    {
                        Int32 value = 0;
						try
						{
							StringBuilder val = new StringBuilder();
							while (raw_data[pos] != ',' && raw_data[pos] != '\n')
							{
								val.Append((char)raw_data[pos]);
								if (++pos >= raw_data.Length)
									break;
							}
							value = Convert.ToInt32(val.ToString());
							if (gd.data_type == REALTIME_DATA_POSTPROCESS && gd.Description().StartsWith("Y"))
								value = -value;
							else if (gd.data_type == STATUS)
								value *= 10;	//Just to make the alarm signal which is 0 or 2 much larger and therefore more visible on the graph.
							if (++pos >= raw_data.Length)
									break;  //Skip past the ',' or New Line
							if (gd.data_type == STATUS) //The STATUS channel isn't read from the etd file, we create it from the status bit (LSb) of the Checksum byte (LSB).
							{
								if (sync_pulse)
									value = 1;
								sync_pulse = false;
							}

						}
						catch
						{
							MessageBox.Show("Error reading data. Incomplete line?");
							return;
						}
                        if (!gd.AddPoint(value))
                            break;
                    }
                } while (pos < raw_data.Length);
				
				//Now add these new GraphDatas to the combo boxes:
				PopulateSourceComboBoxes();
            }
            catch (SystemException ex)
            {
                tbConsoleFileScan.AppendText(ex.Message);
            }

            message_text += FileData[0].data.Length.ToString() + " points per Channel.\n" + FileData.Length.ToString() + " Channels of data:\n";

            foreach (GraphData gd in FileData)
            {
                message_text += "  " + gd.Description() + "\n";
            }
            tabControl1.SelectedTab = tabControl1.TabPages["tpPhaseplane"];

			//If we read in the XML for the csv file, lets make use oif it!
			if (str_xml != null && loaded_file_header_info != null)
			{
				ControlValue cv = loaded_file_header_info.GetControlValueFromTagAndPath("RAIL/CSCAN_X_TICKS_MM");
				decimal ticks_mm = cv.decimal_display_value;
				cv = loaded_file_header_info.GetControlValueFromTagAndPath("RAIL/CSCAN_TIMEBASE_LENGTH");
				decimal timebase_length_mm = cv.decimal_display_value;

				//Label every cm, lets try that
				decimal ticks_in_unit;
				string unit_string = "";
				decimal label1 = 0, label_increment = 1;
				int startOffset = -2;
				int endOffset = 2;

				//timebase_length_mm can be read from the instrument settings, ie 100Metres. BUT, if the data was recorded over 10 screen-fulls of data, it is better to
				// use a value of 1km. So lets calculate this value rather than from the settings:

				timebase_length_mm = data_sets / (ticks_mm / 100);	//The final /100 is because this number is stored 100 times greater.
				//If the scan is equal to or less than 1 metre
				if (timebase_length_mm <= 1000)
				{
					ticks_in_unit = ticks_mm / 100;	//ticks per cm. This would be *10, but the value is 100 times larger;
					unit_string = "cm";
					label_increment = 1;	//Every cm
				}
				else if (timebase_length_mm <= 10000)	//Less than 10 Metres
				{
					ticks_in_unit = ticks_mm / 10;	//ticks per 10cm. This would be *10, but the value is 100 times larger;
					unit_string = "cm";
					label_increment = 10;	//Every 10cm
				}
				else if (timebase_length_mm <= 100000)	//ticks per metre. Less than 100 Metres
				{
					ticks_in_unit = ticks_mm;	//This would be *10, but the value is 100 times larger;
					unit_string = "m";
					label_increment = 1;	//Every metre
				}
				else if (timebase_length_mm <= 1000000)	//Less than 1km
				{
					ticks_in_unit = ticks_mm * 10;	//Ticks per 10m. This would be *10, but the value is 100 times larger;
					unit_string = "m";
					label_increment = 10;	//Every 10 metres
				}
				else if (timebase_length_mm <= 10000000)	//Less than 10km
				{
					ticks_in_unit = ticks_mm * 100;	//Ticks per 100m. This would be *10, but the value is 100 times larger;
					unit_string = "m";
					label_increment = 100;	//Every 100 metres
				}
				else  // Greater than 10km
				{
					ticks_in_unit = ticks_mm * 1000;	//Ticks per km. This would be *10, but the value is 100 times larger;
					unit_string = "km";
					label_increment = 1;	//Every 1km
				}

				int offset_range = (int)(ticks_in_unit / 10);
				//Make sure we're not squashing the labels too much!
				if (offset_range < 1)
				{
					offset_range = 1;
				}
				for (int x = 0; x < S.Points.Count; x+=(int)ticks_in_unit, label1+=label_increment)
				{
					startOffset = x-offset_range;
					endOffset = x+offset_range;
					//S.Points[x].AxisLabel = label1.ToString() + "cm";
					CustomLabel clb = new CustomLabel(startOffset, endOffset, label1.ToString() + unit_string, 0, LabelMarkStyle.None);
					ctTimebase.ChartAreas[0].AxisX.CustomLabels.Add(clb);
					
				}
				ctTimebase.ChartAreas[0].AxisX.Interval = (double)((int)ticks_in_unit);

				//Now we'll set the Combo Boxes of Source data of the Strip Charts to the X & Y data just loaded in.
				int y = 0;
				foreach (string src in cbSource.Items)
				{
					if (src == "X - Chan 1")
					{
						cbSource.SelectedIndex = y;
						break;
					}
					y++;
				}
				y = 0;
				foreach (string src in cbSource2.Items)
				{
					if (src == "Y - Chan 1")
					{
						cbSource2.SelectedIndex = y;
						//Trigger an axis change of the Timebase which will then draw the Phaseplane :-) Lovely Jubley
						ctTimebase_AxisViewChanged(this, new ViewEventArgs(ctTimebase.ChartAreas[0].AxisX, 0));
						break;
					}
					y++;
				}
			}
		}
		/// <summary>
		/// User wants to view a CScan file that is on the local filesystem!
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btViewCScanFile_Click(object sender, EventArgs e)
		{
			OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "C-Scan csn files (*.csn)|*.csn|All files (*.*)|*.*";
            if (ofd.ShowDialog() != DialogResult.OK || ofd.SafeFileNames.Length != 1)
                return;

			CScanViewer cscan_view = new CScanViewer(ofd.FileName);
			//Display which file we're viewing
			tbCurrentFile.Text = ofd.FileName;
			cscan_view.Show();
		}

		private void cbRemoveReverse_CheckedChanged(object sender, EventArgs e)
		{
			reverse_refresh = true;
			cbSource_SelectedIndexChanged(sender, new EventArgs());
			reverse_refresh = false;
		}

		private void ETherRealtime_Resize(object sender, EventArgs e)
		{
			if (!panelKeypad.Visible && !panelEmbedEC.Visible)
				tabControl1.Width = this.Width - 35;
			else
			{
				panelEmbedEC.Location = panelKeypad.Location;
				tabControl1.Width = panelEmbedEC.Left - 40;
			}
		}

		private void lbXY_Click(object sender, EventArgs e)
		{
			byte channel = 0;

			if (rbVictorCh2.Checked)
				channel = 1;
			else if (rbVictorCh3.Checked)
				channel = 2;

			byte[] bt = new byte[] { 4, 0x3f, 0x78, channel };	//4=Lemgth, 0x3f='?', 0x78 = x, ie What are the XY values?
			WriteToInstrument(bt, current_instrument);
		}


		private void lbFirmwareEmbedEC_Click(object sender, EventArgs e)
		{
			byte[] bt = new byte[] { 4, 0x3f, 0x56, 0 };	//4=Lemgth, 0x3f='?', 0x50 = P, ie What is the Phase?, 0=Channel
			WriteToInstrument(bt, current_instrument);
		}

		private void cbLockAxis_CheckedChanged(object sender, EventArgs e)
		{
			if (cbLockAxis.Checked)
			{
				//If the Checkbox is set then we scale the Phaseplane axis size to that of the Y axis of the Timebase
				ctPhase.ChartAreas[0].AxisY.Minimum = ctPhase.ChartAreas[0].AxisX.Minimum = ctTimebase.ChartAreas[0].AxisY.ScaleView.ViewMinimum;
				ctPhase.ChartAreas[0].AxisY.Maximum = ctPhase.ChartAreas[0].AxisX.Maximum = ctTimebase.ChartAreas[0].AxisY.ScaleView.ViewMaximum;
			}
			else
			{
				//If the Checkbox is set then we scale the Phaseplane axis size to that of the Y axis of the Timebase
				ctPhase.ChartAreas[0].AxisY.Minimum = ctPhase.ChartAreas[0].AxisX.Minimum = ctTimebase.ChartAreas[0].AxisY.Minimum;
				ctPhase.ChartAreas[0].AxisY.Maximum = ctPhase.ChartAreas[0].AxisX.Maximum = ctTimebase.ChartAreas[0].AxisY.Maximum;
			}
		}

		private void btConvertETDtoCSV_Click(object sender, EventArgs e)
		{
			if (FileData.Length == 0)
			{
				MessageBox.Show("No Data present to export.");
				return;
			}
			SaveFileDialog sfd = new SaveFileDialog();
			StreamWriter str = null;

			try
			{
				//Save the XML file
				string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
				sfd.Title = "Select Save File";
				sfd.InitialDirectory = path;
				sfd.DefaultExt = "xml";
				sfd.Filter = "csv files (*.csv)|*.csv|All files (*.*)|*.*";
				if (sfd.ShowDialog() != DialogResult.OK)
					return;

				str = new StreamWriter(sfd.FileName);

				Int32 pos = 0;

				//Lets save the headers to the file!
				foreach (GraphData gd in FileData)
				{
					if (pos++ > 0)
						str.Write(",");
					str.Write(gd.Description());
				}
				//Now we loop for all data.
				for (Int32 index = 0; index < FileData[0].data.Length; index++)
				{
					if (index == 42166)
						pos++;
					str.WriteLine();
					pos = 0;
					foreach (GraphData gd in FileData)
					{
						if (pos++ > 0)
							str.Write(",");
						str.Write(gd.data[index]);
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
			finally
			{
				if (str != null)
					str.Close();
			}

		}

		/// <summary>
		/// User has changed what must fit on the axis, so configure the axis!
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void cbFitSC1onAxis_CheckedChanged(object sender, EventArgs e)
		{
			SetGraphAxisValues();
		}
		/// <summary>
		/// User has changed what must fit on the axis, so configure the axis!
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void cbFitSC2onAxis_CheckedChanged(object sender, EventArgs e)
		{
			SetGraphAxisValues();
		}

		/// <summary>
		/// Button to start the rotary drive has been clicked!
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btRotaryStart_Click(object sender, EventArgs e)
		{
			byte[] bt;
			if (serialPortRS232.IsOpen)
				bt = new byte[]{ 3, 0, 27};	//3=length
			else
				bt = new byte[]{ 1, 0, 27};
            WriteToInstrument(bt, current_instrument);
		}
		/// <summary>
		/// Button to stop the rotary drive has been clicked!
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btRotaryStop_Click(object sender, EventArgs e)
		{
			byte[] bt;
			if (serialPortRS232.IsOpen)
				bt = new byte[]{ 3, 0, 28};	//3=length
			else
				bt = new byte[]{ 1, 0, 28};
            WriteToInstrument(bt, current_instrument);
		}
        /// <summary>
        /// A timer to "Click" the Connect/Disconenct button twice, for the purposes of automatically disconencting from the ETi bootloader
		/// and reconnecting to the Eti main code.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timerReconnect_Tick(object sender, EventArgs e)
        {
            Int32 timer_reconnect_ms = 1001;    //Time until we try to reconnect to the ETi after it jumps from Bootloader to main code.

            tbConsole.AppendText("Reconnect timer\n");
            this.Invoke(new Action(() => btConnectUSB_Click(this, new EventArgs())));

            if (timerReconnect.Interval == timer_reconnect_ms)
                timerReconnect.Stop();
            else
                timerReconnect.Interval = timer_reconnect_ms;
        }

        /// <summary>
        /// Turn ON the DLL logging.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button3_Click(object sender, EventArgs e)
		{
			if (EtherObj == null)
			{
				MessageBox.Show("Must be connected to DLL first!");
				return;
			}
			if (EtherObj.Turn_ON_Logging())
				MessageBox.Show("Logging Enabled:\nC:\\Users\\<user>\\AppData\\Local\\logging.txt");
			else
				MessageBox.Show("Logging failed to open log file.");
		}
	}
	/// <summary>
	/// Simple class to hold a sequence of numbers 0-31 that can be sent to the EmbedEC.
	/// </summary>
	public class DigitalIOSequence
	{
		byte[] data_values;
		string[] str_values;
		int array_index = -1;
		public bool loop_sequence = false;

		public DigitalIOSequence(string[] numbers, bool loop)
		{
			data_values = new byte[numbers.Length];
			str_values = new string[numbers.Length];
			array_index = -1;

			for(int x=0; x<numbers.Length; x++)
			{
				try
				{
					data_values[x] = Convert.ToByte(numbers[x], 16);
					//We have 5 bits, so 0 to 31 only permissable.
					if (data_values[x] > 31)
					{
						MessageBox.Show("0-1F only!");
						throw (new Exception());
					}
					//If we got this far, the conversion was all OK, so we can safely store the number as text as well as a byte/
					str_values[x] = data_values[x].ToString();
				}
				catch
				{
					//User had text in the sequence box, so we can assume they intended to send it, so we've shown them the error now exit.
					return;
				}
			}
			//Store whether we should loop from the last back to the first vlaue.
			loop_sequence = loop;
			array_index = 0;	//Setting this to the first value will allow the appliation to run
		}
		/// <summary>
		/// Returns the next value in the sequence, or NULL if there is no value.
		/// If the sequence is set to Loop, this is taken in to account and this function will loop.
		/// </summary>
		/// <returns></returns>
		public string GetNextValue()
		{
			if (array_index == -1 || array_index >= str_values.Length)
				return "";
			else
			{
				string val = str_values[array_index];
				//Increase and check the array_index
				if (++array_index >= str_values.Length)
				{
					if (loop_sequence)
						array_index = 0;
					else
						array_index = -1;
				}
				return val;
			}

		}

		/// <summary>
		/// Return a Comma seperated list of the sequence.
		/// </summary>
		/// <returns></returns>
		internal string GetWholeSequenceString()
		{
			if (str_values.Length == 0)
				return "";

			string seq = "";
			int x = 0;
			for (; x < str_values.Length - 1; x++)
			{
				seq += str_values[x] + ",";
			}
			seq += str_values[x];	//No comma after the last value
			return seq;
		}
	}
    /// <summary>
    /// A small structure like class that holds the data
    /// </summary>
    public class GraphData
    {
        
        UInt16 bytes_in_data_field; //How many bytes make up each data point, usually 4 or 2.
        public Int32[] data;               //Array of points.
        String description;         //Textual description of the data, can be used for graph axis etc.
        UInt32 array_index = 0;    //Keep a position of the array for when writing to it.
        public Int32 min = Int32.MaxValue, max = Int32.MinValue; //Set the Min and Max to the opposite so they WILL change.
        public UInt16 data_type = 0;   //An optional value that indicates what sort of data we're holding, ie REALTIME_PROCESSED etc. If left to 0 or REALTIME_DATA_RAW, any graph will be re-scalled.

        /// <summary>
        /// 
        /// </summary>
        /// <param name="desc"></param>
        /// <param name="no_of_points"></param>
        /// <param name="byte_count"></param>
        public GraphData(string desc, UInt32 no_of_points, UInt16 byte_count, UInt16 type)
        {
            bytes_in_data_field = byte_count;
            description = desc;
            data = new Int32[no_of_points];
            data_type = type;
        }
        public UInt16 ByteSize()
        {
            return bytes_in_data_field;
        }
        public String Description()
        {
            return description;
        }
        public bool AddPoint(Int32 value)
        {
            if (array_index >= data.Length)
                return false;
            data[array_index++] = value;
            //Store the min and maximum values in the set.
            if (value < min)
                min = value;
            if (value > max)
                max = value;
            return true;
        }
    }
    public class ButtonMessager : Button
    {
        [SecurityPermissionAttribute(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        protected override void WndProc(ref Message m)
        {
            //Trying the NEW touchscreen stuff:
            switch (m.Msg)
            {
                case Win32.WM_POINTERDOWN:
                    Console.WriteLine("WM_POINTERDOWN\n");
                    this.PerformClick();
                    break;
                case Win32.WM_POINTERUP:
                    Console.WriteLine("WM_POINTERUP\n");
                    break;
                case Win32.WM_POINTERUPDATE:
                    Console.WriteLine("WM_POINTERUPDATE\n");
                    break;
                case Win32.WM_POINTERCAPTURECHANGED:
                    Console.WriteLine("WM_POINTERCAPTURECHANGED\n");
                    break;
                case 132:
                    break;
                default:
                    base.WndProc(ref m);
                    return;
            }
            /*int pointerID = Win32.GET_POINTER_ID(m.WParam);
            Win32.POINTER_INFO pi = new Win32.POINTER_INFO();
            if (!Win32.GetPointerInfo(pointerID, ref pi))
            {
                Win32.CheckLastError();
            }
            bool processed = false;
            switch (m.Msg)
            {
                case Win32.WM_POINTERDOWN:
                    tbConsoleFileScan.AppendText("WM_POINTERDOWN\n");
                    break;
                case Win32.WM_POINTERUP:
                    tbConsoleFileScan.AppendText("WM_POINTERUP\n");
                    break;
                case Win32.WM_POINTERUPDATE:
                    tbConsoleFileScan.AppendText("WM_POINTERUPDATE\n");
                    break;
                case Win32.WM_POINTERCAPTURECHANGED:
                    tbConsoleFileScan.AppendText("WM_POINTERCAPTURECHANGED\n");
                    break;
            }*/
            base.WndProc(ref m);	    // pass message on to base form
        }
    }
}
