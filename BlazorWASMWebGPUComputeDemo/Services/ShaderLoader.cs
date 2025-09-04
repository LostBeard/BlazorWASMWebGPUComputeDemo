using System.Text.RegularExpressions;

namespace BlazorWASMWebGPUComputeDemo.Services
{
    /// <summary>
    /// Uses HttpClient to loads shader strings from `wwwroot/shaders/`
    /// </summary>
    public class ShaderLoader
    {
        Regex IncludeRegex = new Regex(@"#include<(.+?)>", RegexOptions.Multiline | RegexOptions.Compiled);
        HttpClient HttpClient;
        public ShaderLoader(HttpClient httpClient)
        {
            HttpClient = httpClient;
        }
        public async Task<string?> GetShaderString(string shader, bool useIncludes = true)
        {
            try
            {
                var ret = await HttpClient.GetStringAsync($"shaders/{shader}");
                if (useIncludes)
                {
                    // #include<shared_functions.wgsl>
                    var matches = IncludeRegex.Matches(ret);
                    foreach (Match match in matches)
                    {
                        var includeName = match.Groups[1].Value;
                        var includeShader = await GetShaderString(includeName, true);
                        if (includeShader != null)
                        {
                            ret = ret.Replace(match.Value, includeShader);
                        }
                    }
                }
                return ret;
            }
            catch
            {
                return null;
            }
        }
    }
}
