# -*- coding: utf-8 -*-
"""
Created on Thu Jun 29 17:03

@author: Rylan

"""
"""
Changes made:
    1. Added connector type control
    2. Fixed _parse_format5_packet: replaced incorrect 3-byte manual extraction with
       struct.unpack_from('<i', ...) 4-byte int32 reads. 
"""
import serial
import time
import numpy as np
import struct
import threading
from collections import deque
from dataclasses import dataclass
from typing import Optional, Dict, List, Tuple
from enum import Enum
import pyqtgraph as pg
from pyqtgraph.Qt import QtWidgets, QtCore


class ConnectorType(Enum):  #Always stick to these connector types. Lemo 1 is essentially probe 1 connector, Lemo 2 is probe 2 connector.
    OFF = 'off'
    BNC = 'BNC'
    LEMO_1_BRIDGE = 'Lemo 1-Bridge'
    LEMO_1_ABS = 'Lemo 1-Abs'
    LEMO_1_REFL = 'Lemo 1-Refl'
    LEMO_2_BRIDGE = 'Lemo 2-Bridge'
    LEMO_2_REFL = 'Lemo 2-Refl'
    


@dataclass
class ChannelConfig:
    "Configuration for a single ETi channel"
    connector: ConnectorType
    frequency: int  # in Hz ie 50 to 5000000 (50Hz to 5MHz).
    phase: int  # in thousandths of a degree but in steps of 100 (0.1 degrees), ie 180.4 is 180400.
    gain_x: int  # in 10ths of a dB from 0 to 70dB (0 to 700).
    gain_y: int  # in 10ths of a dB from 0 to 70dB (0 to 700).
    hp_filter: int  # in 100ths of a Hz from 0.01Hz steps to 2kHz with 0 being DC (0 to 200000).
    lp_filter: int  # in 100ths of a Hz from In 0.01Hz steps to 4kHz with 0 being DC. (0-400000).


class ETi300:
    "Class for communicating with and visualizing data from ETi 300 device"
    
    # Configuration constants
    DEFAULT_BAUD_RATE = 115200
    DEFAULT_TIMEOUT = 1
    DEFAULT_WINDOW_SIZE = 10  # seconds visible on plot
    DEFAULT_MAX_POINTS = 1000  # Maximum points to keep in memory
    
    # Parameter codes for reading configuration
    PARAM_CODES = {
        'frequency': 0x46,
        'gain_x': 0x58,
        'gain_y': 0x59,
        'phase': 0x50,
        'hp_filter': 0x48,
        'lp_filter': 0x4C
    }
    
    def __init__(self, com_port: str = 'COM4', baud_rate: int = None, timeout: float = None):
        """
        Initialize ETi 300 communication
        
        Args:
            com_port: Serial port name (e.g., 'COM4' on Windows, '/dev/ttyUSB0' on Linux)
            baud_rate: Serial baud rate (default: 115200)
            timeout: Serial timeout in seconds (default: 1)
        """
        self.com_port = com_port
        self.baud_rate = baud_rate or self.DEFAULT_BAUD_RATE
        self.timeout = timeout or self.DEFAULT_TIMEOUT
        
        self.serial_port: Optional[serial.Serial] = None
        self.running = False
        self.animation = None
        self.start_time = None
        
        # Data storage
        self.data_queues = {
            'x1': deque(maxlen=self.DEFAULT_MAX_POINTS),
            'y1': deque(maxlen=self.DEFAULT_MAX_POINTS),
            'x2': deque(maxlen=self.DEFAULT_MAX_POINTS),
            'y2': deque(maxlen=self.DEFAULT_MAX_POINTS),
            'x3': deque(maxlen=self.DEFAULT_MAX_POINTS),
            'y3': deque(maxlen=self.DEFAULT_MAX_POINTS),
            'x4': deque(maxlen=self.DEFAULT_MAX_POINTS),
            'y4': deque(maxlen=self.DEFAULT_MAX_POINTS),
            't': deque(maxlen=self.DEFAULT_MAX_POINTS)
        }
        
        # Channel configurations, refer to instructions for value range in class ChannelConfig
        # frequency: int  # in Hz ie 50 to 5000000 (50Hz to 5MHz).
        # phase: int  # in thousandths of a degree but in steps of 100 (0.1 degrees), ie 180.4 is 180400.
        # gain_x: int  # in 10ths of a dB from 0 to 70dB (0 to 700).
        # gain_y: int  # in 10ths of a dB from 0 to 70dB (0 to 700).
        # hp_filter: int  # in 100ths of a Hz from 0.01Hz steps to 2kHz with 0 being DC (0 to 200000).
        # lp_filter: int  # in 100ths of a Hz from In 0.01Hz steps to 4kHz with 0 being DC. (0-400000).
        # THESE VALUES ARE ONLY PLACEHOLDERS!! PLEASE ENTER YOUR VALUES THAT ARE WITHIN THE LIMIT AS STATED ABOVE!!
        self.channel_configs = {
            0: ChannelConfig(ConnectorType.LEMO_1_BRIDGE,2000000, 0, 400, 500, 0, 3000), #connector type,frequency, phase, gainx, gainy,hp filter,lp filter
            1: ChannelConfig(ConnectorType.LEMO_1_BRIDGE,500000, 0, 400, 500, 0, 3000),
            2: ChannelConfig(ConnectorType.OFF,30000, 200000, 300, 400, 5000, 4000),
            3: ChannelConfig(ConnectorType.OFF,18000, 300000, 300, 500, 4600, 4000)
        }
        
        # Plotting elements
        self.fig = None
        self.axes = None
        self.lines = None
    
    def connect(self):
        "Establish serial connection to ETi 300"
        try:
            self.serial_port = serial.Serial(self.com_port, self.baud_rate, timeout=self.timeout)
            self.serial_port.reset_input_buffer()
            print("Serial connection established")
            return True
        except Exception as e:
            print(f"Error opening serial port: {e}")
            return False
    
    def disconnect(self):
        "Close serial connection"
        if self.serial_port and self.serial_port.is_open:
            self.serial_port.close()
            print("Serial connection closed")
    
    def _send_xml_acquire(self, xml_command: str):
        "Send XML command to acquire data"
        if not isinstance(xml_command, str):
            xml_command = str(xml_command)
        
        # Build packet: 0x01, 0x00, xml bytes, 0x00 terminator
        pkt = bytes([1, 0]) + xml_command.encode('utf-8') + bytes([0])
        self.serial_port.write(pkt)
        
        cmd = bytes([1, 0, 0x08])  # Predefined command (balance of probe)
        self.serial_port.write(cmd)
    
    def _send_xml_config(self, xml_command: str):
        "Send XML command to set configuration"
        if not isinstance(xml_command, str):
            xml_command = str(xml_command)
        
        # Build packet: 0x01, 0x00, xml bytes, 0x00 terminator
        pkt = bytes([1, 0]) + xml_command.encode('utf-8') + bytes([0])
        self.serial_port.write(pkt)
    
    def perform_bootloader_handshake(self):
        "Perform bootloader handshake and restart ETi"
        print('PERFORMING BOOTLOADER HANDSHAKE!')
        
        # Tell the loader to run the main firmware
        self._send_xml_config('<USB_OUTPUT>99</USB_OUTPUT>')
        time.sleep(1)
        
        # ETi will drop out and re-enumerate USB
        self.disconnect()
        time.sleep(5)  # give it time to reboot
        
        # Reconnect
        if not self.connect():
            raise Exception("Failed to reconnect after bootloader handshake")
    
    def configure_device(self, sample_rate_divisor: int = 1000):
        """
        Configure basic device settings
        
        Args:
            sample_rate_divisor: ETi sample rate is 16kHz, so 1000 gives 16Hz output
        """
        print('STOPPING ANY ACTIVE STREAM OF DATA FROM ETi 300!')
        self._send_xml_config('<USB_OUTPUT>0</USB_OUTPUT>')
        time.sleep(1)
        
        self._send_xml_config(f'<USB_RATE>{sample_rate_divisor}</USB_RATE>')
        time.sleep(1)
    
    def configure_channel(self, channel: int, config: Optional[ChannelConfig] = None):
        """
        Configure a single channel
        
        Args:
            channel: Channel number (0-3)
            config: ChannelConfig object (uses default if None)
        """
        if config is None:
            config = self.channel_configs[channel]
        else:
            self.channel_configs[channel] = config
        
        print(f'Configuring channel {channel + 1}...')
        
        # Send each parameter
        commands = [
            f'<CHANNEL>{channel}</CHANNEL>',
            f'<CONNECTOR>{config.connector.value}</CONNECTOR>',
            f'<CHANNEL>{channel}\r<FREQUENCY>{config.frequency}</FREQUENCY>',
            f'<CHANNEL>{channel}\r<PHASE>{config.phase}</PHASE>',
            f'<CHANNEL>{channel}\r<GAIN_X>{config.gain_x}</GAIN_X>',
            f'<CHANNEL>{channel}\r<GAIN_Y>{config.gain_y}</GAIN_Y>',
            f'<CHANNEL>{channel}\r<HP>{config.hp_filter}</HP>',
            f'<CHANNEL>{channel}\r<LP>{config.lp_filter}</LP>'
        ]
        
        for cmd in commands:
            self._send_xml_config(cmd)
            time.sleep(0.5)
    
    def configure_all_channels(self):
        "Configure all channels with their stored configurations"
        print('SENDING ETi PROBE CONFIGURATION!')
        for channel in range(4):
            self.configure_channel(channel)
    
    def _reconnect(self, wait: float = 3.0):
        """
        Close and reopen the serial port — handles USB re-enumeration after
        certain commands cause the device to disconnect (same as MATLAB wakeupETI retry loop).
        """
        print(f'  Device disconnected — waiting {wait}s for re-enumeration...')
        try:
            self.serial_port.close()
        except Exception:
            pass
        time.sleep(wait)
        connected = False
        attempts = 0
        while not connected and attempts < 10:
            try:
                self.serial_port = serial.Serial(self.com_port, self.baud_rate, timeout=self.timeout)
                self.serial_port.reset_input_buffer()
                connected = True
                print('  Reconnected.')
            except Exception as e:
                attempts += 1
                print(f'  Reconnect attempt {attempts} failed: {e}')
                time.sleep(0.5)
        if not connected:
            raise Exception('Failed to reconnect to ETi after USB re-enumeration')

    def read_parameter(self, channel: int, param_name: str) -> int:
        """
        Read a parameter value from the ETi.

        The device sometimes drops the USB connection when it receives a query
        command ([4, 0x3F, ...]) — this matches the bootloader handshake behaviour.
        This method detects that, reconnects, and retries automatically.

        Args:
            channel: Channel number (0-3)
            param_name: Parameter name (frequency, gain_x, gain_y, phase, hp_filter, lp_filter)

        Returns:
            Raw parameter value as unsigned 32-bit integer
        """
        param_code = self.PARAM_CODES[param_name]

        for attempt in range(3):  # retry up to 3 times in case of reconnect
            try:
                # Flush stale data before querying
                self.serial_port.reset_input_buffer()
                time.sleep(0.1)

                cmd = bytes([4, 0x3F, channel, param_code])
                self.serial_port.write(cmd)
                print(f'  Querying {param_name}: sent {list(cmd)}')

                # Accumulate bytes and scan for 0xC0 0x3F ACK header
                accumulated = bytearray()
                t0 = time.time()
                timeout = 2.0

                while time.time() - t0 < timeout:
                    try:
                        if self.serial_port.in_waiting > 0:
                            accumulated += self.serial_port.read(self.serial_port.in_waiting)
                    except Exception:
                        # Port dropped mid-read — break out to reconnect
                        raise serial.SerialException('Port lost during read')

                    # Scan for ACK header 0xC0 0x3F
                    for i in range(len(accumulated) - 6):
                        if accumulated[i] == 0xC0 and accumulated[i + 1] == 0x3F:
                            reply = accumulated[i:i + 7]
                            if reply[2] != param_code:
                                print(f'  Warning: expected 0x{param_code:02X}, got 0x{reply[2]:02X} — skipping')
                                continue
                            print(f'  Received: {list(reply)}')
                            raw32 = (reply[3] << 24) | (reply[4] << 16) | (reply[5] << 8) | reply[6]
                            return raw32

                    time.sleep(0.01)

                # Timed out on this attempt — check if port is still alive
                if not self.serial_port.is_open:
                    raise serial.SerialException('Port closed after query')

                raise Exception(f'Timed-out waiting for {param_name} reply on channel {channel}')

            except serial.SerialException as e:
                print(f'  Serial error on attempt {attempt + 1}: {e}')
                self._reconnect(wait=3.0)
                print(f'  Retrying {param_name} query...')
                continue

        raise Exception(f'Failed to read {param_name} on channel {channel} after 3 attempts')
    
    def verify_channel_config(self, channel: int) -> Dict[str, float]:
        """
        Read and verify all parameters for a channel
        
        Args:
            channel: Channel number (0-3)
            
        Returns:
            Dictionary of parameter values
        """
        print(f'\nReading channel {channel + 1} configuration:')
        
        values = {}
        
        # Read frequency
        raw = self.read_parameter(channel, 'frequency')
        values['frequency_khz'] = raw / 1000
        print(f'  Frequency: {values["frequency_khz"]:.1f} kHz')
        self.serial_port.reset_input_buffer()
        time.sleep(0.5)
        
        # Read gains
        raw = self.read_parameter(channel, 'gain_x')
        values['gain_x_db'] = raw / 10
        print(f'  Gain-X: {values["gain_x_db"]:.1f} dB')
        self.serial_port.reset_input_buffer()
        time.sleep(0.5)
        
        raw = self.read_parameter(channel, 'gain_y')
        values['gain_y_db'] = raw / 10
        print(f'  Gain-Y: {values["gain_y_db"]:.1f} dB')
        self.serial_port.reset_input_buffer()
        time.sleep(0.5)
        
        # Read phase
        raw = self.read_parameter(channel, 'phase')
        values['phase_deg'] = raw / 10
        print(f'  Phase: {values["phase_deg"]:.1f} degrees')
        self.serial_port.reset_input_buffer()
        time.sleep(0.5)
        
        # Read filters
        raw = self.read_parameter(channel, 'hp_filter')
        values['hp_filter_hz'] = raw
        print(f'  High-Pass Filter: {values["hp_filter_hz"]:.1f} Hz')
        self.serial_port.reset_input_buffer()
        time.sleep(0.5)
        
        raw = self.read_parameter(channel, 'lp_filter')
        values['lp_filter_hz'] = raw
        print(f'  Low-Pass Filter: {values["lp_filter_hz"]:.1f} Hz')
        self.serial_port.reset_input_buffer()
        time.sleep(0.5)
        
        return values
    
    def verify_all_channels(self) -> Dict[int, Dict[str, float]]:
        "Verify configuration of all channels"
        print('REQUESTING PROBE CONFIGURATION VALUES FROM ETi!')
        results = {}
        for channel in range(4):
            results[channel] = self.verify_channel_config(channel)
        return results
    
    def _parse_format5_packet(self, data: bytes, start_idx: int) -> Dict[str, int]:
        """Parse a format 5 data packet.
        
        Packet layout (offsets from packet start, 0-indexed):
          [0-1]   : 0x80 0x7F  (sync header)
          [2-5]   : X1  (int32, little-endian)
          [6-9]   : Y1  (int32, little-endian)
          [10-13] : X2  (int32, little-endian)
          [14-17] : Y2  (int32, little-endian)
          [18-21] : X3  (int32, little-endian)
          [22-25] : Y3  (int32, little-endian)
          [26-29] : X4  (int32, little-endian)
          [30-33] : Y4  (int32, little-endian)
        
        Matches MATLAB typecast(uint8(packet(3:6)), 'int32') etc. (1-indexed → 0-indexed offset 2).
        """
        i = start_idx

        # Each value is a 4-byte little-endian signed int32, matching MATLAB's typecast(...,'int32')
        X1, = struct.unpack_from('<i', data, i + 2)
        Y1, = struct.unpack_from('<i', data, i + 6)
        X2, = struct.unpack_from('<i', data, i + 10)
        Y2, = struct.unpack_from('<i', data, i + 14)
        X3, = struct.unpack_from('<i', data, i + 18)
        Y3, = struct.unpack_from('<i', data, i + 22)
        X4, = struct.unpack_from('<i', data, i + 26)
        Y4, = struct.unpack_from('<i', data, i + 30)

        return {
            'x1': X1, 'y1': Y1,
            'x2': X2, 'y2': Y2,
            'x3': X3, 'y3': Y3,
            'x4': X4, 'y4': Y4
        }
    
    def _setup_plots(self):
        "Setup PyQtGraph window with 4 subplots (X1, Y1, X2, Y2 vs Time)"
        self.app = QtWidgets.QApplication.instance() or QtWidgets.QApplication([])

        self.win = pg.GraphicsLayoutWidget(title="ETi 300 Live Data")
        self.win.resize(1200, 800)
        self.win.setWindowTitle("ETi 300 Live Data")

        # Row 0: X1, X2
        ax_x1 = self.win.addPlot(row=0, col=0, title="X1 vs Time")
        ax_x2 = self.win.addPlot(row=0, col=1, title="X2 vs Time")
        # Row 1: Y1, Y2
        ax_y1 = self.win.addPlot(row=1, col=0, title="Y1 vs Time")
        ax_y2 = self.win.addPlot(row=1, col=1, title="Y2 vs Time")

        for ax, xlabel, ylabel in [
            (ax_x1, "Time (s)", "X1"),
            (ax_x2, "Time (s)", "X2"),
            (ax_y1, "Time (s)", "Y1"),
            (ax_y2, "Time (s)", "Y2"),
        ]:
            ax.showGrid(x=True, y=True, alpha=0.3)
            ax.setLabel("bottom", xlabel)
            ax.setLabel("left", ylabel)
            ax.enableAutoRange(axis='y', enable=True)
            ax.setMouseEnabled(x=False, y=False)  # disable pan/zoom during live stream

        self.axes = {'x1': ax_x1, 'y1': ax_y1, 'x2': ax_x2, 'y2': ax_y2}

        # PlotDataItems — PyQtGraph equivalent of matplotlib Line2D
        self.lines = {
            'x1': ax_x1.plot(pen=pg.mkPen('b', width=1)),
            'y1': ax_y1.plot(pen=pg.mkPen('r', width=1)),
            'x2': ax_x2.plot(pen=pg.mkPen('b', width=1)),
            'y2': ax_y2.plot(pen=pg.mkPen('r', width=1)),
        }

        self.win.show()

    def start_streaming(self, duration: float = 500, output_format: int = 5):
        """
        Start streaming data from ETi 300.

        Architecture — two completely separated threads:
          SERIAL THREAD  : reads bytes, parses packets, appends to numpy ring buffer.
                           Never touches the GUI.
          GUI MAIN THREAD: QTimer fires at 30 fps, grabs a snapshot of the buffer,
                           calls setData(). Never touches serial.

       

        Args:
            duration: Streaming duration in seconds
            output_format: ETi output format (default: 5 for X,Y pairs)
        """
        PLOT_FPS        = 30       # GUI refresh rate
        CHUNK_SIZE      = 4096     # pre-allocated numpy chunk size; grows in chunks

        print(f'SENDING PROCESSED DATA COMMAND (FORMAT {output_format})!')
        self._send_xml_acquire(f'<USB_OUTPUT>{output_format}</USB_OUTPUT>')
        time.sleep(1)
        print('STREAMING X,Y PAIR VALUES FOR ALL 4 CHANNELS!')

        # ── Shared data buffer (written by serial thread, read by GUI thread) ──────
        # Pre-allocate numpy arrays in chunks — avoids repeated reallocation
        _lock       = threading.Lock()
        _n          = [0]           # sample count, inside list for closure mutation
        _capacity   = [CHUNK_SIZE]

        def _make_arrays(cap):
            return {k: np.empty(cap, dtype=np.float64)
                    for k in ('t','x1','y1','x2','y2','x3','y3','x4','y4')}

        _buf = _make_arrays(CHUNK_SIZE)

        def _append(t, x1, y1, x2, y2, x3, y3, x4, y4):
            """Append one sample to the shared buffer. Grows buffer if needed."""
            n = _n[0]
            if n >= _capacity[0]:
                new_cap = _capacity[0] + CHUNK_SIZE
                new_buf = _make_arrays(new_cap)
                for k in _buf:
                    new_buf[k][:_capacity[0]] = _buf[k]
                _buf.update(new_buf)
                _capacity[0] = new_cap
            _buf['t'][n]  = t
            _buf['x1'][n] = x1;  _buf['y1'][n] = y1
            _buf['x2'][n] = x2;  _buf['y2'][n] = y2
            _buf['x3'][n] = x3;  _buf['y3'][n] = y3
            _buf['x4'][n] = x4;  _buf['y4'][n] = y4
            _n[0] = n + 1

        # ── Serial reader thread ───────────────────────────────────────────────────
        self.start_time = time.time()
        self.running    = True

        def _serial_reader():
            t0 = self.start_time
            while self.running and (time.time() - t0) < duration:
                if self.serial_port.in_waiting < 53:
                    continue   # spin — no sleep, keeps latency minimal
                raw = self.serial_port.read(self.serial_port.in_waiting)
                now = time.time() - t0
                i = 0
                while i < len(raw) - 1:
                    if raw[i] == 0x80 and raw[i + 1] == 0x7F:
                        if i + 52 >= len(raw):
                            break
                        v = self._parse_format5_packet(raw, i)
                        with _lock:
                            _append(now,
                                    v['x1'], v['y1'],
                                    v['x2'], v['y2'],
                                    v['x3'], v['y3'],
                                    v['x4'], v['y4'])
                        i += 53
                    else:
                        i += 1
            self.running = False

        serial_thread = threading.Thread(target=_serial_reader, daemon=True)

        # ── PyQtGraph GUI setup ────────────────────────────────────────────────────
        self._setup_plots()

        def _gui_update():
            """Called by QTimer on the main thread. Snapshots buffer, updates plots."""
            with _lock:
                n = _n[0]
                if n == 0:
                    return
                # Slice a view — no copy needed, PyQtGraph handles it
                t_snap  = _buf['t'][:n]
                x1_snap = _buf['x1'][:n]
                y1_snap = _buf['y1'][:n]
                x2_snap = _buf['x2'][:n]
                y2_snap = _buf['y2'][:n]

            # setData on pre-sliced numpy arrays is the fastest path in PyQtGraph
            self.lines['x1'].setData(t_snap, x1_snap)
            self.lines['y1'].setData(t_snap, y1_snap)
            self.lines['x2'].setData(t_snap, x2_snap)
            self.lines['y2'].setData(t_snap, y2_snap)

            elapsed = time.time() - self.start_time
            for ax in self.axes.values():
                ax.setXRange(0, max(elapsed, 1), padding=0)

            # Stop timer once serial thread is done
            if not self.running:
                timer.stop()
                for ax in self.axes.values():
                    ax.enableAutoRange()

        timer = QtCore.QTimer()
        timer.setInterval(int(1000 / PLOT_FPS))   # ms
        timer.timeout.connect(_gui_update)

        # ── Start both and hand control to Qt event loop ───────────────────────────
        serial_thread.start()
        timer.start()

        try:
            self.app.exec()   # blocks here; serial thread runs in background
        except KeyboardInterrupt:
            pass

        self.running = False
        serial_thread.join(timeout=2.0)

        # Store in data_queues so save_data() still works
        n = _n[0]
        for key in ('t','x1','y1','x2','y2','x3','y3','x4','y4'):
            self.data_queues[key].clear()
            self.data_queues[key].extend(_buf[key][:n].tolist())

        print(f'Done streaming. {n} samples collected.')

    def stop_streaming(self):
        "Stop streaming data"
        self.running = False
        self._send_xml_config('<USB_OUTPUT>0</USB_OUTPUT>')
    
    def save_data(self, filename: str):
        "Save collected data to file"
        import pandas as pd
        
        # Convert deques to lists
        data = {key: list(queue) for key, queue in self.data_queues.items()}
        
        # Create DataFrame
        df = pd.DataFrame(data)
        
        # Save to CSV
        df.to_csv(filename, index=False)
        print(f"Data saved to {filename}")
    
    def full_initialization(self):
        "Perform full initialization sequence"
        if not self.connect():
            return False
        
        self.perform_bootloader_handshake()
        self.configure_device()
        self.configure_all_channels()

        # Give the device time to settle after all config commands before querying.
        # The ETi can drop USB briefly after receiving config — matching MATLAB behaviour
        # where verify_channel_config is called well after set_channel_basic completes.
        print('Waiting for device to settle before parameter verification...')
        time.sleep(3.0)
        self.serial_port.reset_input_buffer()

        self.verify_all_channels()
        
        return True
    



if __name__ == "__main__":
    # Create ETi instance
    eti = ETi300(com_port='COM4')
    
    try:
        # Initialize device
        if eti.full_initialization():
            # Start streaming for 500 seconds
            eti.start_streaming(duration=60)
            
            # Save data
            # eti.save_data('eti_data.csv')
    finally:
        # Ensure cleanup
        eti.disconnect()

# Example usage of this class within your own code

#  from eti300 import ETi300

# with ETi300(com_port='COM4') as eti:
#     eti.perform_bootloader_handshake()
#     eti.configure_device(sample_rate_divisor=500)  # 32 Hz output
#     eti.configure_all_channels()
#     eti.verify_all_channels()
#     eti.start_streaming(duration=120)
#     eti.save_data('measurement.csv')