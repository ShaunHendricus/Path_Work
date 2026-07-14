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
