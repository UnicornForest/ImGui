﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ImGui
{
    static class Utility
    {
        /// <summary>
        /// Get rect of the context box
        /// </summary>
        /// <param name="rect">rect of the entire box</param>
        /// <param name="style">style</param>
        /// <returns>rect of the context box</returns>
        public static Rect GetContentRect(Rect rect, Style style)
        {
            //Widths of border
            var bt = style.BorderTop;
            var br = style.BorderRight;
            var bb = style.BorderBottom;
            var bl = style.BorderLeft;

            //Widths of padding
            var pt = style.PaddingTop;
            var pr = style.PaddingRight;
            var pb = style.PaddingBottom;
            var pl = style.PaddingLeft;

            //4 corner of the border-box
            var btl = new Point(rect.Left, rect.Top);
            var bbr = new Point(rect.Right, rect.Bottom);

            //4 corner of the padding-box
            var ptl = new Point(btl.X + bl, btl.Y + bt);
            var pbr = new Point(bbr.X - br, bbr.Y - bb);

            //4 corner of the content-box
            var ctl = new Point(ptl.X + pl, ptl.Y + pt);
            var cbr = new Point(pbr.X - pr, pbr.Y - pb);
            var contentBoxRect = new Rect(ctl, cbr);
            return contentBoxRect;
        }
        
        public static byte[] PngHeaderEightBytes =
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        };

        public static string SvgFileFirstLineTextPrefix = "<?xml";
        
        //https://blez.wordpress.com/2012/09/17/determine-os-with-netmono/
        // CurrentOS Class by blez
        // Detects the current OS (Windows, Linux, MacOS)
        //
        public static class CurrentOS
        {
            static CurrentOS()
            {
                IsWindows = System.IO.Path.DirectorySeparatorChar == '\\';
                if (IsWindows)
                {
                    Name = Environment.OSVersion.VersionString;

                    Name = Name.Replace("Microsoft ", "");
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace(" )", ")");
                    Name = Name.Trim();

                    Name = Name.Replace("NT 6.2", "8 %bit 6.2");
                    Name = Name.Replace("NT 6.1", "7 %bit 6.1");
                    Name = Name.Replace("NT 6.0", "Vista %bit 6.0");
                    Name = Name.Replace("NT 5.", "XP %bit 5.");
                    Name = Name.Replace("%bit", (Is64bitWindows ? "64bit" : "32bit"));

                    if (Is64bitWindows)
                        Is64bit = true;
                    else
                        Is32bit = true;
                }
                else
                {
                    var UnixName = ReadProcessOutput("uname");
                    if (UnixName.Contains("Darwin"))
                    {
                        IsUnix = true;
                        IsMac = true;

                        Name = "MacOS X " + ReadProcessOutput("sw_vers", "-productVersion");
                        Name = Name.Trim();

                        var machine = ReadProcessOutput("uname", "-m");
                        if (machine.Contains("x86_64"))
                            Is64bit = true;
                        else
                            Is32bit = true;

                        Name += " " + (Is32bit ? "32bit" : "64bit");
                    }
                    else if (UnixName.Contains("Linux"))
                    {
                        IsUnix = true;
                        IsLinux = true;

                        Name = ReadProcessOutput("lsb_release", "-d");
                        Name = Name.Substring(Name.IndexOf(":") + 1);
                        Name = Name.Trim();

                        var machine = ReadProcessOutput("uname", "-m");
                        if (machine.Contains("x86_64"))
                            Is64bit = true;
                        else
                            Is32bit = true;

                        Name += " " + (Is32bit ? "32bit" : "64bit");
                    }
                    else if (UnixName != "")
                    {
                        IsUnix = true;
                    }
                    else
                    {
                        IsUnknown = true;
                    }
                }
            }

            public static bool IsWindows { get; private set; }
            public static bool IsUnix { get; private set; }
            public static bool IsMac { get; private set; }
            public static bool IsLinux { get; private set; }
            public static bool IsUnknown { get; private set; }
            public static bool Is32bit { get; private set; }
            public static bool Is64bit { get; private set; }

            public static bool Is64BitProcess
            {
                get { return (IntPtr.Size == 8); }
            }

            public static bool Is32BitProcess
            {
                get { return (IntPtr.Size == 4); }
            }

            public static string Name { get; private set; }

            private static bool Is64bitWindows
            {
                get
                {
                    if ((Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1) ||
                       Environment.OSVersion.Version.Major >= 6)
                    {
                        using (var p = Process.GetCurrentProcess())
                        {
                            bool retVal;
                            if (!IsWow64Process(p.Handle, out retVal)) return false;
                            return retVal;
                        }
                    }
                    return false;
                }
            }

            [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool IsWow64Process([In] IntPtr hProcess, [Out] out bool wow64Process);

            private static string ReadProcessOutput(string name, string args = null)
            {
                try
                {
                    var p = new Process
                    {
                        StartInfo =
                        {
                            UseShellExecute = false,
                            RedirectStandardOutput = true
                        }
                    };
                    if (!string.IsNullOrEmpty(args)) p.StartInfo.Arguments = " " + args;
                    p.StartInfo.FileName = name;
                    p.Start();
                    // Do not wait for the child process to exit before
                    // reading to the end of its redirected stream.
                    // p.WaitForExit();
                    // Read the output stream first and then wait.
                    var output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    output = output.Trim();
                    return output;
                }
                catch
                {
                    return "";
                }
            }
        }

        public static Rect GetScreenRect(Rect rect, BaseForm form)
        {
            var sfmlWindow =  ((SFML.Window.Window) (form.InternalForm));
            var posInWindow = new SFML.System.Vector2i((int)rect.X, (int)rect.Y);
            var posInScreen = posInWindow;
            if (Utility.CurrentOS.IsWindows)
            {
                var windowHandle = sfmlWindow.SystemHandle;
                Native.Win32.ClientToScreen(windowHandle, ref posInScreen);
            }
            else if (Utility.CurrentOS.IsLinux)
            {
                posInScreen = sfmlWindow.Position + posInWindow;
            }
            else
            {
                throw new NotImplementedException();
            }
            rect.X = posInScreen.X;
            rect.Y = posInScreen.Y;
            return rect;
        }

        public static Point ScreenToClient(Point point, BaseForm form)
        {
            var sfmlWindow = ((SFML.Window.Window)(form.InternalForm));
            var posInScreen = new SFML.System.Vector2i((int)point.X, (int)point.Y);
            var posInClient = posInScreen;
            if (Utility.CurrentOS.IsWindows)
            {
                var windowHandle = sfmlWindow.SystemHandle;
                Native.Win32.ScreenToClient(windowHandle, ref posInClient);
            }
            else if (Utility.CurrentOS.IsLinux)
            {
                posInClient = posInScreen - sfmlWindow.Position;
            }
            else
            {
                throw new NotImplementedException();
            }

            point.X = posInClient.X;
            point.Y = posInClient.Y;
            return point;
        }

        public static Point ClientToScreen(Point point, BaseForm form)
        {
            var sfmlWindow = ((SFML.Window.Window)(form.InternalForm));
            var posInClient = new SFML.System.Vector2i((int)point.X, (int)point.Y);
            var posInScreen = posInClient;
            if (Utility.CurrentOS.IsWindows)
            {
                var windowHandle = sfmlWindow.SystemHandle;
                Native.Win32.ClientToScreen(windowHandle, ref posInScreen);
            }
            else if (Utility.CurrentOS.IsLinux)
            {
                posInScreen = sfmlWindow.Position + posInClient;
            }
            else
            {
                throw new NotImplementedException();
            }

            point.X = posInScreen.X;
            point.Y = posInScreen.Y;
            return point;
        }

    }
}
