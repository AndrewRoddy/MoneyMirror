using Microsoft.EntityFrameworkCore;

namespace PittMoney.Data;

public class PittMoneyDbContext : DbContext
{
    public PittMoneyDbContext(
        DbContextOptions<PittMoneyDbContext> options)
        : base(options)
    {
    }
}