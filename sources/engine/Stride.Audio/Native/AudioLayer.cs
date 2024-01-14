// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using System.Runtime.InteropServices;
using System.Security;
using Stride.Core.Mathematics;

namespace Stride.Audio
{
    /// <summary>
    /// Wrapper around OpenAL
    /// </summary>
    public partial class AudioLayer
    {
        public struct Device
        {
            public IntPtr Ptr;
        }

        public struct Listener
        {
            public IntPtr Ptr;
        }

        public struct Source
        {
            public IntPtr Ptr;
        }

        public struct Buffer
        {
            public IntPtr Ptr;
        }

        static AudioLayer()
        {
            NativeInvoke.PreLoad();
        }

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioInit")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool Init();

        public enum DeviceFlags
        {
            None,
            Hrtf,
        }

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioCreate", StringMarshalling = StringMarshalling.Utf16)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial Device Create(string deviceName, DeviceFlags flags);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioDestroy")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void Destroy(Device device);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioUpdate")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void Update(Device device);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSetMasterVolume")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void SetMasterVolume(Device device, float volume);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioListenerCreate")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial Listener ListenerCreate(Device device);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioListenerDestroy")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void ListenerDestroy(Listener listener);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioListenerEnable")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ListenerEnable(Listener listener);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioListenerDisable")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void ListenerDisable(Listener listener);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceCreate")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial Source SourceCreate(Listener listener, int sampleRate, int maxNumberOfBuffers, [MarshalAs(UnmanagedType.Bool)] bool mono, [MarshalAs(UnmanagedType.Bool)] bool spatialized, [MarshalAs(UnmanagedType.Bool)] bool streamed, [MarshalAs(UnmanagedType.Bool)] bool hrtf, float hrtfDirectionFactor, HrtfEnvironment environment);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceDestroy")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void SourceDestroy(Source source);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceGetPosition")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial double SourceGetPosition(Source source);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceSetPan")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void SourceSetPan(Source source, float pan);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioBufferCreate")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial Buffer BufferCreate(int maxBufferSizeBytes);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioBufferDestroy")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void BufferDestroy(Buffer buffer);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioBufferFill")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void BufferFill(Buffer buffer, IntPtr pcm, int bufferSize, int sampleRate, [MarshalAs(UnmanagedType.Bool)] bool mono);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceSetBuffer")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void SourceSetBuffer(Source source, Buffer buffer);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceFlushBuffers")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void SourceFlushBuffers(Source source);

        public enum BufferType
        {
            None,
            BeginOfStream,
            EndOfStream,
            EndOfLoop,
        }

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceQueueBuffer")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void SourceQueueBuffer(Source source, Buffer buffer, IntPtr pcm, int bufferSize, BufferType streamType);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceGetFreeBuffer")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial Buffer SourceGetFreeBuffer(Source source);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSourcePlay")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void SourcePlay(Source source);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSourcePause")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void SourcePause(Source source);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceStop")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void SourceStop(Source source);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceSetLooping")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void SourceSetLooping(Source source, [MarshalAs(UnmanagedType.Bool)] bool looped);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceSetRange")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void SourceSetRange(Source source, double startTime, double stopTime);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceSetGain")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void SourceSetGain(Source source, float gain);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceSetPitch")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void SourceSetPitch(Source source, float pitch);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioListenerPush3D")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void ListenerPush3D(Listener listener, ref Vector3 pos, ref Vector3 forward, ref Vector3 up, ref Vector3 vel, ref Matrix worldTransform);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSourcePush3D")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void SourcePush3D(Source source, ref Vector3 pos, ref Vector3 forward, ref Vector3 up, ref Vector3 vel, ref Matrix worldTransform);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceIsPlaying")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SourceIsPlaying(Source source);
    }
}
