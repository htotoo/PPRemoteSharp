﻿using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using System.Text;

namespace PortaPackRemoteApi
{
    public class PPApi
    {
        private static int SendChunkMax = 300;

        static string PROMPT = "ch> ";
         ~PPApi() { Close(); }

        //todo detect serial port error / close / disappear

        private SerialPort? _serialPort = null;
        // Events
        public event EventHandler? SerialOpened;
        public event EventHandler? SerialClosed;
        public event EventHandler? SerialError;

        public event EventHandler<string>? OnLine;
        private readonly ManualResetEventSlim lineEvent = new ManualResetEventSlim(false);
        public event EventHandler<byte[]>? OnRawBytes;

        private string dataInBuffer = "";
        private List<string> lastLines = new List<string>();
        private bool isWaitingForReply = false; //any task waiting and subscribed
        private bool sentcommandRn = false; //if sent command, and waiting for ANY reply, so don't send multiple at once

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
            _serialPort.WriteTimeout = 3000;
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
            if (_serialPort == null) return;
            int bytes = _serialPort.BytesToRead;
            sentcommandRn = false; //to prevent pp crash
            if (bytes <=0)
            {
                OnSerialError();
            }            

            byte[] buffer = new byte[bytes];
            _serialPort.Read(buffer, 0, bytes);
            string sajt = Encoding.UTF8.GetString(buffer, 0, bytes);
            dataInBuffer += sajt;
            
            int o = dataInBuffer.IndexOf("\r\n");
            while (o>=0 && dataInBuffer.Length>0)
            {
                string line = dataInBuffer.Substring(0, o);
                OnLineReceived(line);
                dataInBuffer = dataInBuffer.Remove(0,o + 2);
                o = dataInBuffer.IndexOf("\r\n");
            }
            if (dataInBuffer.EndsWith(PROMPT)) //bc it has no line ending, we must send it too
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

        private void PreWaitForReply()
        {
            lock (lastLines) { lastLines.Clear(); }
            isWaitingForReply = true;
            lineEvent.Reset();
        }

        private async Task<List<string>> ReadStringsAsync(string endMarker, bool presetWait = false)
        {
            isWaitingForReply = true;
            if (!presetWait) { lock (lastLines) { lastLines.Clear(); }
                lineEvent.Reset();
            }
            
            List<string> myLines = new List<string>();
            bool isFirst = true;
            while (true)
            {
                //lineEvent.Wait(10000);
                if (!isFirst)
                {
                    await Task.Run(() => lineEvent.Wait(12000));

                    if (!lineEvent.IsSet)
                    {
                        lineEvent.Reset();
                        isWaitingForReply = false;
                        return myLines; //error
                    }
                }
                lineEvent.Reset();
                isFirst = false;
                lock (lastLines)
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
            sentcommandRn = false;
            isWaitingForReply = false;
            try
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
            } catch { }
        }
        



        public bool WriteSerial(string line, bool ignoreLock = false) 
        {
            Trace.WriteLine(">" + line);
            if ((sentcommandRn || isWaitingForReply) && ! ignoreLock)
            {
                Trace.WriteLine("SEND COND FAILED");
                return false;
            }
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
                //_serialPort.DiscardInBuffer();
                _serialPort.BaseStream.Flush();
                int chunkSize = SendChunkMax;
                // Send data in chunks
                for (int i = 0; i < line.Length; i += chunkSize)
                {
                    int remainingBytes = Math.Min(chunkSize, line.Length - i);
                    string chunk = line.Substring(i, remainingBytes);
                    byte[] byteArray = Encoding.UTF8.GetBytes(chunk);
                    _serialPort.Write(byteArray, 0, byteArray.Length);
                    // Flush the serial port
                    _serialPort.BaseStream.Flush();
                }
                //sentcommandRn = true;
                return true;
            }
            catch (Exception ex) { 
                Trace.WriteLine(ex.ToString());
            }            
            return false;
        }

        public async Task<bool> WriteSerialBinary(byte[] data)
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                {
                    OnSerialError(); return false;
                }
                
                _serialPort.BaseStream.Flush();
                int chunkSize = SendChunkMax;
                // Send data in chunks
                for (int i = 0; i < data.Length; i += chunkSize)
                {
                    int remainingBytes = Math.Min(chunkSize, data.Length - i);
                    await Task.Delay(13);
                    _serialPort.BaseStream.Write(data, i, remainingBytes);
                    _serialPort.BaseStream.Flush();
                    Trace.WriteLine(i.ToString());
                }
                //sentcommandRn = true;
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }
            return false;
        }

       

        public async Task<List<string>> LS(string path = "/")
        {
            if (WriteSerial("ls " + path))
                return await ReadStringsAsync(PROMPT);
            else return new List<string>();
        }

        public async Task SendButton(ButtonState btn)
        {
            if (WriteSerial($"button {(int)btn}"))
                await ReadStringsAsync(PROMPT);
        }
        public async Task SendKeyboard(string keys)
        {
            byte[] readed = Encoding.ASCII.GetBytes(keys);
            string toWrite = BytesToHex(readed, readed.Length);
            for (int i=0; i<toWrite.Length; i+=30)
            {
                int rem = 30;
                if (toWrite.Length - i < 30) rem = toWrite.Length - i;
                if (WriteSerial($"keyboard " + toWrite.Substring(i,rem)))
                    await ReadStringsAsync(PROMPT);
            }
        }

        public async Task SendTouch(int x, int y)
        {
            if (WriteSerial($"touch {(int)x} {(int)y}"))
                await ReadStringsAsync(PROMPT);
        }


        public async Task SendFileDel(string file)
        {
            if (WriteSerial("unlink " + file))
                await ReadStringsAsync("ok");
        }


        public async Task<Bitmap> SendScreenFrameShort()
        {
#pragma warning disable CA1416 // Validate platform compatibility
            Bitmap bmp = new(241, 321);

            if (!WriteSerial("screenframeshort")) return bmp;
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
                    if (x >= 240)
                    {
                        Trace.WriteLine("Invalid line: " + y.ToString());
                        break;
                    }
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
                        Trace.WriteLine(e.ToString());
                    }
                }                
            }
            return bmp;
#pragma warning restore CA1416 // Validate platform compatibility
        }

        public async Task<Bitmap> SendScreenFrame()
        {
#pragma warning disable CA1416 // Validate platform compatibility
            Bitmap bmp = new(241, 321);

            if (!WriteSerial("screenframe")) return bmp;
            var lines = await ReadStringsAsync("ok");
            int y = -1;
            foreach (string line in lines)
            {
                y++;
                if (y>320) break;
                int x = -1;
                if (line.StartsWith("screenframe")) continue;
                for (int o = 0; o < line.Length; o += 6)
                {
                    x++;
                    if (x>240) break;
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
#pragma warning restore CA1416 // Validate platform compatibility
        }

        public async Task DownloadFile(string src, string dst, Action<int>? onProgress = null)
        {
            try
            {
                if (!WriteSerial("fclose")) return;
                await ReadStringsAsync(PROMPT);
                if (!WriteSerial("filesize " + src)) return;
                var lines = await ReadStringsAsync(PROMPT);
                if (lines.Last() != "ok")
                {
                    throw new Exception("Error downloading (size) file");
                }
                int size = int.Parse(lines[lines.Count - 2]);
                WriteSerial("fopen " + src);
                lines = await ReadStringsAsync(PROMPT);
                if (lines.Last() != "ok" && lines.Last() != "file already open")
                {
                    throw new Exception("Error downloading (open) file");
                }
                WriteSerial("fseek 0");
                lines = await ReadStringsAsync(PROMPT);
                if (lines.Last() != "ok")
                {
                    throw new Exception("Error downloading (seek) file");
                }
                var dFile = File.OpenWrite(dst);
                int rem = size;
                int chunk = 62 * 15;
                while (rem > 0)
                {
                    if (rem < chunk) { chunk = rem; }
                    WriteSerial("fread " + chunk.ToString());
                    lines = await ReadStringsAsync(PROMPT);
                    lines = lines.Skip(1).ToList();
                    var o = lines.Last();

                    if (o != "ok")
                    {
                        WriteSerial("fclose");
                        dFile.Close();
                        throw new Exception("Error downloading (data) file");
                    }
                    //parse and save!

                    for (int i = 0; i < lines.Count - 1; i++)
                    {
                        var bArr = ParseHexToByte(lines[i].ToUpper());
                        rem -= bArr.Length;
                        dFile.Write(bArr);
                    }
                    onProgress?.Invoke((int)((float)(size - rem) / (float)size * 100));

                }
                dFile.Close();
                WriteSerial("fclose");
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public async Task UploadFile(string src, string dst, Action<int>? onProgress = null, bool overWrite = false)
        {
            if (!WriteSerial("filesize " + dst)) return;
            var lines = await ReadStringsAsync(PROMPT);
            if (lines.Last() == "ok")
            {
                if (!overWrite)
                {
                    throw new Exception("Error uploading (overwrite) file");
                }
                else
                {
                    WriteSerial("unlink " + dst);
                    await ReadStringsAsync(PROMPT);
                }
            }
            long size = new FileInfo(src).Length;
            WriteSerial("fopen " + dst);
            lines = await ReadStringsAsync(PROMPT);
            if (lines.Last() != "ok" && lines.Last() != "file already open")
            {
                throw new Exception("Error uploading (open) file");
            }
            WriteSerial("fseek 0");
            lines = await ReadStringsAsync(PROMPT);
            if (lines.Last() != "ok")
            {
                throw new Exception("Error uploading (seek) file");
            }
            var sFile = File.OpenRead(src);
            sFile.Position = 0;
            long rem = size;
            long chunk = 54/2; 
            int trycount = 0;
            while (rem > 0)
            {
                if (rem < chunk) { chunk = rem; }
                byte[] readed = new byte[500];
                sFile.Read(readed, 0, (int)chunk);
                string toWrite = BytesToHex(readed, (int)chunk);

                WriteSerial("fwrite " + toWrite);
                lines = await ReadStringsAsync(PROMPT);
                if (lines.Count<1)
                {
                    Trace.WriteLine("No response");
                    trycount++;
                    if (trycount>5) throw new Exception("Error uploading (retry) file");
                    continue;
                }
                var o = lines.Last();
                if (o != "ok")
                {
                    WriteSerial("close");
                    sFile.Close();
                    await ReadStringsAsync(PROMPT);
                    throw new Exception("Error uploading (data) file");
                }
                rem -= chunk;
                trycount = 0;
                onProgress?.Invoke((int)((float)(size - rem) / (float)size * 100));
            }
            sFile.Close();
            WriteSerial("fclose");
            await ReadStringsAsync(PROMPT);
        }

        public async Task UploadFileBinary(string src, string dst, Action<int>? onProgress = null, bool overWrite = false)
        {
            if (!WriteSerial("filesize " + dst)) return;
            var lines = await ReadStringsAsync(PROMPT);
            if (lines.Last() == "ok")
            {
                if (!overWrite)
                {
                    throw new Exception("Error uploading (overwrite) file");
                }
                else
                {
                    WriteSerial("unlink " + dst);
                    await ReadStringsAsync(PROMPT);
                }
            }
            long size = new FileInfo(src).Length;
            WriteSerial("fopen " + dst);
            lines = await ReadStringsAsync(PROMPT);
            if (lines.Last() != "ok" && lines.Last() != "file already open")
            {
                throw new Exception("Error uploading (open) file");
            }
            PreWaitForReply();
            WriteSerial("fseek 0", true);
            lines = await ReadStringsAsync(PROMPT, true);
            if (lines.Last() != "ok")
            {
                throw new Exception("Error uploading (seek) file");
            }
            var sFile = File.OpenRead(src);
            sFile.Position = 0;
            long rem = size;
            long chunk = 9000;
            
            while (rem > 0)
            {
                if (rem < chunk) { chunk = rem; }
                byte[] readed = new byte[chunk];
                sFile.Read(readed, 0, (int)chunk);
                //PreWaitForReply();
                WriteSerial("fwb " + chunk.ToString());
                await ReadStringsAsync("send", false);
                await WriteSerialBinary(readed);
                lines = await ReadStringsAsync(PROMPT);                
                var o = lines.Last();
                if (o != "ok")
                {
                    WriteSerial("fclose");
                    sFile.Close();
                    await ReadStringsAsync(PROMPT);
                    throw new Exception("Error uploading (data) file");
                } 
                rem -= chunk;
                onProgress?.Invoke((int)((float)(size - rem) / (float)size * 100));
            }
            sFile.Close();
            WriteSerial("fclose");
            await ReadStringsAsync(PROMPT);
        }



        string BytesToHex(byte[] arr, int size)
        {
            StringBuilder hexString = new StringBuilder(size * 2);
            for (int i = 0; i < size; i++)
            {
                hexString.AppendFormat("{0:X2}", arr[i]);
            }
            return hexString.ToString();
        }

        private byte[] ParseHexToByte(string v)
        {
            // Remove any spaces or non-hex characters from the input string
            string hexString = "";
            foreach (char c in v)
            {
                if (Uri.IsHexDigit(c))
                {
                    hexString += c;
                }
            }
            // Check if the length of the hex string is odd, and pad with a leading zero if necessary
            if (hexString.Length % 2 != 0)
            {
                hexString = "0" + hexString;
            }
            // Convert the hex string to a byte array
            byte[] byteArray = new byte[hexString.Length / 2];
            for (int i = 0; i < hexString.Length; i += 2)
            {
                byteArray[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            }

            return byteArray;
        }

        public async Task SendRestart()
        {
            if (WriteSerial("reboot"))
                OnSerialClosed();  
        }

        public async Task SendHFMode()
        {
            if (WriteSerial("hackrf"))
               OnSerialClosed();   
        }

        public async Task SendScreenshot()
        {
            if (WriteSerial("screenshot"))
                await ReadStringsAsync(PROMPT);
        }
        public async Task SendDFU()
        {
            if (WriteSerial("dfu"))
                OnSerialClosed();  
        }
        public async Task SendSDOUsb()
        {
            if (WriteSerial("sd_over_usb"))
                OnSerialClosed();  
        }

        public async Task SendFlash(string file)
        {
            if (WriteSerial("flash " + file))
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
