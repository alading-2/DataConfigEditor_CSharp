namespace DataConfigEditor.Core;

public interface ITypeMetadataProvider
{
    bool TryResolve(
        string typeSyntax,
        string currentNamespace,
        IReadOnlyList<string> usingNamespaces,
        out TypeMetadata metadata);

    bool TryResolveConstantExpression(string expression, out string value);
}
