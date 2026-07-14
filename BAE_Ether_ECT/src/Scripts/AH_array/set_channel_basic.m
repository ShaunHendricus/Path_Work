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
    connector='off';
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
    xml='<CONNECTOR>off</CONNECTOR>';
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
    xml='<CONNECTOR>off</CONNECTOR>';
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
    xml='<CONNECTOR>off</CONNECTOR>';
    xml_packet = uint8([1, 0, uint8(xml), 0]);
    write(serialObj, xml_packet, 'uint8');
    pause(0.5);
    cmd = uint8([1, 0, 8]);
    write(serialObj, cmd, "uint8")
    pause(0.5);
    poll_cmd = uint8([1, 10, 0x08]);
    write(serialObj, poll_cmd, 'uint8');

end