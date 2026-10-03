using ExamBox.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Data;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<Answer> Answers => Set<Answer>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.Property(u => u.Username).UseCollation("NOCASE");
            e.Property(u => u.Email).UseCollation("NOCASE");
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Role).HasConversion<string>();
        });
        b.Entity<Attempt>(e =>
        {
            // one attempt per student per exam
            e.HasIndex(a => new { a.ExamId, a.StudentId }).IsUnique();
            e.HasOne(a => a.Exam).WithMany(x => x.Attempts).HasForeignKey(a => a.ExamId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Student).WithMany(s => s.Attempts).HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Answer>(e =>
        {
            e.HasIndex(a => new { a.AttemptId, a.QuestionId }).IsUnique();
            e.HasOne(a => a.Question).WithMany().HasForeignKey(a => a.QuestionId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
