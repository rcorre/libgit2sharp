using LibGit2Sharp.Core;
using LibGit2Sharp.Core.Handles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace LibGit2Sharp
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct GitConfigBackend
    {
        static GitConfigBackend()
        {
            GCHandleOffset = Marshal.OffsetOf(typeof(GitConfigBackend), "GCHandle").ToInt32();
        }

        public uint Version;

#pragma warning disable 169

        /// <summary>
        /// This field is populated by libgit2 at backend addition time, and exists for its
        /// use only. From this side of the interop, it is unreferenced.
        /// </summary>
        public int Readonly;

        /// <summary>
        /// This field is populated by libgit2 at backend addition time, and exists for its
        /// use only. From this side of the interop, it is unreferenced.
        /// </summary>
        private IntPtr Cfg;

#pragma warning restore 169

        public open_callback Open;
        public get_callback Get;
        public set_callback Set;
        public set_multivar_callback SetMultivar;
        public del_callback Del;
        public del_multivar_callback DelMultivar;
        public iterator_callback Iterator;
        public snapshot_callback Snapshot;
        public lock_callback Lock;
        public unlock_callback Unlock;
        public free_callback Free;

        /* The libgit2 structure definition ends here. Subsequent fields are for libgit2sharp bookkeeping. */

        public IntPtr GCHandle;

        /* The following static fields are not part of the structure definition. */

        public static int GCHandleOffset;

        public delegate int open_callback(IntPtr backend, ConfigurationLevel level);

        public delegate int get_callback(IntPtr backend, IntPtr name, out IntPtr entry_out);

        public delegate int set_callback(IntPtr backend, IntPtr name, IntPtr value);

        public delegate int set_multivar_callback(IntPtr backend, IntPtr name, IntPtr regexp, IntPtr value);

        public delegate int del_callback(IntPtr backend, IntPtr name);

        public delegate int del_multivar_callback(IntPtr backend, IntPtr name, IntPtr regexp);

        public delegate int iterator_callback(out IntPtr iterator_out, IntPtr backend);

        public delegate int snapshot_callback(out IntPtr backend_out, IntPtr backend);

        public delegate int lock_callback(IntPtr backend);

        public delegate int unlock_callback(IntPtr backend, int success);

        public delegate void free_callback(IntPtr backend);
    }

    /// <summary>
    /// Base class for all custom managed backends for libgit2 configs.
    /// <para>
    /// TODO:
    /// If the derived backend implements <see cref="IDisposable"/>, the <see cref="IDisposable.Dispose"/>
    /// method will be honored and invoked upon the disposal of the repository.
    /// </para>
    /// </summary>
    public abstract class ConfigBackend
    {
        /// <summary>
        /// Invoked by libgit2 when this backend is no longer needed.
        /// </summary>
        internal void Free()
        {
            if (nativeBackendPointer == IntPtr.Zero)
            {
                return;
            }

            GCHandle.FromIntPtr(Marshal.ReadIntPtr(nativeBackendPointer, GitConfigBackend.GCHandleOffset)).Free();
            Marshal.FreeHGlobal(nativeBackendPointer);
            nativeBackendPointer = IntPtr.Zero;
        }

        public abstract int Open(ConfigurationLevel level);
        public abstract int Get(string name, out ConfigurationEntry<string> entry);
        public abstract int Set(string name, string value);
        public abstract int Snapshot(out ConfigBackend snapshot);

        private IntPtr nativeBackendPointer;

        internal IntPtr ConfigBackendPointer
        {
            get
            {
                if (IntPtr.Zero == nativeBackendPointer)
                {
                    var nativeBackend = new GitConfigBackend();
                    nativeBackend.Version = 1;

                    // The "free" entry point is always provided.
                    nativeBackend.Free = BackendEntryPoints.FreeCallback;
                    nativeBackend.Open = BackendEntryPoints.OpenCallback;
                    nativeBackend.Get = BackendEntryPoints.GetCallback;
                    nativeBackend.Set = BackendEntryPoints.SetCallback;
                    nativeBackend.Snapshot = BackendEntryPoints.SnapshotCallback;

                    nativeBackend.GCHandle = GCHandle.ToIntPtr(GCHandle.Alloc(this));
                    nativeBackendPointer = Marshal.AllocHGlobal(Marshal.SizeOf(nativeBackend));
                    Marshal.StructureToPtr(nativeBackend, nativeBackendPointer, false);
                }

                return nativeBackendPointer;
            }
        }

        private static class BackendEntryPoints
        {
            // Because our GitConfigBackend structure exists on the managed heap only for a short time (to be marshaled
            // to native memory with StructureToPtr), we need to bind to static delegates. If at construction time
            // we were to bind to the methods directly, that's the same as newing up a fresh delegate every time.
            // Those delegates won't be rooted in the object graph and can be collected as soon as StructureToPtr finishes.
            public static readonly GitConfigBackend.open_callback OpenCallback = Open;
        	public static readonly GitConfigBackend.get_callback GetCallback = Get;
        	public static readonly GitConfigBackend.set_callback SetCallback = Set;
        	//public static readonly GitConfigBackend.set_multivar_callback SetMultivarCallback = SetMultivar;
        	//public static readonly GitConfigBackend.del_callback DelCallback = Del;
        	//public static readonly GitConfigBackend.del_multivar_callback DelMultivarCallback = DelMultivar;
        	//public static readonly GitConfigBackend.iterator_callback IteratorCallback = Iterator;
        	public static readonly GitConfigBackend.snapshot_callback SnapshotCallback = Snapshot;
        	public static readonly GitConfigBackend.free_callback FreeCallback = Free;

            private static ConfigBackend MarshalConfigBackend(IntPtr backendPtr)
            {
                var intPtr = Marshal.ReadIntPtr(backendPtr, GitConfigBackend.GCHandleOffset);
                var configBackend = GCHandle.FromIntPtr(intPtr).Target as ConfigBackend;

                if (configBackend == null)
                {
                    Proxy.giterr_set_str(GitErrorCategory.Reference, "Cannot retrieve the managed ConfigBackend.");
                    return null;
                }

                return configBackend;
            }

            private unsafe static int Open(IntPtr backend, ConfigurationLevel level)
            {
                var configBackend = MarshalConfigBackend(backend);
                if (configBackend == null)
                {
                    return (int)GitErrorCode.Error;
                }

                try
                {
                    return configBackend.Open(level);
                }
                catch (Exception ex)
                {
                    Proxy.giterr_set_str(GitErrorCategory.Config, ex);
                    return (int)GitErrorCode.Error;
                }
            }

            private unsafe static int Get(IntPtr backend, IntPtr name, out IntPtr entry_out)
            {
                entry_out = IntPtr.Zero;

                var configBackend = MarshalConfigBackend(backend);
                if (configBackend == null)
                {
                    return (int)GitErrorCode.Error;
                }

                try
                {
                    var str = LaxUtf8Marshaler.FromNative(name);
                    ConfigurationEntry<string> entry;
                    int toReturn = configBackend.Get(str, out entry);

                    //nativeBackend.GCHandle = GCHandle.ToIntPtr(GCHandle.Alloc(this));
                    var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ConfigurationEntry<string>)));
                    Marshal.StructureToPtr(entry, ptr, true);

                    return toReturn;
                }
                catch (Exception ex)
                {
                    Proxy.giterr_set_str(GitErrorCategory.Config, ex);
                    return (int)GitErrorCode.Error;
                }
            }

            private unsafe static int Set(IntPtr backend, IntPtr name, IntPtr value)
            {
                var configBackend = MarshalConfigBackend(backend);
                if (configBackend == null)
                {
                    return (int)GitErrorCode.Error;
                }

                try
                {
                    string nameStr = LaxUtf8Marshaler.FromNative(name);
                    string valueStr = LaxUtf8Marshaler.FromNative(value);

                    return configBackend.Set(nameStr, valueStr);
                }
                catch (Exception ex)
                {
                    Proxy.giterr_set_str(GitErrorCategory.Config, ex);
                    return (int)GitErrorCode.Error;
                }
            }

            private unsafe static int Snapshot(out IntPtr backend_out, IntPtr backend)
            {
                backend_out = IntPtr.Zero;

                var configBackend = MarshalConfigBackend(backend);
                if (configBackend == null)
                {
                    return (int)GitErrorCode.Error;
                }

                try
                {
                    ConfigBackend snapshot;
                    int toReturn = configBackend.Snapshot(out snapshot);
                    backend_out = snapshot.ConfigBackendPointer;

                    return toReturn;
                }
                catch (Exception ex)
                {
                    Proxy.giterr_set_str(GitErrorCategory.Config, ex);
                    return (int)GitErrorCode.Error;
                }
            }

            private static void Free(IntPtr backend)
            {
                var configBackend = MarshalConfigBackend(backend);
                if (configBackend == null)
                {
                    return;
                }

                try
                {
                    configBackend.Free();

                    var disposable = configBackend as IDisposable;

                    if (disposable == null)
                    {
                        return;
                    }

                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    Proxy.giterr_set_str(GitErrorCategory.Config, ex);
                }
            }

        }
    }
}
