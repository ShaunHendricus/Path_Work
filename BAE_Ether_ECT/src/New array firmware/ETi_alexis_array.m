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

%set the configuration for the channel, frequency,phase,gainX, gainY,
%load, connector,timeout
set_channel_basic(eti,0,1000000,0,600,600,43,'BNC',1)

% % Stream data with live plotting for 8 channels
flag = 1;
clear X;clear Y;clear Z;clear T;
[X, Y, Z, T, data] = StreamETiDataLivePlot(eti, 20);
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

    % Start reading response
    startTime = tic;
    buffer = strings;
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


 function response = set_channel_basic(serialObj, channel, frequency, phase, gainX, gainY, load, connector, timeout)

    % set_channel_basic  Set basic parameters for one channel on ETi instrument.
    disp('configuring ETI be patience')
    
    if nargin < 9 || isempty(timeout)
    timeout = 1;   % seconds
    end
    
    if nargin < 8 || isempty(connector)
    connector='Lemo 1-Bridge';
    end
    
    if nargin < 6 || isempty(gainY)
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


    %phase configuration
    xmlLines = {
    ['<CHANNEL>' num2str(channel)]
    ['<PHASE>' num2str(phase) '</PHASE>']
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

        %gainY configuration
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


% For some reason the connector and load values are not sent the way
% freq,phase,Lp are sent.
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

end


function [Xarray, Yarray, Zarray, Tarray, data] = StreamETiDataLivePlot(eti, duration)
% STREAMETIDATA Streams ETi 8-channel array probe data with live plotting
%   eti      : serial object for ETi
%   duration : streaming duration in seconds
%
%   Returns Xarray, Yarray, Zarray, Tarray (each 8 cells for 8 channels)
flush(eti);
    if nargin < 2
        duration = 3;  % default duration 3 seconds
    end

    % Initialize arrays for 8 channels
    Xarray = cell(8,1);
    Yarray = cell(8,1);
    Zarray = cell(8,1);
    Tarray = cell(8,1);
    for ch = 1:8
        Xarray{ch} = [];
        Yarray{ch} = [];
        Zarray{ch} = [];
        Tarray{ch} = [];
    end
    
    sample_count = 0;
    plot_update_counter = 0;
    plot_update_interval = 100;  % Update plot every 50 samples

    % Setup live plot with 2 subplots - all channels on each
    fig = figure('Name', 'ETI 300 8-Channel Array Probe', 'Position', [100 100 1400 600]);
    
    hx = cell(8,1);  % handles for X plots
    hy = cell(8,1);  % handles for Y plots
    
    % Define colors for 8 channels
    colors = lines(8);  % Get 8 distinct colors
    
    % X vs Time plot (all channels)
    subplot(1,2,1);
    hold on;
    for ch = 1:8
        hx{ch} = plot(nan, nan, '-', 'LineWidth', 1.5, 'Color', colors(ch,:), 'DisplayName', ['Ch ' num2str(ch)]);
    end
    xlabel('Time (s)'); ylabel('X');
    title('All Channels: X vs Time');
    legend('Location', 'best');
    grid on;
    hold off;
    
    % Y vs Time plot (all channels)
    subplot(1,2,2);
    hold on;
    for ch = 1:8
        hy{ch} = plot(nan, nan, '-', 'LineWidth', 1.5, 'Color', colors(ch,:), 'DisplayName', ['Ch ' num2str(ch)]);
    end
    xlabel('Time (s)'); ylabel('Y');
    title('All Channels: Y vs Time');
    legend('Location', 'best');
    grid on;
    hold off;
    
    t0 = tic;
    try
        while toc(t0) < duration
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

                    % Extract channel number from byte 51 (MATLAB 1-indexed)
                    channel_num = packet(51) + 1;  % +1 for MATLAB indexing (0->1, 7->8)
                    
                    % Validate channel number
                    if channel_num < 1 || channel_num > 8
                        continue;  % skip invalid channel
                    end

                    % extract X and Y (4 bytes each, int32)
                    x1 = double(typecast(uint8(packet(3:6)), 'int32'));
                    y1 = double(typecast(uint8(packet(7:10)), 'int32'));

                    % Print packet for debugging
                    % fprintf('Channel %d Packet: ', channel_num-1);
                    % fprintf('%02x ', packet);
                    % fprintf('\n');

                    % store values in appropriate channel array
                    Xarray{channel_num}(end+1) = x1;
                    Yarray{channel_num}(end+1) = y1;
                    Zarray{channel_num}(end+1) = sqrt(x1.^2 + y1.^2);
                    Tarray{channel_num}(end+1) = toc(t0);
                    
                    sample_count = sample_count + 1;
                    plot_update_counter = plot_update_counter + 1;
                end
                
                % Update plots only every N samples (much faster!)
                if plot_update_counter >= plot_update_interval
                    for ch = 1:8
                        if ~isempty(Tarray{ch})
                            set(hx{ch}, 'XData', Tarray{ch}, 'YData', Xarray{ch});
                            set(hy{ch}, 'XData', Tarray{ch}, 'YData', Yarray{ch});
                        end
                    end
                    drawnow limitrate;
                    plot_update_counter = 0;
                end
            end
        end
        
        % Final update to show all data
        for ch = 1:8
            if ~isempty(Tarray{ch})
                set(hx{ch}, 'XData', Tarray{ch}, 'YData', Xarray{ch});
                set(hy{ch}, 'XData', Tarray{ch}, 'YData', Yarray{ch});
            end
        end
        drawnow;

    catch ME
        disp('Streaming stopped unexpectedly.');
        rethrow(ME);
    end
end