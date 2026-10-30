// Descriptor-driven HID input. The Raw Input window is shared with the DS4 reader.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace AnalogDrive {
    public sealed class InputAxis {
        public string Id, Label;
        public double Value, Min=-1, Max=1;
        public bool Button;
        public InputAxis Copy() { return (InputAxis)MemberwiseClone(); }
    }
    public sealed class InputDevice {
        public string Id, Name;
        public bool Connected, Sampled;
        public long ShiftUp,ShiftDown;
        public List<InputAxis> Axes=new List<InputAxis>();
        public InputDevice Copy() {
            InputDevice d=new InputDevice {Id=Id,Name=Name,Connected=Connected,Sampled=Sampled,ShiftUp=ShiftUp,ShiftDown=ShiftDown};
            foreach(InputAxis a in Axes) d.Axes.Add(a.Copy());
            return d;
        }
    }
    public sealed class HidDevices : IDisposable {
        [StructLayout(LayoutKind.Sequential)] private struct DeviceEntry { public IntPtr Handle; public uint Type; }
        [DllImport("user32.dll")] private static extern uint GetRawInputDeviceList([Out] DeviceEntry[] list,ref uint count,uint size);
        [DllImport("user32.dll",EntryPoint="GetRawInputDeviceInfoW")] private static extern uint Info(IntPtr device,uint command,IntPtr data,ref uint size);
        [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr data,[Out] byte[] caps);
        [DllImport("hid.dll")] private static extern int HidP_GetValueCaps(int type,[Out] byte[] caps,ref ushort count,IntPtr data);
        [DllImport("hid.dll")] private static extern int HidP_GetButtonCaps(int type,[Out] byte[] caps,ref ushort count,IntPtr data);
        [DllImport("hid.dll")] private static extern int HidP_GetUsageValue(int type,ushort page,ushort link,ushort usage,out uint value,IntPtr data,byte[] report,uint length);
        [DllImport("hid.dll")] private static extern int HidP_GetUsageValueArray(int type,ushort page,ushort link,ushort usage,[Out] byte[] values,ushort size,IntPtr data,byte[] report,uint length);
        [DllImport("hid.dll")] private static extern int HidP_GetUsages(int type,ushort page,ushort link,[Out] ushort[] usages,ref uint count,IntPtr data,byte[] report,uint length);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
        [DllImport("hid.dll")] private static extern bool HidD_GetProductString(SafeFileHandle file,[Out] byte[] text,uint length);
        private const int Success=0x110000;
        private sealed class Field {
            public ushort Page,Link,Usage,Bits,Count;
            public byte Report;
            public int Minimum,Maximum,Element;
            public bool Button,Hat;
            public bool Seen;
            public InputAxis Axis;
        }
        private sealed class Device {
            public IntPtr Data;
            public InputDevice Frame;
            public List<Field> Fields=new List<Field>();
        }
        private readonly object gate=new object();
        private readonly Dictionary<IntPtr,Device> devices=new Dictionary<IntPtr,Device>();
        public static string StableId(string path) {
            using(SHA256 hash=SHA256.Create()) return "hid:"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(path.ToUpperInvariant()))).Replace("-","").ToLowerInvariant();
        }
        public static double Normalize(uint raw,int minimum,int maximum,int bits) {
            long value=raw;
            if(minimum<0 && bits>0 && bits<=32) {
                long sign=1L<<(bits-1),mask=(1L<<bits)-1;
                value&=mask; if((value&sign)!=0) value-=1L<<bits;
            }
            double high=minimum>=0 ? (double)(uint)maximum:maximum;
            if(high<=minimum) return 0;
            return Math.Max(-1,Math.Min(1,2.0*(value-minimum)/(high-minimum)-1));
        }
        private static string UsageName(ushort page,ushort usage) {
            if(page==1 && usage>=0x30 && usage<=0x39) return new string[]{"X","Y","Z","Rotation X","Rotation Y","Rotation Z","Slider","Dial","Wheel","Hat"}[usage-0x30];
            if(page==2) {
                if(usage==0xc4) return "Accelerator"; if(usage==0xc5) return "Brake";
                if(usage==0xc8) return "Steering"; if(usage==0xbb) return "Throttle";
                if(usage==0xba) return "Rudder";
            }
            return "Usage "+page.ToString("X4")+":"+usage.ToString("X4");
        }
        private Device Open(IntPtr handle) {
            uint size=0;
            if(Info(handle,0x20000005,IntPtr.Zero,ref size)==uint.MaxValue || size==0) return null;
            IntPtr data=Marshal.AllocHGlobal((int)size);
            try {
                if(Info(handle,0x20000005,data,ref size)==uint.MaxValue) return null;
                byte[] caps=new byte[64]; if(HidP_GetCaps(data,caps)!=Success) return null;
                ushort page=BitConverter.ToUInt16(caps,2),usage=BitConverter.ToUInt16(caps,0);
                if(!(page==1 && (usage==4 || usage==5 || usage==8) || page==2)) return null;
                uint chars=0;Info(handle,0x20000007,IntPtr.Zero,ref chars);
                if(chars==0 || chars>32768) return null;
                IntPtr nameBuffer=Marshal.AllocHGlobal((int)(chars+1)*2);
                string path;
                try { if(Info(handle,0x20000007,nameBuffer,ref chars)==uint.MaxValue) return null; path=Marshal.PtrToStringUni(nameBuffer); }
                finally { Marshal.FreeHGlobal(nameBuffer); }
                string name="HID "+UsageName(page,usage);
                using(SafeFileHandle file=CreateFile(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
                    byte[] product=new byte[512];
                    if(!file.IsInvalid && HidD_GetProductString(file,product,(uint)product.Length)) name=Encoding.Unicode.GetString(product).TrimEnd('\0');
                }
                Device d=new Device {Data=data,Frame=new InputDevice {Id=StableId(path),Name=name,Connected=true}};
                AddCaps(d,BitConverter.ToUInt16(caps,48),false);
                AddCaps(d,BitConverter.ToUInt16(caps,46),true);
                data=IntPtr.Zero;
                return d;
            } finally { if(data!=IntPtr.Zero) Marshal.FreeHGlobal(data); }
        }
        private static void AddCaps(Device d,ushort count,bool button) {
            if(count==0) return;
            byte[] caps=new byte[count*72];
            int status=button ? HidP_GetButtonCaps(0,caps,ref count,d.Data):HidP_GetValueCaps(0,caps,ref count,d.Data);
            if(status!=Success) return;
            for(int n=0;n<count;n++) {
                int offset=n*72;
                if(caps[offset+3]!=0) continue; // aliased controls refer to an already represented field
                ushort page=BitConverter.ToUInt16(caps,offset),link=BitConverter.ToUInt16(caps,offset+6);
                ushort first=BitConverter.ToUInt16(caps,offset+56),last=caps[offset+12]!=0 ? BitConverter.ToUInt16(caps,offset+58):first;
                for(int usage=first;usage<=last;usage++) {
                    int elements=!button && caps[offset+12]==0 ? Math.Max(1,(int)BitConverter.ToUInt16(caps,offset+20)):1;
                    for(int element=0;element<elements;element++) {
                        string id=(button ? "b":"a")+page+"_"+link+"_"+caps[offset+2]+"_"+usage+(elements>1 ? "_"+element:"");
                        InputAxis a=new InputAxis {Id=id,Label=button ? "Button "+usage:UsageName(page,(ushort)usage)+(elements>1 ? " "+(element+1):""),Button=button,Min=button ? 0:-1};
                        d.Frame.Axes.Add(a);
                        d.Fields.Add(new Field {Page=page,Link=link,Usage=(ushort)usage,Report=caps[offset+2],Button=button,Axis=a,
                            Bits=button ? (ushort)1:BitConverter.ToUInt16(caps,offset+18),Count=(ushort)elements,Element=element,
                            Minimum=button ? 0:BitConverter.ToInt32(caps,offset+40),Maximum=button ? 1:BitConverter.ToInt32(caps,offset+44),Hat=page==1 && usage==0x39});
                    }
                }
            }
        }
        public void Refresh() {
            lock(gate) {
                uint count=0,size=(uint)Marshal.SizeOf(typeof(DeviceEntry));
                if(GetRawInputDeviceList(null,ref count,size)==uint.MaxValue) return;
                DeviceEntry[] entries=new DeviceEntry[count];
                uint actual=GetRawInputDeviceList(entries,ref count,size);if(actual==uint.MaxValue) return;
                HashSet<IntPtr> present=new HashSet<IntPtr>();
                for(int n=0;n<actual;n++) if(entries[n].Type==2) {
                    present.Add(entries[n].Handle);
                    if(!devices.ContainsKey(entries[n].Handle)) {
                        Device d=Open(entries[n].Handle); if(d!=null) devices.Add(entries[n].Handle,d);
                    }
                }
                List<IntPtr> removed=new List<IntPtr>();foreach(IntPtr handle in devices.Keys) if(!present.Contains(handle)) removed.Add(handle);
                foreach(IntPtr handle in removed) { Marshal.FreeHGlobal(devices[handle].Data);devices.Remove(handle); }
            }
        }
        public void Receive(IntPtr handle,byte[] report) {
            lock(gate) {
                Device d;if(!devices.TryGetValue(handle,out d) || report.Length==0) return;
                bool sampled=false;
                Dictionary<string,HashSet<ushort>> buttons=new Dictionary<string,HashSet<ushort>>();
                foreach(Field f in d.Fields) {
                    if(f.Report!=report[0]) continue;
                    if(f.Button) {
                        string key=f.Page+":"+f.Link;HashSet<ushort> active;
                        if(!buttons.TryGetValue(key,out active)) {
                            ushort[] usages=new ushort[65536];uint count=(uint)usages.Length;
                            if(HidP_GetUsages(0,f.Page,f.Link,usages,ref count,d.Data,report,(uint)report.Length)!=Success) continue;
                            active=new HashSet<ushort>();for(int n=0;n<count;n++) active.Add(usages[n]);buttons.Add(key,active);
                        }
                        f.Axis.Value=active.Contains(f.Usage) ? 1:0;
                    } else {
                        uint raw;
                        if(f.Count>1) {
                            int bytes=(f.Count*f.Bits+7)/8;if(bytes>65535) continue;
                            byte[] values=new byte[bytes];
                            if(HidP_GetUsageValueArray(0,f.Page,f.Link,f.Usage,values,(ushort)bytes,d.Data,report,(uint)report.Length)!=Success) continue;
                            raw=0;for(int bit=0;bit<Math.Min(32,(int)f.Bits);bit++) {int pos=f.Element*f.Bits+bit;raw|=(uint)((values[pos/8]>>(pos%8))&1)<<bit;}
                        } else if(HidP_GetUsageValue(0,f.Page,f.Link,f.Usage,out raw,d.Data,report,(uint)report.Length)!=Success) continue;
                        f.Axis.Value=f.Hat && (raw<f.Minimum || raw>f.Maximum) ? 0:Normalize(raw,f.Minimum,f.Maximum,f.Bits);
                    }
                    sampled=true;f.Seen=true;
                }
                d.Frame.Sampled=d.Frame.Sampled || sampled && d.Fields.TrueForAll(delegate(Field f) { return f.Seen; });
            }
        }
        public List<InputDevice> Snapshot() {
            lock(gate) { List<InputDevice> result=new List<InputDevice>();foreach(Device d in devices.Values) result.Add(d.Frame.Copy());return result; }
        }
        public string Identity(IntPtr handle) { lock(gate) { Device d;return devices.TryGetValue(handle,out d) ? d.Frame.Id:null; } }
        public void Dispose() {lock(gate) {foreach(Device d in devices.Values) Marshal.FreeHGlobal(d.Data);devices.Clear();}}
    }
}
