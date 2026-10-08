namespace PaymentService.Domain.Common;

/// <summary>
/// Base de todo objeto del dominio que tiene identidad propia (Payment, PaymentReceipt, Promotion...).
/// Dos entidades son "la misma" si son del mismo tipo y tienen el mismo Id, aunque el resto de sus
/// datos cambie con el tiempo.
/// </summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>> where TId : notnull
{
    /// <summary>Identificador; lo asigna la base de datos (IDENTITY) al guardar.</summary>
    public TId Id { get; protected set; } = default!;

    // Constructor sin parámetros: lo usa EF Core para materializar las filas.
    protected Entity() { }

    protected Entity(TId id)
    {
        Id = id;
    }

    /// <summary>Igualdad por identidad: mismo tipo concreto y mismo Id ya asignado.</summary>
    public bool Equals(Entity<TId>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;

        // Una entidad nueva (todavía sin Id de la base) no es igual a ninguna otra.
        if (EqualityComparer<TId>.Default.Equals(Id, default!)) return false;

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
