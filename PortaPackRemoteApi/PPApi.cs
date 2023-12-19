using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;

namespace PortaPackRemoteApi
{
    public class PPApi
    {
        static string PROMPT = "ch> ";
         ~PPApi() { Close(); }

        //commands: help exit info systime reboot dfu hackrf sd_over_usb flash screenshot write_memory read_memory button ls rm open seek close read write
        //to implement: info systime reboot dfu hackrf sd_over_usb flash screenshot button ls rm open seek close read write
        //implemented: reboot dfu hackrf sd_over_usb screenshot button ls

        //todo detect serial port error / close / disappear

        private SerialPort? _serialPort = null;
        // Events
        public event EventHandler SerialOpened;
        public event EventHandler SerialClosed;
        public event EventHandler SerialError;

        public event EventHandler<string> OnLine;
        private readonly ManualResetEventSlim lineEvent = new ManualResetEventSlim(false);

        private string dataInBuffer = "";
        private List<string> lastLines = new List<string>();
        private bool isWaitingForReply = false;

        public enum ButtonState
        {
            BUTTON_RIGHT = 1,
            BUTTON_LEFT = 2,
            BUTTON_DOWN= 3,
            BUTTON_UP = 4, 
            BUTTON_ENTER = 5,
            BUTTON_PERF = 6,
            BUTTON_ROTLEFT = 7,
            BUTTON_ROTRIGHT = 8
        }

        public string[] GetPorts()
        {
            return SerialPort.GetPortNames();
        }

        public bool IsConnected()
        {
            return (_serialPort != null && _serialPort.IsOpen);
        }

        public async Task OpenPort(string portName)
        {
            _serialPort = new SerialPort(portName);
            _serialPort.BaudRate = 115200; // Set your baud rate
            _serialPort.Parity = Parity.None;
            _serialPort.StopBits = StopBits.One;
            _serialPort.DataBits = 8;
            _serialPort.WriteTimeout = 1000;
            _serialPort.NewLine = "\r\n"; // Set your newline character
            _serialPort.ErrorReceived += _serialPort_ErrorReceived;
            _serialPort.DataReceived += _serialPort_DataReceived;

            await Task.Run(() => {
                try
                {
                    _serialPort.Open();
                    _serialPort.DiscardInBuffer();
                    OnSerialOpened();
                }
                catch { OnSerialError(); }
            }  ) ;
        }

        private void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int bytes = _serialPort.BytesToRead;
            if (bytes <=0)
            {
                OnSerialError();
            }
            byte[] buffer = new byte[bytes];
            _serialPort.Read(buffer, 0, bytes);
            string sajt = Encoding.UTF8.GetString(buffer, 0, bytes );
            dataInBuffer += sajt;
            
            int o = dataInBuffer.IndexOf("\r\n");
            while (o>=0 && dataInBuffer.Length>0)
            {
                string line = dataInBuffer.Substring(0, o);
                OnLineReceived(line);
                dataInBuffer = dataInBuffer.Remove(0,o + 2);
                o = dataInBuffer.IndexOf("\r\n");
            }
            if (dataInBuffer == PROMPT)
            {
                dataInBuffer = "";
                OnLineReceived(PROMPT);
            }
        }
        private void OnLineReceived(string line)
        {
            lock(lastLines)
            {
                if (isWaitingForReply)
                {
                    lastLines.Add(line);
                }
            }
            lineEvent.Set();
            OnLine?.Invoke(this,line);
            Trace.WriteLine(line);
        }
        private async Task<List<string>> ReadStringsAsync(string endMarker)
        {
            lock (lastLines) { lastLines.Clear(); isWaitingForReply = true; }
            lineEvent.Reset();
            List<string> myLines = new List<string>();
            while (true)
            {
                //lineEvent.Wait(10000);
                await Task.Run(() => lineEvent.Wait(10000));
                if (!lineEvent.IsSet)
                {
                    lineEvent.Reset();
                    return myLines; //error
                }
                lineEvent.Reset();                
                lock(lastLines)
                {
                    int i = 0;
                    for (i = 0; i<lastLines.Count; i++)
                    {
                        if (lastLines[i].StartsWith(endMarker))
                        {
                            isWaitingForReply = false;
                            return myLines;
                        }
                        myLines.Add(lastLines[i]);
                    }
                    lastLines.RemoveRange(0, i);
                }
            }
        }

        private void _serialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            OnSerialError();
        }

        public void Close()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.ErrorReceived -= _serialPort_ErrorReceived;
                _serialPort.DataReceived -= _serialPort_DataReceived;
                _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;
                OnSerialClosed();
            }
        }
        



        public bool WriteSerial(string line) 
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                {
                    OnSerialError(); return false;
                }
                if (!line.EndsWith("\n\r"))
                {
                    line = line + "\n\r";
                }
                byte[] byteArray = Encoding.UTF8.GetBytes(line);
                _serialPort.Write(byteArray, 0, byteArray.Length);
                return true;
            }
            catch (Exception ex) { 
                Trace.WriteLine(ex.ToString());
            }            
            return false;
        }


        public async Task<List<string>> LS(string path = "/")
        {
            WriteSerial("ls " + path);
            return await ReadStringsAsync(PROMPT);
        }

        public async Task SendButton(ButtonState btn)
        {
            WriteSerial($"button {(int)btn}");
            await ReadStringsAsync(PROMPT);
        }

        public async Task SendFileDel(string file)
        {
            //WriteSerial("rm " + file);
            //await ReadStringsAsync("ok");
            throw new NotImplementedException();
        }

        public async Task<Bitmap> SendScreenFrameShort()
        {
            Bitmap bmp = new Bitmap(241, 321);
            
            WriteSerial("screenframeshort");
            var lines = await ReadStringsAsync("ok");
            int y = -1;
            foreach(string line in lines)
            {
                y++;
                int x = -1;
                if (line.StartsWith("screenframe")) continue;
                for (int o = 0; o < line.Length; o+=1)
                {
                    x++;
                    if (x >= 240) break;
                    try
                    {
                        byte[] bys = Encoding.ASCII.GetBytes(line.Substring(o, 1));
                        byte by =(byte) (bys[0] - 32);
                        // -  R   G  B 
                        // 00 11 11 11
                        byte r = (byte)(by >> 4 << 6);
                        byte g = (byte)(by >> 2 << 6);
                        byte b = (byte)(by << 6);
                        bmp.SetPixel(x, y, Color.FromArgb(r, g, b));
                    }
                    catch (Exception e){
                        var oo = 1;
                        oo+=line.Length;
                    }
                }                
            }
            return bmp;
        }

        public async Task<Bitmap> SendScreenFrame()
        {
            Bitmap bmp = new Bitmap(241, 321);

            WriteSerial("screenframeshort");
            var lines = await ReadStringsAsync("ok");
            int y = -1;
            foreach (string line in lines)
            {
                y++;
                int x = -1;
                if (line.StartsWith("screenframe")) continue;
                for (int o = 0; o < line.Length; o += 6)
                {
                    x++;
                    try
                    {
                        var r = Convert.ToByte(line.Substring(o, 2), 16) ;
                        var g = Convert.ToByte(line.Substring(o + 2, 2), 16) ;
                        var b = Convert.ToByte(line.Substring(o + 4, 2), 16) ;
                        bmp.SetPixel(x, y, Color.FromArgb(r, g, b));
                    }
                    catch
                    {
                        var oo = 1;
                        oo++;
                    }
                }
            }
            return bmp;
        }

        public async Task SendRestart()
        {
            WriteSerial("reboot");
            OnSerialClosed();  
        }

        public async Task SendHFMode()
        {
            WriteSerial("hackrf");
            OnSerialClosed();   
        }

        public async Task SendScreenshot()
        {
            WriteSerial("screenshot");
            await ReadStringsAsync(PROMPT);
        }
        public async Task SendDFU()
        {
            WriteSerial("dfu");
            OnSerialClosed();  
        }
        public async Task SendSDOUsb()
        {
            WriteSerial("sd_over_usb");
            OnSerialClosed();  
        }



        // Event handlers
        protected virtual void OnSerialOpened()
        {
            SerialOpened?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnSerialClosed()
        {
            SerialClosed?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnSerialError()
        {
            SerialError?.Invoke(this, EventArgs.Empty);
        }
    }


}
