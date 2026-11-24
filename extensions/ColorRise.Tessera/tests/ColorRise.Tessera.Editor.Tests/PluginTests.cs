namespace ColorRise.Tessera.Editor.Tests;

public sealed class PluginTests
{
    [Fact]
    public async Task A_session_with_the_plugin_opens_without_an_error()
    {
        await using var session = await TesseraTest.OpenAsync();
        Assert.False(session.Result.HasErrors, session.Log);
    }
}
