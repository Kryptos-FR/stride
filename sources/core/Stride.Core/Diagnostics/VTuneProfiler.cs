// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

#if STRIDE_PLATFORM_DESKTOP
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Stride.Core.Annotations;

namespace Stride.Core.Diagnostics
{
    /// <summary>
    /// This static class gives access to the Pause/Resume API of VTune Amplifier. It is available on Windows Desktop platform only.
    /// </summary>
    public static partial class VTuneProfiler
    {
        private const string VTune2015DllName = "ittnotify_collector.dll";
        private static readonly Dictionary<string, StringHandle> StringHandles = new Dictionary<string, StringHandle>();

        /// <summary>
        /// Resumes the profiler.
        /// </summary>
        public static void Resume()
        {
            try
            {
                __itt_resume();
            }
            catch (DllNotFoundException)
            {
            }
        }

        /// <summary>
        /// Suspends the profiler.
        /// </summary>
        public static void Pause()
        {
            try
            {
                __itt_pause();
            }
            catch (DllNotFoundException)
            {
            }
        }

        public static readonly bool IsAvailable = NativeLibrary.Load(VTune2015DllName) != IntPtr.Zero;

        public static Event CreateEvent([NotNull] string eventName)
        {
            if (eventName == null) throw new ArgumentNullException(nameof(eventName));
            return IsAvailable ? __itt_event_createW(eventName, eventName.Length) : new Event();
        }

        public static Domain CreateDomain([NotNull] string domaiName)
        {
            if (domaiName == null) throw new ArgumentNullException(nameof(domaiName));
            return IsAvailable ? __itt_domain_createW(domaiName) : new Domain();
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Event
        {
            private readonly int id;

            public void Start()
            {
                if (id == 0)
                    return;
                __itt_event_start(this);
            }

            public void End()
            {
                if (id == 0)
                    return;
                __itt_event_end(this);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Domain
        {
            internal readonly IntPtr Pointer;

            public void BeginFrame()
            {
                if (Pointer == IntPtr.Zero)
                    return;
                __itt_frame_begin_v3(this, IntPtr.Zero);
            }

            public void BeginTask(string taskName)
            {
                if (Pointer == IntPtr.Zero)
                    return;
                __itt_task_begin(this, new IttId(), new IttId(), GetStringHandle(taskName));
            }

            public void EndTask()
            {
                if (Pointer == IntPtr.Zero)
                    return;
                __itt_task_end(this);
            }

            public void EndFrame()
            {
                if (Pointer == IntPtr.Zero)
                    return;
                __itt_frame_end_v3(this, IntPtr.Zero);
            }
        }

        private static StringHandle GetStringHandle([NotNull] string text)
        {
            StringHandle result;
            lock (StringHandles)
            {
                if (!StringHandles.TryGetValue(text, out result))
                {
                    result = __itt_string_handle_createW(text);
                    StringHandles.Add(text, result);
                }
            }
            return result;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StringHandle
        {
            private readonly IntPtr ptr;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct IttId
        {
            private readonly long d1;
            private readonly long d2;
            private readonly long d3;
        }

#pragma warning disable SA1300 // Element must begin with upper-case letter
        [LibraryImport(VTune2015DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial void __itt_resume();

        [LibraryImport(VTune2015DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial void __itt_pause();

        [LibraryImport(VTune2015DllName)] // not working
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial void __itt_frame_begin_v3(Domain domain, IntPtr id);

        [LibraryImport(VTune2015DllName)] // not working
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial void __itt_frame_end_v3(Domain domain, IntPtr id);

        [LibraryImport(VTune2015DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial Domain __itt_domain_createW([MarshalAs(UnmanagedType.LPWStr)] string domainName);

        [LibraryImport(VTune2015DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial Event __itt_event_createW([MarshalAs(UnmanagedType.LPWStr)] string eventName, int eventNameLength);

        [LibraryImport(VTune2015DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial int __itt_event_start(Event eventHandler);

        [LibraryImport(VTune2015DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial int __itt_event_end(Event eventHandler);

        [LibraryImport(VTune2015DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial StringHandle __itt_string_handle_createW([MarshalAs(UnmanagedType.LPWStr)] string text);

        [LibraryImport(VTune2015DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial void __itt_task_begin(Domain domain, IttId taskid, IttId parentid, StringHandle name);

        [LibraryImport(VTune2015DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial void __itt_task_end(Domain domain);
#pragma warning restore SA1300 // Element must begin with upper-case letter
    }
}

#endif
