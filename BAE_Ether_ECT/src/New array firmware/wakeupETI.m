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