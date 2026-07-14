using System;
using System.Collections.Generic;
//using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;

namespace ETherCheckDataAcq
{
    public class ETherRealtimeClass
    {
        public delegate void DataReceived(Int32 X1, Int32 Y1, Int32 X2, Int32 Y2, Int32 X1mix, Int32 Ymix, Int32 Theta, Int32 Radius);
        public delegate void DataReceivedNonRealtime(Int32 X1, Int32 Y1, Int32 X2, Int32 Y2, Int32 X1mix, Int32 Ymix, Int32 Theta, Int32 Radius, Int32 Theta2, Int32 Radius2, Int32 blank1, Int32 blank2);
        public delegate void FileReceived(string file_name);
        public delegate void SetProgressBar(double percentage);
		public delegate void EC_Response(Byte command, Byte channel, Int32 value);
        //Keep a copy of the Delegate that may be provided by the calling Application:
        DataReceived data_callback;
        DataReceivedNonRealtime data_callback_non_realtime;
        FileReceived file_callback;
        SetProgressBar progress_bar_callback;
		EC_Response ec_hardware_response;
        bool logging = false;    //Just for debugging purposes, we log communications to a file.
        StreamWriter log_file;

        Int32[][] x_coords_ch1;   //An array of the X coordinates, Channel 1
        Int32[][] y_coords_ch1;   //An array of the Y coordinates, Channel 1
        Int32[][] x_coords_ch2;   //An array of the X coordinates, Channel 2
        Int32[][] y_coords_ch2;   //An array of the Y coordinates, Channel 2
        Int32[][] x_coords_chMix;   //An array of the X coordinates, Channel Mixed
        Int32[][] y_coords_chMix;   //An array of the Y coordinates, Channel Mixed

        Int32 number_of_elements = 0;   //how many data points each array can hold.
        Int32 number_of_arrays = 0;     //How many arrays we have, so we can read one while writing to another!

        Int32 write_pointer = 0;
        private System.IO.Ports.SerialPort serialPortUSB;
        private System.IO.Ports.SerialPort serialPortRS232;

        //Variables for the Data reception function:
        //////// The following is what the status byte is made up from: ////////
        public const int NOTHING = 0;
        //Least Significant Nibble
        public const int ROTARY_SYNC_PULSE_BIT = 0x01;
        public const int ALARM_BIT1 = 0x02;
        public const int ALARM_BIT2 = 0x04;
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
		public const int CSCAN_ENCODER_TRIG = 0xB0;
		public const int EC_COMMAND_ACK = 0xC0;		//Introduced for the YETi, it is the response to a command to change Phase, Gain, Freq etc.
        public const int MAX_TX_PACKET_SIZE = 64;      //The packet size expected by the instrument

        int data_transfer_state = NOTHING;  //When we receive a status byte, we set here what sort of data we are now expecting.
        int requested_data_type = NOTHING;            //Store what sort of data we've been requested to obtain from the instrument.
        int data_transfer_count = 0;        //When we know what sort of data to expect after a status byte, set this counter to that number of bytes.
        int bytes = 0;
        Int32 next_packet_offset = 0;
        Byte[] serial_data_buffer = new Byte[25600];    //25600 is 25bytes * 1024. 25 bytes is 1 header then 8 bytes for Ch1, Ch2 & Mix
        Byte[] RS232_data_buffer = new Byte[600];    //Just an arbitrary value. It is up to the host Application to read this buffer before any more RS232 data is received as this will overwrite!
        public GCHandle _gcHandleSerial_Buff;
        public GCHandle _gcHandleRS232_Buff;
        String rs232_string = "";
        Int32 data_point_count = 0;
        Int32 byte_ptr = 0;
        byte stored_status_byte = 0;        //For realtime data the status byte is important and must also be stored, so we remember it here to output to a file.
        byte vee_scan_command = 0;
        string file_name_received = "";     //When we have received a file name from the instrument, save it here!
        Int32 file_size_bytes = 0;          //We store the size of a file as the instrument transmitts this information
        Int32 full_file_size = 0;           //We remember the size of a file we are about to receive so we can calculate the percentage received.
        FileStream fs_file, fs; //fs = filestream for realtime logging. fs_file = filestream for file upload
        byte packet_size = 0;
        StringBuilder str_bldr = new StringBuilder();           //A string builder used to convert the incoming bytes to a string then display as the settings XML.
        Int32[] values = new Int32[12];    //Data read from each packet
        //bool reset_write_pointer = false;   //Flag to tell the receiving thread to reset the pointer next time it writes.
        Int32 reading_counter = 0;      //To help debugging the LabView code, use an incrementing counter.
        Int32 request_counter = 0;      //As above, but only increment each time we are asked for data.

        Int32 current_array_writing = 0;
        Int32 current_array_reading = 0;
        Int32[] points_written = new Int32[4];
        String error_string = "";
        Int32 last_packet_received_time;
        DateTime last_call_time = DateTime.Now; //Use this as a timer for how often we are asked for Data from the host.
        byte return_raw_port_data = 0;      //If we are asked to, we return all the data we receive, not process it and return it in nice packets. This is the number of bytes in each packet. Can be 0 (off), 1, 2, 4.
        bool beadseat_comp_on = false;
        bool data_transfer_paused = false;
        /// <summary>
        /// Create the object and we are passed an array of data points created by the host.
        /// These arrays are multi-dimensional so that one can be being written too while the host applciation is reading/processing the next.
        /// Read/process data too slowly and data cna be overwritten!
        /// The buffer that is currently being written too is returned when RequestData is called
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public ETherRealtimeClass(Int32[][] x1, Int32[][] y1, Int32[][] x2, Int32[][] y2, Int32[][] xMix, Int32[][] yMix, Int32 arrays, Int32 size_of_arrays, Int32 data_type)
        {
            if (data_type == REALTIME_DATA_RAW || data_type == REALTIME_DATA_POSTPROCESS || data_type == SINGLE_CHAN_POST || data_type == CONDUCTIVITY)
                requested_data_type = data_type;
            if (x1 != null)
                x_coords_ch1 = x1;
            if (y1 != null)
                y_coords_ch1 = y1;
            if (x2 != null)
                x_coords_ch2 = x2;
            if (y2 != null)
                y_coords_ch2 = y2;
            if (xMix != null)
                x_coords_chMix = xMix;
            if (yMix != null)
                y_coords_chMix = yMix;

            number_of_elements = size_of_arrays;
            number_of_arrays = arrays;
        }
        /// <summary>
        /// We create the storage arrays and handle the memory our selves.
        /// </summary>
        /// <param name="size_of_arrays"></param>
        /// <param name="data_type">REALTIME_DATA_RAW, REALTIME_DATA_POSTPROCESS,  or SINGLE_CHAN_POST</param>
        public ETherRealtimeClass(Int32 size_of_arrays, Int32 data_type)
        {
            _gcHandleSerial_Buff = GCHandle.Alloc(serial_data_buffer, GCHandleType.Pinned);    //25600 is 25bytes * 1024. 25 bytes is 1 header then 8 bytes for Ch1, Ch2 & Mix
            _gcHandleRS232_Buff = GCHandle.Alloc(RS232_data_buffer, GCHandleType.Pinned);    //Just an arbitrary value. It is up to the host Application to read this buffer before any more RS232 data is received as this will overwrite!

            number_of_elements = size_of_arrays;

            if (size_of_arrays == 0)    //Therefore there are to be NO arrays for storing data an we assume a Call back metho will be set!
            {
                if (logging)
                    log_file.WriteLine("Class constructed with 0 length Array (callback?).");
                return;
            }           

            if (data_type == REALTIME_DATA_RAW || data_type == REALTIME_DATA_POSTPROCESS || data_type == SINGLE_CHAN_POST || data_type == CONDUCTIVITY)
                requested_data_type = data_type;

            number_of_arrays = 4;
			x_coords_ch1 = new Int32[number_of_arrays][];
			x_coords_ch2 = new Int32[number_of_arrays][];
			y_coords_ch1 = new Int32[number_of_arrays][];
			y_coords_ch2 = new Int32[number_of_arrays][];
			x_coords_chMix = new Int32[number_of_arrays][];
			y_coords_chMix = new Int32[number_of_arrays][];
            for (int x = 0; x < number_of_arrays; x++)
            {
                x_coords_ch1[x] = new Int32[size_of_arrays];
                y_coords_ch1[x] = new Int32[size_of_arrays];
                x_coords_ch2[x] = new Int32[size_of_arrays];
                y_coords_ch2[x] = new Int32[size_of_arrays];
                x_coords_chMix[x] = new Int32[size_of_arrays];
                y_coords_chMix[x] = new Int32[size_of_arrays];
            }
            if (logging)
                log_file.WriteLine("Class constructed.");
            /*for (int x = 0; x < 4; x++)
            {
                points_written[x] = 0;

                x_coords_ch1[x] = new Int32[size_of_arrays];
                y_coords_ch1[x] = new Int32[size_of_arrays];

                if (data_type == SINGLE_CHAN_POST || data_type == CONDUCTIVITY)  //Single channel post processed only requires 2 arrays.
                    return;

                x_coords_ch2[x] = new Int32[size_of_arrays];
                y_coords_ch2[x] = new Int32[size_of_arrays];

                if (data_type == REALTIME_DATA_RAW) //Realtime raw data returns 2 channels of data
                    return;

                //Full post processed data requires 3 channels.
                x_coords_chMix[x] = new Int32[size_of_arrays];
                y_coords_chMix[x] = new Int32[size_of_arrays];
            }
            */
        }
        ~ETherRealtimeClass()
        {
            CloseSerialConnection();
            if (logging)
            {
             //   log_file.Flush();
                log_file.Close();
            }
        }

		/// <summary>
        /// Once this is called, and ec_hardware_response (our local copy of the delegate) is no longer null, we can use
        /// the call back to pass command responses directly back to the host.
        /// </summary>
        /// <param name="callback"></param>
        public void RegisterECResponseCallback(EC_Response callback)
        {
            ec_hardware_response = callback;
        }
        /// <summary>
        /// Once this is called, and data_callback (our local copy of the delegate) is no longer null, we can use
        /// the call back to pass data directly back to the host.
        /// </summary>
        /// <param name="callback"></param>
        public void RegisterDataCallback(DataReceived callback)
        {
            data_callback = callback;
        }
        /// <summary>
        /// Once this is called, and data_callback_non_realtime (our local copy of the delegate) is no longer null, we can use
        /// the call back to pass data directly back to the host.
        /// This is a special one that can return 12 values back, speed is not an issue for non realtime.
        /// The normal callback (DataReceived) can still receive non_realtime data, but only 8 values.
        /// </summary>
        /// <param name="callback"></param>
        public void RegisterDataCallbackNonRealtime(DataReceivedNonRealtime callback)
        {
            data_callback_non_realtime = callback;
        }
        /// <summary>
        /// Once this is called, and file_callback (our local copy of the delegate) is no longer null, we can use
        /// the call back to pass file names directly back to the host.
        /// </summary>
        /// <param name="callback"></param>
        public void RegisterFileCallback(FileReceived callback)
        {
            file_callback = callback;
        }
        /// <summary>
        /// Once this is called, and progress_bar_callback (our local copy of the delegate) is no longer null, we can use
        /// the call back to pass the progress of file uploads to the host.
        /// </summary>
        /// <param name="callback"></param>
        public void RegisterProgressCallback(SetProgressBar callback)
        {
            progress_bar_callback = callback;
        }

        /// <summary>
        /// Return any error string then reset it to "".
        /// </summary>
        /// <returns></returns>
        public String GetError()
        {
            String temp = error_string;
            if (temp == "")
                temp = "No Error - Last Packet at " + last_packet_received_time.ToString() + "s. Data Type:" + data_transfer_state.ToString();
            error_string = "";

            return temp;
        }
        /// <summary>
        /// Return any RS232 data that has been received.
        /// </summary>
        /// <returns></returns>
        public String GetSerialData()
        {
            String temp = rs232_string;
            rs232_string = "";
            return temp;
        }
        /// <summary>
        /// Turn on text logging. The file is in the Local Application Data folder and is called logging.txt
        /// </summary>
        /// <returns>TRUE - File opened OK.</returns>
        public bool Turn_ON_Logging()
        {
            try
            {
                log_file = new StreamWriter(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\logging.txt");
            }
            catch
            {
                return false;
            }
            logging = true;
            return true;
        }
        /// <summary>
        /// Return what the current format of the data is that we are receiving. This is from the usual list of:
        ///         public const int FILE_SIZE = 0x10;
        //  FILE_DATA = 0x20;
        //  FILE_NAME = 0x30;
        //  REALTIME_DATA_RAW = 0x40;
        //  REALTIME_DATA_POSTPROCESS = 0x50;
        //  XML_HEADER = 0x60;
        //  SINGLE_CHAN_POST = 0x70;
        //  NON_REALTIME = 0x80;
        //  CONDUCTIVITY = 0x90;
        //  VEE_SCAN_RESPONSE = 0xA0
		//	EC_COMMAND_ACK
        /// </summary>
        /// <returns></returns>
        public UInt16 GetCurrentDataFormat()
        {
            return (UInt16)data_transfer_state;
        }
        /// <summary>
        /// Return a version number.
        /// </summary>
        /// <returns></returns>
        public String GetVersion()
        {
            //!!!!!!!!!!!! If changing the version number of the DLL for functionality related to EtherMap, check the function ConnectToDLL() as it contains ETherMaps requried version of the DLL

            return "1.1.6";	//10/4/2024 - the variable data_transfer_paused stops ALL data comms, including file transfers!! This command now simply doesn't call the callback data_callback_non_realtime()
            //return "1.1.5";   //Commands PauseRealtimeData() and ContinueRealtimeData() for ETherMap to call
            //return "1.1.4";	//A new callback to allow the hardware to respond to a command, ec_hardware_response. Can send back a byte (command ID) and an Int32 (copy of the acknowledged command value).
            //return "1.1.3";	//The old LabView method of getting the latest data (GetCopyOfLatestData()) was broken, now fixed.
            //return "1.1.2";	//The default folder that files are written to (when received from the instrument) has changed to 
            //return "1.1.1";   //There is a new call back, DataReceivedNonrealtime. This, if present, will return 12 values if Non realtime data is required. The usual callback (DataReceived) will still receive 8 values for NonRealtime if the new one is NULL.#return "1.1.0";     //There is a new call back, DataReceivedNonrealtime. This, if present, will return 12 values if Non realtime data is required. The usual callback (DataReceived) will still receive 8 values for NonRealtime if the new one is NULL.
            //return "1.1.0";   //There is a new call back, DataReceivedNonrealtime. This, if present, will return 12 values if Non realtime data is required. The usual callback (DataReceived) will still receive 8 values for NonRealtime if the new one is NULL.#return "1.1.0";     //There is a new call back, DataReceivedNonrealtime. This, if present, will return 12 values if Non realtime data is required. The usual callback (DataReceived) will still receive 8 values for NonRealtime if the new one is NULL.
            //return "1.0.1";   //Can now return Non-Realtime data format from the DLL: X1, Y1, X2, Y2, Theta, Radius, X%, Y%
            //return "1.0.0";   //We now have the added option of passing in a call-back function that is called each time a new set of data points is received. THIS IS THE IDEAL METHOD of receiving realtime data! All arrays are now[][] rather than [,].
            //return "0.0.8";   //Release for the NAVAIR VeeScans. Returns fixed values for the mixed channel when in VeeScna mode. Has new logging. Log file and instrument settings written to host application folder.
            //return "0.0.7d";  //Still debugging but need a new version number!
            //return "0.0.6d";  //Debugging to fix labView Veescan issues.
            //return "0.0.5";   //This version includes logging to a file if the logging boolean is TRUE.
            //return "0.0.4";
        }
        /// <summary>
        /// Return a version number.
        /// </summary>
        /// <returns></returns>
        public String GetVeeScanResponse()
        {
            string response = "";
            switch (vee_scan_command)
            {
                case 0:
                    response = "Nothing";
                    break;
                case 5:
                    response = "VeeScanSettingsLoaded";
                    vee_scan_command = 0;               //Reset the stored command.
                    break;
                case 6:
                    response = "AutoPhaseReady";
                    vee_scan_command = 0;               //Reset the stored command.
                    break;
                case 7:
                    response = "AutoPhaseComplete";
                    vee_scan_command = 0;               //Reset the stored command.
                    break;
            }
            if (logging && response != "" && response != "Nothing")
                log_file.WriteLine("Get Response returned: " + response);
            return response;
        }
  /*      public void GetBufferPointers(ref Int32[] x1, ref Int32[] y1, ref Int32[] x2, ref Int32[] y2, ref Int32[] xMix, ref Int32[] yMix)
        {
            x1 = x_coords_ch1;
            y1 = y_coords_ch1;
            x2 = x_coords_ch2;
            y2 = y_coords_ch2;
            xMix = x_coords_chMix;
            yMix = y_coords_chMix;
        }*/
        /// <summary>
        /// Instead of handling the memory nicely using pointers provided or by returning our own, we are doing things the slower (but more reliable) way
        /// of copying all of the latest data in to provided arrays. This is primarily for the benefit of labView.
        /// The function will only return any data an array is FULL UP adn would therefore the number of points returned will be the array size. Otherwise the function
        /// returns -1.
        /// </summary>
        /// <param name="x1"></param>
        /// <param name="y1"></param>
        /// <param name="x2"></param>
        /// <param name="y2"></param>
        /// <param name="xMix"></param>
        /// <param name="yMix"></param>
        /// <returns></returns>
        public int GetCopyOfLatestData(Int32[] x1, Int32[] y1, Int32[] x2, Int32[] y2, Int32[] xMix, Int32[] yMix)
        {
            int x;

            if (current_array_reading == current_array_writing)
            {
                //if (logging)
                //    log_file.WriteLine("Reading array = Writing array!");
                return -1;
            }
            if (x1 != null)
            {
                for (x = 0; x < points_written[current_array_reading]; x++)
                {
                    x1[x] = x_coords_ch1[current_array_reading][x];
                  //  if (logging && (x1[x] > 1000000 || x1[x] < -1000000))
                  //      log_file.WriteLine("x1 FAIL at " + x.ToString() + ", value " + x1[x].ToString());

                }
            }
            if (y1 != null)
            {
                for (x = 0; x < points_written[current_array_reading]; x++)
                {
                    y1[x] = y_coords_ch1[current_array_reading][x];
                   // if (logging && (y1[x] > 1000000 || y1[x] < -1000000))
                   //     log_file.WriteLine("y1 FAIL at " + x.ToString() + ", value " + y1[x].ToString());
                }
            }
            if (x2 != null)
            {
                for (x = 0; x < points_written[current_array_reading]; x++)
                    x2[x] = x_coords_ch2[current_array_reading][x];
            }
            if (y2 != null)
            {
                for (x = 0; x < points_written[current_array_reading]; x++)
                    y2[x] = y_coords_ch2[current_array_reading][x];
            }
            if (xMix != null)
            {
                for (x = 0; x < points_written[current_array_reading]; x++)
                    xMix[x] = x_coords_chMix[current_array_reading][x];
            }
            if (yMix != null)
            {
                for (x = 0; x < points_written[current_array_reading]; x++)
                    yMix[x] = y_coords_chMix[current_array_reading][x];
            }

            //Store ready to return the number of points, then set to 0 for that array.
            int temp = points_written[current_array_reading];
            points_written[current_array_reading] = 0;

            if (++current_array_reading == number_of_arrays)
                current_array_reading = 0;

           // if (logging)
           //     log_file.WriteLine(DateTime.Now - last_call_time);

            last_call_time = DateTime.Now;
            request_counter++;          //This is simply a counter of how many times we've been asked for data, for debugging the LabView.

            return temp;
        }
        /// <summary>
        /// This function is used when the DLL is using arrays that were provided by the host application.
        /// It returns the number of points that have been written to the array, while storing which array in array_num the latest data is in.
        /// </summary>
        /// <param name="array_num"></param>
        /// <returns></returns>
        public Int32 RequestData(ref Int32 array_num)
        {
            //Store ready to return the number of points, then set to 0 for that array.
            int temp = points_written[current_array_reading];
            //We return the array number that the host application should read from.
            array_num = current_array_reading;

            points_written[current_array_reading] = 0;

            if (++current_array_reading == number_of_arrays)
                current_array_reading = 0;

           // if (logging)
            //    log_file.WriteLine(DateTime.Now - last_call_time);

            last_call_time = DateTime.Now;
            request_counter++;          //This is simply a counter of how many times we've been asked for data, for debugging the LabView.

            return temp;
        }
        public bool OpenSerialConnection(Int32 com_port_number)
        {
            serialPortUSB = new System.IO.Ports.SerialPort();
            serialPortUSB.BaudRate = 115200;
            serialPortUSB.ReadTimeout = 500;
            serialPortUSB.DataBits = 8;
            serialPortUSB.Parity = System.IO.Ports.Parity.None;
			serialPortUSB.StopBits = System.IO.Ports.StopBits.One;
			serialPortUSB.Handshake = System.IO.Ports.Handshake.None;
            serialPortUSB.ReadBufferSize = 8192;
            serialPortUSB.WriteBufferSize = 2048;
            serialPortUSB.RtsEnable = false;
            serialPortUSB.ReceivedBytesThreshold = 1;
            //serialPortUSB.DataReceived +=new System.IO.Ports.SerialDataReceivedEventHandler(serialPortUSB_DataReceived);
            serialPortUSB.PortName = "COM" + com_port_number.ToString();
			try
			{
				serialPortUSB.Open();

				if (return_raw_port_data > 0 && serialPortUSB.IsOpen)
				{
					ThreadPool.QueueUserWorkItem(handleRawBytes);
					return serialPortUSB.IsOpen;
				}
				else
				{
					//Tell the instrument what sort of data we'd like
					switch (requested_data_type)
					{
						case CONDUCTIVITY:
							WriteToInstrument(1, 0, "<USB_OUTPUT>9</USB_OUTPUT>");  //Set to output Single channel post processed data
							break;
						case SINGLE_CHAN_POST:
							WriteToInstrument(1, 0, "<USB_OUTPUT>7</USB_OUTPUT>");  //Set to output Single channel post processed data
							break;
						case REALTIME_DATA_POSTPROCESS:
							WriteToInstrument(1, 0, "<USB_OUTPUT>5</USB_OUTPUT>");  //Set to output Single channel post processed data
							break;
						case REALTIME_DATA_RAW:
							WriteToInstrument(1, 0, "<USB_OUTPUT>4</USB_OUTPUT>");  //Set to output Single channel post processed data
							break;
						case NON_REALTIME:
							WriteToInstrument(1, 0, "<USB_OUTPUT>8</USB_OUTPUT>");  //Set to NON realtime data, which on the YETi is what we are using.
							break;
					}
				}
			}
			catch (SystemException xe)
			{
				error_string = xe.Message;
			};
            if (serialPortUSB.IsOpen)
                ThreadPool.QueueUserWorkItem(handleReceivedBytes);

            if (logging)
                log_file.WriteLine("Coms Opened, is now:" + (serialPortUSB.IsOpen?"OPEN":"CLOSED"));
            return serialPortUSB.IsOpen;
        }

        public bool OpenSerialConnection(Int32 com_port_number, UInt32 baud_rate)
        {
            serialPortRS232 = new System.IO.Ports.SerialPort();
            serialPortRS232.BaudRate = (int)baud_rate;
            serialPortRS232.ReadTimeout = 500;
            serialPortRS232.DataBits = 8;
            serialPortRS232.Parity = System.IO.Ports.Parity.None;
            serialPortRS232.ReadBufferSize = 8192;
            serialPortRS232.WriteBufferSize = 2048;
            serialPortRS232.RtsEnable = false;
            serialPortRS232.ReceivedBytesThreshold = 1;
            serialPortRS232.DataReceived +=new System.IO.Ports.SerialDataReceivedEventHandler(serialPortRS232_DataReceived);
            serialPortRS232.PortName = "COM" + com_port_number.ToString();

            if (serialPortRS232.IsOpen)
                serialPortRS232.Close();

            serialPortRS232.Open();

            //if (serialPortRS232.IsOpen)
            //    ThreadPool.QueueUserWorkItem(handleRS232ReceivedBytes);

            if (logging)
                log_file.WriteLine("RS232 Coms is now:" + (serialPortUSB.IsOpen ? "OPEN" : "CLOSED"));
            return serialPortRS232.IsOpen;
        }
        //public System.IO.Ports.SerialDataReceivedEventHandler serialPortUSB_DataReceived { get; set; }
        private void serialPortUSB_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            this.serialPortUSB.DataReceived -= serialPortUSB_DataReceived;
            //debugging = true;
            //This menthod will read and write as much as possible to speed things up!               
            ThreadPool.QueueUserWorkItem(handleReceivedBytes);
        }
        private void serialPortRS232_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            StringBuilder stb = new StringBuilder();
            int length = serialPortRS232.Read(RS232_data_buffer, 0, RS232_data_buffer.Length);

            for (int x = 0; x < length; x++)
            {
                stb.Append((RS232_data_buffer[x]).ToString());
                if (x < length-1)
                    stb.Append(',');
            }
            rs232_string = stb.ToString();
        }
        public bool IsUSBConnected()
        {
            return serialPortUSB.IsOpen;
        }
        public void CloseSerialConnection()
        {
            try
            {
                if (serialPortUSB != null && serialPortUSB.IsOpen)
                    serialPortUSB.Close();
                if (serialPortRS232 != null && serialPortRS232.IsOpen)
                    serialPortRS232.Close();
            }
            catch { }
			if (logging)
			{
				log_file.WriteLine("Coms CLOSED.");
				log_file.Close();
			}
        }
        public void ClickUP()
        {
            byte[] bt = { 1, 0, 3 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: UP");
        }
        public void ClickDOWN()
        {
            byte[] bt = { 1, 0, 4 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: DOWN");
        }
        public void ClickBALANCE1()
        {
            byte[] bt = { 1, 0, 8 };
            
            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: BAL1");
        }
        public void BeadseatCompensationOFF()
        {
            byte[] bt = { 1, 0, 26 };
            beadseat_comp_on = false;

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Beadseat Comp: OFF");
        }
        public void BeadseatCompensationON()
        {
            byte[] bt = { 1, 0, 25 };
            beadseat_comp_on = true;    //We remember this state as it allows us to check the raw data better.

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Beadseat Comp: ON");
        }

        private void handleRawBytes(object state)
        {
            Thread.CurrentThread.Name = "HandleRawBytes";

            int[] bst = new int[100];
            int pos = next_packet_offset;
            int serial_buffer_offset = 0;

            data_transfer_count = 0;

            try
            {
                while (serialPortUSB.IsOpen)
                {
                    pos = 0;

                    try
                    {
                        bytes = 0;  //Must reset this first as if jumps out due to TimeoutException, still has a value
                        bytes = serialPortUSB.Read(serial_data_buffer, serial_buffer_offset, 25600 - serial_buffer_offset);                 //Read data from the VCP
                        if (bytes == 0)
                        {
                            System.Threading.Thread.Sleep(10);
                            continue;
                        }
                        if (bytes + serial_buffer_offset < values.Length * return_raw_port_data)   //We don't have enough data to fill the values array, so store it and wait for more data
                        {
                            serial_buffer_offset = bytes;
                            continue;
                        }
                        serial_buffer_offset = 0;
                    }
                    catch (System.TimeoutException)
                    {
                    }
                    catch (System.Exception f)
                    {
                        error_string = f.Message;
                    }

                    last_packet_received_time = DateTime.Now.Second;
                    data_point_count += bytes;                                              //Keep count how many bytes we receive

                    //We loop through the received bytes and use them to populate the "values" array.
                    //This array is of UInt32s, so the variable return_raw_port_data is how many bytes we want to put in each element (1, 2 or 4)
                    for (pos = 0; pos < bytes && (pos / return_raw_port_data)<values.Length; pos++)
                        values[pos / return_raw_port_data] += serial_data_buffer[pos] * (Int32)Math.Pow(256, pos % return_raw_port_data);
                    //Fill the remaining slots with 0.
                    for (; (pos / return_raw_port_data) < values.Length; pos++)
                        values[pos / return_raw_port_data] = 0;

                    data_callback_non_realtime(values[0], values[1], values[2], values[3], values[6], values[7], values[4], values[5], values[8], values[9], values[10], values[11]);
                    values[0] = values[1] = values[2] = values[3] = values[6] = values[7] = values[4] = values[5] = values[8] = values[9] = values[10] = values[11] = 0;


                }
            }
            catch
            {
                int ian = 4;
            }
        }
    /*    private void handleRS232ReceivedBytes(object state)
        {
            int serial_buffer_offset = 0;       //If there are unprocessed bytes at the end of a read from the serial port (part of a packet) this is copied to the start of the bufffer and the next read written after it.

            int pos = 0;
            try
            {
                while (serialPortRS232.IsOpen)
                {
                    pos = 0;

                    try
                    {
                        bytes = 0;  //Must reset this first as if jumps out due to TimeoutException, still has a value
                        bytes = serialPortRS232.Read(serial_data_buffer, serial_buffer_offset, 25600 - serial_buffer_offset);                 //Read data from the VCP
                        if (bytes == 0)
                        {
                            System.Threading.Thread.Sleep(10);
                        }
                    }
                    catch (System.TimeoutException e)
                    {
                    }
                    catch (System.Exception f)
                    {
                        error_string = f.Message;
                    }

                    if (bytes == 0)
                        continue;
                    error_string = bytes.ToString();
                }
            }
            catch (SystemException e)
            {
                // tbConsole.Invoke(new Action(() => tbConsole.AppendText(e.Message + "\n")));
                // btConnectUSB.Invoke(new Action(() => btConnectUSB.Text = "Connect"));
                if (serialPortRS232 != null && serialPortRS232.IsOpen)
                {
                    try
                    {
                        serialPortRS232.Close();
                    }
                    catch
                    { }
                }
            }
        }
      */
        private void handleReceivedBytes(object state)
        {
            Thread.CurrentThread.Name = "HandleReceivedBytes";
            /*const byte eNO_USB_OUTPUT = 0;
            const byte eFILE_SYSTEM = 1;
            const byte eFILE_DATA = 2;
            const byte eREALTIME_RAW = 3;
            const byte eREALTIME_POSTPROCESS = 4;*/
            int[] bst = new int[100];
            int pos = next_packet_offset;
            int serial_buffer_offset = 0;       //If there are unprocessed bytes at the end of a read from the serial port (part of a packet) this is copied to the start of the bufffer and the next read written after it.
            byte status_byte = 0;
            data_transfer_count = 0;
            int status_1 = 0, status_2 = 0; //Must be global in this funciton otherwise they were getting forgotten once a packet wrapped round!

            try
            {
                while (serialPortUSB.IsOpen)
                {
                    pos = 0;

                    try
                    {
                        bytes = 0;  //Must reset this first as if jumps out due to TimeoutException, still has a value
                        bytes = serialPortUSB.Read(serial_data_buffer, serial_buffer_offset, 25600 - serial_buffer_offset);                 //Read data from the VCP
						if (bytes == 0)
						{
							System.Threading.Thread.Sleep(10);
						}
						//else if (logging)
						//	log_file.BaseStream.Write(serial_data_buffer, serial_buffer_offset, bytes);
                    }
                    catch (System.TimeoutException)
                    {
                    }
                    catch (System.Exception f)
                    {
                        error_string = f.Message;
                    }

                    if (bytes == 0)
                        continue;

                    last_packet_received_time = DateTime.Now.Second;
                    data_point_count += bytes;                                              //Keep count how many bytes we receive
                    bytes += serial_buffer_offset;  //We must process all bytes read PLUS what was left over from before!
                    
                    serial_buffer_offset = 0;

                    /**********************************************************************************************************
                     Processing of data is handled in 3 sections.
                     * 1. The start of a new "packet" which starts with the status byte and the inverse of the status byte.
                     * 2. Processing data within a packet.
                     * 3. The packet is finished, handle any processing before the next packet is processed.
                     ***********************************************************************************************************/

                    while (pos < bytes)     //Continue while the current byte position is less than the number we read from the VCP port.
                    {
                        /********************************************************************************************
                        * 3. The packet is finished, handle any processing before the next packet is processed.     *
                        ********************************************************************************************/

                        if (data_transfer_count == 0)   //A previous action has counted down and been fully processed, so handle any clearing up at the end of the packet.
                        {
                            if (data_transfer_state == FILE_SIZE)   //We have received the full file size
                            {
                                //tbConsole.Invoke(new Action(() => tbConsole.AppendText("File Length: " + file_size_bytes.ToString() + "\n")));
                                //progressBar1.Invoke(new Action(() => progressBar1.Maximum = file_size_bytes));
                                full_file_size = file_size_bytes;
								if (logging)
									log_file.WriteLine("FILE_SIZE" + full_file_size.ToString() + "\n");
                            }
                            else if (data_transfer_state == FILE_DATA)   //We have received the full file!
                            {
                                fs_file.Close();
								if (logging)
									log_file.WriteLine("FILE_DATA fully received!\n");
                                //tbConsole.Invoke(new Action(() => tbConsole.AppendText("File Received. \n")));
                                //progressBar1.Invoke(new Action(() => progressBar1.Value = 0));
                                try
                                {
                                    progress_bar_callback(0);
                                }
                                catch (SystemException)
                                {
                                    int ian = 4;
                                }

                                if (file_size_bytes == -1)
                                    file_size_bytes = -2;   //We set this to -2 here so only when this is the case will a file be saved.
                            }
                            else if (data_transfer_state == FILE_NAME)   //We have received the full file name, so we can now save it.
                            {
                                //prevent any spurious file saves!
                                if (file_size_bytes == -2)
                                {
                                    //Call the function ProcessFileSystem on the main thread.
                                    //if (file_name_received == "FS.txt") //Indicates this is the file system description.
                                    try
                                    {
										if (logging)
											log_file.WriteLine("FILE_NAME" + file_name_received + "\n");
                                        string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                                        //We must remove any file that has the name of what we have just received.
                                        File.Delete(path + "\\" + file_name_received);
                                        //Then move the uploaded file to the name provided.
                                        File.Move(path + "\\temp_upload.dat", path + "\\" + file_name_received);
                                        if (file_callback != null)
                                            file_callback(path + "\\" + file_name_received);
                                        //this.Invoke(new Action(() => ProcessFileSystem()));
                                    }
                                    catch (System.Exception se)
                                    {
                                        error_string = "File Move error: " + se.Message;
                                        if (logging)
                                            log_file.WriteLine(error_string);
                                    }
                                    file_size_bytes = 0;
                                }
                            }
                            else if (data_transfer_state == XML_HEADER)
                            {
                                //We have received the header so now close the file as we only want to keep the header and NOT to log realtime data.
                                if (fs != null && fs.CanWrite)
                                {
                                    fs.WriteByte(0x0D); //Output a Carriage Return
                                    fs.WriteByte(0x00); //Output a NULL.
                                    fs.Close();
                                    //Let anyone connected to us know there is a new file!
                                    if (file_callback != null)
                                        file_callback(fs.Name);
                                    fs = null;
                                }

                            }
                            data_transfer_state = 0;    //Since we've actioned the end of a state, clear the data_transfer_state

                            /********************************************************************************************************
                            * 1. The start of a new "packet" which starts with the status byte and the inverse of the status byte.  *
                            ********************************************************************************************************/
                            status_byte = serial_data_buffer[pos];

                            if (pos >= bytes - 1)   //We can't read the next byte to check if it is the inverse of the status byte, so remember what we have just received!
                            {
                                serial_data_buffer[0] = status_byte;
                                serial_buffer_offset = 1;
                                pos++;
                                continue;
                            }
                            if (status_byte != (0xff & (~(serial_data_buffer[++pos]))))
                            {
                                //The check failed :-(
                                status_byte = 0;
                                //invalid_packet++;
                                //tbInvalidPacket.Invoke(new Action(() => tbInvalidPacket.Text = invalid_packet.ToString()));
                                continue;
                            }
                            pos++;  //We have checked the 2nd status byte (the inverse) and it was OK, so skip past it.

                            data_transfer_state = NOTHING;

                            //Set this to 0 before we then set it's correct value depending on the data we expect.
                            packet_size = 0;
                            //Now check the status byte!
                            if ((status_byte & 0xF0) == FILE_DATA)  //Prepare to receive file data!
                            {
                                if (file_size_bytes > 0)
                                {
									if (logging)
                                            log_file.WriteLine("FILE_DATA starting.");
                                    data_transfer_state = FILE_DATA;
                                    data_transfer_count = file_size_bytes;
                                    file_size_bytes = -1;    //Use this is a flag to indicate that we are receiving file data
                                    //Open a file to upload to!
                                    string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                                    if (fs_file != null)
                                        fs_file.Close();
                                    fs_file = new FileStream(path + "\\temp_upload.dat", FileMode.Create);
                                }
                                else
                                {
                                    data_transfer_count = 0;
                                    data_transfer_state = NOTHING;
                                }
                            }
                            else if ((status_byte & 0xF0) == FILE_NAME)
                            {
								if (logging)
									log_file.WriteLine("FILE_NAME starting.");
                                file_name_received = "";
                                data_transfer_state = FILE_NAME;
                                data_transfer_count = 256;  //Maximum of 256 chars for the filename, but we check for a NULL.
                            }
                            else if ((status_byte & 0xF0) == FILE_SIZE)
                            {
								if (logging)
                                            log_file.WriteLine("FILE_SIZE starting.");
                                file_size_bytes = 0;
                                data_transfer_state = FILE_SIZE;
                                data_transfer_count = 10;
                            }
                            else if ((status_byte & 0xF0) == XML_HEADER)
                            {
								if (logging)
									log_file.WriteLine("latest_settings.xml expecting " + file_size_bytes.ToString() + "bytes \n");

                                data_transfer_state = XML_HEADER;
                                data_transfer_count = file_size_bytes;
                                file_size_bytes = 0;    //Reset this ASAP so we can prevent any spurious file saves!
                                str_bldr = new StringBuilder();

                                string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                                if (fs != null)
                                    fs.Close();
                                try
                                {
                                    fs = new FileStream(path + "\\latest_settings.xml", System.IO.FileMode.Create);
                                }
                                catch (SystemException e)   //If we fail to open the ifle, set the button back to a RED background.
                                {
                                    fs = null;
									if (logging)
										log_file.WriteLine("latest_settings.xml FAILED " + e.Message + "\n");
                                    //btLogging.Invoke(new Action(() => btLogging.BackColor = Color.Red));
                                }
                            }
                            else if ((status_byte & 0xF0) == REALTIME_DATA_RAW)
                            {
                                stored_status_byte = status_byte;   //We must store the status byte to output to the file later. It may contain other data like sync pulses etc.
                                data_transfer_state = REALTIME_DATA_RAW;
                                packet_size = 18;//2 sets of XY pairs of 8 bytes each (4 for X and 4 for Y).
                            }
                            else if ((status_byte & 0xF0) == REALTIME_DATA_POSTPROCESS)
                            {
                                stored_status_byte = status_byte;   //We must store the status byte to output to the file later. It may contain other data like sync pulses etc.
                                data_transfer_state = REALTIME_DATA_POSTPROCESS;
                                packet_size = 26;
                            }
                            else if ((status_byte & 0xF0) == SINGLE_CHAN_POST)
                            {
                                stored_status_byte = status_byte;   //We must store the status byte to output to the file later. It may contain other data like sync pulses etc.
                                data_transfer_state = SINGLE_CHAN_POST;
                                packet_size = 18;
                            }
                            else if ((status_byte & 0xF0) == CONDUCTIVITY)
                            {
                                stored_status_byte = status_byte;   //We must store the status byte to output to the file later. It may contain other data like sync pulses etc.
                                data_transfer_state = CONDUCTIVITY;
                                packet_size = 18;
                            }
                            else if ((status_byte & 0xF0) == VEE_SCAN_RESPONSE)
                            {
                                stored_status_byte = status_byte;   //We must store the status byte to output to the file later. It may contain other data like sync pulses etc.
                                data_transfer_state = VEE_SCAN_RESPONSE;
                                packet_size = 4;
                            }
							else if ((status_byte & 0xF0) == EC_COMMAND_ACK)
							{
                                stored_status_byte = status_byte;   //We must store the status byte to output to the file later. It may contain other data like sync pulses etc.
                                data_transfer_state = EC_COMMAND_ACK;
                                packet_size = 7;	//Response always 7 bytes: 2 STATUS BYTES, COMMAND, 4 bytes for Data.
                            }
							else if ((status_byte & 0xF0) == CSCAN_ENCODER_TRIG)
                            {
                                stored_status_byte = status_byte;   //We must store the status byte to output to the file later. It may contain other data like sync pulses etc.
                                data_transfer_state = CSCAN_ENCODER_TRIG;
                                packet_size = 34;
                            }
                            else if ((status_byte & 0xF0) == NON_REALTIME)
                            {
                                stored_status_byte = status_byte;   //We must store the status byte to output to the file later. It may contain other data like sync pulses etc.
                                data_transfer_state = NON_REALTIME;
                                //We will always send 50 bytes (12 4-byte words and then some blank). This allows for easier future expansion!
                                packet_size = 50;   //2 status bytes + 4-bytes for: X1, Y1, X2, Y2, Theta, Radius, X%, Y%, Theta2, Radius2
                            }
                            else    // we have an invalid status byte
                            {
                               // invalid_packet++;
                               // tbInvalidPacket.Invoke(new Action(() => tbInvalidPacket.Text = invalid_packet.ToString()));
                            }

                            if (packet_size > 0)   //Meaning that we have received a valid packet type above.
                            {
                                data_transfer_count = packet_size;
                                byte_ptr = 2;   //The status has already been read in if we get here for the first time, so start checking data afterwards.
                            }
                        }

                        /********************************************************
                        * 2. Processing data within a packet.                   *
                        *********************************************************/
                        else if (data_transfer_count > 0)   //We process and/or store the incomming data as we are expecting some!
                        {
                            if (data_transfer_state == FILE_SIZE)   //We are receiving the full file size IN BINARY, but must be right alligned so we can ignore most significant ZEROS.
                            {
                                if (data_transfer_count > 5 && serial_data_buffer[pos] != 0)    //The first 5 bytes MUST be 0, this still allows us 256^5 file size, which is over a trillion. 5 zeros's is a kind of check that the data is valid.
                                {
                                    pos++;
                                    data_transfer_count = 0;    //This was an invlaid file size, so we can stop looking for file_size.
                                    data_transfer_state = NOTHING;
                                }
                                else
                                {
                                    if (serial_data_buffer[pos] != 0 || file_size_bytes != 0)
                                    {
                                        file_size_bytes *= 256;
                                        file_size_bytes += serial_data_buffer[pos];
                                    }
                                    pos++;
                                    data_transfer_count--;
                                }
								if (logging)
									log_file.WriteLine("Size received: " + file_size_bytes.ToString());
                            }
                            else if (data_transfer_state == FILE_DATA)   //We are receiving the full file!
                            {
                                if (file_size_bytes == -1)  //This is a flag to ensure that things happened in the correct order.
                                {
                                    int bytes_to_write = Math.Min(data_transfer_count, bytes - pos);    //How many bytes shall we write! As many as we can
                                    fs_file.Write(serial_data_buffer, pos, bytes_to_write);     //Write as many bytes as we have.
                                    //progressBar1.Invoke(new Action(() => progressBar1.Value += bytes_to_write));
                                    try
                                    {
                                        progress_bar_callback((double)(full_file_size - data_transfer_count) / (double)full_file_size);
                                    }
                                    catch (SystemException)
                                    {
                                        int ian = 4;
                                    }
                                    data_transfer_count -= bytes_to_write;                      //Reduce the bytes remaining by how many we've just received.
                                    pos += bytes_to_write;                                      //Increase the position in the received bytes array.
                                }
                                else
                                {
                                    data_transfer_count = 0;
                                    data_transfer_state = NOTHING;
                                }
                            }
                            else if (data_transfer_state == XML_HEADER)   //We are receiving the XML Header prior to logging!
                            {
                                int bytes_to_write = Math.Min(data_transfer_count, bytes - pos);    //How many bytes shall we write! As many as we can
                                if (fs != null && fs.CanWrite)
                                    fs.Write(serial_data_buffer, pos, bytes_to_write);
                                AddToString(serial_data_buffer, pos, bytes_to_write);
                                data_transfer_count -= bytes_to_write;
                                pos += bytes_to_write;
                            }
                            else if (data_transfer_state == FILE_NAME)   //We are receiving the full file name.
                            {
                                while (data_transfer_count > 0 && pos < bytes && serial_data_buffer[pos] != 0)
                                {
                                    file_name_received += (char)serial_data_buffer[pos];
                                    pos++;
                                    data_transfer_count--;
                                }
                                if (serial_data_buffer[pos] == 0)   //The full name has been received.
                                {
                                    data_transfer_count = 0;
                                }
                            }
                            else if (data_transfer_state == VEE_SCAN_RESPONSE)
                            {
                                if (data_transfer_count == 4)
                                {
                                    if (serial_data_buffer[pos] == serial_data_buffer[pos + 1])
                                    {
                                        vee_scan_command = serial_data_buffer[pos];
                                        if (logging)
                                            log_file.WriteLine("VeeScan response received:" + vee_scan_command.ToString());
                                    }
                                    pos += 2;
                                    data_transfer_count = 0;
                                }
                            }
								//EC_COMMAND_ACK
							else if (data_transfer_state == EC_COMMAND_ACK)
                            {
                                if (data_transfer_count == 7)
                                {
									Byte ec_command_code = 0;		//The command that is being ACK'd
									Byte ec_channel = 0;			//The channel that the command was regarding
									Int32 ec_command_response = 0;	//The value of the command being ACK'd

									data_transfer_count -= 4;	//Skip past the Status bytes, command ID and channel.
                                    ec_command_code = serial_data_buffer[pos++];
									ec_channel = serial_data_buffer[pos];
                                    if (logging)
                                        log_file.WriteLine("EC Command response received:" + vee_scan_command.ToString());
									ec_command_response = 0;
									while (data_transfer_count > 0 && ++pos < bytes)
									{
										data_transfer_count--;
										ec_command_response += (Int32)(serial_data_buffer[pos]*(Math.Pow(256,data_transfer_count)));
									}
                                    data_transfer_count = 0;
									//Send response back to the host!
									if (ec_hardware_response != null)
										ec_hardware_response(ec_command_code, ec_channel, ec_command_response);
                                }
                            }
                            else if (data_transfer_state == SINGLE_CHAN_POST || data_transfer_state == REALTIME_DATA_RAW || data_transfer_state == REALTIME_DATA_POSTPROCESS || data_transfer_state == NON_REALTIME || data_transfer_state == CONDUCTIVITY || data_transfer_state == CSCAN_ENCODER_TRIG)   //We are receiving a packet of post processed single-channel data, ie VeeScan data.
                            {
                                //Even if we're not writing to a file, we still must reduce the count of bytes we're expecting
                                int bytes_to_write = Math.Min(data_transfer_count, bytes - pos);    //How many bytes shall we write! As many as we can
                                //int status_1 = 0, status_2 = 0;

                                //Keep looping while we are processing bytes that have arrived AND for the current stayus_byte type.
                                while (pos < bytes && byte_ptr < data_transfer_count) //+2 to include the status bytes
                                {
                                    switch (byte_ptr++) //Which value of the packet are we reading and processing.
                                    {
                                        case 0:
                                            status_1 = serial_data_buffer[pos++];
                                            status_2 = 0;
                                            break;
                                        case 1:
                                            status_2 = serial_data_buffer[pos++];

                                            //First check that the status byte is valid by checking that byte 2 is the inverse of 1.
                                            if (status_2 != (~status_1 & 0xFF)) //We must & with 0xFF to leave the LSB
                                            {
                                                pos--;  //Go through a byte at a time, so go back one byte then set that to be status_1.
                                                //while (pos < bytes - 2 && serial_data_buffer[pos] != data_transfer_state)
                                                //    pos++;
                                                byte_ptr = 0;
                                                continue;
                                            }
                                            //Check to see if the status byte has changed and is no longer realtime!
                                            if ((status_1 & 0xF0) != SINGLE_CHAN_POST && (status_1 & 0xF0) != REALTIME_DATA_RAW && (status_1 & 0xF0) != REALTIME_DATA_POSTPROCESS && (status_1 & 0xF0) != NON_REALTIME && (status_1 & 0xF0) != CONDUCTIVITY && (status_1 & 0xF0) != CSCAN_ENCODER_TRIG)
                                            {
                                                data_transfer_count = 0;
                                                byte_ptr = 0;
                                                pos -= 2;   //Jump to before the status byte
                                                break;
                                            }

                                            values[0] = values[1] = values[2] = values[3] = values[4] = values[5] = values[6] = values[7] = -1;
                                            break;
                                        //Extract the 4-byte int32 data from the BINARY byte VCP Data
                                        case 2: values[0] = serial_data_buffer[pos++]; break;
                                        case 3: values[0] += (serial_data_buffer[pos++] * 256); break;
                                        case 4: values[0] += (serial_data_buffer[pos++] * 256 * 256); break;
                                        case 5: values[0] += (serial_data_buffer[pos++] * 256 * 256 * 256); break;
                                        case 6: values[1] = serial_data_buffer[pos++]; break;
                                        case 7: values[1] += (serial_data_buffer[pos++] * 256); break;
                                        case 8: values[1] += (serial_data_buffer[pos++] * 256 * 256); break;
                                        case 9: values[1] += (serial_data_buffer[pos++] * 256 * 256 * 256); break;
                                        case 10: values[2] = serial_data_buffer[pos++]; break;
                                        case 11: values[2] += (serial_data_buffer[pos++] * 256); break;
                                        case 12: values[2] += (serial_data_buffer[pos++] * 256 * 256); break;
                                        case 13: values[2] += (serial_data_buffer[pos++] * 256 * 256 * 256); break;
                                        case 14: values[3] = serial_data_buffer[pos++]; break;
                                        case 15: values[3] += (serial_data_buffer[pos++] * 256); break;
                                        case 16: values[3] += (serial_data_buffer[pos++] * 256 * 256); break;
                                        case 17: values[3] += (serial_data_buffer[pos++] * 256 * 256 * 256); break;
                                        case 18: values[4] = serial_data_buffer[pos++]; break;
                                        case 19: values[4] += (serial_data_buffer[pos++] * 256); break;
                                        case 20: values[4] += (serial_data_buffer[pos++] * 256 * 256); break;
                                        case 21: values[4] += (serial_data_buffer[pos++] * 256 * 256 * 256); break;
                                        case 22: values[5] = serial_data_buffer[pos++]; break;
                                        case 23: values[5] += (serial_data_buffer[pos++] * 256); break;
                                        case 24: values[5] += (serial_data_buffer[pos++] * 256 * 256); break;
                                        case 25: values[5] += (serial_data_buffer[pos++] * 256 * 256 * 256); break;
                                        case 26: values[6] = serial_data_buffer[pos++]; break;
                                        case 27: values[6] += (serial_data_buffer[pos++] * 256); break;
                                        case 28: values[6] += (serial_data_buffer[pos++] * 256 * 256); break;
                                        case 29: values[6] += (serial_data_buffer[pos++] * 256 * 256 * 256); break;
                                        case 30: values[7] = serial_data_buffer[pos++]; break;
                                        case 31: values[7] += (serial_data_buffer[pos++] * 256); break;
                                        case 32: values[7] += (serial_data_buffer[pos++] * 256 * 256); break;
                                        case 33: values[7] += (serial_data_buffer[pos++] * 256 * 256 * 256); break;
                                        case 34: values[8] = serial_data_buffer[pos++]; break;
                                        case 35: values[8] += (serial_data_buffer[pos++] * 256); break;
                                        case 36: values[8] += (serial_data_buffer[pos++] * 256 * 256); break;
                                        case 37: values[8] += (serial_data_buffer[pos++] * 256 * 256 * 256); break;
                                        case 38: values[9] = serial_data_buffer[pos++]; break;
                                        case 39: values[9] += (serial_data_buffer[pos++] * 256); break;
                                        case 40: values[9] += (serial_data_buffer[pos++] * 256 * 256); break;
                                        case 41: values[9] += (serial_data_buffer[pos++] * 256 * 256 * 256); break;
                                        case 42: values[10] = serial_data_buffer[pos++]; break;
                                        case 43: values[10] += (serial_data_buffer[pos++] * 256); break;
                                        case 44: values[10] += (serial_data_buffer[pos++] * 256 * 256); break;
                                        case 45: values[10] += (serial_data_buffer[pos++] * 256 * 256 * 256); break;
                                        case 46: values[11] = serial_data_buffer[pos++]; break;
                                        case 47: values[11] += (serial_data_buffer[pos++] * 256); break;
                                        case 48: values[11] += (serial_data_buffer[pos++] * 256 * 256); break;
                                        case 49: values[11] += (serial_data_buffer[pos++] * 256 * 256 * 256); break;
                                    }
                                    //Have we finished the current packet?
                                    if (byte_ptr == packet_size)
                                    {
                                        if (fs != null)
                                        {
                                            fs.Write(serial_data_buffer, pos - packet_size, packet_size);
                                        }
                                        byte_ptr = 0;   //Reset ready for the next packet.

                                        //In all modes EXCEPT NON_REALTIME values[6] and values[7] are NOT used so we use them to hold the status bytes fof X & Y (alarms and rotary sync pulse).
                                        if (data_transfer_state != NON_REALTIME && data_transfer_state != CSCAN_ENCODER_TRIG)
                                        {
                                            values[6] = 0;
                                            if ((status_1 & 0x01) == ROTARY_SYNC_PULSE_BIT) //Do we see the Rotary sync pulse bit!?
                                                values[6] = 1;
                                            if ((status_1 & 0x02) == ALARM_BIT1) //Do we see the Rotary sync pulse bit!?
                                                values[6] |= 2;
                                            if ((status_1 & 0x04) == ALARM_BIT2) //Do we see the Rotary sync pulse bit!?
                                                values[6] |= 4;
                                        }
										if (data_transfer_state == CSCAN_ENCODER_TRIG)
                                        {
                                            //Scale the values to be screen coords.
                                            values[0] += 320;
                                            values[1] += 320;
											values[2] += 320;
                                            values[3] += 240;
											values[4] += 240;
                                            values[5] += 240;
                                            if (data_callback != null)
                                            {   //Chan 1 X & Y, Chan 2 X & Y, Mix X & Y, Encoder2, Encoder 1.
												// ALSO! We OR the Least Sig 4 bits of the status byte in to the Most Sig 4 bits of the Mix X.
                                                data_callback(values[0], values[3], values[1], values[4], values[2] | status_1<<28, values[5], values[6], values[7]);
                                            }
                                            else
                                            {
                                                x_coords_ch1[current_array_writing][write_pointer] = values[0];
                                                y_coords_ch1[current_array_writing][write_pointer] = values[1];
                                                x_coords_ch2[current_array_writing][write_pointer] = reading_counter++;
                                                y_coords_ch2[current_array_writing][write_pointer] = request_counter;
                                            }
                                        }
                                        //Any processing done at this point will take place on EVERY one of the 8000 per second data points.
                                        //Display something to the Phase Plane!
                                        if (data_transfer_state == SINGLE_CHAN_POST)
                                        {
                                            //Scale the values to be screen coords.
                                            values[0] += 320;
                                            values[1] += 240;
                                            if (data_callback != null)
                                            {   //Chan 1 X & Y, Counter, Encoder
                                                data_callback(values[0], values[1], values[2], values[3], 0, 0, values[6], values[7]);
                                            }
                                            else
                                            {
                                                x_coords_ch1[current_array_writing][write_pointer] = values[0];
                                                y_coords_ch1[current_array_writing][write_pointer] = values[1];
                                                x_coords_ch2[current_array_writing][write_pointer] = reading_counter++;
                                                y_coords_ch2[current_array_writing][write_pointer] = request_counter;
                                            }
                                        }
                                        else if (data_transfer_state == NON_REALTIME)
                                        {
                                            if (data_callback_non_realtime != null)
                                            {
                                                //Check for negative values of the percent! If the values are negative they come in as 2-Byte UInts, ie -2 is 65533
                                                if (values[6] > 0x8000) //MSb set, then assume Negative
                                                    values[6] -= 0xFFFF;
                                                if (values[7] > 0x8000) //MSb set, then assume Negative
                                                    values[7] -= 0xFFFF;

                                                if (!data_transfer_paused)
                                                {
                                                    //X1, Y1, X2, Y2, X%, Y%, Theta, Radius, Theta2, Radius2, BLANK1, BLANK2
                                                    data_callback_non_realtime(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8], values[9], values[10], values[11]);
                                                }
                                            }
                                            else if (data_callback != null)
                                            {
                                                //Check for negative values of the percent! If the values are negative they come in as 2-Byte UInts, ie -2 is 65533
                                                if (values[6] > 0x8000) //MSb set, then assume Negative
                                                    values[6] -= 0xFFFF;
                                                if (values[7] > 0x8000) //MSb set, then assume Negative
                                                    values[7] -= 0xFFFF;

                                                //X1, Y1, X2, Y2, Theta, Radius, X%, Y%
                                                data_callback(values[0], values[1], values[2], values[3], values[6], values[7], values[4], values[5]);
                                            }
                                        }
                                        else if (data_transfer_state == REALTIME_DATA_RAW)  //X & Y data for 2 channels.
                                        {
                                            if (data_callback != null)
                                            {
                                                data_callback(values[0], values[1], values[2], values[3], 0, 0, values[6], values[7]);
                                            }
                                            else
                                            {
                                                x_coords_ch1[current_array_writing][write_pointer] = values[0];
                                                y_coords_ch1[current_array_writing][write_pointer] = values[1];
                                                if (x_coords_ch2 != null)
                                                    x_coords_ch2[current_array_writing][write_pointer] = values[2];
                                                if (y_coords_ch2 != null)
                                                    y_coords_ch2[current_array_writing][write_pointer] = values[3];
                                            }
                                        }
                                        else if (data_transfer_state == CONDUCTIVITY)   //&IACS, Lift-Off, Angle, Vector
                                        {
                                            if (data_callback != null)
                                            {
                                                //Send the conductivity stuff to the host as Xpercent, Ypercent, Theta, Radius.
                                                data_callback(0, 0, 0, 0, values[0], values[1], values[2], values[3]);
                                            }
                                        }
                                        else if (data_transfer_state == REALTIME_DATA_POSTPROCESS)  //X & Y data for 3 channels.
                                        {
                                            //We range check the values that are important to the VeeScan. We have seen the USB data get a BIT out of sync (or 2 or 3)!
                                            if (beadseat_comp_on == true &&
                                                (values[2] != 0xAA || values[5] != 0x55 ||
                                                values[0] > 70000 || values[0] < -70000 ||
                                                values[1] > 70000 || values[1] < -70000 ||
                                                values[3] > 70000 || values[3] < -70000 ||
                                                values[4] > 70000 || values[4] < -70000
                                                ))
                                            {
                                                //We have an error. We won't store it. Reduce the write_pointer so that when it is increased, it has no effect.
                                                write_pointer--;
                                                if (logging)
                                                {
                                                    log_file.WriteLine(string.Format("Fail at {0}, Data:{1},{2},{3},{4},{5},{6}", write_pointer.ToString(), values[0].ToString(), values[1].ToString(), values[2].ToString(), values[3].ToString(), values[4].ToString(), values[5].ToString()));
                                                }
                                            }
                                            else
                                            {
                                                if (data_callback != null)
                                                {
                                                    data_callback(values[0], values[3], values[1], values[4], values[2], values[5], values[6], values[7]);    //Different order of variables compared to other data, X, X1, X2, Y, Y1, Y2, so we add it to the call back in a different order.
                                                }
                                                else
                                                {
                                                    x_coords_ch1[current_array_writing][write_pointer] = values[0];
                                                    y_coords_ch1[current_array_writing][write_pointer] = values[3];
                                                    if (x_coords_ch2 != null)
                                                        x_coords_ch2[current_array_writing][write_pointer] = values[1];
                                                    //x_coords_ch2[write_pointer] = values[1];
                                                    if (y_coords_ch2 != null)
                                                        y_coords_ch2[current_array_writing][write_pointer] = values[4];
                                                    //y_coords_ch2[write_pointer] = values[4];
                                                    if (x_coords_chMix != null)
                                                        x_coords_chMix[current_array_writing][write_pointer] = values[2];
                                                    if (y_coords_chMix != null)
                                                        y_coords_chMix[current_array_writing][write_pointer] = values[5];
                                                }
                                            }
                                        }

                                        if (++write_pointer >= number_of_elements)
                                        {
                                            points_written[current_array_writing] = write_pointer;  //Store how many points were written in to the array.
                                            write_pointer = 0;
                                            if (++current_array_writing >= number_of_arrays)
                                                current_array_writing = 0;
                                            if (points_written[current_array_writing] != 0)
                                            {
                                                error_string = "Array not read.";
                                                //if (logging)
                                                //    log_file.WriteLine("An Array wasn't read.");
                                            }
                                        }
                                    }
                                }   //END of While (pos<bytes)
                                if (byte_ptr != 0)  //We didn't have a complete packet to process, so copy what wasn;t processed back to the start of the serial buffer and set an offset to write the next data to.
                                {
                                    for (int x = 0; x < byte_ptr && bytes - (byte_ptr - x) > 0; x++)   //Loop round for the number of bytes we have processed, copying them to the start. Also check that we have enough bytes to do this!
                                    {
                                        serial_data_buffer[x] = serial_data_buffer[bytes - (byte_ptr - x)];
                                    }
                                    serial_buffer_offset = byte_ptr;
                                    byte_ptr = 0;   //We copy the data to the start including the status byte, so must start processing the packet from the beginning.
                                    data_transfer_count = 0;
                                }
                            }
                        }
                        else if (data_transfer_count < 0)//If we're not expecting any bytes for a particular purpose, it should be a status byte
                        {
                            //Not sure what to do here ;-)
                            bytes = 0;
                            serialPortUSB.DiscardInBuffer();
                            data_transfer_count = 0;
                            data_transfer_state = NOTHING;
                        }
                    }   //End of if receiving realtime data
                }   //End of the while true!
            }
            catch (SystemException)
            {
               // tbConsole.Invoke(new Action(() => tbConsole.AppendText(e.Message + "\n")));
               // btConnectUSB.Invoke(new Action(() => btConnectUSB.Text = "Connect"));
                if (serialPortUSB != null && serialPortUSB.IsOpen)
                {
                    try
                    {
                        serialPortUSB.Close();
                    }
                    catch
                    { }
                }
            }
            Console.WriteLine("Ended HandleReceivedBytes Thread.");
            //debugging = false;  //Make sure the interrupt handler for dataReceived enters once again.
            //this.serialPortUSB.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(this.serialPortUSB_DataReceived);
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
        /// Send a string to the instrument.
        /// If we are to send over RS232, we must convert the binary to ASCII and make them comma seperated
        /// </summary>
        /// <param name="first_byte"></param>
        /// <param name="second_byte"></param>
        /// <param name="command">String to send</param>
        /// <returns>FALSE if the port wasn't open</returns>
        public bool WriteToInstrument(byte first_byte, byte second_byte, string command)
        {
            if (serialPortUSB == null || !serialPortUSB.IsOpen)
            {
                return false;
            }
            Int32 length = command.Length;

            byte[] bt = new byte[length + 3];   //Was +2, but now add a CR to the end.
            bt[0] = first_byte;
            bt[1] = second_byte;
            Int32 x = 0;
            for (; x < length; x++)
            {
                bt[x + 2] = (byte)command[x];
            }
            bt[x+2] = 0xd;  //CR

            if (serialPortUSB != null && serialPortUSB.IsOpen)
                serialPortUSB.Write(bt, 0, bt.Length-1);
            if (serialPortRS232 != null && serialPortRS232.IsOpen)
                serialPortRS232.Write(bt, 0, bt.Length);

            if (logging)
                log_file.WriteLine("Sent String: " + bt.ToString());

            return true;
        }
        /// <summary>
        /// Send connected instruments the string of bytes.
        /// </summary>
        /// <param name="byte_data"></param>
        /// <returns>TRUE if we were able to send the data.</returns>
        public bool WriteToInstrument(byte[] byte_data)
        {
            try
            {
                if (serialPortUSB != null && serialPortUSB.IsOpen)
                {
                    serialPortUSB.Write(byte_data, 0, byte_data.Length);
                    return true;
                }
                if (serialPortRS232 != null && serialPortRS232.IsOpen)
                {
                    serialPortRS232.Write(new byte[] { (byte)byte_data.Length }, 0, 1);  //Send the number of bytes to follow
                    serialPortRS232.Write(byte_data, 0, byte_data.Length);
                    //serialPortRS232.Write(new byte[] {0xd}, 0, 1);  //Send a CR
                    return true;
                }
            }
            catch
            { }

            return false;
        }
        public void ClickBLANK1()
        {
            byte[] bt = { 1, 0, 9 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: BLANK1");
        }

        public void ClickFREEZE()
        {
            byte[] bt = { 1, 0, 10 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: FREEZE");
        }

        public void ClickOK()
        {
            byte[] bt = { 1, 0, 11 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: OK");
        }

        public void ClickBALANCE2()
        {
            byte[] bt = { 1, 0, 2 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: BAL2");
        }

        public void ClickBLANK2()
        {
            byte[] bt = { 1, 0, 7 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: BLANK2");
        }

        public void ClickMENU()
        {
            byte[] bt = { 1, 0, 1 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: CANCEL");
        }

        public void ClickLEFT()
        {
            byte[] bt = { 1, 0, 5 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: LEFT");
        }

        public void ClickRIGHT()
        {
            byte[] bt = { 1, 0, 6 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: RIGHT");
        }

        public void ClickBALANCE1Long()
        {
            byte[] bt = { 1, 0, 20 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: BAL1 LONG");
        }

        public void ClickBLANK1Long()
        {
            byte[] bt = { 1, 0, 21 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: BLANK1 LONG");
        }

        public void ClickFREEZELong()
        {
            byte[] bt = { 1, 0, 22 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: FREEZE LONG");
        }

        public void ClickOKLong()
        {
            byte[] bt = { 1, 0, 23 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: OK LONG");
        }

        public void ClickBALANCE2Long()
        {
            byte[] bt = { 1, 0, 14 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: BAL2 LONG");
        }

        public void ClickBLANK2Long()
        {
            byte[] bt = { 1, 0, 19 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: BLANK2 LONG");
        }

        public void ClickMENULong()
        {
            byte[] bt = { 1, 0, 13 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Click: MENU LONG");
        }
        public void LoadVeeScanSettings()
        {
            byte[] bt = { 1, 0, 18 };

            WriteToInstrument(bt);

            if (logging)
                log_file.WriteLine("Load VeeScan Settings.");
        }
        public void ResetUSB()
        {
            byte[] bt = { 1, 0, 27 };

            WriteToInstrument(bt);

            CloseSerialConnection();
            if (logging)
                log_file.WriteLine("Reset USB");
        }
        public void PauseRealtimeData()
        {
            data_transfer_paused = true;
        }
        public void ContinueRealtimeData()
        {
            data_transfer_paused = false;
        }
        /// <summary>
        /// If non-zero, the number of bytes (1 to 4) to put in to each returned value, which are currently U32s.
        /// </summary>
        /// <param name="p"></param>
        public void ReceiveAllData(byte p)
        {
            return_raw_port_data = p;
        }
    }
}
