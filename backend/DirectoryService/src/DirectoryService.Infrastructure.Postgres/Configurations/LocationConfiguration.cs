using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DirectoryService.Domain;

namespace DirectoryService.Infrastructure.Postgres.Configurations;

public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(l => l.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(DbConstants.MaxStringLength);

        builder.Property(l => l.Address)
            .HasColumnName("address")
            .IsRequired()
            .HasMaxLength(DbConstants.MaxAddressLength);

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(l => l.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}
