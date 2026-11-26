using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlanningReview;

public sealed class Problem(int status, string message) : Exception(message) { public int Status => status; }
public sealed class Notebook
{
    public int Schema { get; set; } = 1;
    public Guid Project { get; set; }
    public Guid Document { get; set; }
    public string Title { get; set; } = "";
    public bool Team { get; set; }
    public long Revision { get; set; }
    public int OrderVersion { get; set; }
    public List<Guid> Order { get; set; } = [];
    public List<Block> Blocks { get; set; } = [];
    public List<Discussion> Threads { get; set; } = [];
    public List<Clash> Conflicts { get; set; } = [];
    public List<Person> People { get; set; } = [];
    public List<Invitation> Invitations { get; set; } = [];
    public Dictionary<string, Receipt> Receipts { get; set; } = [];
}
public sealed class Block
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "text";
    public string Text { get; set; } = "";
    public int Version { get; set; } = 1;
    public bool Removed { get; set; }
    public string? Hash { get; set; }
    public string? Mime { get; set; }
    public string? Field { get; set; }
    public bool ReadOnly { get; set; }
}
public sealed class Person
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public bool Owner { get; set; }
    public bool Active { get; set; } = true;
}
public sealed record Invitation(Guid Id, string Label, DateTimeOffset Expires, Guid? Claimed = null, bool Cancelled = false);
public sealed record Receipt(string Digest, string Outcome);
public sealed record Note(Guid Author, string Name, DateTimeOffset At, string Text);
public sealed class Anchor
{
    public Guid Target { get; set; }
    public int Start { get; set; }
    public string Quote { get; set; } = "";
    public double[]? Rect { get; set; }
    public string? Hash { get; set; }
    public bool Detached { get; set; }
}
public sealed class Discussion
{
    public Guid Id { get; set; }
    public Anchor Anchor { get; set; } = new();
    public List<Note> Notes { get; set; } = [];
    public bool Resolved { get; set; }
    public int StatusVersion { get; set; } = 1;
}
public sealed record Clash(Guid Id, Guid Author, string AuthorName, DateTimeOffset At, Guid Target, string Action,
    int Base, string Submitted, string Current, bool Closed = false);
public sealed class Edit
{
    public Guid Id { get; set; }
    public Guid Project { get; set; }
    public string Action { get; set; } = "";
    public Guid Target { get; set; }
    public int Base { get; set; }
    public string Text { get; set; } = "";
    public Anchor? Anchor { get; set; }
    public Guid After { get; set; }
    public bool Flag { get; set; }
    public string? Image { get; set; }
}
public static class Rules
{
    public const int MemberSeats = 3;
    public const int ImageBytes = 5 * 1024 * 1024;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static void Need(bool condition, string message, int status = 422) { if (!condition) throw new Problem(status, message); }
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    public static string Short(string? text, int length = 4000) { Need(!string.IsNullOrWhiteSpace(text) && text.Length <= length, $"请填写 1–{length} 个字符。"); return text!.Trim(); }
    public static bool Pending(Invitation invitation) => !invitation.Cancelled && invitation.Claimed is null && invitation.Expires > DateTimeOffset.UtcNow;
    public static int Used(Notebook book) => book.People.Count(p => p.Active && !p.Owner) + book.Invitations.Count(Pending);
    public static void Track(Notebook book, Block block, string next)
    {
        var old = block.Text; int prefix = 0, suffix = 0;
        while (prefix < Math.Min(old.Length, next.Length) && old[prefix] == next[prefix]) prefix++;
        while (suffix < Math.Min(old.Length, next.Length) - prefix && old[^(suffix + 1)] == next[^(suffix + 1)]) suffix++;
        foreach (var anchor in book.Threads.Select(t => t.Anchor).Where(a => a.Target == block.Id && !a.Detached && a.Quote.Length > 0))
        {
            if (anchor.Start + anchor.Quote.Length <= prefix) continue;
            if (anchor.Start >= old.Length - suffix) anchor.Start += next.Length - old.Length;
            else anchor.Detached = true;
        }
        block.Text = next; block.Version++;
    }
}
