// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolsetTest.CoreIntegration
{
    using System;
    using System.IO;
    using System.Linq;
    using Example.Extension;
    using WixBuildTools.TestSupport;
    using WixToolset.Core.TestPackage;
    using WixToolset.Data;
    using WixToolset.Data.Tuples;
    using Xunit;

    public class WixlibFixture
    {
        [Fact]
        public void CanBuildSimpleBundleUsingWixlib()
        {
            var folder = TestData.Get(@"TestData\SimpleBundle");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "MultiFileBootstrapperApplication.wxs"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(intermediateFolder, @"test.wixlib")
                });

                result.AssertSuccess();

                result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "MultiFileBundle.wxs"),
                    "-loc", Path.Combine(folder, "Bundle.en-us.wxl"),
                    "-lib", Path.Combine(intermediateFolder, @"test.wixlib"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(baseFolder, @"bin\test.exe")
                });

                result.AssertSuccess();

                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\test.exe")));
                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\test.wixpdb")));
            }
        }

        [Fact]
        public void CanBuildWixlibWithBinariesFromNamedBindPaths()
        {
            var folder = TestData.Get(@"TestData\WixlibWithBinaries");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");
                var wixlibPath = Path.Combine(intermediateFolder, @"test.wixlib");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-bf",
                    "-bindpath", Path.Combine(folder, "data"),
                    // Use names that aren't excluded in default .gitignores.
                    "-bindpath", $"AlphaBits={Path.Combine(folder, "data", "alpha")}",
                    "-bindpath", $"MipsBits={Path.Combine(folder, "data", "mips")}",
                    "-bindpath", $"PowerBits={Path.Combine(folder, "data", "powerpc")}",
                    "-intermediateFolder", intermediateFolder,
                    "-o", wixlibPath,
                });

                result.AssertSuccess();

                var wixlib = Intermediate.Load(wixlibPath);
                var binaryTuples = wixlib.Sections.SelectMany(s => s.Tuples).OfType<BinaryTuple>().ToList();
                Assert.Equal(3, binaryTuples.Count);
                Assert.Single(binaryTuples.Where(t => t.Data.Path == "wix-ir/foo.dll"));
                Assert.Single(binaryTuples.Where(t => t.Data.Path == "wix-ir/foo.dll-1"));
                Assert.Single(binaryTuples.Where(t => t.Data.Path == "wix-ir/foo.dll-2"));
            }
        }

        [Fact]
        public void CanBuildSingleFileUsingWixlib()
        {
            var folder = TestData.Get(@"TestData\SingleFile");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");
                var wixlibPath = Path.Combine(intermediateFolder, @"test.wixlib");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", wixlibPath,
                });

                result.AssertSuccess();

                var wixlib = Intermediate.Load(wixlibPath);

                Assert.True(wixlib.HasLevel(IntermediateLevels.Compiled));
                Assert.True(wixlib.HasLevel(IntermediateLevels.Combined));
                Assert.False(wixlib.HasLevel(IntermediateLevels.Linked));
                Assert.False(wixlib.HasLevel(IntermediateLevels.Resolved));

                result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-lib", Path.Combine(intermediateFolder, @"test.wixlib"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(baseFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                var intermediate = Intermediate.Load(Path.Combine(baseFolder, @"bin\test.wixpdb"));

                Assert.False(intermediate.HasLevel(IntermediateLevels.Compiled));
                Assert.False(intermediate.HasLevel(IntermediateLevels.Combined));
                Assert.True(intermediate.HasLevel(IntermediateLevels.Linked));
                Assert.True(intermediate.HasLevel(IntermediateLevels.Resolved));

                var section = intermediate.Sections.Single();

                var wixFile = section.Tuples.OfType<FileTuple>().First();
                Assert.Equal(Path.Combine(folder, @"data\test.txt"), wixFile[FileTupleFields.Source].AsPath().Path);
                Assert.Equal(@"test.txt", wixFile[FileTupleFields.Source].PreviousValue.AsPath().Path);
            }
        }

        [Fact]
        public void CanBuildWithExtensionUsingWixlib()
        {
            var folder = TestData.Get(@"TestData\ExampleExtension");
            var extensionPath = Path.GetFullPath(new Uri(typeof(ExampleExtensionFactory).Assembly.CodeBase).LocalPath);

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-ext", extensionPath,
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(intermediateFolder, @"test.wixlib")
                });

                result.AssertSuccess();

                result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-lib", Path.Combine(intermediateFolder, @"test.wixlib"),
                    "-ext", extensionPath,
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(intermediateFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                var intermediate = Intermediate.Load(Path.Combine(intermediateFolder, @"bin\test.wixpdb"));
                var section = intermediate.Sections.Single();

                var fileTuple = section.Tuples.OfType<FileTuple>().Single();
                Assert.Equal(Path.Combine(folder, @"data\example.txt"), fileTuple[FileTupleFields.Source].AsPath().Path);
                Assert.Equal(@"example.txt", fileTuple[FileTupleFields.Source].PreviousValue.AsPath().Path);

                var example = section.Tuples.Where(t => t.Definition.Type == TupleDefinitionType.MustBeFromAnExtension).Single();
                Assert.Equal("Foo", example.Id?.Id);
                Assert.Equal("Bar", example[0].AsString());
            }
        }

        [Fact]
        public void CanBuildWithExtensionUsingMultipleWixlibs()
        {
            var folder = TestData.Get(@"TestData\ComplexExampleExtension");
            var extensionPath = Path.GetFullPath(new Uri(typeof(ExampleExtensionFactory).Assembly.CodeBase).LocalPath);

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "PackageComponents.wxs"),
                    "-ext", extensionPath,
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(intermediateFolder, @"components.wixlib")
                });

                result.AssertSuccess();

                result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "OtherComponents.wxs"),
                    "-ext", extensionPath,
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(intermediateFolder, @"other.wixlib")
                });

                result.AssertSuccess();

                result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Package.wxs"),
                    "-loc", Path.Combine(folder, "Package.en-us.wxl"),
                    "-lib", Path.Combine(intermediateFolder, @"components.wixlib"),
                    "-lib", Path.Combine(intermediateFolder, @"other.wixlib"),
                    "-ext", extensionPath,
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(intermediateFolder, @"bin\test.msi")
                });

                result.AssertSuccess();

                var intermediate = Intermediate.Load(Path.Combine(intermediateFolder, @"bin\test.wixpdb"));
                var section = intermediate.Sections.Single();

                var fileTuples = section.Tuples.OfType<FileTuple>().OrderBy(t => Path.GetFileName(t.Source.Path)).ToArray();
                Assert.Equal(Path.Combine(folder, @"data\example.txt"), fileTuples[0][FileTupleFields.Source].AsPath().Path);
                Assert.Equal(@"example.txt", fileTuples[0][FileTupleFields.Source].PreviousValue.AsPath().Path);
                Assert.Equal(Path.Combine(folder, @"data\other.txt"), fileTuples[1][FileTupleFields.Source].AsPath().Path);
                Assert.Equal(@"other.txt", fileTuples[1][FileTupleFields.Source].PreviousValue.AsPath().Path);

                var examples = section.Tuples.Where(t => t.Definition.Type == TupleDefinitionType.MustBeFromAnExtension).ToArray();
                Assert.Equal(new string[] { "Foo", "Other" }, examples.Select(t => t.Id?.Id).ToArray());
                Assert.Equal(new[] { "Bar", "Value" }, examples.Select(t => t[0].AsString()).ToArray());
            }
        }
    }
}
