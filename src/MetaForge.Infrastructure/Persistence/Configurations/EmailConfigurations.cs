using MetaForge.Domain.Notifications;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaForge.Infrastructure.Persistence.Configurations;

public class EmailChannelConfiguration : IEntityTypeConfiguration<EmailChannel>
{
    public void Configure(EntityTypeBuilder<EmailChannel> builder)
    {
        builder.ToTable("EmailChannels");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FromAddress).HasMaxLength(320).IsRequired();
        builder.Property(x => x.FromDisplayName).HasMaxLength(200);
        builder.Property(x => x.SmtpHost).HasMaxLength(200);
        builder.Property(x => x.SmtpUsername).HasMaxLength(200);
        builder.Property(x => x.CredentialSecretName).HasMaxLength(100);
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class EmailRetryPolicyConfiguration : IEntityTypeConfiguration<EmailRetryPolicy>
{
    public void Configure(EntityTypeBuilder<EmailRetryPolicy> builder)
    {
        builder.ToTable("EmailRetryPolicies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.BackoffStrategy).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("EmailTemplates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Subject).HasMaxLength(500).IsRequired();
        builder.Property(x => x.BodyHtml).IsRequired();
        builder.Property(x => x.DefaultToExpression).HasMaxLength(500);
        builder.Property(x => x.DefaultCc).HasMaxLength(1000);
        builder.Property(x => x.DefaultBcc).HasMaxLength(1000);
        builder.Property(x => x.Culture).HasMaxLength(10).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasOne(x => x.EmailChannel).WithMany(x => x.Templates).HasForeignKey(x => x.EmailChannelId);
        builder.HasOne(x => x.RetryPolicy).WithMany().HasForeignKey(x => x.RetryPolicyId);
    }
}

public class EmailTemplateBindingConfiguration : IEntityTypeConfiguration<EmailTemplateBinding>
{
    public void Configure(EntityTypeBuilder<EmailTemplateBinding> builder)
    {
        builder.ToTable("EmailTemplateBindings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TriggerEvent).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ActionCode).HasMaxLength(100);
        builder.Property(x => x.RecipientField).HasMaxLength(200);
        builder.Property(x => x.ConditionExpression).HasMaxLength(500);
        builder.HasOne(x => x.EmailTemplate).WithMany(x => x.Bindings).HasForeignKey(x => x.EmailTemplateId);
        builder.HasOne(x => x.Form).WithMany().HasForeignKey(x => x.FormId);
    }
}

public class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        builder.ToTable("EmailMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ToAddress).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Cc).HasMaxLength(1000);
        builder.Property(x => x.Bcc).HasMaxLength(1000);
        builder.Property(x => x.Subject).HasMaxLength(500).IsRequired();
        builder.Property(x => x.BodyHtml).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.SourceEntity).HasMaxLength(200);
        builder.Property(x => x.SourceRecordId).HasMaxLength(50);
        builder.HasIndex(x => new { x.Status, x.NextAttemptUtc });
        builder.HasIndex(x => x.CreatedUtc);
        builder.HasOne(x => x.EmailTemplate).WithMany().HasForeignKey(x => x.EmailTemplateId);
        builder.HasOne(x => x.EmailChannel).WithMany().HasForeignKey(x => x.EmailChannelId);
        builder.HasOne(x => x.RetryPolicy).WithMany().HasForeignKey(x => x.RetryPolicyId);
    }
}
