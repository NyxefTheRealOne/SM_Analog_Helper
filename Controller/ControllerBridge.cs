// Windows XInput + DualShock 4 Raw Input -> local JSON. No input injection.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AnalogDrive {
    public static class ControllerBridge {
        [StructLayout(LayoutKind.Sequential)]
        public struct Gamepad {
            public ushort Buttons;
            public byte LeftTrigger, RightTrigger;
            public short ThumbLX, ThumbLY, ThumbRX, ThumbRY;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct State { public uint Packet; public Gamepad Pad; }
        [DllImport("xinput1_4.dll", EntryPoint="XInputGetState")]
        private static extern uint GetState14(uint index, out State state);
        [DllImport("xinput9_1_0.dll", EntryPoint="XInputGetState")]
        private static extern uint GetState910(uint index, out State state);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint period);

        private static bool modern = true;
        private static volatile bool stop;
        public sealed class PreviewFrame {
            public bool Connected, Focused, Armed, OutputHealthy;
            public string Device="Controller";
            public double LeftX, LeftY, RightX, RightY, LeftTrigger, RightTrigger;
            public ushort Buttons;
        }
        public static volatile PreviewFrame Latest = new PreviewFrame();
        public static void RequestStop() { stop = true; }
        public static uint Read(uint index, out State state) {
            if (modern) {
                try { return GetState14(index, out state); }
                catch (DllNotFoundException) { modern = false; }
                catch (EntryPointNotFoundException) { modern = false; }
            }
            return GetState910(index, out state);
        }
        public static double Axis(short raw, double deadzone) {
            double x = raw < 0 ? raw / 32768.0 : raw / 32767.0;
            double a = Math.Abs(x);
            return a <= deadzone ? 0 : Math.Sign(x) * Math.Min(1, (a - deadzone) / (1 - deadzone));
        }
        public static double Trigger(byte raw, int threshold) {
            return raw <= threshold ? 0 : (raw - threshold) / (255.0 - threshold);
        }
        private static bool GameFocused() {
            try {
                uint pid;
                GetWindowThreadProcessId(GetForegroundWindow(), out pid);
                using (Process p = Process.GetProcessById((int)pid)) {
                    return String.Equals(p.ProcessName, "ScrapMechanic", StringComparison.OrdinalIgnoreCase);
                }
            } catch { return false; }
        }
        private static string Number(double x) { return x.ToString("0.00000", CultureInfo.InvariantCulture); }
        private static string Bool(bool b) { return b ? "true" : "false"; }
        private static string Packet(string session, long sequence, bool connected, bool focused,
            bool armed, double steer, double throttle, double brake, ushort buttons, int index,
            long shiftUp, long shiftDown, double leftY, double rightX, double rightY) {
            return "{\"protocol\":1,\"session\":\"" + session + "\",\"sequence\":" + sequence
                + ",\"published\":" + (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond)
                + ",\"connected\":" + Bool(connected) + ",\"focused\":" + Bool(focused)
                + ",\"armed\":" + Bool(armed) + ",\"steer\":" + Number(steer)
                + ",\"throttle\":" + Number(throttle) + ",\"brake\":" + Number(brake)
                + ",\"leftY\":" + Number(leftY) + ",\"rightX\":" + Number(rightX) + ",\"rightY\":" + Number(rightY)
                + ",\"buttons\":" + buttons + ",\"controller\":" + index
                + ",\"shiftUp\":" + shiftUp + ",\"shiftDown\":" + shiftDown
                + ",\"commit\":" + sequence + "}";
        }
        private static void Write(string path, string json) {
            // Keep file identities stable. Replacing a file underneath the game's
            // content reader can leave it trying to open a deleted Windows file.
            // The other two slots remain readable while this one is being written.
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            using (FileStream file = new FileStream(path, FileMode.OpenOrCreate,
                FileAccess.Write, FileShare.ReadWrite | FileShare.Delete)) {
                file.Write(bytes, 0, bytes.Length);
                file.SetLength(bytes.Length);
            }
        }
        public static void Run(string path, int selected, double deadzone, int triggerThreshold,
            int hz, double seconds, bool allowUnfocused) {
            RunWithBackend(path,selected,deadzone,triggerThreshold,hz,seconds,allowUnfocused,"Auto");
        }
        public static void RunWithBackend(string path,int selected,double deadzone,int triggerThreshold,
            int hz,double seconds,bool allowUnfocused,string backend) {
            if(String.Equals(backend,"auto",StringComparison.OrdinalIgnoreCase)) backend="Auto";
            if(String.Equals(backend,"xinput",StringComparison.OrdinalIgnoreCase)) backend="XInput";
            if(String.Equals(backend,"dualshock4",StringComparison.OrdinalIgnoreCase)) backend="DualShock4";
            if(backend!="Auto" && backend!="XInput" && backend!="DualShock4") throw new ArgumentException("Unknown controller backend.");
            if(backend=="DualShock4" && selected>=0) throw new ArgumentException("ControllerIndex selects Xbox slots; use -1 for DualShock 4.");
            if (selected < -1 || selected > 3 || deadzone < 0 || deadzone >= 1
                || triggerThreshold < 0 || triggerThreshold >= 255 || hz < 10 || hz > 120)
                throw new ArgumentException("Invalid controller configuration.");
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Only one writer per mod installation, including two copies of the launcher.
            string lockPath = Path.Combine(Path.GetDirectoryName(path), "bridge.lock");
            using (FileStream singleWriter = new FileStream(lockPath, FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None))
            using (DualShockInput sony=backend!="XInput" && selected<0 ? new DualShockInput():null) {
                if(sony!=null && sony.Error!=null) {
                    if(backend=="DualShock4") throw new InvalidOperationException(sony.Error);
                    Console.WriteLine("DualShock input unavailable: "+sony.Error+". Xbox input remains available.");
                }
                string session = Guid.NewGuid().ToString("N");
                string folder = Path.GetDirectoryName(path);
                string stem = Path.GetFileNameWithoutExtension(path);
                string[] slots = { Path.Combine(folder, stem + "-a.json"),
                    Path.Combine(folder, stem + "-b.json"), path };
                stop = false;
                ConsoleCancelEventHandler cancel = delegate(object sender, ConsoleCancelEventArgs e) {
                    e.Cancel = true; stop = true;
                };
                Console.CancelKeyPress += cancel;
                Stopwatch timer = Stopwatch.StartNew();
                long sequence = 0, shiftUp = 0, shiftDown = 0;
                int lastIndex = -1, errors = 0;
                string lastSource=null;
                bool armed = false, wasFocused = false, wasConnected = false;
                ushort lastButtons = 0;
                double nextPrint = 0, nextFrame = 0, lastPublished = -1;
                bool highResolution = timeBeginPeriod(1) == 0;
                try {
                    Console.WriteLine("Analog Controller Input controller reader. Keep this window open; Ctrl+C stops it.");
                    Console.WriteLine("Focus game, release triggers to arm. X exits; A slot 1; manual: LB down / RB up.");
                    Console.WriteLine("Output: " + path + " (3 buffered snapshots, " + hz + " Hz)");
                    string initial = Packet(session, 0, false, false, false, 0, 0, 0, 0, -1, 0, 0, 0, 0, 0);
                    foreach (string slot in slots) Write(slot, initial);
                    while (!stop && (seconds <= 0 || timer.Elapsed.TotalSeconds < seconds)) {
                        State state = new State();
                        int index = selected >= 0 ? selected : lastIndex;
                        string source=null,device="Xbox";
                        bool connected=sony!=null && sony.TryRead(out state,out source);
                        if(connected) { index=4;device="DualShock 4"; }
                        if(!connected && backend!="DualShock4") connected=index>=0 && index<4 && Read((uint)index,out state)==0;
                        if (!connected && selected == -1 && backend!="DualShock4") {
                            for (int n = 0; n < 4; n++) {
                                if (Read((uint)n, out state) == 0) { index = n; connected = true; break; }
                            }
                        }
                        if(connected && source==null) source="xinput:"+index;
                        bool focused = allowUnfocused || GameFocused();
                        if (!connected || !focused || !wasConnected || !wasFocused || source != lastSource) armed = false;
                        double throttle = connected ? Trigger(state.Pad.RightTrigger, triggerThreshold) : 0;
                        double brake = connected ? Trigger(state.Pad.LeftTrigger, triggerThreshold) : 0;
                        if (connected && focused && throttle == 0 && brake == 0) armed = true;
                        double steer = connected ? Axis(state.Pad.ThumbLX, deadzone) : 0;
                        double leftY = connected ? Axis(state.Pad.ThumbLY, deadzone) : 0;
                        double rightX = connected ? Axis(state.Pad.ThumbRX, deadzone) : 0;
                        double rightY = connected ? Axis(state.Pad.ThumbRY, deadzone) : 0;
                        ushort buttons = connected ? state.Pad.Buttons : (ushort)0;
                        // Cumulative press counts survive short presses between game ticks
                        // and repeated or missed network packets. Holding a bumper shifts once.
                        if (armed && wasFocused && wasConnected && source == lastSource) {
                            if ((buttons & 512) != 0 && (lastButtons & 512) == 0) shiftUp++;
                            if ((buttons & 256) != 0 && (lastButtons & 256) == 0) shiftDown++;
                        }
                        try {
                            sequence++;
                            Write(slots[(sequence - 1) % slots.Length], Packet(session, sequence, connected, focused, armed,
                                armed ? steer : 0, armed ? throttle : 0, armed ? brake : 0,
                                armed ? buttons : (ushort)0, connected ? index : -1, shiftUp, shiftDown,
                                armed ? leftY : 0, armed ? rightX : 0, armed ? rightY : 0));
                            errors = 0;
                            lastPublished = timer.Elapsed.TotalSeconds;
                        } catch (IOException) {
                            if (++errors == hz) Console.WriteLine("Waiting to write controller file. Check the installed mod path.");
                        }
                        // Immutable after publication; the optional trailer UI reads
                        // this snapshot without file I/O or delaying the 120 Hz writer.
                        Latest = new PreviewFrame { Connected=connected, Focused=focused, Armed=armed,
                            Device=device,
                            OutputHealthy=errors==0, LeftX=steer, LeftY=leftY, RightX=rightX, RightY=rightY,
                            LeftTrigger=brake, RightTrigger=throttle, Buttons=buttons };
                        if (timer.Elapsed.TotalSeconds >= nextPrint) {
                            nextPrint = timer.Elapsed.TotalSeconds + 1;
                            Console.WriteLine("{0} | {1} | LX {2:0.00} LY {3:0.00} RX {4:0.00} RY {5:0.00} | RT {6:0.00} LT {7:0.00}",
                                connected ? device+(index<4 ? " "+(index+1):"") : "No controller (Xbox / DualShock 4)",
                                lastPublished < 0 || timer.Elapsed.TotalSeconds - lastPublished > 0.1 ? "OUTPUT BLOCKED" :
                                (armed ? "READY" : (focused ? "Release triggers" : "Focus game")), steer, leftY, rightX, rightY, throttle, brake);
                        }
                        lastIndex = connected ? index : -1;
                        lastSource=connected ? source:null;
                        wasFocused = focused; wasConnected = connected;
                        lastButtons = buttons;
                        nextFrame += 1.0 / hz;
                        if (nextFrame < timer.Elapsed.TotalSeconds) nextFrame = timer.Elapsed.TotalSeconds;
                        while (!stop && timer.Elapsed.TotalSeconds < nextFrame) Thread.Sleep(1);
                    }
                } finally {
                    Latest = new PreviewFrame();
                    string final = Packet(session, ++sequence, false, false, false, 0, 0, 0, 0, -1, shiftUp, shiftDown, 0, 0, 0);
                    foreach (string slot in slots) {
                        try { Write(slot, final); } catch (IOException) { }
                    }
                    if (highResolution) timeEndPeriod(1);
                    Console.CancelKeyPress -= cancel;
                }
            }
        }
    }
}
