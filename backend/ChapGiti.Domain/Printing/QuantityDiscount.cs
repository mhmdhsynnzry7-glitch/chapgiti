namespace ChapGiti.Domain.Printing;

public sealed record QuantityDiscount(bool Active, int Threshold, int Percentage)
{
    public static QuantityDiscount Standard { get; } = new(true, 50, 10);
    public int PercentFor(long quantity) => Active && quantity > Threshold ? Percentage : 0;
    public long Savings(long eligible, long quantity) => checked(eligible * PercentFor(quantity)) / 100;
}
