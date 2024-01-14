// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Stride.Core;
using Stride.Core.Mathematics;

namespace Stride.VirtualReality
{
    internal static partial class OculusOvr
    {
        static OculusOvr()
        {
            NativeLibraryHelper.PreloadLibrary(NativeInvoke.Library, typeof(OculusOvr));
        }

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrStartup")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool Startup();

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrShutdown")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void Shutdown();

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrCreateSessionDx")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial IntPtr CreateSessionDx(out long adapterLuidStr);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrDestroySession")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void DestroySession(IntPtr outSessionPtr);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrCreateTexturesDx")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CreateTexturesDx(IntPtr session, IntPtr dxDevice, out int outTextureCount, float pixelPerScreenPixel, int mirrorBufferWidth = 0, int mirrorBufferHeight = 0);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrCreateQuadLayerTexturesDx")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial IntPtr CreateQuadLayerTexturesDx(IntPtr session, IntPtr dxDevice, out int outTextureCount, int width, int height, int mipLevels, int sampleCount);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrSetQuadLayerParams")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void SetQuadLayerParams(IntPtr layer, ref Vector3 position, ref Quaternion rotation, ref Vector2 size, [MarshalAs(UnmanagedType.Bool)] bool headLocked);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrGetTextureAtIndexDx")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial IntPtr GetTextureDx(IntPtr session, Guid textureGuid, int index);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrGetQuadLayerTextureAtIndexDx")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial IntPtr GetQuadLayerTextureDx(IntPtr session, IntPtr layer, Guid textureGuid, int index);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrGetMirrorTextureDx")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial IntPtr GetMirrorTexture(IntPtr session, Guid textureGuid);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrGetCurrentTargetIndex")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int GetCurrentTargetIndex(IntPtr session);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrGetCurrentQuadLayerTargetIndex")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial int GetCurrentQuadLayerTargetIndex(IntPtr session, IntPtr layer);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrCommitFrame")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CommitFrame(IntPtr session, int numberOfExtraLayers, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] extraLayer);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct FrameProperties
        {
            public float Near;
            public float Far;
            public Matrix ProjLeft;
            public Matrix ProjRight;
            public Vector3 PosLeft;
            public Vector3 PosRight;
            public Quaternion RotLeft;
            public Quaternion RotRight;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct PosesProperties
        {
            public Vector3 PosHead;
            public Quaternion RotHead;
            public Vector3 AngularVelocityHead;
            public Vector3 AngularAccelerationHead;
            public Vector3 LinearVelocityHead;
            public Vector3 LinearAccelerationHead;

            public Vector3 PosLeftHand;
            public Quaternion RotLeftHand;
            public Vector3 AngularVelocityLeftHand;
            public Vector3 AngularAccelerationLeftHand;
            public Vector3 LinearVelocityLeftHand;
            public Vector3 LinearAccelerationLeftHand;
            public int StateLeftHand;

            public Vector3 PosRightHand;
            public Quaternion RotRightHand;
            public Vector3 AngularVelocityRightHand;
            public Vector3 AngularAccelerationRightHand;
            public Vector3 LinearVelocityRightHand;
            public Vector3 LinearAccelerationRightHand;
            public int StateRightHand;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct InputProperties
        {
            public uint Buttons;
            public uint Touches;
            public float IndexTriggerLeft;
            public float IndexTriggerRight;
            public float HandTriggerLeft;
            public float HandTriggerRight;
            public Vector2 ThumbstickLeft;
            public Vector2 ThumbstickRight;
            public bool Valid;
        }

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrUpdate")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void Update(IntPtr session);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrGetFrameProperties")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void GetFrameProperties(IntPtr session, ref FrameProperties properties);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrGetPosesProperties")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void GetPosesProperties(IntPtr session, out PosesProperties properties);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrGetInputProperties")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void GetInputProperties(IntPtr session, out InputProperties properties);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrGetError")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial int GetError(IntPtr errorString);

        public static unsafe string GetError()
        {
            var buffer = stackalloc char[256];
            var errorCStr = new IntPtr(buffer);
            var error = GetError(errorCStr);
            var errorStr = Marshal.PtrToStringAnsi(errorCStr);
            return $"OculusOVR-Error({error}): {errorStr}";
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct SessionStatusInternal
        {
            public int IsVisible;
            public int HmdPresent;
            public int HmdMounted;
            public int DisplayLost;
            public int ShouldQuit;
            public int ShouldRecenter;
        }

        public struct SessionStatus
        {
            /// <summary>
            /// True if the process has VR focus and thus is visible in the HMD.
            /// </summary>
            public bool IsVisible;
            /// <summary>
            /// True if an HMD is present.
            /// </summary>
            public bool HmdPresent;
            /// <summary>
            /// True if the HMD is on the user's head.
            /// </summary>
            public bool HmdMounted;
            /// <summary>
            /// True if the session is in a display-lost state. See ovr_SubmitFrame.
            /// </summary>
            public bool DisplayLost;
            /// <summary>
            /// True if the application should initiate shutdown.    
            /// </summary>
            public bool ShouldQuit;
            /// <summary>
            /// True if UX has requested re-centering. 
            /// </summary>
            public bool ShouldRecenter;
        }

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrGetStatus")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial void GetStatus(IntPtr session, ref SessionStatusInternal status);

        public static SessionStatus GetStatus(IntPtr session)
        {
            var statusInternal = new SessionStatusInternal { DisplayLost = 0, IsVisible = 0, ShouldQuit = 0, HmdMounted = 0, HmdPresent = 0, ShouldRecenter = 0 };
            GetStatus(session, ref statusInternal);
            return new SessionStatus
            {
                DisplayLost = statusInternal.DisplayLost == 1,
                HmdMounted = statusInternal.HmdMounted == 1,
                HmdPresent = statusInternal.HmdPresent == 1,
                IsVisible = statusInternal.IsVisible == 1,
                ShouldQuit = statusInternal.ShouldQuit == 1,
                ShouldRecenter = statusInternal.ShouldRecenter == 1,
            };
        }

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnOvrRecenter")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void Recenter(IntPtr session);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnOvrGetAudioDeviceID", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern void GetAudioDeviceID(StringBuilder deviceName);

        public static string GetAudioDeviceFullName()
        {
            var deviceName = new StringBuilder(128);
            GetAudioDeviceID(deviceName);
            return $"\\\\?\\SWD#MMDEVAPI#{deviceName}#{{e6327cad-dcec-4949-ae8a-991e976a79d2}}"; //this should not change the guid is related to audio device type
        }
    }
}
