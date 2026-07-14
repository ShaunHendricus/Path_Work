%clear all the variables
close all; clc; clear all
BAUD_RATE = 115200; PORT="COM5";

%serial connections
eti = serialport(PORT, BAUD_RATE); flush(eti);
configureTerminator(eti, 'LF');

%waking up ETI
eti = wakeupETI(eti, PORT, BAUD_RATE);

%change the type of data 5= post process, 4=raw, 7=single channel
send_and_check_xml(eti, '<USB_OUTPUT>5</USB_OUTPUT>', 1);

%set the configuration for the channel, frequency, gainX, gainY, load,
%connector
set_channel_basic(eti,0,2000000,400,400,43,'BNC',1)