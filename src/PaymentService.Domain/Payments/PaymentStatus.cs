namespace PaymentService.Domain.Payments;

public enum PaymentStatus
{
    Pending = 1,
    InReview = 2,
    Approved = 3,
    Rejected = 4,
    Refunded = 5
}