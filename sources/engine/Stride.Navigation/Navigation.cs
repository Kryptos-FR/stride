// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Security;
using Stride.Core.Mathematics;

namespace Stride.Navigation
{
    internal partial class Navigation
    {
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public unsafe struct TileHeader
        {
            public int Magic;
            public int Version;
            public int X;
            public int Y;
            public int Layer;
            public uint UserId;
            public int PolyCount;
            public int VertCount;
            public int MaxLinkCount;
            public int DetailMeshCount;
            public int DetailVertCount;
            public int DetailTriCount;
            public int BvNodeCount;
            public int OffMeshConCount;
            public int OffMeshBase;
            public float WalkableHeight;
            public float WalkableRadius;
            public float WalkableClimb;
            public fixed float Bmin[3];
            public fixed float Bmax[3];
            public float BvQuantFactor;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public unsafe struct Poly
        {
            public uint FirstLink;
            public fixed ushort Vertices[6];
            public fixed ushort Neighbours[6];
            public ushort Flags;
            public byte VertexCount;
            public byte AreaAndType;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct BuildSettings
        {
            public BoundingBox BoundingBox;
            public float CellHeight;
            public float CellSize;
            public int TileSize;
            public Point TilePosition;
            public int RegionMinArea;
            public int RegionMergeArea;
            public float EdgeMaxLen;
            public float EdgeMaxError;
            public float DetailSampleDist;
            public float DetailSampleMaxError;
            public float AgentHeight;
            public float AgentRadius;
            public float AgentMaxClimb;
            public float AgentMaxSlope;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct GeneratedData
        {
            public bool Success;
            public IntPtr NavmeshVertices;
            public int NumNavmeshVertices;
            public IntPtr NavmeshData;
            public int NavmeshDataLength;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct PathFindQuery
        {
            public Vector3 Source;
            public Vector3 Target;
            public Vector3 FindNearestPolyExtent;
            public int MaxPathPoints;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct PathFindResult
        {
            public bool PathFound;

            /// <summary>
            /// Should point to a preallocated array of <see cref="Vector3"/>'s matching the amount in <see cref="PathFindQuery.MaxPathPoints"/>
            /// </summary>
            public IntPtr PathPoints;

            public int NumPathPoints;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct RaycastQuery
        {
            public Vector3 Source;
            public Vector3 Target;
            public Vector3 FindNearestPolyExtent;
            public int MaxPathPoints;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct RaycastResult
        {
            public bool Hit;
            public Vector3 Position;
            public Vector3 Normal;
        }

        #region Handles
        #warning TODO: do this better; use SafeHandle?
        public readonly unsafe struct NavBuilderHandle : IEquatable<NavBuilderHandle>
        {
            #pragma warning disable CS0649 // Field is never assigned to - it is a handle obtained through P/Invoke
            private readonly void* handle;
            #pragma warning restore CS0649 // Field is never assigned to - it is a handle obtained through P/Invoke

            public override bool Equals(object obj) => obj is NavBuilderHandle other && Equals(other);
            public bool Equals(NavBuilderHandle other) => handle == other.handle;
            public override int GetHashCode() => HashCode.Combine((nint)handle);
            public static bool operator ==(NavBuilderHandle x, NavBuilderHandle y) => x.handle == y.handle;
            public static bool operator !=(NavBuilderHandle x, NavBuilderHandle y) => x.handle != y.handle;
        }
        public readonly unsafe struct NavMeshHandle {
            #pragma warning disable CS0649 // Field is never assigned to - it is a handle obtained through P/Invoke
            private readonly void* handle;
            #pragma warning restore CS0649 // Field is never assigned to - it is a handle obtained through P/Invoke
            public override bool Equals(object obj) => obj is NavMeshHandle other && Equals(other);
            public bool Equals(NavMeshHandle other) => handle == other.handle;
            public override int GetHashCode() => HashCode.Combine((nint)handle);
            public static bool operator ==(NavMeshHandle x, NavMeshHandle y) => x.handle == y.handle;
            public static bool operator !=(NavMeshHandle x, NavMeshHandle y) => x.handle != y.handle;
        }
        #endregion
        static Navigation()
        {
            NativeInvoke.PreLoad();
        }

        // Navmesh generation API
        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnNavigationCreateBuilder")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial NavBuilderHandle CreateBuilder();

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnNavigationDestroyBuilder")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void DestroyBuilder(NavBuilderHandle builder);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnNavigationBuildNavmesh")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static unsafe partial GeneratedData* Build(NavBuilderHandle builder,
            Vector3* vertices, int numVertices,
            int* indices, int numIndices);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnNavigationSetSettings")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static unsafe partial void SetSettings(NavBuilderHandle builder, BuildSettings* settings);

        /// <summary>
        /// Creates a new navigation mesh object.
        /// You must add tiles to it with AddTile before you can perform navigation queries using Query
        /// </summary>
        /// <returns></returns>
        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnNavigationCreateNavmesh")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial NavMeshHandle CreateNavmesh(float cellTileSize);

        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnNavigationDestroyNavmesh")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void DestroyNavmesh(NavMeshHandle query);

        /// <summary>
        /// Adds a new tile to the navigation mesh object
        /// </summary>
        /// <param name="navmesh"></param>
        /// <param name="data">Navigation mesh binary data in the detour format to load</param>
        /// <param name="dataLength">Length of the binary mesh data</param>
        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnNavigationAddTile")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static unsafe partial bool AddTile(NavMeshHandle navmesh, byte* data, int dataLength);

        /// <summary>
        /// Removes a tile from the navigation mesh object
        /// </summary>
        /// <param name="navmesh"></param>
        /// <param name="tileCoordinate">Coordinate of the tile to remove</param>
        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnNavigationRemoveTile")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool RemoveTile(NavMeshHandle navmesh, Point tileCoordinate);

        /// <summary>
        /// Perform a pathfinding query on the navigation mesh
        /// </summary>
        /// <param name="query"></param>
        /// <param name="pathFindQuery">The query to perform</param>
        /// <param name="resultStructure">A structure of type PathFindResult, should have the PathPoints field initialized to point to an array of Vector3's with the appropriate size</param>
        /// <returns>A PathFindQueryResult</returns>
        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnNavigationPathFindQuery")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static unsafe partial void DoPathFindQuery(NavMeshHandle query, PathFindQuery pathFindQuery, ref PathFindResult resultStructure);

        /// <summary>
        /// Perform a raycast on the navigation mesh
        /// </summary>
        /// <param name="query"></param>
        /// <param name="pathFindQuery">The query to perform</param>
        /// <param name="resultStructure">A structure of type PathFindResult</param>
        /// <returns>A RaycastQueryResult</returns>
        [SuppressUnmanagedCodeSecurity]
        [LibraryImport(NativeInvoke.Library, EntryPoint = "xnNavigationRaycastQuery")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static partial void DoRaycastQuery(NavMeshHandle query, RaycastQuery pathFindQuery, out RaycastResult resultStructure);

        public static int DtAlign4(int size)
        {
            return (size + 3) & ~3;
        }
    }
}
