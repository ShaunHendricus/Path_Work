"""
Exact Python conversion of MATLAB ETI configuration script
No changes to logic or parameters
"""

import serial
import time
import numpy as np
import struct
import matplotlib.pyplot as plt


BAUD_RATE = 115200
PORT = "COM11"


def wakeupETI(eti, port, baud):
    """Wake up ETI device"""
    xml_command = '<USB_OUTPUT>99</USB_OUTPUT>'
    xml_packet = bytes([1, 0]) + xml_command.encode('utf-8') + bytes([0])
    
    # Try the write - this is where it usually dies
    try:
        eti.write(xml_packet)
        print("Wake command sent, no reconnect needed.")
        return eti  # IMPORTANT: do NOT try to reconnect if this worked
    except Exception as ME:
        print(f"Write caused disconnect (expected): {ME}")
    
    # Now the device is rebooting / waking up - give it a moment
    time.sleep(1.0)
    
    # Make sure any previous object is cleared
    try:
        eti.close()
    except:
        pass
    
    time.sleep(0.5)  # let Windows / driver release the port
    
    # Try to reconnect until it works
    connected = False
    while not connected:
        try:
            eti = serial.Serial(port, baud, timeout=1)
            connected = True
            print(f"ETI is now awake and connected to {port} at {baud} baud.")
        except Exception as ME:
            print(f"Reconnect failed: {ME}\nRetrying...")
            time.sleep(0.5)
    
    return eti


def send_and_check_xml(serialObj, xml_command, timeout=1):
    """Sends an XML command and waits for <INSTRUMENT>...</INSTRUMENT>"""
    response = ''
    
    # Ensure string
    if not isinstance(xml_command, str):
        xml_command = str(xml_command)
    
    # Flush any stale data before sending
    serialObj.reset_input_buffer()
    time.sleep(0.5)
    
    # Build XML packet: [0x01 0x00 xml 0x00]
    xml_packet = bytes([1, 0]) + xml_command.encode('utf-8') + bytes([0])
    serialObj.write(xml_packet)
    
    # Send poll command [0x01 0x00 0x08]
    time.sleep(0.5)
    
    cmd = bytes([1, 0, 8])
    serialObj.write(cmd)
    time.sleep(0.5)
    
    poll_cmd = bytes([1, 10, 0x08])
    serialObj.write(poll_cmd)
    
    # Give instrument a moment to process
    time.sleep(0.05)
    
    # Start reading response
    start_time = time.time()
    buffer = []
    
    while time.time() - start_time < timeout:
        if serialObj.in_waiting > 0:
            try:
                line = serialObj.readline().decode('utf-8', errors='ignore')
                buffer.append(line)
                
                # Stop once closing tag is seen
                if '<INSTRUMENT>ETI300' in line:
                    break
            except:
                pass
    
    if buffer:
        # Find first occurrence of <INSTRUMENT>ETI300
        idx = None
        for i, line in enumerate(buffer):
            if '<INSTRUMENT>ETI300' in line:
                idx = i
                break
        
        if idx is not None:
            clean_buffer = buffer[idx:]
            response = '\n'.join(clean_buffer)
        else:
            response = ""
        
        print('Waking up ETI')
        print(response)
    else:
        print('No valid XML received.')
    
    return response


def set_channel_basic(serialObj, channel, frequency, gainX, gainY, load, connector, timeout=1):
    """
    Set basic parameters for one channel on ETi instrument.
    
    Only writes:
        <CHANNEL>channel
            <FREQUENCY>frequency</FREQUENCY>
            <GAIN_X>gainX</GAIN_X>
            <GAIN_Y>gainY</GAIN_Y>
            <CONNECTOR>connector</CONNECTOR>
        </CHANNEL>
    """
    print('configuring ETI be patience')
    
    # Frequency configuration
    xml = f'<CHANNEL>{channel}\n<FREQUENCY>{frequency}</FREQUENCY>\n</CHANNEL>'
    
    serialObj.reset_input_buffer()
    time.sleep(0.5)
    xml_packet = bytes([1, 0]) + xml.encode('utf-8') + bytes([0])
    serialObj.write(xml_packet)
    time.sleep(0.5)
    cmd = bytes([1, 0, 8])
    serialObj.write(cmd)
    time.sleep(0.5)
    poll_cmd = bytes([1, 10, 0x08])
    serialObj.write(poll_cmd)
    
    # GainX configuration
    xml = f'<CHANNEL>{channel}\n<GAIN_X>{gainX}</GAIN_X>\n</CHANNEL>'
    
    serialObj.reset_input_buffer()
    time.sleep(0.5)
    xml_packet = bytes([1, 0]) + xml.encode('utf-8') + bytes([0])
    serialObj.write(xml_packet)
    time.sleep(0.5)
    cmd = bytes([1, 0, 8])
    serialObj.write(cmd)
    time.sleep(0.5)
    poll_cmd = bytes([1, 10, 0x08])
    serialObj.write(poll_cmd)
    
    # GainY configuration
    xml = f'<CHANNEL>{channel}\n<GAIN_Y>{gainY}</GAIN_Y>\n</CHANNEL>'
    
    serialObj.reset_input_buffer()
    time.sleep(0.5)
    xml_packet = bytes([1, 0]) + xml.encode('utf-8') + bytes([0])
    serialObj.write(xml_packet)
    time.sleep(0.5)
    cmd = bytes([1, 0, 8])
    serialObj.write(cmd)
    time.sleep(0.5)
    poll_cmd = bytes([1, 10, 0x08])
    serialObj.write(poll_cmd)
    
    # Channel selection
    xml = f'<CHANNEL>{channel}</CHANNEL>'
    
    serialObj.reset_input_buffer()
    time.sleep(0.5)
    xml_packet = bytes([1, 0]) + xml.encode('utf-8') + bytes([0])
    serialObj.write(xml_packet)
    time.sleep(0.5)
    cmd = bytes([1, 0, 8])
    serialObj.write(cmd)
    time.sleep(0.5)
    poll_cmd = bytes([1, 10, 0x08])
    serialObj.write(poll_cmd)
    
    # Connector configuration
    xml = f'<CONNECTOR>{connector}</CONNECTOR>'
    
    serialObj.reset_input_buffer()
    time.sleep(0.5)
    xml_packet = bytes([1, 0]) + xml.encode('utf-8') + bytes([0])
    serialObj.write(xml_packet)
    time.sleep(0.5)
    cmd = bytes([1, 0, 8])
    serialObj.write(cmd)
    time.sleep(0.5)
    poll_cmd = bytes([1, 10, 0x08])
    serialObj.write(poll_cmd)
    
    # Load configuration
    xml = f'<LOAD>{load}</LOAD>'
    
    serialObj.reset_input_buffer()
    time.sleep(0.5)
    xml_packet = bytes([1, 0]) + xml.encode('utf-8') + bytes([0])
    serialObj.write(xml_packet)
    time.sleep(0.5)
    cmd = bytes([1, 0, 8])
    serialObj.write(cmd)
    time.sleep(0.5)
    poll_cmd = bytes([1, 10, 0x08])
    serialObj.write(poll_cmd)
    
    # Rest of channels to 0 (actually sets them to Lemo 1-Bridge, not off!)
    
    # Channel 1
    serialObj.reset_input_buffer()
    time.sleep(0.5)
    xml = '<CHANNEL>1</CHANNEL>'
    xml_packet = bytes([1, 0]) + xml.encode('utf-8') + bytes([0])
    serialObj.write(xml_packet)
    time.sleep(0.5)
    cmd = bytes([1, 0, 8])
    serialObj.write(cmd)
    time.sleep(0.5)
    poll_cmd = bytes([1, 10, 0x08])
    serialObj.write(poll_cmd)
    
    serialObj.reset_input_buffer()
    time.sleep(0.5)
    xml = '<CONNECTOR>Lemo 1-Bridge</CONNECTOR>'
    xml_packet = bytes([1, 0]) + xml.encode('utf-8') + bytes([0])
    serialObj.write(xml_packet)
    time.sleep(0.5)
    cmd = bytes([1, 0, 8])
    serialObj.write(cmd)
    time.sleep(0.5)
    poll_cmd = bytes([1, 10, 0x08])
    serialObj.write(poll_cmd)
    
    # Channel 2
    serialObj.reset_input_buffer()
    time.sleep(0.5)
    xml = '<CHANNEL>2</CHANNEL>'
    xml_packet = bytes([1, 0]) + xml.encode('utf-8') + bytes([0])
    serialObj.write(xml_packet)
    time.sleep(0.5)
    cmd = bytes([1, 0, 8])
    serialObj.write(cmd)
    time.sleep(0.5)
    poll_cmd = bytes([1, 10, 0x08])
    serialObj.write(poll_cmd)
    
    serialObj.reset_input_buffer()
    time.sleep(0.5)
    xml = '<CONNECTOR>Lemo 1-Bridge</CONNECTOR>'
    xml_packet = bytes([1, 0]) + xml.encode('utf-8') + bytes([0])
    serialObj.write(xml_packet)
    time.sleep(0.5)
    cmd = bytes([1, 0, 8])
    serialObj.write(cmd)
    time.sleep(0.5)
    poll_cmd = bytes([1, 10, 0x08])
    serialObj.write(poll_cmd)
    
    # Channel 3
    serialObj.reset_input_buffer()
    time.sleep(0.5)
    xml = '<CHANNEL>3</CHANNEL>'
    xml_packet = bytes([1, 0]) + xml.encode('utf-8') + bytes([0])
    serialObj.write(xml_packet)
    time.sleep(0.5)
    cmd = bytes([1, 0, 8])
    serialObj.write(cmd)
    time.sleep(0.5)
    poll_cmd = bytes([1, 10, 0x08])
    serialObj.write(poll_cmd)
    
    serialObj.reset_input_buffer()
    time.sleep(0.5)
    xml = '<CONNECTOR>Lemo 1-Bridge</CONNECTOR>'
    xml_packet = bytes([1, 0]) + xml.encode('utf-8') + bytes([0])
    serialObj.write(xml_packet)
    time.sleep(0.5)
    cmd = bytes([1, 0, 8])
    serialObj.write(cmd)
    time.sleep(0.5)
    poll_cmd = bytes([1, 10, 0x08])
    serialObj.write(poll_cmd)


def StreamETiData(eti, duration=3):
    """
    Stream ETi data for a given duration
    
    Returns Xarray, Yarray, Zarray of measurements
    """
    eti.reset_input_buffer()
    
    Xarray = []
    Yarray = []
    Zarray = []
    sample_count = 0
    
    t0 = time.time()
    
    try:
        while time.time() - t0 < duration:
            # Read ETi data if available
            if eti.in_waiting > 50:
                data = eti.read(eti.in_waiting)
                
                # Find packet start bytes 0x80 0x7F
                packet_start = []
                for i in range(len(data) - 1):
                    if data[i] == 0x80 and data[i+1] == 0x7F:
                        packet_start.append(i)
                
                # Filter valid packet starts
                packet_start = [i for i in packet_start if i + 52 <= len(data)]
                
                for k in range(len(packet_start)):
                    i = packet_start[k]
                    packet = data[i:i+53]
                    
                    # --- VALIDATION CHECKS ---
                    if len(packet) != 53:
                        continue  # discard if not complete
                    
                    if packet[0] != 0x80 or packet[1] != 0x7F:
                        continue  # discard if header is wrong
                    
                    # Extract X and Y (4 bytes each, int32, little-endian)
                    x = struct.unpack('<i', bytes(packet[2:6]))[0]
                    y = struct.unpack('<i', bytes(packet[6:10]))[0]
                    
                    packet_hex = ' '.join(f'{data[i+j]:02x}' for j in range(53))
                    print(f"  DEBUG: Full packet: {packet_hex}")
                    
                    # Store values
                    Xarray.append(float(x))
                    Yarray.append(float(y))
                    Zarray.append(np.sqrt(x**2 + y**2))
                    sample_count += 1
    
    except Exception as ME:
        print('Streaming stopped unexpectedly.')
        raise ME
    
    return Xarray, Yarray, Zarray, data


# ==================== MAIN SCRIPT ====================

if __name__ == "__main__":
    # Serial connections
    eti = serial.Serial(PORT, BAUD_RATE, timeout=1)
    eti.reset_input_buffer()
    eti.write_timeout = 1
    
    # Waking up ETI
    eti = wakeupETI(eti, PORT, BAUD_RATE)
    
    # Change the type of data 5 = post process, 4 = raw, 7 = single channel
    send_and_check_xml(eti, '<USB_OUTPUT>5</USB_OUTPUT>', 1)
    
    # Set the configuration for the channel, frequency, gainX, gainY, load, connector
    set_channel_basic(eti, 0, 2000000, 400, 400, 0, 'Lemo 1-Bridge', 1)
    
    time.sleep(0.5)
    
    # Stream data
    X = []
    Y = []
    Z = []
    
    print("Streaming data for 10 seconds...")
    X, Y, Z, data = StreamETiData(eti, 10)
    
    
    # Plot Y
    plt.figure()
    plt.plot(Y)
    plt.title('Y Values')
    plt.xlabel('Sample')
    plt.ylabel('Y')
    plt.grid(True)
    plt.show()
    
    # Print raw data info
    print(f"Collected {len(X)} samples")
    print(f"Raw data length: {len(data)} bytes")
    
    # Send final command
    eti.write(bytes([1, 10]))
    
    # Close connection
    eti.close()