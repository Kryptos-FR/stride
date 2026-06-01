// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using System.IO;
using Stride.Core.Assets.Editor.Editors;
using Xunit;

namespace Stride.Core.Assets.Editor.Tests
{
    public sealed class TestFileContentSignature
    {
        [Fact]
        public void CaptureNullPathReturnsNonExistent()
        {
            var signature = FileContentSignature.Capture(null);
            Assert.False(signature.Exists);
            Assert.Equal(default, signature);
        }

        [Fact]
        public void CaptureMissingFileReturnsNonExistent()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".missing");
            Assert.False(FileContentSignature.Capture(path).Exists);
        }

        [Fact]
        public void CaptureSameUnchangedFileIsEqual()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "hello");
                var a = FileContentSignature.Capture(path);
                var b = FileContentSignature.Capture(path);
                Assert.True(a.Exists);
                Assert.Equal(a, b);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void CaptureAfterContentChangeIsNotEqual()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "hello");
                var before = FileContentSignature.Capture(path);
                File.WriteAllText(path, "hello world"); // different length
                var after = FileContentSignature.Capture(path);
                Assert.NotEqual(before, after);
            }
            finally { File.Delete(path); }
        }
    }
}
