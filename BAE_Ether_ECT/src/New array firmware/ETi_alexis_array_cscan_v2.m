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

% verify_channel_config(eti, 0);

% Stream data with live C-scan plotting
flag = 1;
clear X;clear Y;clear Z;clear T;

% Choose what to display in C-scan: 'X', 'Y', or 'Z' (magnitude)
cscan_display = 'Y';  % Change this to 'X' or 'Y' if desired

[X, Y, Z, T, data] = StreamETiDataCScan(eti, 200, cscan_display);
write(eti, [1, 10], "uint8");   % 0x01 0x0A


%% Functions


function eti = wakeupETI(eti, port, baud)

    xml_command = '<USB_OUTPUT>99</USB_OUTPUT>';
    xml_packet  = uint8([1, 0, uint8(xml_command), 0]);

    try
         write(eti, xml_packet, 'uint8');
        fprintf("Wake command sent, no reconnect needed.\n");
         return;
    catch ME
        fprintf("Write caused disconnect (expected): %s\n", ME.message);
    end

    pause(1.0);

    try
        clear eti;
    catch
    end
    pause(0.5);

    connected = false;
    while ~connected
        try
            eti = serialport(port, baud);
            connected = true;
            fprintf("ETI is now awake and connected to %s at %d baud.\n", port, baud);
        catch ME
            fprintf("Reconnect failed: %s\nRetrying...\n", ME.message);
            pause(0.5);
        end
    end
end


function response = send_and_check_xml(serialObj, xml_command, timeout)
    if nargin < 3
        timeout = 1;
    end

    response = '';

    if isstring(xml_command)
        xml_command = char(xml_command);
    end

    flush(serialObj, "input");
    pause(0.5);

    xml_packet = uint8([1, 0, uint8(xml_command), 0]);
    write(serialObj, xml_packet, 'uint8');
    pause(0.5);

    cmd = uint8([1, 0, 8]);
    write(serialObj, cmd, "uint8")
    pause(0.5);

    poll_cmd = uint8([1, 10, 0x08]);
    write(serialObj, poll_cmd, 'uint8');
    pause(0.05);

    startTime = tic;
    buffer = strings;
    while toc(startTime) < timeout
        if serialObj.NumBytesAvailable > 0
            try
                line = readline(serialObj);
                buffer(end+1) = string(line); %#ok<AGROW>
                if contains(line, '<INSTRUMENT>ETI300')
                    break;
                end
            catch
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

    disp('configuring ETI be patience')

    if nargin < 9 || isempty(timeout)
        timeout = 1;
    end
    if nargin < 8 || isempty(connector)
        connector='Lemo 1-Bridge';
    end
    if nargin < 6 || isempty(gainY)
        gainY = '30';
    end
    if nargin < 5 || isempty(gainX)
        gainX = '30';
    end

    %frequency configuration
    xmlLines = {
        ['<CHANNEL>' num2str(channel)]
        ['<FREQUENCY>' num2str(frequency) '</FREQUENCY>']
        '</CHANNEL>'
    };
    xml = strjoin(xmlLines, newline);
    flush(serialObj, "input"); pause(0.5);
    write(serialObj, uint8([1, 0, uint8(xml), 0]), 'uint8');
    pause(0.5);
    write(serialObj, uint8([1, 0, 8]), "uint8");
    pause(0.5);
    write(serialObj, uint8([1, 10, 0x08]), 'uint8');

    %phase configuration
    xmlLines = {
        ['<CHANNEL>' num2str(channel)]
        ['<PHASE>' num2str(phase) '</PHASE>']
        '</CHANNEL>'
    };
    xml = strjoin(xmlLines, newline);
    flush(serialObj, "input"); pause(0.5);
    write(serialObj, uint8([1, 0, uint8(xml), 0]), 'uint8');
    pause(0.5);
    write(serialObj, uint8([1, 0, 8]), "uint8");
    pause(0.5);
    write(serialObj, uint8([1, 10, 0x08]), 'uint8');

    %gainX configuration
    xmlLines = {
        ['<CHANNEL>' num2str(channel)]
        ['<GAIN_X>' num2str(gainX) '</GAIN_X>']
        '</CHANNEL>'
    };
    xml = strjoin(xmlLines, newline);
    flush(serialObj, "input"); pause(0.5);
    write(serialObj, uint8([1, 0, uint8(xml), 0]), 'uint8');
    pause(0.5);
    write(serialObj, uint8([1, 0, 8]), "uint8");
    pause(0.5);
    write(serialObj, uint8([1, 10, 0x08]), 'uint8');

    %gainY configuration
    xmlLines = {
        ['<CHANNEL>' num2str(channel)]
        ['<GAIN_Y>' num2str(gainY) '</GAIN_Y>']
        '</CHANNEL>'
    };
    xml = strjoin(xmlLines, newline);
    flush(serialObj, "input"); pause(0.5);
    write(serialObj, uint8([1, 0, uint8(xml), 0]), 'uint8');
    pause(0.5);
    write(serialObj, uint8([1, 0, 8]), "uint8");
    pause(0.5);
    write(serialObj, uint8([1, 10, 0x08]), 'uint8');

    % Channel select (needed before connector/load)
    xmlLines = { ['<CHANNEL>' num2str(channel) '</CHANNEL>'] };
    xml = strjoin(xmlLines, newline);
    flush(serialObj, "input"); pause(0.5);
    write(serialObj, uint8([1, 0, uint8(xml), 0]), 'uint8');
    pause(0.5);
    write(serialObj, uint8([1, 0, 8]), "uint8");
    pause(0.5);
    write(serialObj, uint8([1, 10, 0x08]), 'uint8');

    %connector configuration
    xmlLines = { ['<CONNECTOR>' connector '</CONNECTOR>'] };
    xml = strjoin(xmlLines, newline);
    flush(serialObj, "input"); pause(0.5);
    write(serialObj, uint8([1, 0, uint8(xml), 0]), 'uint8');
    pause(0.5);
    write(serialObj, uint8([1, 0, 8]), "uint8");
    pause(0.5);
    write(serialObj, uint8([1, 10, 0x08]), 'uint8');

    %load configuration
    xmlLines = { ['<LOAD>' num2str(load) '</LOAD>'] };
    xml = strjoin(xmlLines, newline);
    flush(serialObj, "input"); pause(0.5);
    write(serialObj, uint8([1, 0, uint8(xml), 0]), 'uint8');
    pause(0.5);
    write(serialObj, uint8([1, 0, 8]), "uint8");
    pause(0.5);
    write(serialObj, uint8([1, 10, 0x08]), 'uint8');

end

function [Xarray, Yarray, Zarray, Tarray, data_out] = StreamETiDataCScan(eti, scan_duration, display_type)
    flush(eti);
    if nargin < 3, display_type = 'Z'; end
    if nargin < 2, scan_duration = 20; end

    % --- 1. Settings & Buffers ---
    Xarray = cell(32,1); Yarray = cell(32,1); Zarray = cell(32,1); Tarray = cell(32,1);
    for ch = 1:32
        Xarray{ch} = []; Yarray{ch} = []; Zarray{ch} = []; Tarray{ch} = [];
    end
    data_out = []; 

    % Timing Logic
    balance_duration = 1; % 10 seconds for baseline recording
    total_time = balance_duration + scan_duration;
    
    % Transient & Sequence State
    state.skip_N = 50; 
    state.known_sequence = [0,31,1,30,2,29,3,28,4,27,5,26,6,25,7,24,8,23,9,22,10,21,11,20,12,19,13,18,14,17,15,16];
    state.skip_counts = zeros(1, 32);
    state.last_coil = -1;
    
    % Baseline/Nulling Buffers
    null_X = zeros(1, 32);
    null_Y = zeros(1, 32);
    null_count = zeros(1, 32);
    is_balancing = true;

    % --- 2. Figure Setup ---
    fig = figure('Name', 'ETI 300 - Dual Phase Scan', 'Position', [50 50 1200 800]);
    ax_cscan = subplot(2,1,1);
    num_cols = 500; 
    cscan_mat = nan(32, num_cols);
    h_cscan = imagesc([0 scan_duration], [1 32], cscan_mat); 
    colorbar; ylabel('Physical Coil'); 
    set(ax_cscan, 'YDir', 'normal', 'XLim', [0, scan_duration]);
    title('STAGE 1: RECORDING BASELINE (HOLD PROBE STILL)');

    ax_timeseries = subplot(2,1,2);
    hx = cell(32,1); colors = lines(32); hold(ax_timeseries, 'on');
    for ch = 1:32, hx{ch} = plot(ax_timeseries, nan, nan, '-', 'Color', colors(ch,:)); end
    grid on; xlabel('Time (s)'); ylabel('Amplitude');
    set(ax_timeseries, 'XLim', [0, scan_duration]);
    hold(ax_timeseries, 'off');

    t_start = tic;
    try
        while toc(t_start) < total_time
            curr_elapsed = toc(t_start);
            
            if eti.NumBytesAvailable >= 53
                new_bytes = read(eti, eti.NumBytesAvailable, 'uint8');
                data_out = [data_out; new_bytes(:)]; 
                
                packet_start = find(new_bytes(1:end-52) == 0x80 & new_bytes(2:end-51) == 0x7F);
                
                for kk = 1:numel(packet_start)
                    idx = packet_start(kk);
                    packet = new_bytes(idx:idx+52);
                    coil = packet(51); 

                    if coil < 0 || coil > 31, continue; end
                    phys_pos = double(coil) + 1;

                    % --- TRANSIENT SKIP LOGIC ---
                    if coil ~= state.last_coil
                        state.skip_counts(phys_pos) = state.skip_N;
                        state.last_coil = coil;
                        continue; 
                    end
                    if state.skip_counts(phys_pos) > 0
                        state.skip_counts(phys_pos) = state.skip_counts(phys_pos) - 1;
                        continue; 
                    end

                    % Extract Raw Data
                    raw_x = double(typecast(uint8(packet(3:6)), 'int32'));
                    raw_y = double(typecast(uint8(packet(7:10)), 'int32'));

                    % --- PHASE 1: BALANCING (0 - 10s) ---
                    if curr_elapsed < balance_duration
                        null_X(phys_pos) = null_X(phys_pos) + raw_x;
                        null_Y(phys_pos) = null_Y(phys_pos) + raw_y;
                        null_count(phys_pos) = null_count(phys_pos) + 1;
                        continue; 
                    end

                    % --- TRANSITION TO PHASE 2: SCANNING ---
                    if is_balancing
                        % Finalize averages
                        for i = 1:32
                            if null_count(i) > 0
                                null_X(i) = null_X(i) / null_count(i);
                                null_Y(i) = null_Y(i) / null_count(i);
                            end
                        end
                        is_balancing = false;
                        title(ax_cscan, 'STAGE 2: SCANNING (MOVE PROBE NOW)');
                        fprintf('Baseline recorded. Start scanning!\n');
                    end

                    % --- PHASE 2: DATA ACQUISITION (Baseline Subtracted) ---
                    scan_time = curr_elapsed - balance_duration;
                    x_corr = raw_x - null_X(phys_pos);
                    y_corr = raw_y - null_Y(phys_pos);
                    z_corr = sqrt(x_corr^2 + y_corr^2);
                    
                    Xarray{phys_pos}(end+1) = x_corr;
                    Yarray{phys_pos}(end+1) = y_corr;
                    Zarray{phys_pos}(end+1) = z_corr;
                    Tarray{phys_pos}(end+1) = scan_time;
                end

                % --- LIVE REFRESH ---
                if ~is_balancing
                    scan_time = curr_elapsed - balance_duration;
                    col = max(1, round((scan_time / scan_duration) * num_cols));
                    col = min(col, num_cols);

                    for ch = 1:32
                        switch upper(display_type)
                            case 'X', d = Xarray{ch};
                            case 'Y', d = Yarray{ch};
                            otherwise, d = Zarray{ch};
                        end
                        if ~isempty(d)
                            set(hx{ch}, 'XData', Tarray{ch}, 'YData', d);
                            cscan_mat(ch, col) = d(end);
                        end
                    end
                    set(h_cscan, 'CData', cscan_mat);
                    drawnow limitrate;
                end
            end
        end
        fprintf('\nScan Complete. Total Duration: %.1f s\n', toc(t_start));
    catch ME
        fprintf('Error: %s\n', ME.message);
    end
end

function raw_value = read_parameter(serialObj, channel, param_name)

    param_codes = containers.Map(...
        {'frequency', 'gain_x', 'gain_y', 'phase'}, ...
        {hex2dec('46'), hex2dec('58'), hex2dec('59'), hex2dec('50')});

    if ~isKey(param_codes, param_name)
        error('Invalid parameter name: %s', param_name);
    end

    param_code = param_codes(param_name);

    flush(serialObj, 'input');
    pause(0.1);

    cmd = uint8([4, hex2dec('3F'), channel, param_code]);
    write(serialObj, cmd, 'uint8');

    fprintf('SENT to ETi to request %s:\n', param_name);
    fprintf('%d, %d, %d, %d\n', cmd(1), cmd(2), cmd(3), cmd(4));

    t0 = tic;
    timeout = 2.0;
    reply = [];

    while toc(t0) < timeout
        if serialObj.NumBytesAvailable >= 7
            available_bytes = serialObj.NumBytesAvailable;
            temp_data = read(serialObj, available_bytes, 'uint8');

            % Search all 0xC0 0x3F headers, skip stale packets
            for i = 1:(length(temp_data) - 6)
                if temp_data(i) == hex2dec('C0') && temp_data(i+1) == hex2dec('3F')
                    candidate = temp_data(i:i+6);
                    if candidate(3) == param_code
                        reply = candidate;
                        break;
                    else
                        fprintf('Skipping stale packet for param code 0x%02X (want 0x%02X)\n', candidate(3), param_code);
                    end
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

    fprintf('RECEIVED:\n');
    fprintf('%d, %d, %d, %d, %d, %d, %d\n', reply(1), reply(2), reply(3), reply(4), reply(5), reply(6), reply(7));

    raw_value = bitor(bitor(bitor(...
        bitshift(uint32(reply(4)), 24), ...
        bitshift(uint32(reply(5)), 16)), ...
        bitshift(uint32(reply(6)), 8)), ...
        uint32(reply(7)));

    raw_value = double(raw_value);
end


function values = verify_channel_config(serialObj, channel)

    fprintf('\nReading channel %d configuration:\n', channel + 1);

    raw = read_parameter(serialObj, channel, 'frequency');
    values.frequency_khz = raw / 1000;
    fprintf('  Frequency: %.1f kHz\n', values.frequency_khz);
    flush(serialObj, 'input'); pause(1);

    raw = read_parameter(serialObj, channel, 'gain_x');
    values.gain_x_db = raw / 10;
    fprintf('  Gain-X: %.1f dB\n', values.gain_x_db);
    flush(serialObj, 'input'); pause(1);

    raw = read_parameter(serialObj, channel, 'gain_y');
    values.gain_y_db = raw / 10;
    fprintf('  Gain-Y: %.1f dB\n', values.gain_y_db);
    flush(serialObj, 'input'); pause(1);

    raw = read_parameter(serialObj, channel, 'phase');
    values.phase_deg = raw / 1000;
    fprintf('  Phase: %.1f degrees\n', values.phase_deg);
    flush(serialObj, 'input'); pause(1);

end