using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rag.Indexer.Models;

namespace Rag.Indexer.Services;

public class CSharpCodeParser
{
    public IEnumerable<CodeChunk> ParseFile(
    string filePath,
    string project)
    {
        var source = File.ReadAllText(filePath);

        return Parse(
            filePath,
            project,
            source);
    }

    public IEnumerable<CodeChunk> Parse(
        string filePath,
        string project,
        string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);

        var root = tree.GetCompilationUnitRoot();

        var namespaceName =
            root.DescendantNodes()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .FirstOrDefault()?
                .Name
                .ToString() ?? "";

        foreach (var cls in root.DescendantNodes()
                     .OfType<ClassDeclarationSyntax>())
        {
            yield return new CodeChunk
            {
                Project = project,
                FilePath = filePath,
                Namespace = namespaceName,
                SymbolType = "class",
                SymbolName = cls.Identifier.Text,
                Content = cls.ToFullString()
            };


            foreach (var method in cls.Members
                         .OfType<MethodDeclarationSyntax>())
            {
                yield return new CodeChunk
                {
                    Project = project,
                    FilePath = filePath,
                    Namespace = namespaceName,
                    SymbolType = "method",
                    SymbolName = method.Identifier.Text,
                    Content = method.ToFullString()
                };
            }
        }
    }
}
