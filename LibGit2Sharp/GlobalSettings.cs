﻿using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using LibGit2Sharp.Core;

namespace LibGit2Sharp
{
    /// <summary>
    /// Global settings for libgit2 and LibGit2Sharp.
    /// </summary>
    public static class GlobalSettings
    {
        private static readonly Lazy<Version> version = new Lazy<Version>(Version.Build);

        private static LogConfiguration logConfiguration = LogConfiguration.None;

        private static string nativeLibraryPath;
        private static bool nativeLibraryPathLocked;

        static GlobalSettings()
        {
            if (Platform.OperatingSystem == OperatingSystemType.Windows)
            {
                string managedPath = new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath;
                nativeLibraryPath = Path.Combine(Path.GetDirectoryName(managedPath), "NativeBinaries");
            }
        }

        /// <summary>
        /// Returns information related to the current LibGit2Sharp
        /// library.
        /// </summary>
        public static Version Version
        {
            get
            {
                return version.Value;
            }
        }

        /// <summary>
        /// Registers a new <see cref="SmartSubtransport"/> as a custom
        /// smart-protocol transport with libgit2.  Any Git remote with
        /// the scheme registered will delegate to the given transport
        /// for all communication with the server.  use this transport to communicate
        /// with the server This is not commonly
        /// used: some callers may want to re-use an existing connection to
        /// perform fetch / push operations to a remote.
        ///
        /// Note that this configuration is global to an entire process
        /// and does not honor application domains.
        /// </summary>
        /// <typeparam name="T">The type of SmartSubtransport to register</typeparam>
        /// <param name="scheme">The scheme (eg "http" or "gopher") to register</param>
        public static SmartSubtransportRegistration<T> RegisterSmartSubtransport<T>(string scheme)
            where T : SmartSubtransport, new()
        {
            Ensure.ArgumentNotNull(scheme, "scheme");

            var registration = new SmartSubtransportRegistration<T>(scheme);

            try
            {
                Proxy.git_transport_register(
                    registration.Scheme,
                    registration.FunctionPointer,
                    registration.RegistrationPointer);
            }
            catch (Exception)
            {
                registration.Free();
                throw;
            }

            return registration;
        }

        /// <summary>
        /// Unregisters a previously registered <see cref="SmartSubtransport"/>
        /// as a custom smart-protocol transport with libgit2.
        /// </summary>
        /// <typeparam name="T">The type of SmartSubtransport to register</typeparam>
        /// <param name="registration">The previous registration</param>
        public static void UnregisterSmartSubtransport<T>(SmartSubtransportRegistration<T> registration)
            where T : SmartSubtransport, new()
        {
            Ensure.ArgumentNotNull(registration, "registration");

            Proxy.git_transport_unregister(registration.Scheme);
            registration.Free();
        }

        /// <summary>
        /// Registers a new <see cref="LogConfiguration"/> to receive
        /// information logging information from libgit2 and LibGit2Sharp.
        ///
        /// Note that this configuration is global to an entire process
        /// and does not honor application domains.
        /// </summary>
        public static LogConfiguration LogConfiguration
        {
            set
            {
                Ensure.ArgumentNotNull(value, "value");

                logConfiguration = value;

                if (logConfiguration.Level == LogLevel.None)
                {
                    Proxy.git_trace_set(0, null);
                }
                else
                {
                    Proxy.git_trace_set(value.Level, value.GitTraceCallback);

                    Log.Write(LogLevel.Info, "Logging enabled at level {0}", value.Level);
                }
            }

            get
            {
                return logConfiguration;
            }
        }

        /// <summary>
        /// Sets a hint path for searching for native binaries: when
        /// specified, native binaries will first be searched in a
        /// subdirectory of the given path corresponding to the architecture
        /// (eg, "x86" or "amd64") before falling back to the default
        /// path ("NativeBinaries\x86" or "NativeBinaries\amd64" next
        /// to the application).
        /// <para>
        /// This must be set before any other calls to the library,
        /// and is not available on Unix platforms: see your dynamic
        /// library loader's documentation for details.
        /// </para>
        /// </summary>
        public static string NativeLibraryPath
        {
            get
            {
                if (Platform.OperatingSystem != OperatingSystemType.Windows)
                {
                    throw new LibGit2SharpException("Querying the native hint path is only supported on Windows platforms");
                }

                return nativeLibraryPath;
            }

            set
            {
                if (Platform.OperatingSystem != OperatingSystemType.Windows)
                {
                    throw new LibGit2SharpException("Setting the native hint path is only supported on Windows platforms");
                }

                if (nativeLibraryPathLocked)
                {
                    throw new LibGit2SharpException("You cannot set the native library path after it has been loaded");
                }

                nativeLibraryPath = value;
            }
        }

        internal static string GetAndLockNativeLibraryPath()
        {
            nativeLibraryPathLocked = true;
            return nativeLibraryPath;
        }

        /// <summary>
        /// Register a filter globally with a default priority of 200 allowing the custom filter
        /// to imitate a core Git filter driver. It will be run last on checkout and first on checkin.
        /// </summary>
        public static FilterRegistration RegisterFilter(Filter filter)
        {
            return RegisterFilter(filter, 200);
        }

        /// <summary>
        /// Register a filter globally with given priority for execution.
        /// A filter with the priority of 200 will be run last on checkout and first on checkin.
        /// A filter with the priority of 0 will be run first on checkout and last on checkin.
        /// </summary>
        public static FilterRegistration RegisterFilter(Filter filter, int priority)
        {
            var registration = new FilterRegistration(filter);

            Proxy.git_filter_register(filter.Name, registration, priority);

            return registration;
        }

        /// <summary>
        /// Remove the filter from the registry, and frees the native heap allocation.
        /// </summary>
        public static void DeregisterFilter(FilterRegistration registration)
        {
            Ensure.ArgumentNotNull(registration, "registration");

            Proxy.git_filter_unregister(registration.Name);
            registration.Free();
        }

        /// <summary>
        /// The separator used in path list strings (like in the PATH environment variable).
        /// A semi-colon ";" is used on Windows, and a colon ":" for all other systems.
        /// </summary>
        public static char PathListSeparator
        {
            get
            {
                return (Platform.OperatingSystem == OperatingSystemType.Windows) ? ';' : ':';
            }
        }

        /// <summary>
        /// Get the paths under which libgit2 searches for the configuration file of a given level.
        /// </summary>
        /// <param name="level">The level (global/system/XDG) of the config.</param>
        /// <returns>The paths that are searched</returns>
        public static IEnumerable<string> GetConfigSearchPaths(ConfigurationLevel level)
        {
            return Proxy.git_libgit2_opts_get_search_path(level).Split(PathListSeparator);
        }

        /// <summary>
        /// Set the path under which libgit2 searches for the configuration file of a given level.
        /// </summary>
        /// <param name="level">The level (global/system/XDG) of the config.</param>
        /// <param name="path">The new search path, or null to reset to default.</param>
        public static void SetConfigSearchPath(ConfigurationLevel level, string path)
        {
            Proxy.git_libgit2_opts_set_search_path(level, path);
        }

        /// <summary>
        /// Set the paths under which libgit2 searches for the configuration file of a given level.
        /// </summary>
        /// <param name="level">The level (global/system/XDG) of the config.</param>
        /// <param name="paths">The new search paths, or null to reset to default.</param>
        public static void SetConfigSearchPaths(ConfigurationLevel level, IEnumerable<string> paths)
        {
            var pathString = (paths == null) ? null : string.Join(PathListSeparator.ToString(), paths);
            Proxy.git_libgit2_opts_set_search_path(level, pathString);
        }
    }
}
