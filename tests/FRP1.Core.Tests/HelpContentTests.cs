using FRP1.Plugin;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class HelpContentTests
{
    [Fact]
    public void EveryPluginHelpTopicHasNonemptyUserFacingContentAndCatalogIsReadOnly()
    {
        Assert.Equal(Enum.GetValues<HelpTopicId>().Order(), HelpContent.All.Keys.Order());
        foreach (var topic in Enum.GetValues<HelpTopicId>())
        {
            var content = HelpContent.Get(topic);
            Assert.False(string.IsNullOrWhiteSpace(content.Title));
            Assert.False(string.IsNullOrWhiteSpace(content.Summary));
            Assert.False(string.IsNullOrWhiteSpace(content.Details));
        }
        var dictionary = Assert.IsAssignableFrom<IDictionary<HelpTopicId, HelpTopic>>(HelpContent.All);
        Assert.True(dictionary.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => dictionary.Clear());
    }

    [Fact]
    public void UnknownHelpTopicIsNotMappedToMisleadingGenericAdvice() =>
        Assert.Throws<KeyNotFoundException>(() => HelpContent.Get((HelpTopicId)int.MaxValue));
}
