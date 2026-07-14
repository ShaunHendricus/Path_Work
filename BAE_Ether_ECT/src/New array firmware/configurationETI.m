%clear all the variables
close all; clc; clear all
BAUD_RATE = 115200; PORT="COM11";


%serial connections
eti = serialport(PORT, BAUD_RATE); flush(eti);
configureTerminator(eti, 'LF');

%waking up ETI
eti = wakeupETI(eti, PORT, BAUD_RATE);

%change the type of data 5= post process, 4=raw, 7=single channel
send_and_check_xml(eti, '<USB_OUTPUT>5</USB_OUTPUT>', 1);

%set the configuration for the channel, frequency, gainX, gainY, load,
%connector
set_channel_basic(eti,0,2000000,400,400,0,'Lemo 1-Bridge',1)


% % Stream data
flag = 1;
clear X;clear Y;clear Z;
[X, Y, Z,data] = StreamETiData(eti, 10);
figure;plot(Y); %figure; plot(Y); figure; plot(Z);
write(eti, [1, 10], "uint8");   % 0x01 0x0A


%% Fuctions


function eti = wakeupETI(eti, port, baud)

    xml_command = '<USB_OUTPUT>99</USB_OUTPUT>';
    xml_packet  = uint8([1, 0, uint8(xml_command), 0]);

    % Try the write – this is where it usually dies
    try
         write(eti, xml_packet, 'uint8');
        fprintf("Wake command sent, no reconnect needed.\n");
         return;  % <<< IMPORTANT: do NOT try to reconnect if this worked
    catch ME
        fprintf("Write caused disconnect (expected): %s\n", ME.message);
    end

    % Now the device is rebooting / waking up – give it a moment
    pause(1.0);  % adjust if needed

        % Make sure any previous object is cleared
    try
        clear eti;
    catch
    end
 pause(0.5);  % let Windows / driver release the port
    % Try to reconnect until it works
    connected = false;
    while ~connected
        try
            eti = serialport(port, baud);
            connected = true;
            fprintf("ETI is now awake and connected to %s at %d baud.\n", port, baud);
        catch ME
            fprintf("Reconnect failed: %s\nRetrying...\n", ME.message);
            
            pause(0.5);  % wait and try again
        end
    end
end



function response = send_and_check_xml(serialObj, xml_command, timeout)
    % Sends an XML command (<USB_OUTPUT>n</USB_OUTPUT>) and waits for <INSTRUMENT>...</INSTRUMENT>
    if nargin < 3
        timeout = 1;
    end

    response = '';

    % Ensure char
    if isstring(xml_command)
        xml_command = char(xml_command);
    end

    % Flush any stale data before sending
    flush(serialObj, "input");
    pause(0.5);

    % Build XML packet: [0x01 0x00 xml 0x00]
    xml_packet = uint8([1, 0, uint8(xml_command), 0]);
    write(serialObj, xml_packet, 'uint8');
     % Send poll command [0x01 0x00 0x08]
      pause(0.5);

    cmd = uint8([1, 0, 8]);  % Predefined command (0x01, 0x00, code)
    write(serialObj, cmd, "uint8")
   pause(0.5);
      poll_cmd = uint8([1, 10, 0x08]);
    write(serialObj, poll_cmd, 'uint8');

      % Give instrument a moment to process
    pause(0.05);

    % % Send poll command [0x01 0x00 0x08]
    % poll_cmd = uint8([1, 0, 0x08]);
    % write(serialObj, poll_cmd, 'uint8');

    %   poll_cmd = uint8([1, 0]);
    % write(serialObj, poll_cmd, 'uint8');

    % Start reading response
    startTime = tic;
    buffer = strings;
    %flush(serialObj);
    while toc(startTime) < timeout
        if serialObj.NumBytesAvailable > 0
            try
                line = readline(serialObj);
                buffer(end+1) = string(line); %#ok<AGROW>

                % Stop once closing tag is seen
                if contains(line, '<INSTRUMENT>ETI300')
                    break;
                end
            catch
                % ignore read errors, keep looping
            end
        end
    end

    if ~isempty(buffer)
       idx = find(contains(buffer, "<INSTRUMENT>ETI300"), 1, "first");

if ~isempty(idx)
    cleanBuffer = buffer(idx:end);
    response = strjoin(cleanBuffer, newline);
else
    response = "";
end
        disp('Waking up ETI');
        disp(response);
    else
        disp('No valid XML received.');
    end
end


function response = set_channel_basic(serialObj, channel, frequency, gainX, gainY, load, connector, timeout)

    % set_channel_basic  Set basic parameters for one channel on ETi instrument.
    %
    %   response = set_channel_basic(serialObj, channel, frequency, gainX, gainY, connector)
    %   response = set_channel_basic(serialObj, channel, frequency, gainX, gainY, connector, timeout)
    %
    %   Only writes:
    %       <CHANNEL>channel
    %           <FREQUENCY>frequency</FREQUENCY>
    %           <GAIN_X>gainX</GAIN_X>
    %           <GAIN_Y>gainY</GAIN_Y>
    %           <CONNECTOR>connector</CONNECTOR>
    %       </CHANNEL>
    disp('configuring ETI be patience')
    
    if nargin < 7 || isempty(timeout)
    timeout = 1;   % seconds
    end
    
    if nargin < 6 || isempty(connector)
    connector='Lemo 1-Bridge';
    end
    
    if nargin < 5 || isempty(gainY)
    gainY = '30';   % default
    end
    
    if nargin < 5 || isempty(gainX)
    gainX = '30';   % default
    end
    
    %frequency configuration
    xmlLines = {
    ['<CHANNEL>' num2str(channel)]
    ['<FREQUENCY>' num2str(frequency) '</FREQUENCY>']
    '</CHANNEL>'
    };
    xml = strjoin(xmlLines, newline);
    
    % Flush any stale data before sending
    flush(serialObj, "input");
    pause(0.5);
    xml_packet = uint8([1, 0, uint8(xml), 0]);
    write(serialObj, xml_packet, 'uint8');
    pause(0.5);
    cmd = uint8([1, 0, 8]);
    write(serialObj, cmd, "uint8")
    pause(0.5);
    poll_cmd = uint8([1, 10, 0x08]);
    write(serialObj, poll_cmd, 'uint8');



    %gainX configuration
    xmlLines = {
    ['<CHANNEL>' num2str(channel)]
    ['<GAIN_X>' num2str(gainX) '</GAIN_X>']
    '</CHANNEL>'
    };
    xml = strjoin(xmlLines, newline);
    
    % Flush any stale data before sending
    flush(serialObj, "input");
    pause(0.5);
    xml_packet = uint8([1, 0, uint8(xml), 0]);
    write(serialObj, xml_packet, 'uint8');
    pause(0.5);
    cmd = uint8([1, 0, 8]);
    write(serialObj, cmd, "uint8")
    pause(0.5);
    poll_cmd = uint8([1, 10, 0x08]);
    write(serialObj, poll_cmd, 'uint8');

        %gainX configuration
    xmlLines = {
    ['<CHANNEL>' num2str(channel)]
    ['<GAIN_Y>' num2str(gainY) '</GAIN_Y>']
    '</CHANNEL>'
    };
    xml = strjoin(xmlLines, newline);
    
    % Flush any stale data before sending
    flush(serialObj, "input");
    pause(0.5);
    xml_packet = uint8([1, 0, uint8(xml), 0]);
    write(serialObj, xml_packet, 'uint8');
    pause(0.5);
    cmd = uint8([1, 0, 8]);
    write(serialObj, cmd, "uint8")
    pause(0.5);
    poll_cmd = uint8([1, 10, 0x08]);
    write(serialObj, poll_cmd, 'uint8');





  

    flush(serialObj, "input");
    pause(0.5);
    xmlLines = {
    ['<CHANNEL>' num2str(channel) '</CHANNEL>'] 
    };
    xml = strjoin(xmlLines, newline);
    xml_packet = uint8([1, 0, uint8(xml), 0]);
    write(serialObj, xml_packet, 'uint8');
    pause(0.5);
    cmd = uint8([1, 0, 8]);
    write(serialObj, cmd, "uint8")
    pause(0.5);
    poll_cmd = uint8([1, 10, 0x08]);
    write(serialObj, poll_cmd, 'uint8');

    %connector configuration
    xmlLines = {
    ['<CONNECTOR>' connector '</CONNECTOR>']
    };
    xml = strjoin(xmlLines, newline);
  
    % Flush any stale data before sending
    flush(serialObj, "input");
    pause(0.5);
    xml_packet = uint8([1, 0, uint8(xml), 0]);
    write(serialObj, xml_packet, 'uint8');
    pause(0.5);
    cmd = uint8([1, 0, 8]);
    write(serialObj, cmd, "uint8")
    pause(0.5);
    poll_cmd = uint8([1, 10, 0x08]);
    write(serialObj, poll_cmd, 'uint8');


          %load configuration
      xmlLines = {
    ['<LOAD>' num2str(load) '</LOAD>'] 
    };
      xml = strjoin(xmlLines, newline);
    % Flush any stale data before sending
    flush(serialObj, "input");
    pause(0.5);
    xml_packet = uint8([1, 0, uint8(xml), 0]);
    write(serialObj, xml_packet, 'uint8');
    pause(0.5);
    cmd = uint8([1, 0, 8]);
    write(serialObj, cmd, "uint8")
    pause(0.5);
    poll_cmd = uint8([1, 10, 0x08]);
    write(serialObj, poll_cmd, 'uint8');



    %rest of channels to 0

    flush(serialObj, "input");
    pause(0.5);
    xml='<CHANNEL>1</CHANNEL>';
    xml_packet = uint8([1, 0, uint8(xml), 0]);
    write(serialObj, xml_packet, 'uint8');
    pause(0.5);
    cmd = uint8([1, 0, 8]);
    write(serialObj, cmd, "uint8")
    pause(0.5);
    poll_cmd = uint8([1, 10, 0x08]);
    write(serialObj, poll_cmd, 'uint8');

    flush(serialObj, "input");
    pause(0.5);
    xml='<CONNECTOR>Lemo 1-Bridge</CONNECTOR>';
    xml_packet = uint8([1, 0, uint8(xml), 0]);
    write(serialObj, xml_packet, 'uint8');
    pause(0.5);
    cmd = uint8([1, 0, 8]);
    write(serialObj, cmd, "uint8")
    pause(0.5);
    poll_cmd = uint8([1, 10, 0x08]);
    write(serialObj, poll_cmd, 'uint8');
    

        flush(serialObj, "input");
    pause(0.5);
    xml='<CHANNEL>2</CHANNEL>';
    xml_packet = uint8([1, 0, uint8(xml), 0]);
    write(serialObj, xml_packet, 'uint8');
    pause(0.5);
    cmd = uint8([1, 0, 8]);
    write(serialObj, cmd, "uint8")
    pause(0.5);
    poll_cmd = uint8([1, 10, 0x08]);
    write(serialObj, poll_cmd, 'uint8');

    flush(serialObj, "input");
    pause(0.5);
    xml='<CONNECTOR>Lemo 1-Bridge</CONNECTOR>';
    xml_packet = uint8([1, 0, uint8(xml), 0]);
    write(serialObj, xml_packet, 'uint8');
    pause(0.5);
    cmd = uint8([1, 0, 8]);
    write(serialObj, cmd, "uint8")
    pause(0.5);
    poll_cmd = uint8([1, 10, 0x08]);
    write(serialObj, poll_cmd, 'uint8');

    flush(serialObj, "input");
    pause(0.5);
    xml='<CHANNEL>3</CHANNEL>';
    xml_packet = uint8([1, 0, uint8(xml), 0]);
    write(serialObj, xml_packet, 'uint8');
    pause(0.5);
    cmd = uint8([1, 0, 8]);
    write(serialObj, cmd, "uint8")
    pause(0.5);
    poll_cmd = uint8([1, 10, 0x08]);
    write(serialObj, poll_cmd, 'uint8');

    flush(serialObj, "input");
    pause(0.5);
    xml='<CONNECTOR>Lemo 1-Bridge</CONNECTOR>';
    xml_packet = uint8([1, 0, uint8(xml), 0]);
    write(serialObj, xml_packet, 'uint8');
    pause(0.5);
    cmd = uint8([1, 0, 8]);
    write(serialObj, cmd, "uint8")
    pause(0.5);
    poll_cmd = uint8([1, 10, 0x08]);
    write(serialObj, poll_cmd, 'uint8');

end


function [Xarray, Yarray, Zarray,data] = StreamETiData(eti, duration)
% STREAMETIDATA Streams ETi data for a given duration while moving a CNC axis
%   s       : serial object for CNC
%   eti     : serial object for ETi
%   duration: streaming duration in seconds
%
%   Returns Xarray, Yarray, Zarray of measurements
flush(eti);
    if nargin < 3
        duration = 3;  % default duration 3 seconds
    end

    Xarray = [];
    Yarray = [];
    Zarray = [];
    sample_count = 0;
    flag = 1;

    t0 = tic;
    try
        while toc(t0) < duration
            % Move CNC once after 1 second
            % if toc(t0) > .5 && flag == 1
            %     writeline(s, "G1 Y-4 F200");
            %     flag = 0;
            % end

            % Read ETi data if available
            if eti.NumBytesAvailable > 50
                data = read(eti, eti.NumBytesAvailable, 'uint8');

                % find packet start bytes 0x80 0x7F
                packet_start = find(data(1:end-1) == 0x80 & data(2:end) == 0x7F);
                packet_start = packet_start(packet_start + 52 <= length(data));

                for k = 1:numel(packet_start)
                    i = packet_start(k);
                    packet = data(i:i+52);

                    % --- VALIDATION CHECKS ---
                    if length(packet) ~= 53
                        continue; % discard if not complete
                    end
                    if packet(1) ~= 0x80 || packet(2) ~= 0x7F
                        continue; % discard if header is wrong
                    end

                    % extract X and Y (4 bytes each, int32)
                    x = double(typecast(uint8(packet(3:6)), 'int32'));
                    y = double(typecast(uint8(packet(7:10)), 'int32'));

                    fprintf('Packet: ');
                    fprintf('%02x ', packet);
                    fprintf('\n');
                    % 
                    % % sanity check for unreasonable values
                    % if abs(x) > 4.2e8 || (-y) > 1e7 || abs(-y) > 5e7
                    %     continue; % skip nonsense
                    % end

                    % store values
                    Xarray(end+1) = x;
                    Yarray(end+1) = y;
                    Zarray(end+1) = sqrt(x.^2 + y.^2);
                    sample_count = sample_count + 1;
                end
            end
        end

        % Move CNC back to initial position
       % writeline(s, "G1 Y4 F600");

    catch ME
        disp('Streaming stopped unexpectedly.');
       % writeline(s, "G1 Y4 F600"); % ensure CNC returns
        rethrow(ME);
    end
end


