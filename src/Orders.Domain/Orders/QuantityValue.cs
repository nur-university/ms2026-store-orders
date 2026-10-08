using Joseco.DDD.Core.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orders.Domain.Orders;

public record  QuantityValue
{
    public int Value { get; init; }

    public QuantityValue(int value)
    {
        if (value < 0)
        {
            throw new DomainException(OrderErrors.QuantityInvalid());
        }

        Value = value;
    }

    public static implicit operator int(QuantityValue quantity)
    {
        return quantity == null ? 0 : quantity.Value;
    }

    public static implicit operator QuantityValue(int a)
    {
        return new QuantityValue(a);
    }

    public static QuantityValue operator +(QuantityValue a, int b) => new (a.Value + b);

    public static QuantityValue operator -(QuantityValue a, int b) => new(a.Value - b);
}
