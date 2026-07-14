# -*- coding: utf-8 -*-
"""
Created on Mon Jun 23 13:22:36 2025

@author: Rylan
"""
import serial
import time
import numpy as np
import matplotlib.pyplot as plt
from matplotlib.animation import FuncAnimation
import struct
from collections import deque


print("Starting ETi 300 communication...")

# Configuration constants
COM_PORT = 'COM4'
BAUD_RATE = 115200
TIMEOUT_S = 1
STREAM_DURATION = 500
WINDOW_SIZE = 10  # seconds visible on plot
MAX_POINTS = 1000  # Maximum points to keep in memory


def send_and_check_xml1(ser, xml_command):
    "Use this function to send commands to acquire data"
    
    # Ensure xml_command is a string
    if not isinstance(xml_command, str):
        xml_command = str(xml_command)
    
    # Build packet: 0x01, 0x00, xml bytes, 0x00 terminator
    pkt = bytes([1, 0]) + xml_command.encode('utf-8') + bytes([0])
    ser.write(pkt)
    
    cmd = bytes([1, 0, 0x08])  # Predefined command (balance)
    ser.write(cmd)

def send_and_check_xml2(ser, xml_command):
    "Use this function to send commands to ETi to set the config"
    
    # Ensure xml_command is a string
    if not isinstance(xml_command, str):
        xml_command = str(xml_command)
    
    # Build packet: 0x01, 0x00, xml bytes, 0x00 terminator
    pkt = bytes([1, 0]) + xml_command.encode('utf-8') + bytes([0])
    ser.write(pkt)

# Initialize serial connection
try:
    eti = serial.Serial(COM_PORT, BAUD_RATE, timeout=TIMEOUT_S)
    eti.reset_input_buffer()  # equivalent to flush(eti)
    print("Serial connection established")
except Exception as e:
    print(f"Error opening serial port: {e}")
    exit(1)

print('PERFORMING BOOTLOADER HANDSHAKE!')

# Tell the loader to run the main firmware
send_and_check_xml2(eti, '<USB_OUTPUT>99</USB_OUTPUT>')
time.sleep(1)

# ETi will drop out and re-enumerate USB
eti.close()
time.sleep(5)  # give it time to reboot

# Reboot the Eti
try:
    eti = serial.Serial(COM_PORT, BAUD_RATE, timeout=TIMEOUT_S)
    eti.reset_input_buffer()
except Exception as e:
    print(f"Error reopening serial port: {e}")
    exit(1)

print('STOPPING ANY ACTIVE STREAM OF DATA FROM ETi 300!')

send_and_check_xml2(eti, '<USB_OUTPUT>0</USB_OUTPUT>')  # stop any streaming of data
time.sleep(1)

send_and_check_xml2(eti, '<USB_RATE>1000</USB_RATE>')  # sample rate for ETi is 16,000 Hz, so sending 1000 will give 16000/1000 (16 Hz)
time.sleep(1)

print('SENDING ETi PROBE CONFIGURATION!')

# Channel 1
c = ['<CHANNEL>0', '<FREQUENCY>1500000</FREQUENCY>']  # FREQUENCY sent as Hz ie 1.5 MHz would be 1500000 Hz
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>0', '<PHASE>300000</PHASE>']  # PHASE sent as factor of 1000 ie 180 would be 180000
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>0', '<GAIN_X>600</GAIN_X>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>0', '<GAIN_Y>900</GAIN_Y>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>0', '<HP>10000</HP>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>0', '<LP>40000</LP>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

# Channel 2
c = ['<CHANNEL>1', '<FREQUENCY>56000</FREQUENCY>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>1', '<PHASE>180000</PHASE>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>1', '<GAIN_X>600</GAIN_X>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>1', '<GAIN_Y>900</GAIN_Y>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>1', '<HP>400</HP>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>1', '<LP>4000</LP>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

# Channel 3
c = ['<CHANNEL>2', '<FREQUENCY>20000</FREQUENCY>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>2', '<PHASE>200000</PHASE>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>2', '<GAIN_X>600</GAIN_X>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>2', '<GAIN_Y>900</GAIN_Y>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>2', '<HP>400</HP>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>2', '<LP>4000</LP>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

# Channel 4
c = ['<CHANNEL>3', '<FREQUENCY>15000</FREQUENCY>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>3', '<PHASE>300000</PHASE>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>3', '<GAIN_X>600</GAIN_X>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>3', '<GAIN_Y>900</GAIN_Y>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>3', '<HP>400</HP>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

c = ['<CHANNEL>3', '<LP>4000</LP>']
c = '\r'.join(c)
send_and_check_xml2(eti, c)
time.sleep(0.5)

print('REQUESTING PROBE CONFIGURATION VALUES FROM ETi!')

# Helper function to read parameter
def read_parameter(eti, channel, param_code):
    cmd = bytes([4, 0x3F, channel, param_code])
    eti.write(cmd)
    
    t0 = time.time()
    while eti.in_waiting < 7:
        time.sleep(0.001)
        if time.time() - t0 > 1:
            raise Exception('Timed-out waiting for parameter reply')
    
    reply = eti.read(7)
    
    # Convert to big-endian 32-bit value
    raw32 = (reply[3] << 24) + (reply[4] << 16) + (reply[5] << 8) + reply[6]
    
    return raw32

# Channel 1 parameters
raw32 = read_parameter(eti, 0, 0x46)
freq = raw32 / 1000
print(f'Frequency channel 1: {freq:.1f} kHz')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 0, 0x58)
gain_dB = raw32 / 10
print(f'Gain-X channel 1: {gain_dB:.1f} dB')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 0, 0x59)
gain_dB = raw32 / 10
print(f'Gain-Y channel 1: {gain_dB:.1f} dB')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 0, 0x50)
phase = raw32 / 10
print(f'Phase channel 1: {phase:.1f} degrees')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 0, 0x48)
HP = raw32
print(f'High-Pass Filter channel 1: {HP:.1f} Hz')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 0, 0x4C)
LP = raw32
print(f'Low-Pass Filter channel 1: {LP:.1f} Hz')
eti.reset_input_buffer()
time.sleep(0.5)

# Channel 2 parameters
raw32 = read_parameter(eti, 1, 0x46)
freq = raw32 / 1000
print(f'Frequency channel 2: {freq:.1f} kHz')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 1, 0x58)
gain_dB = raw32 / 10
print(f'Gain-X channel 2: {gain_dB:.1f} dB')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 1, 0x59)
gain_dB = raw32 / 10
print(f'Gain-Y channel 2: {gain_dB:.1f} dB')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 1, 0x50)
phase = raw32 / 10
print(f'Phase channel 2: {phase:.1f} degrees')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 1, 0x48)
HP = raw32
print(f'High-Pass Filter channel 2: {HP:.1f} Hz')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 1, 0x4C)
LP = raw32
print(f'Low-Pass Filter channel 2: {LP:.1f} Hz')
eti.reset_input_buffer()
time.sleep(0.5)

# Channel 3 parameters
raw32 = read_parameter(eti, 2, 0x46)
freq = raw32 / 1000
print(f'Frequency channel 3: {freq:.1f} kHz')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 2, 0x58)
gain_dB = raw32 / 10
print(f'Gain-X channel 3: {gain_dB:.1f} dB')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 2, 0x59)
gain_dB = raw32 / 10
print(f'Gain-Y channel 3: {gain_dB:.1f} dB')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 2, 0x50)
phase = raw32 / 10
print(f'Phase channel 3: {phase:.1f} degrees')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 2, 0x48)
HP = raw32
print(f'High-Pass Filter channel 3: {HP:.1f} Hz')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 2, 0x4C)
LP = raw32
print(f'Low-Pass Filter channel 3: {LP:.1f} Hz')
eti.reset_input_buffer()
time.sleep(0.5)

# Channel 4 parameters
raw32 = read_parameter(eti, 3, 0x46)
freq = raw32 / 1000
print(f'Frequency channel 4: {freq:.1f} kHz')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 3, 0x58)
gain_dB = raw32 / 10
print(f'Gain-X channel 4: {gain_dB:.1f} dB')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 3, 0x59)
gain_dB = raw32 / 10
print(f'Gain-Y channel 4: {gain_dB:.1f} dB')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 3, 0x50)
phase = raw32 / 10
print(f'Phase channel 4: {phase:.1f} degrees')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 3, 0x48)
HP = raw32
print(f'High-Pass Filter channel 4: {HP:.1f} Hz')
eti.reset_input_buffer()
time.sleep(0.5)

raw32 = read_parameter(eti, 3, 0x4C)
LP = raw32
print(f'Low-Pass Filter channel 4: {LP:.1f} Hz')
eti.reset_input_buffer()

# Send the command for format 5 and stream x1 and y1 values
print('SENDING PROCESSED DATA COMMAND (FORMAT 5)!')

send_and_check_xml1(eti, '<USB_OUTPUT>5</USB_OUTPUT>')  # format 5
time.sleep(1)

# Format 5 stream
print('STREAMING X,Y PAIR VALUES FOR ALL 4 CHANNELS!')

# Use deques for efficient data management
x1 = deque(maxlen=MAX_POINTS)
y1 = deque(maxlen=MAX_POINTS)
x2 = deque(maxlen=MAX_POINTS)
y2 = deque(maxlen=MAX_POINTS)
x3 = deque(maxlen=MAX_POINTS)
y3 = deque(maxlen=MAX_POINTS)
x4 = deque(maxlen=MAX_POINTS)
y4 = deque(maxlen=MAX_POINTS)
t_values = deque(maxlen=MAX_POINTS)

# Setup plots with matplotlib animation
fig, ((ax_x1, ax_x2), (ax_y1, ax_y2)) = plt.subplots(2, 2, figsize=(12, 8))
fig.suptitle('Live Scroll Plots')

# Initialize empty line objects
line_x1, = ax_x1.plot([], [], 'b-')
ax_x1.set_xlabel('Time (s)')
ax_x1.set_ylabel('X1')
ax_x1.set_title('X1 vs Time')
ax_x1.grid(True)

line_y1, = ax_y1.plot([], [], 'r-')
ax_y1.set_xlabel('Time (s)')
ax_y1.set_ylabel('Y1')
ax_y1.set_title('Y1 vs Time')
ax_y1.grid(True)

line_x2, = ax_x2.plot([], [], 'b-')
ax_x2.set_xlabel('Time (s)')
ax_x2.set_ylabel('X2')
ax_x2.set_title('X2 vs Time')
ax_x2.grid(True)

line_y2, = ax_y2.plot([], [], 'r-')
ax_y2.set_xlabel('Time (s)')
ax_y2.set_ylabel('Y2')
ax_y2.set_title('Y2 vs Time')
ax_y2.grid(True)

plt.tight_layout()

# Global variables for animation
t0 = time.time()
running = True

def update_plot(frame):
    """Animation update function"""
    global running
    
    if not running:
        return line_x1, line_y1, line_x2, line_y2
    
    # Check if streaming duration exceeded
    if time.time() - t0 > STREAM_DURATION:
        running = False
        return line_x1, line_y1, line_x2, line_y2
    
    # Read available data
    if eti.in_waiting < 50:  # format 5
        return line_x1, line_y1, line_x2, line_y2
    
    data = eti.read(eti.in_waiting)
    
    # Find packet starts: format 5
    pkt_starts = []
    for i in range(len(data) - 1):
        if data[i] == 128 and data[i + 1] == 127:
            pkt_starts.append(i)
    
    # Filter valid packet starts (ensure full packet available)
    pkt_starts = [start for start in pkt_starts if start + 49 < len(data)]
    
    for start_idx in pkt_starts:
        i = start_idx
        pkt = data[i:i + 50]
        
        # parse X1, X2, X3, X4
        rawX1 = data[i + 3] + (data[i + 4] * 256) + (data[i + 5] * 256 * 256)
        rawX1 = rawX1 - (2**24 if rawX1 >= 2**23 else 0)
        X1 = rawX1
        
        rawX2 = data[i + 11] + (data[i + 12] * 256) + (data[i + 13] * 256 * 256)
        rawX2 = rawX2 - (2**24 if rawX2 >= 2**23 else 0)
        X2 = rawX2
        
        rawX3 = data[i + 19] + (data[i + 20] * 256) + (data[i + 21] * 256 * 256)
        rawX3 = rawX3 - (2**24 if rawX3 >= 2**23 else 0)
        X3 = rawX3
        
        rawX4 = data[i + 27] + (data[i + 28] * 256) + (data[i + 29] * 256 * 256)
        rawX4 = rawX4 - (2**24 if rawX4 >= 2**23 else 0)
        X4 = rawX4
        
        # parse Y1, Y2, Y3, Y4
        rawY1 = data[i + 7] + (data[i + 8] * 256) + (data[i + 9] * 256 * 256)
        rawY1 = rawY1 - (2**24 if rawY1 >= 2**23 else 0)
        Y1 = rawY1
        
        rawY2 = data[i + 15] + (data[i + 16] * 256) + (data[i + 17] * 256 * 256)
        rawY2 = rawY2 - (2**24 if rawY2 >= 2**23 else 0)
        Y2 = rawY2
        
        rawY3 = data[i + 23] + (data[i + 24] * 256) + (data[i + 25] * 256 * 256)
        rawY3 = rawY3 - (2**24 if rawY3 >= 2**23 else 0)
        Y3 = rawY3
        
        rawY4 = data[i + 31] + (data[i + 32] * 256) + (data[i + 33] * 256 * 256)
        rawY4 = rawY4 - (2**24 if rawY4 >= 2**23 else 0)
        Y4 = rawY4
        
        # Store values
        x1.append(X1)
        y1.append(Y1)
        x2.append(X2)
        y2.append(Y2)
        x3.append(X3)
        y3.append(Y3)
        x4.append(X4)
        y4.append(Y4)
        t_values.append(time.time() - t0)
    
    # Update plots only if we have data
    if len(t_values) > 0:
        current_time = t_values[-1]
        
        # Convert deques to lists for plotting
        t_list = list(t_values)
        x1_list = list(x1)
        y1_list = list(y1)
        x2_list = list(x2)
        y2_list = list(y2)
        
        if current_time > WINDOW_SIZE:
            # Find indices for the time window
            start_time = current_time - WINDOW_SIZE
            valid_indices = [i for i, t in enumerate(t_list) if t >= start_time]
            
            plot_times = [t_list[i] for i in valid_indices]
            plot_x1 = [x1_list[i] for i in valid_indices]
            plot_y1 = [y1_list[i] for i in valid_indices]
            plot_x2 = [x2_list[i] for i in valid_indices]
            plot_y2 = [y2_list[i] for i in valid_indices]
            
            xlim = [current_time - WINDOW_SIZE, current_time]
        else:
            plot_times = t_list
            plot_x1 = x1_list
            plot_y1 = y1_list
            plot_x2 = x2_list
            plot_y2 = y2_list
            xlim = [0, WINDOW_SIZE]
        
        # Update line data
        line_x1.set_data(plot_times, plot_x1)
        line_y1.set_data(plot_times, plot_y1)
        line_x2.set_data(plot_times, plot_x2)
        line_y2.set_data(plot_times, plot_y2)
        
        # Update axis limits
        for ax in [ax_x1, ax_y1, ax_x2, ax_y2]:
            ax.set_xlim(xlim)
            ax.relim()
            ax.autoscale_view(scalex=False, scaley=True)
    
    return line_x1, line_y1, line_x2, line_y2

# Create animation
ani = FuncAnimation(fig, update_plot, interval=50, blit=True, cache_frame_data=False)

# Show plot
plt.show()

# Keep the script running until animation completes
try:
    while running:
        plt.pause(0.1)
except KeyboardInterrupt:
    print("\nStopping data stream...")
    running = False

print('Done streaming.')

# Close serial connection
eti.close()

# Keep plot window open
plt.show(block=True)