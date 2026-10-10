using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace LOS_Backend_Service.Models;

public partial class LosDbContext : DbContext
{
    public LosDbContext()
    {
    }

    public LosDbContext(DbContextOptions<LosDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<UserMaster> UserMasters { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
    if(!optionsBuilder.IsConfigured)
        {
            throw new Exception("Error while connecting database:dbc");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserMaster>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_master_pkey");

            entity.ToTable("user_master");

            entity.HasIndex(e => e.Email, "user_master_email_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Age).HasColumnName("age");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
