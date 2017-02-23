using System.Collections.Generic;
using Xunit;
using System.Threading;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Services.Agent.Tests
{
    public sealed class DotnetsdkDownloadScriptL0
    {
        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Agent")]
        public async Task EnsureDotnetsdkDownloadScriptUpToDate()
        {
            string shDownloadUrl = "https://raw.githubusercontent.com/dotnet/cli/master/scripts/obtain/dotnet-install.sh";
            string ps1DownloadUrl = "https://raw.githubusercontent.com/dotnet/cli/master/scripts/obtain/dotnet-install.ps1";

            using(HttpClient downloadClient = new HttpClient())
            {
                var response = await downloadClient.GetAsync("https://www.bing.com");
                if(!response.IsSuccessStatusCode)
                {
                    return;
                }

                string shScript = await downloadClient.GetStringAsync(shDownloadUrl);
                string ps1Script = await downloadClient.GetStringAsync(ps1DownloadUrl);

                string existingShScript = File.ReadAllText(Path.Combine(TestUtil.GetSrcPath(),"Misc/dotnet-install.sh"));
                string existingPs1Script = File.ReadAllText(Path.Combine(TestUtil.GetSrcPath(),"Misc/dotnet-install.ps1"));

                Assert.Equal(shScript.TrimEnd('\n', '\r', '\0').Replace("\r\n", "\n").Replace("\r", "\n"), existingShScript.TrimEnd('\n', '\r', '\0').Replace("\r\n", "\n").Replace("\r", "\n"));
                Assert.Equal(ps1Script.TrimEnd('\n', '\r', '\0').Replace("\r\n", "\n").Replace("\r", "\n"), existingPs1Script.TrimEnd('\n', '\r', '\0').Replace("\r\n", "\n").Replace("\r", "\n"));              
            }
        }
    }
}