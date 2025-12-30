using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace LSUtils.Tests.Core;

[TestFixture]
public class LSExtensionHelpersTests
{
    [Test]
    public void Swap_ShouldExchangeElements()
    {
        var list = new List<int> { 1, 2, 3 };

        list.Swap(0, 2);

        Assert.That(list, Is.EqualTo(new[] { 3, 2, 1 }));
    }

    [Test]
    public void Shuffle_ShouldPreserveElements()
    {
        var list = Enumerable.Range(0, 10).ToList();

        list.Shuffle();

        Assert.That(list.OrderBy(x => x), Is.EqualTo(Enumerable.Range(0, 10)));
    }
}
