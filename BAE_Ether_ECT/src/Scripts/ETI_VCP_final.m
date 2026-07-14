clear all
clear all;instrreset;
clc

COM_PORT  = 'COM4';      
BAUD_RATE = 115200;      
TIMEOUT_S = 1;          
STREAM_DURATION = 500;
eti = serialport(COM_PORT, BAUD_RATE, 'Timeout', TIMEOUT_S);
configureTerminator(eti, 'LF');  % use LF‐terminated lines so readline() works
flush(eti);                      % drop any old data

fprintf('PERFORMING BOOTLOADER HANDSHAKE! \n');

% Tell the loader to run the main firmware…
send_and_check_xml2(eti, '<USB_OUTPUT>99</USB_OUTPUT>');
pause(1)
% ETi will drop out and re-enumerate USB…
clear eti
pause(5)  % give it time to reboot

% Reboot the Eti
eti = serialport(COM_PORT, BAUD_RATE, 'Timeout', TIMEOUT_S);
configureTerminator(eti, 'LF');
flush(eti);

fprintf('STOPPING ANY ACTIVE STREAM OF DATA FROM ETi 300! \n')

send_and_check_xml2(eti, '<USB_OUTPUT>0</USB_OUTPUT>'); %stop any streaming of data
pause(1)

send_and_check_xml2(eti, '<USB_RATE>1000</USB_RATE>'); %sample rate for ETi is 16,000 Hz, so sending 1000 will give 16000/1000 (16 Hz)
pause(1)

fprintf('SENDING ETi PROBE CONFIGURATION! \n')

%Channel 1

c= { '<CHANNEL>0','<FREQUENCY>1500000</FREQUENCY>'} ; %FREQUENCY sent as Hz ie 1.5 MHz would be 1500000 Hz
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)
    
c= { '<CHANNEL>0','<PHASE>300000</PHASE>'} ; %PHASE sent as factor of 1000 ie 180 would be 180000
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>0','<GAIN_X>600</GAIN_X>'} ; 
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>0','<GAIN_Y>900</GAIN_Y>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>0','<HP>10000</HP>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>0','<LP>40000</LP>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

%Channel 2

c= { '<CHANNEL>1','<FREQUENCY>56000</FREQUENCY>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>1','<PHASE>180000</PHASE>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>1','<GAIN_X>600</GAIN_X>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>1','<GAIN_Y>900</GAIN_Y>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>1','<HP>400</HP>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>1','<LP>4000</LP>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

%Channel 3

c= { '<CHANNEL>2','<FREQUENCY>20000</FREQUENCY>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>2','<PHASE>200000</PHASE>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>2','<GAIN_X>600</GAIN_X>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>2','<GAIN_Y>900</GAIN_Y>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>2','<HP>400</HP>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>2','<LP>4000</LP>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

%Channel 4

c= { '<CHANNEL>3','<FREQUENCY>15000</FREQUENCY>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>3','<PHASE>300000</PHASE>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>3','<GAIN_X>600</GAIN_X>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>3','<GAIN_Y>900</GAIN_Y>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>3','<HP>400</HP>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)

c= { '<CHANNEL>3','<LP>4000</LP>'} ;
c= strjoin(c, char(13));
send_and_check_xml2(eti, c);
pause(0.5)


% hdr2 = send_and_check_xml2(eti, '<INSTRUMENT><SETTINGS><CHANNEL>0 <CONNECTOR>Lemo1-Bridge</CONNECTOR></SETTINGS></INSTRUMENT>');
% c= {'<INSTRUMENT>', '<SETTINGS>', '<CHANNEL>0','<CONNECTOR>Lemo1-Bridge</CONNECTOR>','</SETTINGS>', '</INSTRUMENT>'} ;
% c= strjoin(c, char(13));
% hdr2 = send_and_check_xml2(eti, c);
% pause(0.5)

fprintf('REQUESTING PROBE CONFIGURATION VALUES FROM ETi! \n');

%channel 1

cmd = uint8([4, 0x3F, 0, 0x46 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Gain_X : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Gain_X  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Raw Value  : %u (decimal)\n', raw32);

freq = double(raw32) / 1000;
fprintf('Frequency channel 1: %.1f kHz\n', freq);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 0, 0x58 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Gain_X : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Gain_X  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Raw Value  : %u (decimal)\n', raw32);

gain_dB = double(raw32) / 10;
fprintf('Gain-X channel 1: %.1f dB\n', gain_dB);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 0, 0x59 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Gain_Y : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Gain_Y  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Value  : %u (decimal)\n', raw32);

gain_dB = double(raw32) / 10;
fprintf('Gain-Y channel 1 : %.1f dB\n', gain_dB);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 0, 0x50 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Phase : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Phase  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Value  : %u (decimal)\n', raw32);

phase = double(raw32)/10;
fprintf('Phase channel 1 : %.1f degrees\n', phase);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 0, 0x48 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Phase : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Phase  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Value  : %u (decimal)\n', raw32);

HP = double(raw32);
fprintf('High-Pass Filter channel 1 : %.1f Hz\n', HP);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 0, 0x4C ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Phase : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Phase  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Value  : %u (decimal)\n', raw32);

LP = double(raw32);
fprintf('Low-Pass Filter channel 1 : %.1f Hz\n', LP);
flush(eti);
pause(0.5)

%channel 2

cmd = uint8([4, 0x3F, 1, 0x46 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Gain_X : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Gain_X  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Raw Value  : %u (decimal)\n', raw32);

freq = double(raw32) / 1000;
fprintf('Frequency channel 2: %.1f kHz\n', freq);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 1, 0x58 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Gain_X : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Gain_X  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Raw Value  : %u (decimal)\n', raw32);

gain_dB = double(raw32) / 10;
fprintf('Gain-X channel 2: %.1f dB\n', gain_dB);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 1, 0x59 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Gain_Y : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Gain_Y  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Value  : %u (decimal)\n', raw32);

gain_dB = double(raw32) / 10;
fprintf('Gain-Y channel 2 : %.1f dB\n', gain_dB);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 1, 0x50 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Phase : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Phase  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Value  : %u (decimal)\n', raw32);

phase = double(raw32)/10;
fprintf('Phase channel 2 : %.1f degrees\n', phase);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 1, 0x48 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Phase : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Phase  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Value  : %u (decimal)\n', raw32);

HP = double(raw32);
fprintf('High-Pass Filter channel 2 : %.1f Hz\n', HP);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 1, 0x4C ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Phase : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Phase  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Value  : %u (decimal)\n', raw32);

LP = double(raw32);
fprintf('Low-Pass Filter channel 2 : %.1f Hz\n', LP);
flush(eti);
pause(0.5)

%channel 3

cmd = uint8([4, 0x3F, 2, 0x46 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Gain_X : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Gain_X  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Raw Value  : %u (decimal)\n', raw32);

freq = double(raw32) / 1000;
fprintf('Frequency channel 3: %.1f kHz\n', freq);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 2, 0x58 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Gain_X : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Gain_X  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Raw Value  : %u (decimal)\n', raw32);

gain_dB = double(raw32) / 10;
fprintf('Gain-X channel 3: %.1f dB\n', gain_dB);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 2, 0x59 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Gain_Y : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Gain_Y  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Value  : %u (decimal)\n', raw32);

gain_dB = double(raw32) / 10;
fprintf('Gain-Y channel 3 : %.1f dB\n', gain_dB);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 2, 0x50 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Phase : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Phase  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Value  : %u (decimal)\n', raw32);

phase = double(raw32)/10;
fprintf('Phase channel 3 : %.1f degrees\n', phase);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 2, 0x48 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Phase : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Phase  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Value  : %u (decimal)\n', raw32);

HP = double(raw32);
fprintf('High-Pass Filter channel 3 : %.1f Hz\n', HP);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 2, 0x4C ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Phase : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Phase  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Value  : %u (decimal)\n', raw32);

LP = double(raw32);
fprintf('Low-Pass Filter channel 3 : %.1f Hz\n', LP);
flush(eti);
pause(0.5)

%channel 4

cmd = uint8([4, 0x3F, 3, 0x46 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Gain_X : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Gain_X  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Raw Value  : %u (decimal)\n', raw32);

freq = double(raw32) / 1000;
fprintf('Frequency channel 4: %.1f kHz\n', freq);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 3, 0x58 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Gain_X : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Gain_X  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Raw Value  : %u (decimal)\n', raw32);

gain_dB = double(raw32) / 10;
fprintf('Gain-X channel 4: %.1f dB\n', gain_dB);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 3, 0x59 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Gain_Y : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Gain_Y  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Value  : %u (decimal)\n', raw32);

gain_dB = double(raw32) / 10;
fprintf('Gain-Y channel 4: %.1f dB\n', gain_dB);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 3, 0x50 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Phase : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Phase  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Value  : %u (decimal)\n', raw32);

phase = double(raw32)/10;
fprintf('Phase channel 4: %.1f degrees\n', phase);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 3, 0x48 ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Phase : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Phase  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Value  : %u (decimal)\n', raw32);

HP = double(raw32);
fprintf('High-Pass Filter channel 4: %.1f Hz\n', HP);

flush(eti);
pause(0.5)

cmd = uint8([4, 0x3F, 3, 0x4C ]); %resposne from the ETI for paramter request
write(eti, cmd, "uint8");

t0 = tic;
while eti.NumBytesAvailable < 7
    pause(0.001);
    if toc(t0) > 1
        error('Timed-out waiting for parameter reply');
    end
end

reply = read(eti, 7, "uint8");        % [C0 3F 58  w  x  y  z]

% fprintf('Header for Phase : %02X %02X %02X\n', reply(1:3));
% fprintf('Value for Phase  : %02X %02X %02X %02X (hex)\n', reply(4:7));

raw32 = uint32(reply(4)) * 2^24 + ...
        uint32(reply(5)) * 2^16 + ...
        uint32(reply(6)) * 2^8  + ...
        uint32(reply(7));                 % big-endian MSB>LSB

% fprintf('Value  : %u (decimal)\n', raw32);

LP = double(raw32);
fprintf('Low-Pass Filter channel 4: %.1f Hz\n', LP);
flush(eti);


% Send the command for format 5 and stream x1 and y1 values
fprintf('SENDING PROCESSED DATA COMMAND (FORMAT 5)!');

hdr1 = send_and_check_xml1(eti, '<USB_OUTPUT>5</USB_OUTPUT>');%format 5
pause(1)

%Format 5 stream
disp('STREAMING X,Y PAIR VALUES FOR ALL 4 CHANNELS! ')
BUFFER_SIZE = 50;
t0 = tic;
x1 = [];
y1 = [];
x2 = [];
y2 = [];
x3 = [];
y3 = [];
x4 = [];
y4 = [];
t  = [];

%Setup plots
fig = figure('Name','Live Scroll Plots','NumberTitle','off');

% subplot for X1 vs t
axX1 = subplot(2,2,1);
hX1 = animatedline('Parent', axX1, 'Color', 'b');
xlabel(axX1, 'Time (s)');
ylabel(axX1, 'X1');
title(axX1, 'X1 vs Time');
grid(axX1, 'on');

% subplot for Y1 vs t
axY1 = subplot(2,2,3);
hY1 = animatedline('Parent', axY1, 'Color', 'r');
xlabel(axY1, 'Time (s)');
ylabel(axY1, 'Y1');
title(axY1, 'Y1 vs Time');
grid(axY1, 'on');

% subplot for X2 vs t
axX2 = subplot(2,2,2);
hX2 = animatedline('Parent', axX2, 'Color', 'b');
xlabel(axX2, 'Time (s)');
ylabel(axX2, 'X2');
title(axX2, 'X2 vs Time');
grid(axX2, 'on');

% subplot for Y2 vs t
axY2 = subplot(2,2,4);
hY2 = animatedline('Parent', axY2, 'Color', 'r');
xlabel(axY2, 'Time (s)');
ylabel(axY2, 'Y2');
title(axY2, 'Y2 vs Time');
grid(axY2, 'on');


while toc(t0) < STREAM_DURATION
    % only proceed when there’s enough bytes to sniff packets
     if eti.NumBytesAvailable < 50 %format 5
        pause(0.005);
        continue;
    end
    
    data = read(eti, eti.NumBytesAvailable, 'uint8');
    
 
    pkt_starts = find(data(1:end-1)==128 & data(2:end)==127);%format 5
   
    pkt_starts = pkt_starts(pkt_starts + 49 <= numel(data)); %fomat 5

    for k = 1:numel(pkt_starts)
        i = pkt_starts(k);

        pkt = data(i : i+49);         
    % build a readable HEX string
        hexStr = sprintf('%02X ', pkt);  % two-digit hex with spaces
%         fprintf('HEX  : %s\n', hexStr);
        
% parse X1, X2, X3, X4
       
rawX1 = double(data(i+3))+ double((data(i+4))*256) + double((data(i+5))*256*256); % most-significant byte (sign bit)
rawX1 = rawX1 - (rawX1 >= 2^23) * 2^24;
X1 = rawX1;                                  % X1 as signed double

rawX2 = double(data(i+11))+ double((data(i+12))*256) + double((data(i+13))*256*256); % most-significant byte (sign bit)
rawX2 = rawX2 - (rawX2 >= 2^23) * 2^24;
X2 = rawX2;                                  % X2 as signed double

rawX3 = double(data(i+19))+ double((data(i+20))*256) + double((data(i+21))*256*256); % most-significant byte (sign bit)
rawX3 = rawX3 - (rawX3 >= 2^23) * 2^24;
X3 = rawX3;                                  % X3 as signed double

rawX4 = double(data(i+27))+ double((data(i+28))*256) + double((data(i+29))*256*256); % most-significant byte (sign bit)
rawX4 = rawX4 - (rawX4 >= 2^23) * 2^24;
X4 = rawX4;                                   % X4 as signed double


% parse Y1, Y2, Y3, Y4

rawY1 =  double(data(i+7)) + double((data(i+8))*256) + double((data(i+9))*256*256);
rawY1 = rawY1 - (rawY1 >= 2^23) * 2^24;
Y1 = rawY1;                                  % Y1 as signed double

rawY2 =  double(data(i+15)) + double((data(i+16))*256) + double((data(i+17))*256*256);
rawY2 = rawY2 - (rawY2 >= 2^23) * 2^24;
Y2 = rawY2;                                 % Y2 as signed double

rawY3 =  double(data(i+23)) + double((data(i+24))*256) + double((data(i+25))*256*256);
rawY3 = rawY3 - (rawY3 >= 2^23) * 2^24;
Y3 = rawY3;                                  % Y3 as signed double

rawY4 =  double(data(i+31)) + double((data(i+32))*256) + double((data(i+33))*256*256);
rawY4 = rawY4 - (rawY4 >= 2^23) * 2^24;
Y4 = rawY4;                                   % Y4 as signed double


        
x1(end+1) = X1;
y1(end+1)= Y1;
x2(end+1) = X2;
y2(end+1)= Y2;
x3(end+1) = X3;
y3(end+1)= Y3;
x4(end+1) = X4;
y4(end+1)= Y4;
t (end +1)= toc(t0);

% fprintf('t=%.3f  X1=%d  Y1=%d  X2=%d  Y2=%d  X3=%d  Y3=%d  X4=%d  Y4=%d \n', t(end), X1, Y1, X2, Y2, X3, Y3, X4, Y4); 



 % Append new data to plots
addpoints(hX1, t(end), X1);
addpoints(hY1, t(end), Y1);
addpoints(hX2, t(end), X2);
addpoints(hY2, t(end), Y2);

% Update X limits to scroll the plot
WINDOW = 10; % seconds visible on plot
if t(end) > WINDOW
    set(axX1, 'XLim', [t(end)-WINDOW, t(end)]);
    set(axY1, 'XLim', [t(end)-WINDOW, t(end)]);
    set(axX2, 'XLim', [t(end)-WINDOW, t(end)]);
    set(axY2, 'XLim', [t(end)-WINDOW, t(end)]);
else
    set(axX1, 'XLim', [0, WINDOW]);
    set(axY1, 'XLim', [0, WINDOW]);
    set(axX2, 'XLim', [0, WINDOW]);
    set(axY2, 'XLim', [0, WINDOW]);
end

drawnow limitrate;

    end
end
disp('Done streaming.')


%%
function header1 = send_and_check_xml1(ser, xml_command)
    % use this function to send commands to aquire data
   
    header1 = '';
    
    % ensure xml_command is a char vector
    if isstring(xml_command)
        xml_command = char(xml_command);
    end
    
    % build packet: 0x01, 0x00, xml bytes, 0x00 terminator
    pkt = [1, 0, uint8(xml_command), 0];
    write(ser, pkt, 'uint8');
     
    cmd = uint8([1, 0, 0x08]);  % Predefined command (balance)
    write(ser, cmd, "uint8")

end 

%%
function header2 = send_and_check_xml2(ser, xml_command)
    % use this function to send commands to ETi to set the config
   
    header2 = '';
    
    % ensure xml_command is a char vector
    if isstring(xml_command)
        xml_command = char(xml_command);
    end
    
    % build packet: 0x01, 0x00, xml bytes, 0x00 terminator
    pkt = [1, 0, uint8(xml_command), 0];
    write(ser, pkt, 'uint8');


end 
 