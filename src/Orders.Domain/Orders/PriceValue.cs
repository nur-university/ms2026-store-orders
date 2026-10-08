using Joseco.DDD.Core.Results;


namespace Orders.Domain.Orders;

public record PriceValue
{
    public decimal Value { get; init; }

    public PriceValue(decimal value)
    {
        if (value < 0)
        {
            throw new DomainException(OrderErrors.PriceInvalid());
        }
        Value = value;
    }

    public static PriceValue operator +(PriceValue price1, PriceValue price2)
    {
        return new PriceValue(price1.Value + price2.Value);
    }

    public static PriceValue operator -(PriceValue price1, PriceValue price2)
    {
        return new PriceValue(price1.Value - price2.Value);
    }

    public static PriceValue operator *(PriceValue price1, PriceValue price2)
    {
        return new PriceValue(price1.Value * price2.Value);
    }

    public static PriceValue operator /(PriceValue price1, PriceValue price2)
    {
        return new PriceValue(price1.Value / price2.Value);
    }

    public static implicit operator decimal(PriceValue price)
    {
        return price == null ? 0 : price.Value;
    }

    public static implicit operator PriceValue(decimal value)
    {
        return new PriceValue(value);
    }
}
