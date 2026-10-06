using FinanceDesk.Models;

namespace FinanceDesk.Models
{
    public static class MoneyExt
    {
        public static string Money(this decimal d) => "₱" + d.ToString("N2");
    }
}

namespace FinanceDesk.Services
{
    public static class BillingHelper
    {
        public static decimal DiscountFor(decimal amount, Discount d)
        {
            if (d == null || !d.IsActive) return 0;
            var v = d.Type == "Percentage" ? Math.Round(amount * d.Value / 100, 2) : d.Value;
            return Math.Min(v, amount);
        }

        public static string NewNo(string prefix) =>
            $"{prefix}-{DateTime.Now:yyyyMM}-{Guid.NewGuid().ToString("N")[..12].ToUpper()}";
    }
}
