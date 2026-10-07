// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using Xunit;

// The launcher keeps its files in the working directory, which is process-wide.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ClassicUO.Launcher.Tests
{
    /// <summary>An empty working directory for one test, where settings.json and launcher.json go.</summary>
    public abstract class WorkingDirectory : IDisposable
    {
        private readonly string _previous = Environment.CurrentDirectory;

        protected WorkingDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wispuo-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            Environment.CurrentDirectory = Path;
        }

        protected string Path { get; }

        protected string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose()
        {
            Environment.CurrentDirectory = _previous;
            Directory.Delete(Path, recursive: true);
        }
    }
}
