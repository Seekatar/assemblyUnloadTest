using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using SerilogTimings;
using Microsoft.AspNetCore.Mvc;

namespace Seekatar.Tools;

public class ProblemDetailsException : Exception
{
    public ProblemDetailsException()
    {
        Details = new ProblemDetails();
    }

    public ProblemDetails Details { get; }
}
public class CompilerError {
    public CompilerError(Diagnostic d, string t)
    {
        Text = t;
        Diagnostic = d;
    }
    public Diagnostic Diagnostic { get; }
    public string Text { get; }
}

public class AssemblyBuilder
{
    private readonly ILogger _logger;
    private List<PortableExecutableReference> _trustedReferences = new();

    public AssemblyBuilder(ILogger logger)
    {
        var trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var trustedAssemblyPaths = trustedAssemblies.Split(Path.PathSeparator);
        _trustedReferences = trustedAssemblyPaths.Select(path => MetadataReference.CreateFromFile(path)).ToList();
        _logger = logger;
    }

    private bool CheckForErrors(List<Diagnostic> diag, string? code = null)
    {
        if (!diag.Any(o => o.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)) return true;

        var ex = new ProblemDetailsException();
        ex.Details.Title = "Compiler errors";
        ex.Details.Status = 400;
        ex.Details.Detail = "The code you submitted contains errors.";
        var i = 1;
        _logger.LogInformation("Compiler errors");
        foreach (var e in diag.Where(o => o.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
        {
            _logger.LogInformation($"   {e.ToString()}");
            if (code != null)
            {
                var index = e.Location.SourceSpan.Start;
                var end = code.IndexOf(Environment.NewLine, index);
                var begin = code.LastIndexOf(Environment.NewLine, index);

                begin = begin < 0 ? 0 : begin + Environment.NewLine.Length;
                end = end < 0 ? code.Length : end;

                _logger.LogInformation(code[begin..end]);
                ex.Details.Extensions.Add($"Error{i++}", new CompilerError( e, code[begin..end] ));
            }
        }
        throw ex;
        // return false;
    }

    public (Stream? pe, Stream? pdb) BuildAssembly(string name, string code)
    {
        using (Operation.Time("Building assembly"))
        {

            // largely from http://www.albahari.com/nutshell/cs10ian-supplement.pdf p54
            SyntaxTree tree;
            using (Operation.Time("Parsing text"))
            {
                tree = CSharpSyntaxTree.ParseText(code);
            }

            CSharpCompilation compilation;
            using (Operation.Time("Compiling code"))
            {

                compilation = CSharpCompilation
                    .Create(name)
                    .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .AddSyntaxTrees(tree)
                    .AddReferences(_trustedReferences);

                var diag = compilation.GetDiagnostics();
                if (!CheckForErrors(diag.ToList(), code)) return (null, null);
            }

            using (Operation.Time("Emitting"))
            {
                var peStream = new MemoryStream();
                var pdbStream = new MemoryStream();

                var diag = compilation.Emit(peStream, pdbStream);
                if (!CheckForErrors(diag.Diagnostics.ToList())) return (null, null);

                peStream.Seek(0, SeekOrigin.Begin);
                pdbStream.Seek(0, SeekOrigin.Begin);
                return (peStream, pdbStream);
            }
        }
    }
}