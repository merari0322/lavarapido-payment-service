namespace PaymentService.Domain.Common;

/// <summary>
/// Raíz de un aggregate (patrón DDD): la única entidad del grupo que el resto del sistema puede
/// modificar directamente. Protege las invariantes de todo el grupo y acumula los eventos de
/// dominio que ocurren en él, para que la capa de aplicación los publique después de guardar.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>Eventos ocurridos desde la última vez que se publicaron.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot() { }

    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>Registra un hecho del negocio; solo el propio aggregate puede hacerlo.</summary>
    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Vacía la lista una vez publicados los eventos (evita publicarlos dos veces).</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
