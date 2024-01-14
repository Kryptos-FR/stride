// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
//
// Copyright (c) 2010-2014 SharpDX - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

#if (STRIDE_UI_WINFORMS || STRIDE_UI_WPF)
#pragma warning disable SA1310 // Field names should not contain underscore
#pragma warning disable SA1132 // Do not combine fields
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter

using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace Stride.Games
{
    internal static partial class Win32Native
    {
        /// <summary>
        /// Internal class to interact with Native Message
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct NativeMessage
        {
            public IntPtr handle;
            public uint msg;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PAINTSTRUCT
        {
            public IntPtr Hdc;
            public bool Erase;
            public RECT PaintRectangle;
            public bool Restore;
            public bool IncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] Reserved;
        }

        public enum WindowLongType : int
        {
            WndProc = (-4),
            HInstance = (-6),
            HwndParent = (-8),
            Style = (-16),
            ExtendedStyle = (-20),
            UserData = (-21),
            Id = (-12),
        }

        public delegate IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public static IntPtr GetWindowLong(IntPtr hWnd, WindowLongType index)
        {
            if (IntPtr.Size == 4)
            {
                return GetWindowLong32(hWnd, index);
            }
            return GetWindowLong64(hWnd, index);
        }

        [LibraryImport("user32.dll", EntryPoint = "GetFocus")]
        public static partial IntPtr GetFocus();

        [LibraryImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static partial IntPtr GetWindowLong32(IntPtr hwnd, WindowLongType index);

        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static partial IntPtr GetWindowLong64(IntPtr hwnd, WindowLongType index);

        public static IntPtr SetWindowLong(IntPtr hwnd, WindowLongType index, IntPtr wndProcPtr)
        {
            if (IntPtr.Size == 4)
            {
                return SetWindowLong32(hwnd, index, wndProcPtr);
            }
            return SetWindowLongPtr64(hwnd, index, wndProcPtr);
        }

        [LibraryImport("user32.dll", EntryPoint = "SetParent")]
        public static partial IntPtr SetParent(IntPtr hWnd, IntPtr hWndParent);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static partial IntPtr SetWindowLong32(IntPtr hwnd, WindowLongType index, IntPtr wndProc);

        [LibraryImport("user32.dll")]
        public static partial short GetKeyState(int keyCode);

        public static bool ShowWindow(IntPtr hWnd, bool windowVisible)
        {
            return ShowWindow(hWnd, windowVisible ? 1 : 0);
        }

        [LibraryImport("user32.dll", EntryPoint = "ShowWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShowWindow(IntPtr hWnd, int mCmdShow);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static partial IntPtr SetWindowLongPtr64(IntPtr hwnd, WindowLongType index, IntPtr wndProc);

        [LibraryImport("user32.dll", EntryPoint = "CallWindowProc")]
        public static partial IntPtr CallWindowProc(IntPtr wndProc, IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandle", StringMarshalling = StringMarshalling.Utf16)]
        public static partial IntPtr GetModuleHandle(string lpModuleName);

        [LibraryImport("ole32.dll")]
        public static partial int CoInitialize(IntPtr pvReserved);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool PeekMessage(out NativeMessage lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [LibraryImport("user32.dll")]
        public static partial sbyte GetMessage(out NativeMessage lpMsg, IntPtr hWnd, uint wMsgFilterMin,
            uint wMsgFilterMax);

        [LibraryImport("user32.dll", EntryPoint = "PeekMessage")]
        public static partial int PeekMessage(out NativeMessage lpMsg, IntPtr hWnd, int wMsgFilterMin, int wMsgFilterMax, int wRemoveMsg);

        [LibraryImport("user32.dll", EntryPoint = "GetMessage")]
        public static partial int GetMessage(out NativeMessage lpMsg, IntPtr hWnd, int wMsgFilterMin, int wMsgFilterMax);

        [LibraryImport("user32.dll", EntryPoint = "TranslateMessage")]
        public static partial int TranslateMessage(ref NativeMessage lpMsg);

        [LibraryImport("user32.dll", EntryPoint = "DispatchMessage")]
        public static partial int DispatchMessage(ref NativeMessage lpMsg);

        [DllImport("user32.dll", EntryPoint = "BeginPaint")]
        public static extern IntPtr BeginPaint(IntPtr hWnd, ref PAINTSTRUCT paintStruct);

        [DllImport("user32.dll", EntryPoint = "EndPaint")]
        public static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT paintStruct);

        [LibraryImport("imm32.dll", EntryPoint = "ImmGetContext")]
        public static partial IntPtr ImmGetContext(IntPtr hWnd);

        [LibraryImport("imm32.dll", EntryPoint = "ImmReleaseContext")]
        public static partial IntPtr ImmReleaseContext(IntPtr hWnd, IntPtr context);

        [LibraryImport("imm32.dll", EntryPoint = "ImmGetCompositionString")]
        public static partial int ImmGetCompositionString(IntPtr himc, int dwIndex, IntPtr buf, int bufLen);

        public const int GCS_COMPSTR = 0x0008;

        public const int WM_SIZE = 0x0005;
        public const int WM_ACTIVATEAPP = 0x001C;
        public const int WM_POWERBROADCAST = 0x0218;
        public const int WM_MENUCHAR = 0x0120;
        public const int WM_SYSCOMMAND = 0x0112;
        public const int WM_KEYDOWN = 0x100;
        public const int WM_KEYUP = 0x101;
        public const int WM_CHAR = 0x102;
        public const int WM_SYSKEYDOWN = 0x104;
        public const int WM_SYSKEYUP = 0x105;
        public const int WM_DEVICECHANGE = 0x0219;
        public const int WM_INPUTLANGCHANGE = 0x0051;
        public const int WM_IME_CHAR = 0x0286;
        public const int WM_IME_COMPOSITION = 0x010F;
        public const int WM_IME_COMPOSITIONFULL = 0x0284;
        public const int WM_IME_CONTROL = 0x0283;
        public const int WM_IME_ENDCOMPOSITION = 0x010E;
        public const int WM_IME_KEYDOWN = 0x0290;
        public const int WM_IME_KEYLAST = 0x010F;
        public const int WM_IME_KEYUP = 0x0291;
        public const int WM_IME_NOTIFY = 0x0282;
        public const int WM_IME_REQUEST = 0x0288;
        public const int WM_IME_SELECT = 0x0285;
        public const int WM_IME_SETCONTEXT = 0x0281;
        public const int WM_IME_STARTCOMPOSITION = 0x010D;
        public const int WM_PAINT = 0x000F;
        public const int WM_NCPAINT = 0x0085;

        public const int PM_REMOVE = 0x0001;
    }
}

#endif
