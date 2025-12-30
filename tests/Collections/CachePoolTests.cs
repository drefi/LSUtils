using LSUtils.Collections;
using NUnit.Framework;

namespace LSUtils.Tests.Collections;

internal class PooledItem
{
    public int State { get; set; }
}

[TestFixture]
public class CachePoolTests
{
    [Test]
    public void GetInstance_ShouldCreateWhenEmpty()
    {
        var pool = new ObjectCachePool<PooledItem>(cacheSize: 2);

        var instance = pool.GetInstance();

        Assert.That(instance, Is.Not.Null);
        Assert.That(pool.InactiveCount, Is.EqualTo(0));
    }

    [Test]
    public void Release_ShouldRespectCapacity()
    {
        var pool = new ObjectCachePool<PooledItem>(cacheSize: 2);
        var first = new PooledItem();
        var second = new PooledItem();
        var third = new PooledItem();

        Assert.That(pool.Release(first), Is.True);
        Assert.That(pool.Release(second), Is.True);
        Assert.That(pool.Release(third), Is.False);
        Assert.That(pool.InactiveCount, Is.EqualTo(2));
    }

    [Test]
    public void TryGetInstance_ShouldReturnCachedInstance()
    {
        var pool = new ObjectCachePool<PooledItem>(cacheSize: 2);
        var obj = new PooledItem { State = 10 };
        pool.Release(obj);

        var got = pool.TryGetInstance(out var retrieved);

        Assert.That(got, Is.True);
        Assert.That(retrieved, Is.SameAs(obj));
        Assert.That(pool.InactiveCount, Is.EqualTo(0));
    }

    [Test]
    public void ResetOnGet_ShouldInvokeResetDelegate()
    {
        var pool = new ObjectCachePool<PooledItem>(
            cacheSize: 2,
            constructorDelegate: () => new PooledItem(),
            resetObjectDelegate: item => item.State = 0,
            resetOnGet: true);

        var obj = new PooledItem { State = 5 };
        pool.Release(obj);

        var retrieved = pool.GetInstance();

        Assert.That(retrieved, Is.SameAs(obj));
        Assert.That(retrieved.State, Is.EqualTo(0));
    }

    [Test]
    public void Release_ShouldInvokeResetWhenConfigured()
    {
        var pool = new ObjectCachePool<PooledItem>(
            cacheSize: 2,
            constructorDelegate: () => new PooledItem(),
            resetObjectDelegate: item => item.State = 0,
            resetOnGet: false);

        var obj = new PooledItem { State = 9 };
        pool.Release(obj);

        Assert.That(obj.State, Is.EqualTo(0));
        Assert.That(pool.InactiveCount, Is.EqualTo(1));
    }
}
