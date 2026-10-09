namespace PaymentService.Domain.Common;

/// <summary>
/// Lo que todo aggregate root expone sin importar el tipo de su Id: los eventos de dominio
/// pendientes de publicar. Permite tratar a todos los aggregates por igual (por ejemplo, al
/// confirmar una Unit of Work o al publicar sus eventos).
/// </summary>
public interface IAggregateRoot
{
    /// <summary>Eventos ocurridos desde la última vez que se publicaron.</summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>Vacía la lista una vez publicados los eventos (evita publicarlos dos veces).</summary>
    void ClearDomainEvents();
}

/// <summary>
/// Raíz de un aggregate (patrón DDD): la única entidad del grupo que el resto del sistema puede
/// modificar directamente. Protege las invariantes de todo el grupo y acumula los eventos de
/// dominio que ocurren en él, para que la capa de aplicación los publique después de guardar.
/// Un aggregate con eventos pendientes cambió, aunque ninguno de sus datos lo haga (por ejemplo,
/// una promoción que consumió un uso): quien lo persiste lo trata como modificado.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <inheritdoc />
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot() { }

    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>Registra un hecho del negocio; solo el propio aggregate puede hacerlo.</summary>
    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <inheritdoc />
    public void ClearDomainEvents() => _domainEvents.Clear();
}
