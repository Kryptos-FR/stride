// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#if (STRIDE_UI_WINFORMS || STRIDE_UI_WPF)
using System;
using System.Runtime.InteropServices;

namespace Stride.Input.RawInput
{
    internal static partial class Win32
    {
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static unsafe partial bool RegisterRawInputDevices(void* pRawInputDevices, uint uiNumDevices, uint cbSize);

        [LibraryImport("user32.dll")]
        public static unsafe partial uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [LibraryImport("user32.dll")]
        public static partial void ClipCursor(IntPtr rect);
    }
}
#endif
