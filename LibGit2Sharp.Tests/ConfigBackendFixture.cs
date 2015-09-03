using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibGit2Sharp.Tests.TestHelpers;
using Xunit;

namespace LibGit2Sharp.Tests
{
    public class ConfigBackendFixture : BaseFixture
    {
        [Fact]
        public void CanGeneratePredictableObjectShasWithAProvidedBackend()
        {
            string repoPath = InitNewRepository();

            using (var repo = new Repository(repoPath))
            {
                repo.Config.AddBackend(new MockConfigBackend(), ConfigurationLevel.Local, true);

                var str = repo.Config.Get<string>("test.value");
            }
        }

        [Fact]
        public void ADisposableOdbBackendGetsDisposedUponRepositoryDisposal()
        {
            string path = InitNewRepository();

            int numDisposeCalls = 0;

            using (var repo = new Repository(path))
            {
                var mockConfigBackend = new MockConfigBackend(() => { numDisposeCalls++; });

                Assert.IsAssignableFrom<IDisposable>(mockConfigBackend);

                repo.Config.AddBackend(mockConfigBackend, ConfigurationLevel.Local, false);

                Assert.Equal(0, numDisposeCalls);
            }

            Assert.Equal(1, numDisposeCalls);
        }

        #region MockConfigBackend

        private class MockConfigBackend : ConfigBackend, IDisposable
        {
            public MockConfigBackend(Action disposer = null)
            {
                this.disposer = disposer;
            }

            public void Dispose()
            {
                if (disposer == null)
                {
                    return;
                }

                disposer();

                disposer = null;
            }

            public override int Open(ConfigurationLevel level)
            {
                return 0;
            }

            public override int Get(string name, out ConfigurationEntry<string> entry)
            {
                entry = new ConfigurationEntry<string>(name, "valuehere", ConfigurationLevel.Local);
                return 0;
            }

            public override int Set(string name, string value)
            {
                throw new NotImplementedException();
            }

            public override int Snapshot(out ConfigBackend snapshot)
            {
                snapshot = this;
                return 0;
            }

            private Action disposer;
        }

        #endregion
    }
}
