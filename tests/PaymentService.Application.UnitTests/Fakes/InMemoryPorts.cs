using System.Reflection;
using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Loyalty;
using PaymentService.Domain.PaymentAccounts;
using PaymentService.Domain.Payments;
using PaymentService.Domain.Promotions;

namespace PaymentService.Application.UnitTests.Fakes;

// Dobles en memoria de los puertos de salida. Imitan lo mínimo que hace el adaptador real: los
// cambios quedan "pendientes" hasta que la Unit of Work confirma, como con EF.

internal static class Ids
{
    /// <summary>Simula el Id que asigna la persistencia al guardar.</summary>
    public static T WithId<T, TId>(this T entity, TId id) where T : class
    {
        typeof(T).GetProperty("Id")!.SetValue(entity, id);
        return entity;
    }

    public static T Create<T>(params (string Property, object? Value)[] values) where T : class
    {
        var instance = (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
        foreach (var (property, value) in values)
            typeof(T).GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!.SetValue(instance, value);
        return instance;
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly List<Action> _pending = new();

    public int Commits { get; private set; }

    public void Enlist(Action apply) => _pending.Add(apply);

    public Task CommitAsync(CancellationToken ct)
    {
        _pending.ForEach(a => a());
        _pending.Clear();
        Commits++;
        return Task.CompletedTask;
    }
}

internal sealed class FakePaymentRepository : IPaymentRepository
{
    private readonly FakeUnitOfWork _uow;
    public List<Payment> Saved { get; } = new();

    public FakePaymentRepository(FakeUnitOfWork uow) => _uow = uow;

    public Task<Payment?> GetByIdAsync(long id, CancellationToken ct) => Task.FromResult(Saved.FirstOrDefault(p => p.Id == id));

    public Task<bool> HasApprovedPaymentAsync(long bookingId, CancellationToken ct) =>
        Task.FromResult(Saved.Any(p => p.BookingId == bookingId && p.Status == PaymentStatus.Approved));

    public Task<bool> HasOpenPaymentAsync(long bookingId, CancellationToken ct) =>
        Task.FromResult(Saved.Any(p => p.BookingId == bookingId && p.Status.IsOpen()));

    public Task<bool> IsTransactionReferenceUsedElsewhereAsync(string reference, long bookingId, CancellationToken ct) =>
        Task.FromResult(Saved.Any(p => p.BookingId != bookingId && p.Receipts.Any(r => r.TransactionReference == reference)));

    public Task<IReadOnlyList<Payment>> ListAsync(PaymentStatus? status, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Payment>>(Saved.Where(p => status == null || p.Status == status).Take(take).ToList());

    public Task<IReadOnlyList<Payment>> ListForBookingsAsync(IReadOnlyCollection<long> bookingIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Payment>>(Saved.Where(p => bookingIds.Contains(p.BookingId)).ToList());

    public void Add(Payment payment) => _uow.Enlist(() => Saved.Add(payment.WithId<Payment, long>(Saved.Count + 1)));
}

internal sealed class FakePaymentAccountRepository : IPaymentAccountRepository
{
    public List<PaymentAccount> Accounts { get; } = new();
    public List<PaymentMethodType> Methods { get; } = new();

    public Task<IReadOnlyList<PaymentAccount>> ListAsync(bool onlyActive, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PaymentAccount>>(Accounts.Where(a => !onlyActive || a.IsActive).ToList());

    public Task<PaymentAccount?> GetByIdAsync(short id, CancellationToken ct) => Task.FromResult(Accounts.FirstOrDefault(a => a.Id == id));

    public Task<IReadOnlyList<PaymentMethodType>> ListMethodTypesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PaymentMethodType>>(Methods);

    public Task<PaymentMethodType?> GetMethodTypeAsync(short id, CancellationToken ct) =>
        Task.FromResult(Methods.FirstOrDefault(m => m.Id == id));

    public Task<PaymentMethodType?> GetMethodTypeByCodeAsync(string code, CancellationToken ct) =>
        Task.FromResult(Methods.FirstOrDefault(m => m.Code == code.Trim().ToUpperInvariant()));

    public void Add(PaymentAccount account) => Accounts.Add(account);
}

internal sealed class FakeLedger : ILoyaltyLedgerRepository
{
    private readonly FakeUnitOfWork _uow;
    public List<LoyaltyTransaction> Saved { get; } = new();

    public FakeLedger(FakeUnitOfWork uow) => _uow = uow;

    public Task<LoyaltyBalance> CurrentBalanceAsync(long customerId, CancellationToken ct) =>
        Task.FromResult(Saved.Where(t => t.CustomerId == customerId).MaxBy(t => t.Sequence)?.ResultingBalance
                        ?? LoyaltyBalance.Empty);

    public Task<IReadOnlyDictionary<long, int>> NetPointsByCustomerForBookingAsync(long bookingId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<long, int>>(Saved.Where(t => t.BookingId == bookingId)
            .GroupBy(t => t.CustomerId)
            .Where(g => g.Sum(t => t.Points) > 0)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Points)));

    public void Add(LoyaltyTransaction transaction) => _uow.Enlist(() => Saved.Add(transaction));

    public int BalanceOf(long customerId) => CurrentBalanceAsync(customerId, default).Result.Points;
}

internal sealed class FakePromotionRepository : IPromotionRepository
{
    public List<Promotion> Promotions { get; } = new();

    public Task<IReadOnlyList<Promotion>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Promotion>>(Promotions);
    public Task<Promotion?> GetByIdAsync(int id, CancellationToken ct) => Task.FromResult(Promotions.FirstOrDefault(p => p.Id == id));

    public Task<Promotion?> GetByCodeAsync(string code, CancellationToken ct) =>
        Task.FromResult(Promotions.FirstOrDefault(p => p.Code == code.Trim().ToUpperInvariant()));

    public Task<bool> ExistsCodeAsync(string code, int? exceptId, CancellationToken ct) =>
        Task.FromResult(Promotions.Any(p => p.Code == code.Trim().ToUpperInvariant() && p.Id != exceptId));

    public void Add(Promotion promotion) => Promotions.Add(promotion);
    public void Remove(Promotion promotion, long deletedBy) => Promotions.Remove(promotion);
}

internal sealed class FakeRedemptionRepository : IPromotionRedemptionRepository
{
    private readonly FakeUnitOfWork _uow;
    public List<PromotionRedemption> Saved { get; } = new();

    public FakeRedemptionRepository(FakeUnitOfWork uow) => _uow = uow;

    public Task<bool> ExistsAsync(long bookingId, int promotionId, CancellationToken ct) =>
        Task.FromResult(Saved.Any(r => r.BookingId == bookingId && r.PromotionId == promotionId));

    public Task<int> CountAsync(int promotionId, CancellationToken ct) => Task.FromResult(Saved.Count(r => r.PromotionId == promotionId));

    public Task<int> CountByCustomerAsync(int promotionId, long customerUserId, CancellationToken ct) =>
        Task.FromResult(Saved.Count(r => r.PromotionId == promotionId && r.RedeemedBy == customerUserId));

    public Task<decimal> AppliedDiscountTotalAsync(long bookingId, CancellationToken ct) =>
        Task.FromResult(Saved.Where(r => r.BookingId == bookingId).Sum(r => r.AppliedAmount));

    public Task<IReadOnlyDictionary<int, RedemptionStats>> StatsByPromotionAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<int, RedemptionStats>>(Saved.GroupBy(r => r.PromotionId)
            .ToDictionary(g => g.Key, g => new RedemptionStats(g.Count(), g.Sum(r => r.AppliedAmount))));

    public void Add(PromotionRedemption redemption) => _uow.Enlist(() => Saved.Add(redemption));
}

internal sealed class FakeBookingDirectory : IBookingDirectory
{
    public Dictionary<long, BookingInfo> Bookings { get; } = new();

    public Task<BookingInfo?> GetForCustomerAsync(long bookingId, CancellationToken ct) => Task.FromResult(Bookings.GetValueOrDefault(bookingId));
    public Task<IReadOnlyList<BookingInfo>> MineAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<BookingInfo>>(Bookings.Values.ToList());
    public Task<BookingInfo?> GetForAdminAsync(long bookingId, CancellationToken ct) => Task.FromResult(Bookings.GetValueOrDefault(bookingId));
}

internal sealed class RecordingPublisher : IIntegrationEventPublisher
{
    public List<IntegrationEvent> Published { get; } = new();

    public Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken ct)
    {
        Published.Add(integrationEvent);
        return Task.CompletedTask;
    }
}

internal sealed class FixedClock : TimeProvider
{
    public static readonly DateTime Now = new(2026, 10, 8, 15, 30, 0, DateTimeKind.Utc);

    public override DateTimeOffset GetUtcNow() => new(Now);
}
