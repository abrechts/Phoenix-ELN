using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ElnCoreModel;

public partial class ElnDataContext : DbContext
{
    public ElnDataContext(DbContextOptions<ElnDataContext> options)
        : base(options)
    {
    }

    public virtual DbSet<sync_Tombstone> sync_Tombstone { get; set; }

    public virtual DbSet<tblAuxiliaries> tblAuxiliaries { get; set; }

    public virtual DbSet<tblComments> tblComments { get; set; }

    public virtual DbSet<tblDatabaseInfo> tblDatabaseInfo { get; set; }

    public virtual DbSet<tblEmbeddedFiles> tblEmbeddedFiles { get; set; }

    public virtual DbSet<tblExperiments> tblExperiments { get; set; }

    public virtual DbSet<tblMaterials> tblMaterials { get; set; }

    public virtual DbSet<tblProducts> tblProducts { get; set; }

    public virtual DbSet<tblProjects> tblProjects { get; set; }

    public virtual DbSet<tblProtocolItems> tblProtocolItems { get; set; }

    public virtual DbSet<tblReagents> tblReagents { get; set; }

    public virtual DbSet<tblRefReactants> tblRefReactants { get; set; }

    public virtual DbSet<tblSeparators> tblSeparators { get; set; }

    public virtual DbSet<tblSolvents> tblSolvents { get; set; }

    public virtual DbSet<tblUsers> tblUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<sync_Tombstone>(entity =>
        {
            entity.HasKey(e => e.GUID);

            entity.Property(e => e.GUID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.DatabaseInfoID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.PrimaryKeyVal).HasColumnType("VARCHAR(50)");
            entity.Property(e => e.SyncState)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");
            entity.Property(e => e.TableName).HasColumnType("VARCHAR(50)");

            entity.HasOne(d => d.DatabaseInfo).WithMany(p => p.sync_Tombstone).HasForeignKey(d => d.DatabaseInfoID);
        });

        modelBuilder.Entity<tblAuxiliaries>(entity =>
        {
            entity.HasKey(e => e.GUID);

            entity.HasIndex(e => e.ProtocolItemID, "unq_tblAuxiliaries").IsUnique();

            entity.Property(e => e.GUID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.IsDisplayAsVolume).HasColumnType("TINYINT");
            entity.Property(e => e.Name).HasColumnType("VARCHAR(50)");
            entity.Property(e => e.ProtocolItemID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.Source).HasColumnType("VARCHAR(40)");
            entity.Property(e => e.SyncState)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");

            entity.HasOne(d => d.ProtocolItem).WithOne(p => p.tblAuxiliaries).HasForeignKey<tblAuxiliaries>(d => d.ProtocolItemID);
        });

        modelBuilder.Entity<tblComments>(entity =>
        {
            entity.HasKey(e => e.GUID);

            entity.HasIndex(e => e.ProtocolItemID, "unq_tblComments").IsUnique();

            entity.Property(e => e.GUID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.CommentFlowDoc).HasColumnType("MEDIUMTEXT");
            entity.Property(e => e.ProtocolItemID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.SyncState)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");

            entity.HasOne(d => d.ProtocolItem).WithOne(p => p.tblComments).HasForeignKey<tblComments>(d => d.ProtocolItemID);
        });

        modelBuilder.Entity<tblDatabaseInfo>(entity =>
        {
            entity.HasKey(e => e.GUID);

            entity.Property(e => e.GUID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.CurrAppVersion).HasColumnType("VARCHAR(16)");
            entity.Property(e => e.LastSyncID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.LastSyncTime).HasColumnType("VARCHAR(30)");
            entity.Property(e => e.SyncState)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");
        });

        modelBuilder.Entity<tblEmbeddedFiles>(entity =>
        {
            entity.HasKey(e => e.GUID);

            entity.HasIndex(e => e.ProtocolItemID, "idx_tblEmbeddedFiles").IsUnique();

            entity.Property(e => e.GUID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.FileName).HasColumnType("VARCHAR(50)");
            entity.Property(e => e.IsPortraitMode)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");
            entity.Property(e => e.IsRotated)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");
            entity.Property(e => e.ProtocolItemID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.SHA256Hash).HasColumnType("VARCHAR(64)");
            entity.Property(e => e.SyncState)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");

            entity.HasOne(d => d.ProtocolItem).WithOne(p => p.tblEmbeddedFiles).HasForeignKey<tblEmbeddedFiles>(d => d.ProtocolItemID);
        });

        modelBuilder.Entity<tblExperiments>(entity =>
        {
            entity.HasKey(e => e.ExperimentID);

            entity.Property(e => e.ExperimentID).HasColumnType("VARCHAR(25)");
            entity.Property(e => e.CreationDate).HasColumnType("VARCHAR(20)");
            entity.Property(e => e.DisplayIndex).HasColumnType("SMALLINT");
            entity.Property(e => e.FinalizeDate).HasColumnType("VARCHAR(20)");
            entity.Property(e => e.IsCurrent).HasColumnType("TINYINT");
            entity.Property(e => e.IsDesignView).HasColumnType("TINYINT");
            entity.Property(e => e.IsNodeExpanded)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");
            entity.Property(e => e.MDLRxnFileString).HasColumnType("LONGTEXT");
            entity.Property(e => e.ProductInChIKey).HasColumnType("VARCHAR(27)");
            entity.Property(e => e.ProjectID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.ReactantInChIKey).HasColumnType("VARCHAR(27)");
            entity.Property(e => e.RefYieldFactor).HasDefaultValue(1.0);
            entity.Property(e => e.RxnSketch).HasColumnType("LONGTEXT");
            entity.Property(e => e.SyncState)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");
            entity.Property(e => e.UserID).HasColumnType("VARCHAR(25)");
            entity.Property(e => e.UserTag)
                .HasDefaultValue((short)0)
                .HasColumnType("SMALLINT");
            entity.Property(e => e.WorkflowState)
                .HasDefaultValue((short)0)
                .HasColumnType("SMALLINT");

            entity.HasOne(d => d.Project).WithMany(p => p.tblExperiments).HasForeignKey(d => d.ProjectID);

            entity.HasOne(d => d.User).WithMany(p => p.tblExperiments).HasForeignKey(d => d.UserID);
        });

        modelBuilder.Entity<tblMaterials>(entity =>
        {
            entity.HasKey(e => e.GUID);

            entity.Property(e => e.GUID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.DatabaseID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.Density).HasDefaultValueSql("NULL");
            entity.Property(e => e.InChIKey).HasColumnType("VARCHAR(30)");
            entity.Property(e => e.IsValidated)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");
            entity.Property(e => e.MatName).HasColumnType("VARCHAR(150)");
            entity.Property(e => e.MatSource).HasColumnType("VARCHAR(100)");
            entity.Property(e => e.MatType).HasColumnType("SMALLINT");
            entity.Property(e => e.Molarity).HasDefaultValueSql("NULL");
            entity.Property(e => e.Molweight).HasDefaultValueSql("NULL");
            entity.Property(e => e.Purity).HasDefaultValueSql("NULL");
            entity.Property(e => e.SyncState)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");

            entity.HasOne(d => d.Database).WithMany(p => p.tblMaterials).HasForeignKey(d => d.DatabaseID);
        });

        modelBuilder.Entity<tblProducts>(entity =>
        {
            entity.HasKey(e => e.GUID);

            entity.HasIndex(e => e.ProtocolItemID, "unq_tblProducts_ProtocolItemID").IsUnique();

            entity.Property(e => e.GUID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.BatchID).HasColumnType("VARCHAR(40)");
            entity.Property(e => e.ElementalFormula).HasColumnType("VARCHAR(50)");
            entity.Property(e => e.InChIKey).HasColumnType("VARCHAR(27)");
            entity.Property(e => e.Name).HasColumnType("VARCHAR(50)");
            entity.Property(e => e.ProductIndex).HasColumnType("SMALLINT");
            entity.Property(e => e.ProtocolItemID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.SyncState)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");

            entity.HasOne(d => d.ProtocolItem).WithOne(p => p.tblProducts).HasForeignKey<tblProducts>(d => d.ProtocolItemID);
        });

        modelBuilder.Entity<tblProjects>(entity =>
        {
            entity.HasKey(e => e.GUID);

            entity.HasIndex(e => e.GUID, "pk_tblProjects_2").IsUnique();

            entity.Property(e => e.GUID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.IsNodeExpanded).HasColumnType("TINYINT");
            entity.Property(e => e.SyncState)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");
            entity.Property(e => e.Title).HasColumnType("VARCHAR(100)");
            entity.Property(e => e.UserID).HasColumnType("VARCHAR(25)");

            entity.HasOne(d => d.User).WithMany(p => p.tblProjects).HasForeignKey(d => d.UserID);
        });

        modelBuilder.Entity<tblProtocolItems>(entity =>
        {
            entity.HasKey(e => e.GUID);

            entity.HasIndex(e => e.ExperimentID, "idx_tblProtocolItems");

            entity.Property(e => e.GUID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.ElementType).HasColumnType("SMALLINT");
            entity.Property(e => e.ExperimentID).HasColumnType("VARCHAR(25)");
            entity.Property(e => e.IsSelected).HasColumnType("TINYINT");
            entity.Property(e => e.SequenceNr).HasColumnType("SMALLINT");
            entity.Property(e => e.SyncState)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");

            entity.HasOne(d => d.Experiment).WithMany(p => p.tblProtocolItems).HasForeignKey(d => d.ExperimentID);
        });

        modelBuilder.Entity<tblReagents>(entity =>
        {
            entity.HasKey(e => e.GUID);

            entity.HasIndex(e => e.ProtocolItemID, "unq_tblReagents_ProtocolItemID").IsUnique();

            entity.Property(e => e.GUID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.IsDisplayAsVolume).HasColumnType("TINYINT");
            entity.Property(e => e.IsMolarity).HasColumnType("TINYINT");
            entity.Property(e => e.Name).HasColumnType("VARCHAR(50)");
            entity.Property(e => e.ProtocolItemID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.Source).HasColumnType("VARCHAR(50)");
            entity.Property(e => e.SyncState)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");

            entity.HasOne(d => d.ProtocolItem).WithOne(p => p.tblReagents).HasForeignKey<tblReagents>(d => d.ProtocolItemID);
        });

        modelBuilder.Entity<tblRefReactants>(entity =>
        {
            entity.HasKey(e => e.GUID);

            entity.HasIndex(e => e.ProtocolItemID, "unq_tblRefReactants").IsUnique();

            entity.Property(e => e.GUID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.InChIKey).HasColumnType("VARCHAR(27)");
            entity.Property(e => e.IsDisplayAsVolume).HasColumnType("TINYINT");
            entity.Property(e => e.Name).HasColumnType("VARCHAR(50)");
            entity.Property(e => e.ProtocolItemID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.Source).HasColumnType("VARCHAR(40)");
            entity.Property(e => e.SyncState)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");

            entity.HasOne(d => d.ProtocolItem).WithOne(p => p.tblRefReactants).HasForeignKey<tblRefReactants>(d => d.ProtocolItemID);
        });

        modelBuilder.Entity<tblSeparators>(entity =>
        {
            entity.HasKey(e => e.GUID);

            entity.HasIndex(e => e.ProtocolItemID, "IX_tblSeparators_ProtocolItemID").IsUnique();

            entity.Property(e => e.GUID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.DisplayType).HasColumnType("SMALLINT");
            entity.Property(e => e.ElementType).HasColumnType("SMALLINT");
            entity.Property(e => e.ProtocolItemID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.SyncState)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");
            entity.Property(e => e.Title).HasColumnType("VARCHAR(80)");

            entity.HasOne(d => d.ProtocolItem).WithOne(p => p.tblSeparators).HasForeignKey<tblSeparators>(d => d.ProtocolItemID);
        });

        modelBuilder.Entity<tblSolvents>(entity =>
        {
            entity.HasKey(e => e.GUID);

            entity.HasIndex(e => e.ProtocolItemID, "idx_tblSolvents").IsUnique();

            entity.Property(e => e.GUID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.IsDisplayAsWeight).HasColumnType("TINYINT");
            entity.Property(e => e.IsMolEquivalents).HasColumnType("TINYINT");
            entity.Property(e => e.Name).HasColumnType("VARCHAR(50)");
            entity.Property(e => e.ProtocolItemID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.Source).HasColumnType("VARCHAR(40)");
            entity.Property(e => e.SpecifiedUnitType).HasDefaultValue(1);
            entity.Property(e => e.SyncState)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");

            entity.HasOne(d => d.ProtocolItem).WithOne(p => p.tblSolvents).HasForeignKey<tblSolvents>(d => d.ProtocolItemID);
        });

        modelBuilder.Entity<tblUsers>(entity =>
        {
            entity.HasKey(e => e.UserID);

            entity.Property(e => e.UserID).HasColumnType("VARCHAR(25)");
            entity.Property(e => e.City).HasColumnType("VARCHAR(50)");
            entity.Property(e => e.CompanyName).HasColumnType("VARCHAR(100)");
            entity.Property(e => e.DatabaseID).HasColumnType("VARCHAR(36)");
            entity.Property(e => e.DepartmentName).HasColumnType("VARCHAR(100)");
            entity.Property(e => e.FirstName).HasColumnType("VARCHAR(50)");
            entity.Property(e => e.IsSpellCheckEnabled).HasColumnType("TINYINT");
            entity.Property(e => e.LastName).HasColumnType("VARCHAR(50)");
            entity.Property(e => e.PWHash).HasColumnType("VARCHAR(64)");
            entity.Property(e => e.PWHint).HasColumnType("VARCHAR(80)");
            entity.Property(e => e.SyncState)
                .HasDefaultValue((byte)0)
                .HasColumnType("TINYINT");

            entity.HasOne(d => d.Database).WithMany(p => p.tblUsers).HasForeignKey(d => d.DatabaseID);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
