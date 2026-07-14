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
% set_channel_basic(eti,1,1000000,0,600,600,43,'Off',1)
% set_channel_basic(eti,2,1000000,0,600,600,43,'Off',1)
% set_channel_basic(eti,3,1000000,0,600,600,43,'Off',1)

verify_channel_config(eti, 0);
% verify_channel_config(eti, 1);
% verify_channel_config(eti, 2);
% verify_channel_config(eti, 3);

% % Stream data with live C-scan plotting
flag = 1;
clear X;clear Y;clear Z;clear T;

% Choose what to display in C-scan: 'X', 'Y', or 'Z' (magnitude)
cscan_display = 'Z';  % Change this to 'X' or 'Y' if desired

[X, Y, Z, T, data] = StreamETiDataCScan(eti, 20, cscan_display);
write(eti, [1, 10], "uint8");   % 0x01 0x0A


%% Functions


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


function [Xarray, Yarray, Zarray, Tarray, data] = StreamETiDataCScan(eti, duration, display_type)
% STREAMETIDATA Streams ETi 8-channel array probe with live 2D C-scan
%   eti          : serial object for ETi
%   duration     : streaming duration in seconds
%   display_type : 'X', 'Y', or 'Z' (magnitude) - what to show in C-scan
%
%   Returns Xarray, Yarray, Zarray, Tarray (each 8 cells for 8 channels)
flush(eti);
    if nargin < 3
        display_type = 'Z';  % default to magnitude
    end
    if nargin < 2
        duration = 3;  % default duration 3 seconds
    end

    % Initialize arrays for 8 channels
    Xarray = cell(32,1);
    Yarray = cell(32,1);
    Zarray = cell(32,1);
    Tarray = cell(32,1);
    for ch = 1:32
        Xarray{ch} = [];
        Yarray{ch} = [];
        Zarray{ch} = [];
        Tarray{ch} = [];
    end
    
    % C-scan data matrix: rows = channels (1-8), columns = time samples
    cscan_data = [];  % Will grow as data arrives
    max_samples = 2000;  % Maximum samples to display
    
    sample_count = 0;
    plot_update_counter = 0;
    plot_update_interval = 50;  % Update plot every 50 samples

    % Setup figure with C-scan and time-series plots
    fig = figure('Name', 'ETI 300 8-Channel C-Scan', 'Position', [50 50 1400 700]);
    
    % C-scan image (top, larger)
    ax_cscan = subplot(2,1,1);
    h_cscan = imagesc(nan(32,1));
    colorbar;
    xlabel('Sample Number (Time →)');
    ylabel('Channel Number');
    title(['2D C-Scan: ' display_type ' values']);
    yticks(1:32);
    yticklabels({'Ch1','Ch2','Ch3','Ch4','Ch5','Ch6','Ch7','Ch8','Ch9','Ch10','Ch11','Ch12','Ch13','Ch14','Ch15','Ch16','Ch17','Ch18','Ch19','Ch20','Ch21','Ch22','Ch23','Ch24','Ch25','Ch26','Ch27','Ch28','Ch29','Ch30','Ch31','Ch32'});
    set(gca, 'YDir', 'normal');  % Channel 1 at bottom
    
    % Time-series overlay (bottom, smaller)
    ax_timeseries = subplot(2,1,2);
    hx = cell(32,1);
    colors = lines(32);
    hold on;
    for ch = 1:32
        hx{ch} = plot(nan, nan, '-', 'LineWidth', 1, 'Color', colors(ch,:), 'DisplayName', ['Ch ' num2str(ch)]);
    end
    xlabel('Sample Number');
    ylabel(display_type);
    title([display_type ' vs Sample Number (All Channels)']);
    legend('Location', 'eastoutside', 'NumColumns', 1);
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
                    if channel_num < 1 || channel_num > 32
                        continue;  % skip invalid channel
                    end

                    % extract X and Y (4 bytes each, int32)
                    x1 = double(typecast(uint8(packet(3:6)), 'int32'));
                    y1 = double(typecast(uint8(packet(7:10)), 'int32'));
                    z1 = sqrt(x1^2 + y1^2);

                    % store values in appropriate channel array
                    Xarray{channel_num}(end+1) = x1;
                    Yarray{channel_num}(end+1) = y1;
                    Zarray{channel_num}(end+1) = z1;
                    Tarray{channel_num}(end+1) = toc(t0);
                    
                    sample_count = sample_count + 1;
                    plot_update_counter = plot_update_counter + 1;
                end
                
                % Update plots only every N samples (much faster!)
                if plot_update_counter >= plot_update_interval
                    % Build C-scan matrix
                    % Find the maximum number of samples across all channels
                    max_len = 0;
                    for ch = 1:32
                        if length(Xarray{ch}) > max_len
                            max_len = length(Xarray{ch});
                        end
                    end
                    
                    if max_len > 0
                        % Create matrix: 8 channels x max_len samples
                        cscan_data = nan(32, max_len);
                        
                        for ch = 1:32
                            % Select which data to display
                            switch upper(display_type)
                                case 'X'
                                    data_to_plot = Xarray{ch};
                                case 'Y'
                                    data_to_plot = Yarray{ch};
                                case 'Z'
                                    data_to_plot = Zarray{ch};
                                otherwise
                                    data_to_plot = Zarray{ch};
                            end
                            
                            % Fill in available data
                            cscan_data(ch, 1:length(data_to_plot)) = data_to_plot;
                        end
                        
                        % Limit display to last max_samples
                        if max_len > max_samples
                            cscan_data = cscan_data(:, end-max_samples+1:end);
                        end
                        
                        % Update C-scan image
                        set(h_cscan, 'CData', cscan_data);
                        set(ax_cscan, 'XLim', [0.5, size(cscan_data,2)+0.5]);
                        
                        % Update time-series plots
                        for ch = 1:32
                            switch upper(display_type)
                                case 'X'
                                    data_to_plot = Xarray{ch};
                                case 'Y'
                                    data_to_plot = Yarray{ch};
                                case 'Z'
                                    data_to_plot = Zarray{ch};
                                otherwise
                                    data_to_plot = Zarray{ch};
                            end
                            
                            if ~isempty(data_to_plot)
                                n= length(data_to_plot);
                                x_vals = 1:n;
                                % Limit to last max_samples
                                if n > max_samples
                                    x_vals = x_vals(end-max_samples+1:end);
                                    data_to_plot = data_to_plot(end-max_samples+1:end);
                                end
                                set(hx{ch}, 'XData', x_vals, 'YData', data_to_plot);
                            end
                        end

                        x_lo = max(1, max_len - max_samples + 1);
                        x_hi = max(x_lo + 1, max_len + 10);
                        set(ax_timeseries, 'XLim', [x_lo, x_hi]);
                        
                        % Auto-scale time-series y-axis
%                         set(ax_timeseries, 'XLim', [max(1, max_len-max_samples+1), max_len +10]);
                        
                        drawnow limitrate;
                        plot_update_counter = 0;
                    end
                end
            end
        end
        
        % Final update
        fprintf('\nC-scan complete! Total samples: %d\n', sample_count);

    catch ME
        disp('Streaming stopped unexpectedly.');
        rethrow(ME);
    end
end


function raw_value = read_parameter(serialObj, channel, param_name)
% READ_PARAMETER Read a single parameter value from the ETi device
%   raw_value = read_parameter(serialObj, channel, param_name)

    
    % Parameter codes (from Python PARAM_CODES)
    param_codes = containers.Map(...
        {'frequency', 'gain_x', 'gain_y', 'phase'}, ...
        {hex2dec('46'), hex2dec('58'), hex2dec('59'), hex2dec('50')});
    
    if ~isKey(param_codes, param_name)
        error('Invalid parameter name: %s', param_name);
    end
    
    param_code = param_codes(param_name);
    
    % Flush buffer to clear any streaming data
    flush(serialObj, 'input');
    pause(0.1);
    
    % Send command: [4, 0x3F, channel, param_code]
    cmd = uint8([4, hex2dec('3F'), channel, param_code]);
    write(serialObj, cmd, 'uint8');
    
    fprintf('SENT to ETi to request %s:\n', param_name);
    fprintf('%d, %d, %d, %d\n', cmd(1), cmd(2), cmd(3), cmd(4));
    
    % Wait for response with timeout
    t0 = tic;
    timeout = 2.0;  % 2 second timeout
    reply = [];
    
    while toc(t0) < timeout
        % Wait for at least 7 bytes
        if serialObj.NumBytesAvailable >= 7
            % Read available data
            available_bytes = serialObj.NumBytesAvailable;
            temp_data = read(serialObj, available_bytes, 'uint8');
            
            % Look for response starting with 0xC0 0x3F (EC_COMMAND_ACK)
            for i = 1:(length(temp_data) - 6)
                if temp_data(i) == hex2dec('C0') && temp_data(i+1) == hex2dec('3F')
                    % Found the response!
                    reply = temp_data(i:i+6);  % Get 7 bytes
                    break;
                end
            end
            
            if ~isempty(reply)
                break;
            end
        end
        pause(0.01);
    end
    
    if isempty(reply)
        error('Timeout waiting for %s reply - no valid response found', param_name);
    end
    
    % Verify this is the correct parameter response
    if reply(3) ~= param_code
        error('Received response for wrong parameter. Expected 0x%02X, got 0x%02X', param_code, reply(3));
    end
    
    fprintf('RECEIVED:\n');
    fprintf('%d, %d, %d, %d, %d, %d, %d\n', reply(1), reply(2), reply(3), reply(4), reply(5), reply(6), reply(7));
    
    % Extract value from bytes 4-7 (1-indexed: reply(4:7))
    % The value is in bytes 3-6 of the response (0-indexed), which is reply(4:7) in MATLAB
    raw_value = bitor(bitor(bitor(...
        bitshift(uint32(reply(4)), 24), ...
        bitshift(uint32(reply(5)), 16)), ...
        bitshift(uint32(reply(6)), 8)), ...
        uint32(reply(7)));
    
    raw_value = double(raw_value);
end


function values = verify_channel_config(serialObj, channel)
% VERIFY_CHANNEL_CONFIG Read and verify all parameters for a channel
%   values = verify_channel_config(serialObj, channel)
%
%   IMPORTANT: Call this AFTER configuration but BEFORE starting data streaming!
%   If data is streaming, stop it first with: send_and_check_xml(eti, '<USB_OUTPUT>0</USB_OUTPUT>', 1)
%
%   Inputs:
%       serialObj : serial port object
%       channel   : channel number (0-3 or 0-7 for array probe)
%
%   Returns:
%       values : structure with fields frequency_khz, gain_x_db, gain_y_db, 
%                phase_deg, hp_filter_hz, lp_filter_hz
    
    fprintf('\nReading channel %d configuration:\n', channel + 1);
    
    % Read frequency
    raw = read_parameter(serialObj, channel, 'frequency');
    values.frequency_khz = raw / 1000;
    fprintf('  Frequency: %.1f kHz\n', values.frequency_khz);
    flush(serialObj, 'input');
    pause(1);
    
    % Read gain X
    raw = read_parameter(serialObj, channel, 'gain_x');
    values.gain_x_db = raw / 10;
    fprintf('  Gain-X: %.1f dB\n', values.gain_x_db);
    flush(serialObj, 'input');
    pause(1);
    
    % Read gain Y
    raw = read_parameter(serialObj, channel, 'gain_y');
    values.gain_y_db = raw / 10;
    fprintf('  Gain-Y: %.1f dB\n', values.gain_y_db);
    flush(serialObj, 'input');
    pause(1);
    
    % Read phase
    raw = read_parameter(serialObj, channel, 'phase');
    values.phase_deg = raw / 1000;  % Phase is in thousandths of a degree
    fprintf('  Phase: %.1f degrees\n', values.phase_deg);
    flush(serialObj, 'input');
    pause(1);
    
%     % Read HP filter
%     raw = read_parameter(serialObj, channel, 'hp_filter');
%     values.hp_filter_hz = raw / 100;  % HP filter is in hundredths of Hz
%     fprintf('  High-Pass Filter: %.2f Hz\n', values.hp_filter_hz);
%     flush(serialObj, 'input');
%     pause(0.5);
%     
%     % Read LP filter
%     raw = read_parameter(serialObj, channel, 'lp_filter');
%     values.lp_filter_hz = raw / 100;  % LP filter is in hundredths of Hz
%     fprintf('  Low-Pass Filter: %.2f Hz\n', values.lp_filter_hz);
%     flush(serialObj, 'input');
%     pause(0.5);
end