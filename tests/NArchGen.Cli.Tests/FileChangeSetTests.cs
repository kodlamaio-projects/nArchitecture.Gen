using System.Text;
using Core.CodeGen.Code.CSharp;
using Core.CodeGen.File;

namespace NArchGen.Cli.Tests;

public class FileChangeSetTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("nArchGen-files-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string path(string name) => Path.Combine(_directory, name);

    [Fact]
    public async Task Recorded_writes_are_read_back_but_not_written_to_disk()
    {
        await File.WriteAllTextAsync(
            path("Registration.cs"),
            "class Registration\n{\n    void Add()\n    {\n    }\n}\n",
            TestContext.Current.CancellationToken
        );

        using (FileChangeSet changeSet = FileChangeSet.BeginRecording())
        {
            await CodeFile.WriteAllTextAsync(path("New.cs"), "class New { }");
            await CSharpCodeInjector.AddCodeLinesToMethodAsync(path("Registration.cs"), "Add", ["first();"]);
            // A later step sees the earlier one, as in a real run.
            await CSharpCodeInjector.AddCodeLinesToMethodAsync(path("Registration.cs"), "Add", ["second();"]);

            Assert.True(CodeFile.Exists(path("New.cs")));
            Assert.Contains("first();", await CodeFile.ReadAllTextAsync(path("Registration.cs")));

            Assert.Collection(
                changeSet.Changes,
                change =>
                {
                    Assert.Equal(path("New.cs"), change.Path);
                    Assert.True(change.IsCreated);
                },
                change =>
                {
                    Assert.Equal(path("Registration.cs"), change.Path);
                    Assert.False(change.IsCreated);
                    Assert.Contains("second();", change.NewContent);
                }
            );
        }

        Assert.False(File.Exists(path("New.cs")));
        Assert.DoesNotContain("first();", await File.ReadAllTextAsync(path("Registration.cs"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Writes_that_leave_a_file_unchanged_are_not_reported()
    {
        await File.WriteAllTextAsync(
            path("Context.cs"),
            "using Domain.Entities;\nclass Context { }\n",
            TestContext.Current.CancellationToken
        );

        using FileChangeSet changeSet = FileChangeSet.BeginRecording();
        await CSharpCodeInjector.AddUsingToFile(path("Context.cs"), ["using Domain.Entities;"]);

        Assert.Empty(changeSet.Changes);
    }

    [Fact]
    public async Task Injecting_keeps_the_line_endings_of_an_existing_file()
    {
        await File.WriteAllTextAsync(
            path("Lf.cs"),
            "class Lf\n{\n    void Add()\n    {\n    }\n}\n",
            TestContext.Current.CancellationToken
        );
        await File.WriteAllTextAsync(
            path("CrLf.cs"),
            "class CrLf\r\n{\r\n    void Add()\r\n    {\r\n    }\r\n}\r\n",
            TestContext.Current.CancellationToken
        );

        await CSharpCodeInjector.AddCodeLinesToMethodAsync(path("Lf.cs"), "Add", ["call();"]);
        await CSharpCodeInjector.AddCodeLinesToMethodAsync(path("CrLf.cs"), "Add", ["call();"]);

        Assert.DoesNotContain("\r", await File.ReadAllTextAsync(path("Lf.cs"), TestContext.Current.CancellationToken));
        string crLf = await File.ReadAllTextAsync(path("CrLf.cs"), TestContext.Current.CancellationToken);
        Assert.Equal(crLf.Split('\n').Length - 1, crLf.Split("\r\n").Length - 1);
    }

    [Fact]
    public async Task Writing_keeps_the_byte_order_mark_of_an_existing_file()
    {
        await File.WriteAllTextAsync(
            path("WithBom.cs"),
            "class WithBom { }\n",
            new UTF8Encoding(true),
            TestContext.Current.CancellationToken
        );
        await File.WriteAllTextAsync(
            path("WithoutBom.cs"),
            "class WithoutBom { }\n",
            new UTF8Encoding(false),
            TestContext.Current.CancellationToken
        );

        await CodeFile.WriteAllLinesAsync(path("WithBom.cs"), ["class WithBom { int x; }"]);
        await CodeFile.WriteAllLinesAsync(path("WithoutBom.cs"), ["class WithoutBom { int x; }"]);

        byte[] bom = Encoding.UTF8.GetPreamble();
        Assert.Equal(bom, (await File.ReadAllBytesAsync(path("WithBom.cs"), TestContext.Current.CancellationToken))[..bom.Length]);
        Assert.NotEqual(bom, (await File.ReadAllBytesAsync(path("WithoutBom.cs"), TestContext.Current.CancellationToken))[..bom.Length]);
    }

    [Fact]
    public void Unified_diff_shows_an_insertion_with_context()
    {
        FileChange change = new("/repo/Registration.cs", "a\nb\nc\nd\ne\nf\ng\nh\n", "a\nb\nc\nd\nnew\ne\nf\ng\nh\n");

        string diff = UnifiedDiff.Create(change, "Registration.cs");

        Assert.Equal(
            """
            --- a/Registration.cs
            +++ b/Registration.cs
            @@ -2,6 +2,7 @@
             b
             c
             d
            +new
             e
             f
             g

            """.Replace("\r\n", "\n"),
            diff
        );
    }

    [Fact]
    public void Unified_diff_of_a_created_file_compares_with_dev_null()
    {
        string diff = UnifiedDiff.Create(new FileChange("/repo/New.cs", null, "x\ny\n"), "New.cs");

        Assert.StartsWith("--- /dev/null\n+++ b/New.cs\n@@ -0,0 +1,2 @@\n+x\n+y\n", diff);
    }
}
