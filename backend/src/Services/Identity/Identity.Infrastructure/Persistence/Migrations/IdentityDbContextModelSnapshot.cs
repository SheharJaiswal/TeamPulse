using System;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
partial class IdentityDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 64);

        modelBuilder.Entity("Identity.Domain.Entities.Employee", b =>
        {
            b.Property<Guid>("Id").HasColumnType("char(36)");
            b.Property<string>("DisplayName").IsRequired().HasMaxLength(150).HasColumnType("varchar(150)");
            b.Property<string>("Email").IsRequired().HasMaxLength(320).HasColumnType("varchar(320)");
            b.Property<string>("NormalizedEmail").IsRequired().HasMaxLength(320).HasColumnType("varchar(320)");
            b.Property<string>("OrganizationId").IsRequired().HasMaxLength(100).HasColumnType("varchar(100)");
            b.Property<string>("PasswordHash").IsRequired().HasColumnType("longtext");
            b.Property<string>("Role").IsRequired().HasMaxLength(50).HasColumnType("varchar(50)");
            b.Property<string>("TeamId").IsRequired().HasMaxLength(100).HasColumnType("varchar(100)");
            b.Property<bool>("IsActive").HasColumnType("tinyint(1)");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime(6)");
            b.HasKey("Id");
            b.HasIndex("NormalizedEmail").IsUnique();
            b.ToTable("employees");
        });
#pragma warning restore 612, 618
    }
}
