using Microsoft.EntityFrameworkCore;

namespace Flowmetry.Infrastructure;

public class FlowmetryDbContext(DbContextOptions<FlowmetryDbContext> options) : DbContext(options)
{
}
