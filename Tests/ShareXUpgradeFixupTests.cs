using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace TrOCR.Tests
{
    [TestFixture]
    public class ShareXUpgradeFixupTests
    {
        [Test]
        public void Forms_DoNotReferenceShareXTypesDirectly()
        {
            var root = FindRepositoryRoot();
            var formsDirectory = Path.Combine(root, "Forms");
            var forbiddenTokens = new[]
            {
                "ShareX.ScreenCaptureLib",
                "RegionCaptureOptions",
                "RegionCaptureForm",
                "RegionCaptureMode",
                "RegionCaptureTasks"
            };

            var violations = Directory
                .EnumerateFiles(formsDirectory, "*.cs", SearchOption.AllDirectories)
                .SelectMany(file => File.ReadLines(file)
                    .Select((line, index) => new { file, line, lineNumber = index + 1 }))
                .Where(item => forbiddenTokens.Any(token => item.line.Contains(token)))
                .Select(item => item.file + ":" + item.lineNumber + ": " + item.line.Trim())
                .ToArray();

            Assert.That(
                violations,
                Is.Empty,
                "Forms code must depend on TrOCR.Services.ScreenCapture instead of ShareX types:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, violations));
        }

        [Test]
        public void ShareXProjectReferences_UseExternalPath()
        {
            var project = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "TrOCR.csproj"));

            Assert.That(project, Does.Contain(@"external\ShareX\ShareX.HelpersLib\ShareX.HelpersLib.csproj"));
            Assert.That(project, Does.Contain(@"external\ShareX\ShareX.ScreenCaptureLib\ShareX.ScreenCaptureLib.csproj"));
            Assert.That(project, Does.Not.Contain(@"references\ShareX\"));
        }

        [Test]
        public void ReleaseOutput_DoesNotContainShareXSatelliteResourceAssemblies()
        {
            var root = FindRepositoryRoot();
            var releaseDirectory = Path.Combine(root, "bin", "x64", "Release");
            if (!Directory.Exists(releaseDirectory))
            {
                Assert.Inconclusive("Run x64 Release build before this test.");
                return;
            }

            var shareXResourceAssemblies = Directory
                .EnumerateFiles(releaseDirectory, "ShareX.*.resources.dll", SearchOption.AllDirectories)
                .ToArray();

            Assert.That(shareXResourceAssemblies, Is.Empty,
                "ShareX satellite resource assemblies should not be shipped.");
        }

        [Test]
        public void ShareXFork_TianruoRegionShortcutsUseHoverAwareClose()
        {
            var root = FindRepositoryRoot();
            var regionCaptureFormPath = Path.Combine(root, "external", "ShareX", "ShareX.ScreenCaptureLib", "Forms", "RegionCaptureForm.cs");
            if (!File.Exists(regionCaptureFormPath))
            {
                Assert.Inconclusive("ShareX submodule not initialized at external/ShareX.");
                return;
            }

            var source = File.ReadAllText(regionCaptureFormPath);

            Assert.That(source, Does.Contain("private void CloseTianruoRegionWindow()"));
            Assert.That(source, Does.Contain("ShapeManager.CurrentHoverShape.AddShapePath(regionFillPath)"));
        }

        private static string FindRepositoryRoot()
        {
            var directory = TestContext.CurrentContext.TestDirectory;
            while (!string.IsNullOrEmpty(directory))
            {
                if (File.Exists(Path.Combine(directory, "TrOCR.sln")))
                    return directory;
                directory = Directory.GetParent(directory)?.FullName;
            }
            throw new DirectoryNotFoundException("无法从测试输出目录定位仓库根目录: " + TestContext.CurrentContext.TestDirectory);
        }
    }
}
