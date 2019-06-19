using System;
using System.Threading;
using System.Threading.Tasks;
using Solid.Arduino;
using Solid.Arduino.Firmata;

namespace OttoHelper
{
    public class OttoSession : IDisposable
    {
        private readonly ArduinoSession _session;
        private static AsyncAutoResetEvent<string> _waitHandle;

        public OttoSession(ISerialConnection connection)
        {
            _session = new ArduinoSession(connection);
            _session.MessageReceived += Session_MessageReceived;
        }

        public async Task<bool> TestConnectionAsync()
        {
            return await GetFirmataVersionAsync();
        }

        private async Task<bool> GetFirmataVersionAsync()
        {
            var firmware = await _session.GetFirmwareAsync();
            Console.WriteLine($"Firmware: {firmware.Name} version {firmware.MajorVersion}.{firmware.MinorVersion}");
            var protocolVersion = await _session.GetProtocolVersionAsync();
            Console.WriteLine($"Firmata protocol version {protocolVersion.Major}.{protocolVersion.Minor}");
            return true;
        }

        public async Task<bool> SendStringCommandAsync(string command, string arg = null)
        {
            if (_waitHandle != null && !_waitHandle.Released) throw new InvalidOperationException();
            _session.SendStringData(command);
            _waitHandle = new AsyncAutoResetEvent<string>();
            return await _waitHandle.WaitAsync() == "ok";
        }

        public async Task<string> GetStringDataAsync(string resource)
        {
            if (_waitHandle != null && !_waitHandle.Released) throw new InvalidOperationException();
            _session.SendStringData(resource);
            _waitHandle = new AsyncAutoResetEvent<string>();
            return await _waitHandle.WaitAsync();
        }

        public void Dispose()
        {
            _session.Dispose();
        }

        private static void Session_MessageReceived(object sender, FirmataMessageEventArgs eventArgs)
        {
            if (eventArgs.Value.Type == MessageType.StringData)
            {
                _waitHandle.Set(((StringData)eventArgs.Value.Value).Text);
            }
        }
    }
}
