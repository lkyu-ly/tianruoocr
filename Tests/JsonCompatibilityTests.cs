using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace TrOCR.Tests
{
    [TestFixture]
    public class JsonCompatibilityTests
    {
        [Test]
        public void Newtonsoft_CanParseCustomProviderList()
        {
            const string json = @"[
  {
    ""Name"": ""OpenAI Compatible"",
    ""Url"": ""https://example.invalid/v1/chat/completions"",
    ""Key"": ""local-test-key"",
    ""Model"": ""vision-model""
  }
]";

            var providers = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(json);

            Assert.That(providers, Has.Count.EqualTo(1));
            Assert.That(providers[0]["Name"], Is.EqualTo("OpenAI Compatible"));
            Assert.That(providers[0]["Model"], Is.EqualTo("vision-model"));
        }

        [Test]
        public void Newtonsoft_CanParseRapidOcrAdvancedConfig()
        {
            const string json = @"{
  ""RapidOCR"": {
    ""padding"": 50,
    ""imgResize"": 1024,
    ""boxScoreThresh"": 0.5,
    ""boxThresh"": 0.3,
    ""unClipRatio"": 1.6,
    ""doAngle"": true,
    ""mostAngle"": true,
    ""numThreads"": 4
  }
}";

            var obj = JObject.Parse(json);
            var rapid = obj["RapidOCR"];

            Assert.That(rapid["padding"].Value<int>(), Is.EqualTo(50));
            Assert.That(rapid["doAngle"].Value<bool>(), Is.True);
            Assert.That(rapid["boxScoreThresh"].Value<float>(), Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void Newtonsoft_AssemblyVersionRemains13()
        {
            var assemblyName = typeof(JsonConvert).Assembly.GetName();

            Assert.That(assemblyName.Name, Is.EqualTo("Newtonsoft.Json"));
            Assert.That(assemblyName.Version.Major, Is.EqualTo(13));
        }
    }
}
