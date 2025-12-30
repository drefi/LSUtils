using System;
using System.Collections.Generic;
using LSUtils.Collections;
using NUnit.Framework;

namespace LSUtils.Tests.Collections;

[TestFixture]
public class BinaryHeapTests
{
    [Test]
    public void AddAndPop_ShouldReturnInDescendingOrder()
    {
        var heap = new BinaryHeap<int>();
        heap.Add(3);
        heap.Add(1);
        heap.Add(5);
        heap.Add(2);

        Assert.That(heap.Count, Is.EqualTo(4));
        Assert.That(heap.Pop(), Is.EqualTo(5));
        Assert.That(heap.Pop(), Is.EqualTo(3));
        Assert.That(heap.Pop(), Is.EqualTo(2));
        Assert.That(heap.Pop(), Is.EqualTo(1));
    }

    [Test]
    public void Peek_ShouldNotRemoveElement()
    {
        var heap = new BinaryHeap<int>();
        heap.Add(2);
        heap.Add(1);

        Assert.That(heap.Peek(), Is.EqualTo(2));
        Assert.That(heap.Count, Is.EqualTo(2));
        Assert.That(heap.Pop(), Is.EqualTo(2));
        Assert.That(heap.Pop(), Is.EqualTo(1));
    }

    [Test]
    public void Pop_OnEmpty_ShouldThrow()
    {
        var heap = new BinaryHeap<int>();
        Assert.Throws<InvalidOperationException>(() => heap.Pop());
    }

    [Test]
    public void RemoveAt_ShouldReheapify()
    {
        var heap = new BinaryHeap<int>();
        heap.Add(5);
        heap.Add(1);
        heap.Add(3);

        var index = heap.IndexOf(1);
        heap.RemoveAt(index);

        Assert.That(heap.Count, Is.EqualTo(2));
        Assert.That(heap.Pop(), Is.EqualTo(5));
        Assert.That(heap.Pop(), Is.EqualTo(3));
    }

    [Test]
    public void IndexerUpdate_ShouldReheapifyDownward()
    {
        var heap = new BinaryHeap<int>();
        heap.Add(5);
        heap.Add(1);
        heap.Add(3);

        // Make the root smaller; setter triggers Update which should bubble the new max to the top
        heap[0] = 2;

        Assert.That(heap.Pop(), Is.EqualTo(3));
        Assert.That(heap.Pop(), Is.EqualTo(2));
        Assert.That(heap.Pop(), Is.EqualTo(1));
    }

    [Test]
    public void ConstructorFromEnumerable_ShouldBuildHeap()
    {
        var data = new List<int> { 4, 7, 1, 5 };

        var heap = new BinaryHeap<int>(data);

        Assert.That(heap.Count, Is.EqualTo(4));
        Assert.That(heap.Pop(), Is.EqualTo(7));
        Assert.That(heap.Pop(), Is.EqualTo(5));
        Assert.That(heap.Pop(), Is.EqualTo(4));
        Assert.That(heap.Pop(), Is.EqualTo(1));
    }
}
