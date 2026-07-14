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
