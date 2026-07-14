%% ETi Live Streaming Script

clc; clear all; close all;

%% --- CONFIGURE SERIAL CONNECTION ---
BAUD_RATE = 115200; % typical baud rate for ETi
eti = serialport("COM11", BAUD_RATE); flush(eti);
send_and_check_xml(eti, '<USB_OUTPUT>99</USB_OUTPUT>', 1); clear eti;
eti = serialport("COM11", BAUD_RATE); flush(eti);
pause(0.5);

%% --- OPTIONAL: Set frequency on ETi --- and wake up scanner

send_and_check_xml(eti, '<USB_OUTPUT>4</USB_OUTPUT>', 1);
pause(0.5);
%send_and_check_xml(eti, '<USB_OUTPUT>0</USB_OUTPUT>', 1);
send_and_check_xml(eti,'<CHANNEL>0</CHANNEL>',1);
pause(0.5);
send_and_check_xml(eti, '<FREQUENCY>100000</FREQUENCY>', 1);
pause(0.5);
send_and_check_xml(eti,'<CONNECTOR>Lemo 1-Bridge</CONNECTOR>',1);  
% Reprocess XML
%write(eti, [1, 10], "uint8");   % 0x01 0x0A
pause(0.5);
send_and_check_xml(eti, '<USB_OUTPUT>4</USB_OUTPUT>', 1);
send_and_check_xml(eti, '<USB_OUTPUT>5</USB_OUTPUT>', 1);

pause(1);
%send_and_check_xml(eti, '<USB_OUTPUT>99</USB_OUTPUT>', 1); clear eti;
%eti = serialport("COM5", BAUD_RATE); flush(eti);
%flush(eti);flush(s);
% % Unlock GRBL
% writeline(s, "$X");
% % Set X acceleration
% writeline(s, "$120=800");
% pause(2);
% % Set Y acceleration
% writeline(s, "$121=800");
% pause(2);
% % Set relative positioning
% writeline(s, "G91");
% pause(2);
% update_channel0_frequency(eti, 100000);  % set channel 0 to 3.5 MHz
% pause(1);

%% --- STREAM DATA LOOP ---
flag = 1;
clear X;clear Y;clear Z;
[X, Y, Z,data] = StreamETiData(eti, 3);
figure;plot(Y); %figure; plot(Y); figure; plot(Z);
data
write(eti, [1, 10], "uint8");   % 0x01 0x0A
