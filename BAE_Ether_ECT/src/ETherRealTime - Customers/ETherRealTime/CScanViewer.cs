using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Excel = Microsoft.Office.Interop.Excel;
using ETherSoftwareComponentStore;

namespace ETherRealTime
{
	public partial class CScanViewer : Form
	{
		private string file_name_received;
		XML_Handler cscan_XML_data;
		Bitmap cscan_bitmap;
		//CScan details read from the XML:
		Int32 header_size = 0, cscan_height = 0, cscan_width = 0, cscan_x_ticks = 0, cscan_y_ticks = 0, cscan_x_res = 0, cscan_y_res = 0, raw_data = 0, frequencies = 1, cscan_x_pixels = 0, cscan_y_pixels = 0, cscan_pattern = 0, cscan_y_values = 0;
		double cscan_theta_multiplier = 1.0;	//The angle in an R-Theta CScan will require a multiplier to go from degrees to data points, unless the encoder is 1 tick/degree and the resolution is 1.
		//StreamReader str;
		//BinaryReader br;
		char[] cscan_data;
		byte[] cscan_bytes;
		int freq_offset = 0;	//If we want toi view the 2nd frequency we need to offset the data by 1.

		public CScanViewer()
		{
			InitializeComponent();
		}

		public CScanViewer(string file_name_received)
		{
			bool result = false;
			StreamReader str = null;
			BinaryReader br;

			// TODO: Complete member initialization
			this.file_name_received = file_name_received;
			FileInfo fi = new FileInfo(file_name_received);
			cscan_data = new char[fi.Length];
			
			try
			{
				str = new StreamReader(file_name_received);
				str.Read(cscan_data, 0, (int)fi.Length);

				String header_text = new String(cscan_data);
				//cscan_XML_data = new InstrumentSettings(header_text, out result);
				cscan_XML_data = new XML_Handler(header_text, out result, file_name_received);
				this.Text = fi.Name;
			}
			catch //When we get to the start of the C-Scan data, we will end up here.
			{
				//MessageBox.Show("CSCan loading error.");
				//return;
			}
			decimal temp_decimal = 0;
			cscan_XML_data.GetValue("HEADER_SIZE", 0, out temp_decimal);
			header_size = (Int32)temp_decimal;
			cscan_XML_data.GetValue("CSCAN_HEIGHT", 0, out temp_decimal);			//mm
			cscan_height = (Int32)temp_decimal;
			cscan_XML_data.GetValue("CSCAN_WIDTH", 0, out temp_decimal);				//mm
			cscan_width = (Int32)temp_decimal;
			cscan_XML_data.GetValue("CSCAN_X_TICKS_MM", 0, out temp_decimal);		//Thousandths of a mm
			cscan_x_ticks = (Int32)temp_decimal;
			cscan_XML_data.GetValue("CSCAN_Y_TICKS_MM", 0, out temp_decimal);		//Thousandths of a mm
			cscan_y_ticks = (Int32)temp_decimal;
			cscan_XML_data.GetValue("CSCAN_RESOLUTION_X_MM", 0, out temp_decimal);	//Divisor to reduce data points by
			cscan_x_res = (Int32)temp_decimal;
			cscan_XML_data.GetValue("CSCAN_RESOLUTION_Y_MM", 0, out temp_decimal);	//Divisor to reduce data points by
			cscan_y_res = (Int32)temp_decimal;
			cscan_XML_data.GetValue("CSCAN_PATTERN", 0, out temp_decimal);	//Divisor to reduce data points by
			cscan_pattern = (Int32)temp_decimal;	//Value is from the enum : eFORWARDS_BACKWARDS, eFORWARDS, eR_THETA

			// Now calculate the exact size of the data store required.
			//First X axis,
			cscan_x_pixels = (cscan_width * cscan_x_ticks) / (1000 * cscan_x_res);
			cscan_theta_multiplier = ((double)cscan_y_ticks / 1000.0) / cscan_y_res;
			if (cscan_pattern == 2)	//This is R-Theta so use x (radius) for both bitmaps sizes
			{
				cscan_y_pixels = cscan_x_pixels;
				//cscan_y_values is only used in R-Theta where the Y axis of the bitmap is the same size as the X BUT the y axis of the data is related to the angle and its resolution.
				cscan_y_values = (Int32)(90 * cscan_theta_multiplier);
			}
			else
				cscan_y_pixels = (cscan_height * cscan_y_ticks) / (1000 * cscan_y_res);

			cscan_XML_data.GetValue("CSCAN_RAW_DATA", 0, out temp_decimal);
			raw_data = (Int32)temp_decimal;
			cscan_XML_data.GetValue("FREQUENCIES", 0, out temp_decimal);
			frequencies = (Int32)temp_decimal;

			//Now we know the header size, we can read in the correct amount of raw data into bytes:
			cscan_bytes = new byte[(int)(fi.Length-header_size-1)];
			br = new BinaryReader(str.BaseStream);
			str.BaseStream.Seek((int)(header_size+1), SeekOrigin.Begin);
			br.Read(cscan_bytes, 0, (int)(fi.Length-header_size-1));
			str.Close();

			//Raw_data will store the number of bytes that must be skipped depending on whether raw_data or only display data is present in the cscan data.
			if (raw_data > 0)
				raw_data = 8 * frequencies;
			else
				raw_data = 1;

			//cscan_data = new char[(int)((cscan_x_res * cscan_y_res) * raw_data)];

			Int32 expected_file_size = 0;

			if (cscan_pattern == 2)	//This is R-Theta so we use the resolution and the angle
				expected_file_size = (Int32)(cscan_x_pixels * raw_data * cscan_y_values);
			else
				expected_file_size = (cscan_x_pixels * cscan_y_pixels) * raw_data;
			try
			{
				if (fi.Length - header_size - 1 != expected_file_size)
				{
					if (MessageBox.Show("", "File size does not match header info.\nContinue?", MessageBoxButtons.YesNo) == DialogResult.No)
						return;
				}
			}
			catch (SystemException exc)
			{
				MessageBox.Show("Error reading CScan raw data file.\n" + exc.Message);
				return;
			}
			InitializeComponent();

			//Enable the relevant Channel radio buttons
			rbChan2.Enabled = rbChanMix.Enabled = frequencies == 1?false:true;

			if (cscan_pattern == 2)
				GeneratePolarCScanBitmap();
			else
				GenerateCartesianCScanBitmap();
		}
		private void GeneratePolarCScanBitmap()
		{
			Int32 x_pixels, x_coord, y_coord, next_x = -1, next_y = -1;
			Color colour;
			Int32 zoom_data = 1, zoom_bitmap = 1;
			double angle_data_values = 0.0, new_angle_data_values = 0.0, zoom = 1.0, local_sin = 0.0, local_cos = 0.0;
			Graphics gp;
			byte data_value;	//THe data read from the memory structure
			int[] spot_sizes = new int[cscan_x_pixels];

			if (nudCScanZoom.Value != 0M)
				zoom = (double)nudCScanZoom.Value;

			if (nudCScanZoom.Value == 0M || nudCScanZoom.Value == 1M)
			{
				x_pixels = cscan_x_pixels;
			}
			else
			{
				x_pixels = (int)(cscan_x_pixels * nudCScanZoom.Value);

				if (nudCScanZoom.Value < 1 && nudCScanZoom.Value > 0)	//Zoom is a zoom out, ie 0.5 or 0.2, so we are skipping data points
					zoom_data = (Int32)(1M / nudCScanZoom.Value);
				else
					zoom_bitmap = (Int32)(nudCScanZoom.Value);			//Zoom in, so we loop several times, increasing the Bitmap coord for each bit of data.
			}
			cscan_bitmap = new Bitmap(x_pixels, x_pixels);		//The bitmap is square as the length and width are defined by the radius!
			gp = Graphics.FromImage(cscan_bitmap);

			Int64 index = 0;
			for (int y_data = 0; y_data < cscan_y_values; /*y_data++, y_bmp++*/)
			{
				//To calculate the X component, take cosine of the angle * radius:
				angle_data_values = Math.PI * ((double)y_data/cscan_theta_multiplier) / 180.0;
				local_cos = Math.Cos(angle_data_values);
				local_sin = Math.Sin(angle_data_values);
				for (int x_data = 0; x_data < cscan_x_pixels; /*x_data++, x_bmp++*/)
				{

					//Get the data from x_data, y_data
					//data_value = cscan_bytes[(((y_data * (int)cscan_x_pixels) + x_data) * (int)raw_data) + freq_offset];
					if (freq_offset == 2)	//Mix Channel
					{
						Int32 temp_data = cscan_bytes[index] - cscan_bytes[index + (raw_data * frequencies)];
						if (cscan_bytes[index] == 0)	//If there was NO data stored, just keep the value at 0 and display WHITE
							data_value = 0;
						else if (temp_data < 0)	//If the MIX results in 0, change it to 1 as 0 represents NO data.
							data_value = 1;
						else
							data_value = (byte)temp_data;
					}
					else
						data_value = cscan_bytes[index + freq_offset];

					index += raw_data * frequencies;
					colour = GetColourFromData(data_value);

					//From the R & Theta X&Y need to calculate an X&Y
					// TO DISPLAY AS A SQUARE BITMAP AS THE DATA WAS STORED IN THE INSTRUMENT MEMORY:
						//cscan_bitmap.SetPixel(x_bmp, y_bmp, colour);
					//Convert each pixel to screen coords using R-Theta to X,Y:
					
					//angle_data_values /= cscan_theta_multiplier;
					x_coord = (int)((local_cos * x_data) * zoom);
					y_coord = (int)((local_sin * x_data) * zoom);

					if (x_coord < cscan_bitmap.Width && y_coord < cscan_bitmap.Height)
					{
						//We compare the new colour to what is already in existance
						colour = UseGreatestColour(cscan_bitmap.GetPixel(x_coord, y_coord), colour);
			
						//for the first row, calculate what the X value is to the next point then the Y coord to the point at the next angle, same X.
						//The greatest of the above values is stored for the spot size in future at the current X value.
						if (y_data == 0)
						{
							next_x = (int)((Math.Cos(angle_data_values) * (x_data+1)) * zoom);	//The next X is at the same angle
							new_angle_data_values = Math.PI * ((double)(y_data + 1) / cscan_theta_multiplier) / 180.0;
							next_y = (int)((Math.Sin(new_angle_data_values) * x_data) * zoom);
							spot_sizes[x_data] = Math.Max(next_y-y_coord, next_x-x_coord)+1;
						}
						DrawOurRectBox(Color.FromArgb(254, colour), x_coord, y_coord, spot_sizes[x_data], spot_sizes[x_data]);
					}

					x_data += zoom_data;
				}
				y_data += zoom_data;
			}
			//Now the bitmap has been created the correct size, it's displayed position is scroll-bar dependant
			DisplayBitmapAccordingToScrollBarPosition();
		}
		/// <summary>
		/// Draw a rectangle on the Bitmap but we check each point to see if it has a higher priority than the previous
		/// </summary>
		/// <param name="colour"></param>
		/// <param name="x_start"></param>
		/// <param name="y_start"></param>
		/// <param name="width"></param>
		/// <param name="height"></param>
		private void DrawOurRectBox(Color colour, int x_start, int y_start, int width, int height)
		{
			int x_end = x_start + width;
			int y_end = y_start + height;

			for (int y = y_start; y<=y_end && y<cscan_bitmap.Height; y++)
				for (int x = x_start; x<=x_end && x<cscan_bitmap.Width; x++)
					cscan_bitmap.SetPixel(x, y, UseGreatestColour(cscan_bitmap.GetPixel(x, y), colour));
		}
		/// <summary>
		/// If there is already a colour at a pixel, we must decide and return which colour to use!
		/// </summary>
		/// <param name="previous_colour"></param>
		/// <param name="colour"></param>
		/// <returns></returns>
		private Color UseGreatestColour(Color previous_colour, Color colour)
		{
			if (previous_colour.A == 255 && colour.A < 255)
				return previous_colour;
			if (previous_colour.A < 255 && colour.A == 255)
				return colour;
			//If the previous colour is 0 (black) return Colour
			if (previous_colour.R == 0 && previous_colour.G == 0 && previous_colour.B == 0)
				return colour;
			//If the colours are the same, return either
			if (previous_colour.R == colour.R && previous_colour.G == colour.G && previous_colour.B == colour.B)
				return colour;
			//First check for the most red, which is looking for the lack of blue and green as WHITE has full red anyway!
			if (previous_colour.G < colour.G && previous_colour.B < colour.B)
				return previous_colour;
			if (previous_colour.G > colour.G && previous_colour.B > colour.B)
				return colour;
			//Now check for the most green (or least Blue and Red
			if (previous_colour.R < colour.R && previous_colour.B < colour.B)
				return previous_colour;
			if (previous_colour.R > colour.R && previous_colour.B > colour.B)
				return colour;
			//Now check for Blue (or least Green and Red)
			if (previous_colour.R < colour.R && previous_colour.G < colour.G)
				return previous_colour;
			if (previous_colour.R > colour.R && previous_colour.G > colour.G)
				return colour;
			//Thinking we shouldn't get to here?
			return colour;
		}
		private void GenerateCartesianCScanBitmap()
		{
			Int32 x_pixels, y_pixels;
			Color colour;
			Int32 zoom_data = 1, zoom_bitmap = 1;

			if (nudCScanZoom.Value == 0M || nudCScanZoom.Value == 1M)
			{
				x_pixels = cscan_x_pixels;
				y_pixels = cscan_y_pixels;
			}
			else
			{
				x_pixels = (int)(cscan_x_pixels * nudCScanZoom.Value);
				y_pixels = (int)(cscan_y_pixels * nudCScanZoom.Value);

				if (nudCScanZoom.Value < 1 && nudCScanZoom.Value > 0)	//Zoom is a zoom out, ie 0.5 or 0.2, so we are skipping data points
					zoom_data = (Int32)(1M / nudCScanZoom.Value);
				else
					zoom_bitmap = (Int32)(nudCScanZoom.Value);
			}
			cscan_bitmap = new Bitmap(x_pixels, y_pixels);
			//Int64 index = 0;
			byte data_value;	//THe data read from the memory structure

			for (int y_bmp = 0, y_data = 0; y_data < (int)cscan_y_pixels; )
			{
				for (Int32 zoom_count_y = zoom_bitmap; zoom_count_y > 0; zoom_count_y--)	//Loop through the bitmap coords extra times if we're zoomed in.
				{
					for (int x_bmp = 0, x_data = 0; x_data < (int)cscan_x_pixels; )
					{
						//Get the data from x_data, y_data
						if (freq_offset == 2)	//Mix Channel
						{
							Int32 temp_data = cscan_bytes[(((y_data * cscan_x_pixels) + x_data) * (int)raw_data)] - cscan_bytes[(((y_data * cscan_x_pixels) + x_data) * (int)raw_data) + 1];
							if (cscan_bytes[(((y_data * cscan_x_pixels) + x_data) * (int)raw_data)] == 0)	//If there was NO data stored, just keep the value at 0 and display WHITE
								data_value = 0;
							else if (temp_data < 0)
								data_value = 1;
							else
								data_value = (byte)temp_data;
						}
						else
							data_value = cscan_bytes[(((y_data * cscan_x_pixels) + x_data) * (int)raw_data) + freq_offset];
						colour = GetColourFromData(data_value);

						for (Int32 zoom_count_x = zoom_bitmap; zoom_count_x > 0; zoom_count_x--)	//Loop through the bitmap coords extra times if we're zoomed in.
						{
							//Set the pixels according to the x_bmp, y_bmp
							cscan_bitmap.SetPixel(x_bmp, y_bmp, colour);
							//if the zoom is less than 0, we skip data, if it's greater than 1 we skip bitmap points (but still add the data we have just read)
							x_bmp++;
						}
						x_data += zoom_data;
					}
					y_bmp++;
				}
				y_data += zoom_data;
			}
			
			//Now the bitmap has been created the correct size, it's displayed position is scroll-bar dependant
			DisplayBitmapAccordingToScrollBarPosition();
		}
		private void DisplayBitmapAccordingToScrollBarPosition()
		{
			
			//Now sort the scroll bars
			if (nudCScanZoom.Value == 0M || cscan_bitmap.Height <= pbCScanBitmap.Height)	//0 zoom is fit to size, so no scroll
			{
				vScrollBar1.Value = 0;
				vScrollBar1.Maximum = 0;
				vScrollBar1.LargeChange = 1;
				vScrollBar1.SmallChange = 1;
			}
			else
			{
				
				vScrollBar1.Maximum = cscan_bitmap.Height;
				//vScrollBar1.Value = 0;// Math.Min(pbCScanBitmap.Height, cscan_bitmap.Height);
				vScrollBar1.LargeChange = pbCScanBitmap.Height;
				vScrollBar1.SmallChange = 1;
			}
			if (nudCScanZoom.Value == 0M || cscan_bitmap.Width <= pbCScanBitmap.Width)	//0 zoom is fit to size, so no scroll
			{
				hScrollBar1.Value = 0;
				hScrollBar1.Maximum = 0;
				hScrollBar1.LargeChange = 1;
				hScrollBar1.SmallChange = 1;
			}
			else
			{
				
				hScrollBar1.Maximum = cscan_bitmap.Width;
				//hScrollBar1.Value = pbCScanBitmap.Height;// Math.Min(pbCScanBitmap.Width, cscan_bitmap.Width);
				hScrollBar1.LargeChange = pbCScanBitmap.Width;
				hScrollBar1.SmallChange = 1;
			}

			pbCScanBitmap.Image = cscan_bitmap;
		}
		/// <summary>
		/// THIs code is basically the same as that in DrawFromBuffer_ncs in Project 16. Converts the data to a colour.
		/// The A part of the colour is set to 255. This should be 255 for REAL data points but 254 when the pixels are for filling in, needs to be changed externally!
		/// </summary>
		/// <param name="char_data"></param>
		/// <returns></returns>
		private Color GetColourFromData(byte char_data)
		{
			int red, green, blue;
			int data = char_data;// -127;

			//0 denotes NOTHING!
			if (char_data == 0)
				return Color.White;

			//Trying BLACK to Blue
			if (data < 64)
			{
				red = 0;
				blue = (data*4);
				green = 0;//48-(s16)(0.75*(float)data);
			}
			//64 to 127 Blue to Green
			//Fix RED TO 0, gradually increase GREEN from 0 to 255 at the same time as decreasing BLUE from 255 to 0
			else if (data < 128)
			{
				green = (data-64)*4;
				red = 0;
				blue = 255-green;
			}
			//128 to 192 Green to Yellow
			//Fix green TO 255 (max), blue to 0 and then gradually increase red from 0 to 31
			else if (data < 192)
			{
				green = 255;
				red = (data-128)*4;
				blue = 0;
			}
			//192 to 255 Yellow to Red
			//Fix RED to 31 (max) blue to 0 and then gradually decrease GREEN from 63 to 0
			else// if (data < 256)
			{
				red = 255;
				green = 255-((data-192)*4);
				blue = 0;
			}
			//We use 254 in R-Theta to indicate a point is FAKE, ie filling in gaps. A colour with A of 255 always takes preference
			return Color.FromArgb(255, red, green, blue); 
		}

		private void CScanViewer_Load(object sender, EventArgs e)
		{
			if (cscan_bitmap != null)
				pbCScanBitmap.Image = cscan_bitmap;
		}
		/// <summary>
		/// Someone's zooming!
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void nudCScanZoom_ValueChanged(object sender, EventArgs e)
		{
			decimal val_of_tag = Convert.ToDecimal(nudCScanZoom.Tag);

			if (nudCScanZoom.Value == val_of_tag)
				return;

			if ((nudCScanZoom.Value < val_of_tag) && val_of_tag <= 1M)	//Decreasing the value
			{
				nudCScanZoom.DecimalPlaces = 1;
				if (val_of_tag == 1M)	//Going from 1 (no zoom) to 0, fit to screen
				{
					nudCScanZoom.DecimalPlaces = 0;
					nudCScanZoom.Tag = 0M;
					nudCScanZoom.Value = 0M;
				}
				else if (val_of_tag == 0M)	//Decrease by 0.5
				{
					nudCScanZoom.Tag = 0.5M;
					nudCScanZoom.Value = 0.5M;
				}
				else if (val_of_tag == 0.5M)	//Decrease by 0.3
				{
					nudCScanZoom.Tag = 0.2M;
					nudCScanZoom.Value = 0.2M;
				}
				else if (val_of_tag == 0.2M)	//Decrease by 0.1
				{
					nudCScanZoom.Tag = 0.1M;
					nudCScanZoom.Value = 0.1M;
				}
				else //No change
				{
					nudCScanZoom.Tag = 0.1M;
					nudCScanZoom.Value = 0.1M;
					return;
				}
			}
			else if (nudCScanZoom.Value > val_of_tag)
			{
				if (val_of_tag == 0.1M)	//Increase by 0.5
				{
					nudCScanZoom.Tag = 0.2M;
					nudCScanZoom.Value = 0.2M;
				}
				else if (val_of_tag == 0.2M)	//Decrease by 0.3
				{
					nudCScanZoom.Tag = 0.5M;
					nudCScanZoom.Value = 0.5M;
				}
				else if (val_of_tag == 0.5M)	//Decrease by 0.1
				{
					nudCScanZoom.DecimalPlaces = 0;
					nudCScanZoom.Tag = 0M;
					nudCScanZoom.Value = 0M;
				}
				else if (val_of_tag == 0M)
				{
					nudCScanZoom.Tag = 1M;
					nudCScanZoom.Value = 1M;
					//return;
				}
			}
			
			nudCScanZoom.Tag = nudCScanZoom.Value;

			if (nudCScanZoom.Value == 0M)
				pbCScanBitmap.SizeMode = PictureBoxSizeMode.StretchImage;
			else
				pbCScanBitmap.SizeMode = PictureBoxSizeMode.Normal;

			if (cscan_pattern == 2)
				GeneratePolarCScanBitmap();
			else
				GenerateCartesianCScanBitmap();
		}

		private void ScrollBar_Scroll(object sender, ScrollEventArgs e)
		{
			
			Rectangle chunk = new Rectangle(hScrollBar1.Value, vScrollBar1.Value, pbCScanBitmap.Width, pbCScanBitmap.Height);
			chunk.Width = Math.Min(cscan_bitmap.Width-chunk.X, chunk.Width+chunk.X);
			chunk.Height = Math.Min(cscan_bitmap.Height-chunk.Y, chunk.Height+chunk.Y);
			if (chunk.Height == 0 || chunk.Width == 0)
				return;
			pbCScanBitmap.Image = ((Bitmap)cscan_bitmap).Clone(chunk, cscan_bitmap.PixelFormat);
		}

		private void CScanViewer_ResizeEnd(object sender, EventArgs e)
		{
			DisplayBitmapAccordingToScrollBarPosition();
		}

		private void btScreenShot_Click(object sender, EventArgs e)
		{
			string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			cscan_bitmap.Save(path + "/snap.bmp", System.Drawing.Imaging.ImageFormat.Bmp);
		}
		/// <summary>
		/// We take a *.csn file which is in Binary and output it directly in to Excel.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btConvertCScanFile_Click(object sender, EventArgs e)
		{
			Excel.Application oXL = null;
            Excel._Workbook oWB;
            Excel._Worksheet oSheet;
			Int32 x = 1;	//Excel counts from 1, not 0.
			MessageBoxNONModal MessBox = new MessageBoxNONModal("Exporting to Excel.", "Exporting");

			try
			{
				//Start Excel and get Application object.
				oXL = new Excel.Application();
				oXL.Visible = true;


				//Get a new workbook.
				oWB = (Excel._Workbook)(oXL.Workbooks.Add(System.Reflection.Missing.Value));
				oSheet = (Excel._Worksheet)oWB.ActiveSheet;

				MessBox.Show(); //Shows the Message Box as Non Modal, ie it won't hold up the main program!

				//Add table headers going cell by cell.
				//Output the headers
				oSheet.Cells[1, x++] = "Height";
				oSheet.Cells[1, x++] = "Width";
				if (raw_data > 1)
				{
					oSheet.Cells[1, x++] = "X1";
					oSheet.Cells[1, x++] = "Y1";
					if (frequencies == 2)
					{
						oSheet.Cells[1, x++] = "X2";
						oSheet.Cells[1, x++] = "Y2";
					}
				}
				oSheet.Cells[1, x++] = "Value";
				oSheet.Cells[1, x++] = "Status";

				UInt16 row = 2;
				UInt32 spreadsheet_row = 2;
				Int32 pos = 0;

				for (Int32 y_pos = 0; y_pos < cscan_y_pixels; y_pos++)
				{
					for (Int32 x_pos = 0; x_pos < cscan_x_pixels; x_pos++)
					{
						x = 1;

						Byte data_value = 0, status = 0;
						oSheet.Cells[spreadsheet_row, x++] = y_pos.ToString();
						oSheet.Cells[spreadsheet_row, x++] = x_pos.ToString();
						
						if (raw_data > 1)
						{
							//Get the first 24bit number
							Int32 val = (Int32)(cscan_bytes[pos + 3] << 24 | cscan_bytes[pos + 2] << 16 | cscan_bytes[pos + 1] << 8);	//This should keep the sign bit as the MSb
							val /= 256;	//Removes the LSB bit keeps the sign.
							oSheet.Cells[spreadsheet_row, x++] = val.ToString();
							data_value = cscan_bytes[pos];	//Store the data value to output later
							pos += 4;
							val = -(Int32)(cscan_bytes[pos + 3] << 24 | cscan_bytes[pos + 2] << 16 | cscan_bytes[pos + 1] << 8);	//This should keep the sign bit as the MSb
							val /= 256;	//Removes the LSB bit keeps the sign.
							oSheet.Cells[spreadsheet_row, x++] = val.ToString();
							status = cscan_bytes[pos];	//Store the status value to output later
							pos += 4;
							if (frequencies == 2)
							{
								val = (Int32)(cscan_bytes[pos + 3] << 24 | cscan_bytes[pos + 2] << 16 | cscan_bytes[pos + 1] << 8);	//This should keep the sign bit as the MSb
								val /= 256;	//Removes the LSB bit keeps the sign.
								oSheet.Cells[spreadsheet_row, x++] = val.ToString();
								pos += 4;
								val = (Int32)(cscan_bytes[pos + 3] << 24 | cscan_bytes[pos + 2] << 16 | cscan_bytes[pos + 1] << 8);	//This should keep the sign bit as the MSb
								val /= 256;	//Removes the LSB bit keeps the sign.
								pos += 4;
								oSheet.Cells[spreadsheet_row, x++] = val.ToString();
								//pos++;
							}
							oSheet.Cells[spreadsheet_row, x++] = data_value.ToString();
							oSheet.Cells[spreadsheet_row, x++] = status.ToString();
						}
						else //Only read 1 byte which is the data value.
						{
							data_value = cscan_bytes[pos++];	//Store the data value to output later
							oSheet.Cells[spreadsheet_row, x++] = data_value.ToString();
						}
						spreadsheet_row++;
					}
					float percent = ((float)row / (float)cscan_y_pixels);
					MessBox.SetHeaderRowText((percent * 100.0).ToString("f0") + "%");
					row++;
				}
			}

			catch (IndexOutOfRangeException)
			{
				MessageBox.Show("Problem processing data. File shorter than expected.");
			}
			catch (SystemException exc)
			{
				MessageBox.Show("Problem processing data:" + Environment.NewLine + exc.Message);
			}
			finally
			{
				MessBox.Close();
				//Make sure Excel is visible and give the user control
				//of Microsoft Excel's lifetime.
				if (oXL != null)
				{
					oXL.Visible = true;
					oXL.UserControl = true;
				}
			}
		}

		private void btConvertCScanFileCSV_Click(object sender, EventArgs e)
		{
			MessageBoxNONModal MessBox = new MessageBoxNONModal("Exporting to Excel.", "Exporting");
			StreamWriter str = null;

			try
			{
				//Need a save file dialogue!
				SaveFileDialog sfd = new SaveFileDialog();
				//Save the XML file
				string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
				sfd.Title = "Select Save File";
				sfd.InitialDirectory = path;
				sfd.DefaultExt = "csv";
				sfd.Filter = "csv files (*.csv)|*.csv|All files (*.*)|*.*";
				if (sfd.ShowDialog() != DialogResult.OK)
					return;

				str = new StreamWriter(sfd.FileName);

				MessBox.Show(); //Shows the Message Box as Non Modal, ie it won't hold up the main program!

				//Add table headers going cell by cell.
				//Output the headers
				str.Write("Height,");
				str.Write("Width,");
				if (raw_data > 1)
				{
					str.Write("X1,");
					str.Write("Y1,");
					if (frequencies == 2)
					{
						str.Write("X2,");
						str.Write("Y2,");
					}
				}
				str.Write("Value,");
				str.WriteLine("Status");

				UInt16 row = 2;
				UInt32 spreadsheet_row = 2;
				Int32 pos = 0;

				for (Int32 y_pos = 0; y_pos < cscan_y_pixels; y_pos++)
				{
					for (Int32 x_pos = 0; x_pos < cscan_x_pixels; x_pos++)
					{
						Byte data_value = 0, status = 0;
						str.Write(y_pos.ToString() + ",");
						str.Write(x_pos.ToString() + ",");
						
						if (raw_data > 1)
						{
							//Get the first 24bit number
							Int32 val = (Int32)(cscan_bytes[pos + 3] << 24 | cscan_bytes[pos + 2] << 16 | cscan_bytes[pos + 1] << 8);	//This should keep the sign bit as the MSb
							val /= 256;	//Removes the LSB bit keeps the sign.
							str.Write(val.ToString() + ",");
							data_value = cscan_bytes[pos];	//Store the data value to output later
							pos += 4;
							val = -(Int32)(cscan_bytes[pos + 3] << 24 | cscan_bytes[pos + 2] << 16 | cscan_bytes[pos + 1] << 8);	//This should keep the sign bit as the MSb
							val /= 256;	//Removes the LSB bit keeps the sign.
							str.Write(val.ToString() + ",");
							status = cscan_bytes[pos];	//Store the status value to output later
							pos += 4;
							if (frequencies == 2)
							{
								val = (Int32)(cscan_bytes[pos + 3] << 24 | cscan_bytes[pos + 2] << 16 | cscan_bytes[pos + 1] << 8);	//This should keep the sign bit as the MSb
								val /= 256;	//Removes the LSB bit keeps the sign.
								str.Write(val.ToString() + ",");
								pos += 4;
								val = (Int32)(cscan_bytes[pos + 3] << 24 | cscan_bytes[pos + 2] << 16 | cscan_bytes[pos + 1] << 8);	//This should keep the sign bit as the MSb
								val /= 256;	//Removes the LSB bit keeps the sign.
								pos += 4;
								str.Write(val.ToString() + ",");
								//pos++;
							}
							str.Write(data_value.ToString() + ",");
							str.WriteLine(status.ToString());
						}
						else //Only read 1 byte which is the data value.
						{
							data_value = cscan_bytes[pos++];	//Store the data value to output later
							str.WriteLine(data_value.ToString());
						}
					}
					float percent = ((float)row / (float)cscan_y_pixels);
					MessBox.SetHeaderRowText((percent * 100.0).ToString("f0") + "%");
					row++;
				}
			}

			catch (IndexOutOfRangeException)
			{
				MessageBox.Show("Problem processing data. File shorter than expected.");
			}
			catch (SystemException exc)
			{
				MessageBox.Show("Problem processing data:" + Environment.NewLine + exc.Message);
			}
			finally
			{
				MessBox.Close();
				//Close the stream
				if (str != null)
					str.Close();
			}
		}
		/// <summary>
		/// User wants to see the 2nd frequency displayed on thge CScan.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void rbChan2_CheckedChanged(object sender, EventArgs e)
		{
			freq_offset = 1;
			if (cscan_pattern == 2)
				GeneratePolarCScanBitmap();
			else
				GenerateCartesianCScanBitmap();
		}
		/// <summary>
		/// User wants to see the 1st frequency displayed on thge CScan.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void rbChan1_CheckedChanged(object sender, EventArgs e)
		{
			freq_offset = 0;
			if (cscan_pattern == 2)
				GeneratePolarCScanBitmap();
			else
				GenerateCartesianCScanBitmap();
		}
		/// <summary>
		/// User wants to see the Mix channel displayed on thge CScan.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void rbChanMix_CheckedChanged(object sender, EventArgs e)
		{
			freq_offset = 2;
			if (cscan_pattern == 2)
				GeneratePolarCScanBitmap();
			else
				GenerateCartesianCScanBitmap();
		}
	}
}
