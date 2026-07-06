using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.Core.Tests.Configuration;

/// <summary>
/// 
/// </summary>
/// <param name="output"></param>
public sealed class XUnitConsoleMsLoggerProvider(ITestOutputHelper output) : ILoggerProvider
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="categoryName"></param>
    /// <returns></returns>
    public ILogger CreateLogger(string categoryName)
    {
        return XUnitLogger.CreateLogger(output);
    }

    /// <summary>
    /// 
    /// </summary>
    public void Dispose()
    {
    }
}
