// Native Windows Raw Input for Sony DualShock 4 (CUH-ZCT1/ZCT2).
// No driver installation, HID output reports, rumble or controller-mode changes.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AnalogDrive {
    public sealed class DualShockInput : IDisposable {
        [StructLayout(LayoutKind.Sequential)]
        private struct RawDevice { public ushort Page,Usage; public uint Flags; public IntPtr Target; }
        [StructLayout(LayoutKind.Sequential)]
        private struct Header { public uint Type,Size; public IntPtr Device,WParam; }
        [StructLayout(LayoutKind.Explicit,Size=32)]
        private struct DeviceInfo {
            [FieldOffset(0)] public uint Size;
            [FieldOffset(4)] public uint Type;
            [FieldOffset(8)] public uint Vendor;
            [FieldOffset(12)] public uint Product;
            [FieldOffset(20)] public ushort Page;
            [FieldOffset(22)] public ushort Usage;
        }
        [DllImport("user32.dll",SetLastError=true)]
        private static extern bool RegisterRawInputDevices(RawDevice[] devices,uint count,uint size);
        [DllImport("user32.dll",SetLastError=true)]
        private static extern uint GetRawInputData(IntPtr handle,uint command,IntPtr data,ref uint size,uint headerSize);
        [DllImport("user32.dll",EntryPoint="GetRawInputDeviceInfoW",SetLastError=true)]
        private static extern uint GetDeviceInfo(IntPtr device,uint command,ref DeviceInfo info,ref uint size);
        [DllImport("user32.dll",SetLastError=true)]
        private static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);

        private sealed class Sample { public ControllerBridge.State State; public long Tick; }
        private readonly object gate=new object();
        private readonly Dictionary<IntPtr,Sample> samples=new Dictionary<IntPtr,Sample>();
        private readonly Dictionary<IntPtr,bool> known=new Dictionary<IntPtr,bool>();
        private readonly ManualResetEvent initialized=new ManualResetEvent(false);
        private readonly Thread thread;
        private IntPtr window;
        private IntPtr selected;
        private volatile bool disposed;
        public string Error { get; private set; }
        private static readonly long FreshTicks=Stopwatch.Frequency/4;

        public DualShockInput() {
            thread=new Thread(Pump);thread.IsBackground=true;thread.SetApartmentState(ApartmentState.STA);thread.Start();
            if(!initialized.WaitOne(2000)) Error="DualShock input initialization timed out.";
        }
        public bool TryRead(out ControllerBridge.State state,out string source) {
            lock(gate) {
                long now=Stopwatch.GetTimestamp(); Sample sample;
                if(samples.TryGetValue(selected,out sample) && now-sample.Tick<=FreshTicks) {
                    state=sample.State;source="ds4:"+selected.ToInt64();return true;
                }
                foreach(KeyValuePair<IntPtr,Sample> entry in samples) {
                    if(now-entry.Value.Tick<=FreshTicks) {
                        selected=entry.Key;state=entry.Value.State;source="ds4:"+selected.ToInt64();return true;
                    }
                }
            }
            state=new ControllerBridge.State();source=null;return false;
        }
        public static bool IsSupported(uint vendor,uint product) {
            return vendor==0x054c && (product==0x05c4 || product==0x09cc || product==0x0ba0);
        }
        private static short Stick(byte raw,bool invert) {
            double n=raw<128 ? (raw-128)/128.0 : (raw-128)/127.0;
            if(invert) n=-n;
            return (short)Math.Round(n*(n<0 ? 32768:32767));
        }
        public static bool Decode(byte[] report,out ControllerBridge.State state) {
            state=new ControllerBridge.State();
            if(report==null || report.Length<10) return false;
            int offset;
            if(report[0]==1) {
                if(report.Length!=10 && report.Length<64) return false;
                if(report.Length>=64 && (report[31]&4)!=0) return false; // wireless adapter without a pad
                offset=1;
            } else if(report[0]>=0x11 && report[0]<=0x19 && report.Length>=78 && (report[1]&0x80)!=0) {
                offset=3;
                // Windows BT input retains the HIDP CRC. Reject corruption rather
                // than applying random steering; USB/compact reports have no CRC.
                uint crc=0xffffffff;
                crc=Crc(crc,0xa1);
                for(int n=0;n<74;n++) crc=Crc(crc,report[n]);
                if((crc^0xffffffff)!=BitConverter.ToUInt32(report,74)) return false;
            } else return false;
            int face=report[offset+4],shoulders=report[offset+5];
            ushort buttons=0;
            if((face&0x10)!=0) buttons|=16384; // square -> X / exit seat
            if((face&0x20)!=0) buttons|=4096;  // cross -> A / toolbar 1
            if((face&0x40)!=0) buttons|=8192;  // circle -> B
            if((face&0x80)!=0) buttons|=32768; // triangle -> Y
            if((shoulders&1)!=0) buttons|=256; if((shoulders&2)!=0) buttons|=512;
            if((shoulders&16)!=0) buttons|=32; if((shoulders&32)!=0) buttons|=16;
            if((shoulders&64)!=0) buttons|=64; if((shoulders&128)!=0) buttons|=128;
            int hat=face&15;
            if(hat==0 || hat==1 || hat==7) buttons|=1;
            if(hat==3 || hat==4 || hat==5) buttons|=2;
            if(hat==5 || hat==6 || hat==7) buttons|=4;
            if(hat==1 || hat==2 || hat==3) buttons|=8;
            state.Pad=new ControllerBridge.Gamepad {ThumbLX=Stick(report[offset],false),ThumbLY=Stick(report[offset+1],true),
                ThumbRX=Stick(report[offset+2],false),ThumbRY=Stick(report[offset+3],true),Buttons=buttons,
                LeftTrigger=report[offset+7],RightTrigger=report[offset+8]};
            return true;
        }
        private static uint Crc(uint crc,byte value) {
            crc^=value;
            for(int n=0;n<8;n++) crc=(crc>>1)^((crc&1)!=0 ? 0xedb88320u:0);
            return crc;
        }
        private bool Accept(IntPtr device) {
            bool accepted;
            if(known.TryGetValue(device,out accepted)) return accepted;
            DeviceInfo info=new DeviceInfo(); info.Size=(uint)Marshal.SizeOf(typeof(DeviceInfo));uint size=info.Size;
            accepted=GetDeviceInfo(device,0x2000000b,ref info,ref size)!=uint.MaxValue && info.Type==2 && IsSupported(info.Vendor,info.Product);
            known[device]=accepted;return accepted;
        }
        private void Receive(IntPtr raw) {
            uint size=0,headerSize=(uint)Marshal.SizeOf(typeof(Header));
            if(GetRawInputData(raw,0x10000003,IntPtr.Zero,ref size,headerSize)==uint.MaxValue || size<headerSize+8 || size>65536) return;
            IntPtr buffer=Marshal.AllocHGlobal((int)size);
            try {
                uint actual=GetRawInputData(raw,0x10000003,buffer,ref size,headerSize);
                if(actual==uint.MaxValue || actual<headerSize+8) return;
                Header header=(Header)Marshal.PtrToStructure(buffer,typeof(Header));
                if(header.Type!=2 || !Accept(header.Device)) return;
                int length=Marshal.ReadInt32(buffer,(int)headerSize),count=Marshal.ReadInt32(buffer,(int)headerSize+4);
                if(length<10 || count<1 || (long)headerSize+8+(long)length*count>actual) return;
                for(int n=0;n<count;n++) {
                    byte[] report=new byte[length];Marshal.Copy(IntPtr.Add(buffer,(int)headerSize+8+n*length),report,0,length);
                    ControllerBridge.State state;
                    if(Decode(report,out state)) lock(gate) samples[header.Device]=new Sample {State=state,Tick=Stopwatch.GetTimestamp()};
                }
            } finally { Marshal.FreeHGlobal(buffer); }
        }
        private sealed class InputWindow : NativeWindow {
            private readonly DualShockInput owner;
            public InputWindow(DualShockInput owner) {
                this.owner=owner;CreateHandle(new CreateParams {Caption="Analog Controller Input - HID",Parent=new IntPtr(-3)});
            }
            protected override void WndProc(ref Message m) {
                if(m.Msg==0xff) {
                    try { owner.Receive(m.LParam); } catch(Exception e) { owner.Error=e.Message; }
                } else if(m.Msg==0xfe && m.WParam.ToInt64()==2) {
                    owner.known.Remove(m.LParam); lock(owner.gate) owner.samples.Remove(m.LParam);
                } else if(m.Msg==0x10) { DestroyHandle();Application.ExitThread();return; }
                base.WndProc(ref m);
            }
        }
        private void Pump() {
            InputWindow target=null;
            try {
                target=new InputWindow(this);window=target.Handle;
                RawDevice[] devices={new RawDevice {Page=1,Usage=5,Flags=0x2100,Target=window}};
                if(!RegisterRawInputDevices(devices,1,(uint)Marshal.SizeOf(typeof(RawDevice))))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                initialized.Set();
                if(!disposed) Application.Run();
            } catch(Exception e) { Error=e.Message;initialized.Set(); }
            finally {
                RegisterRawInputDevices(new RawDevice[] {new RawDevice {Page=1,Usage=5,Flags=1,Target=IntPtr.Zero}},1,(uint)Marshal.SizeOf(typeof(RawDevice)));
                if(target!=null && target.Handle!=IntPtr.Zero) target.DestroyHandle();
                window=IntPtr.Zero;
            }
        }
        public void Dispose() {
            disposed=true;
            if(window!=IntPtr.Zero) PostMessage(window,0x10,IntPtr.Zero,IntPtr.Zero);
            if(thread.IsAlive) thread.Join(2000);
        }
    }
}
