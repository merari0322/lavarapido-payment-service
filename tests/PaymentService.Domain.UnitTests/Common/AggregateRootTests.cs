using PaymentService.Domain.Common;
using Xunit;

namespace PaymentService.Domain.UnitTests.Common;

public class AggregateRootTests
{
    // Clases de juguete, solo para probar la base.
    private sealed class SampleEvent : IDomainEvent
    {
        public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
    }

    private sealed class SampleAggregate : AggregateRoot<long>
    {
        public SampleAggregate(long id) : base(id) { }

        public void DoSomething() => AddDomainEvent(new SampleEvent());
    }

    [Fact]
    public void DoSomething_RegistersOneDomainEvent()
    {
        var aggregate = new SampleAggregate(1);

        aggregate.DoSomething();

        Assert.Single(aggregate.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var aggregate = new SampleAggregate(1);
        aggregate.DoSomething();

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void TwoEntitiesWithSameId_AreEqual()
    {
        var first = new SampleAggregate(7);
        var second = new SampleAggregate(7);

        Assert.Equal(first, second);
    }
}
