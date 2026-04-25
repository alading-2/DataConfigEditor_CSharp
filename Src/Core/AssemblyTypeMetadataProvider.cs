using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace DataConfigEditor.Core;

public sealed class AssemblyTypeMetadataProvider : ITypeMetadataProvider
{
    private static readonly Dictionary<string, string> BuiltInAliases = new(StringComparer.Ordinal)
    {
        ["string"] = typeof(string).FullName!,
        ["bool"] = typeof(bool).FullName!,
        ["byte"] = typeof(byte).FullName!,
        ["sbyte"] = typeof(sbyte).FullName!,
        ["short"] = typeof(short).FullName!,
        ["ushort"] = typeof(ushort).FullName!,
        ["int"] = typeof(int).FullName!,
        ["uint"] = typeof(uint).FullName!,
        ["long"] = typeof(long).FullName!,
        ["ulong"] = typeof(ulong).FullName!,
        ["float"] = typeof(float).FullName!,
        ["double"] = typeof(double).FullName!,
        ["decimal"] = typeof(decimal).FullName!,
        ["char"] = typeof(char).FullName!,
    };

    private readonly Dictionary<string, TypeMetadata> _typesByFullName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<TypeMetadata>> _typesBySimpleName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _constantValuesByExpression = new(StringComparer.Ordinal);
    private readonly List<string> _indexedAssemblyPaths = [];

    public AssemblyTypeMetadataProvider(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("程序集路径不能为空。", nameof(assemblyPath));
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException("程序集不存在。", assemblyPath);

        AssemblyPath = Path.GetFullPath(assemblyPath);
        IndexAssemblies();
    }

    public string AssemblyPath { get; }

    public static AssemblyTypeMetadataProvider? TryCreateForSourceFile(string sourceFilePath)
    {
        var projectRoot = FindProjectRoot(Path.GetDirectoryName(sourceFilePath) ?? "");
        if (string.IsNullOrWhiteSpace(projectRoot))
            return null;

        var projectFile = Directory.GetFiles(projectRoot, "*.csproj", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(projectFile))
            return null;

        var assemblyName = Path.GetFileNameWithoutExtension(projectFile);
        var binDirectory = Path.Combine(projectRoot, "bin");
        if (!Directory.Exists(binDirectory))
            return null;

        var assemblyPath = Directory.GetFiles(binDirectory, $"{assemblyName}.dll", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}ref{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(assemblyPath)
            ? null
            : new AssemblyTypeMetadataProvider(assemblyPath);
    }

    public bool TryResolve(
        string typeSyntax,
        string currentNamespace,
        IReadOnlyList<string> usingNamespaces,
        out TypeMetadata metadata)
    {
        var normalized = TypeMetadata.NormalizeTypeSyntax(typeSyntax);

        if (BuiltInAliases.TryGetValue(normalized, out var builtInFullName))
        {
            metadata = CreateBuiltInMetadata(normalized, builtInFullName);
            return true;
        }

        if (_typesByFullName.TryGetValue(normalized, out metadata!))
            return true;

        if (!string.IsNullOrWhiteSpace(currentNamespace) &&
            _typesByFullName.TryGetValue($"{currentNamespace}.{normalized}", out metadata!))
        {
            return true;
        }

        foreach (var usingNamespace in usingNamespaces)
        {
            if (_typesByFullName.TryGetValue($"{usingNamespace}.{normalized}", out metadata!))
                return true;
        }

        if (_typesBySimpleName.TryGetValue(normalized, out var candidates) && candidates.Count == 1)
        {
            metadata = candidates[0];
            return true;
        }

        metadata = TypeMetadata.FromTypeSyntax(typeSyntax);
        return false;
    }

    public bool TryResolveConstantExpression(string expression, out string value)
    {
        value = "";
        var normalized = expression.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (_constantValuesByExpression.TryGetValue(normalized, out var constantValue))
        {
            value = constantValue;
            return true;
        }

        return TryResolveRuntimeStaticField(normalized, out value);
    }

    private void IndexAssemblies()
    {
        var assemblyDirectory = Path.GetDirectoryName(AssemblyPath) ?? "";
        var candidatePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AssemblyPath,
        };

        if (Directory.Exists(assemblyDirectory))
        {
            foreach (var dllPath in Directory.GetFiles(assemblyDirectory, "*.dll", SearchOption.TopDirectoryOnly))
                candidatePaths.Add(dllPath);
        }

        foreach (var path in candidatePaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            _indexedAssemblyPaths.Add(path);
            IndexAssemblyFile(path);
        }
    }

    private void IndexAssemblyFile(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        if (!peReader.HasMetadata)
            return;

        var reader = peReader.GetMetadataReader();
        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var typeDefinition = reader.GetTypeDefinition(typeHandle);
            var name = reader.GetString(typeDefinition.Name);
            if (string.IsNullOrWhiteSpace(name) || name == "<Module>")
                continue;

            var fullName = BuildFullName(reader, typeDefinition, typeHandle);
            var metadata = CreateTypeMetadata(reader, typeDefinition, fullName, name);
            IndexConstants(reader, typeDefinition, fullName);

            _typesByFullName[fullName] = metadata;
            if (!_typesBySimpleName.TryGetValue(name, out var sameNameTypes))
            {
                sameNameTypes = [];
                _typesBySimpleName[name] = sameNameTypes;
            }

            sameNameTypes.Add(metadata);
        }
    }

    private static TypeMetadata CreateTypeMetadata(
        MetadataReader reader,
        TypeDefinition typeDefinition,
        string fullName,
        string simpleName)
    {
        var baseTypeFullName = ResolveTypeFullName(reader, typeDefinition.BaseType);
        var isEnum = string.Equals(baseTypeFullName, "System.Enum", StringComparison.Ordinal);
        var isFlags = HasAttribute(reader, typeDefinition.GetCustomAttributes(), "System.FlagsAttribute");

        return new TypeMetadata
        {
            TypeName = simpleName,
            TypeFullName = fullName,
            IsEnum = isEnum,
            IsFlags = isFlags,
            IsNumeric = false,
            IsBool = false,
            IsString = false,
            EnumOptions = isEnum
                ? ReadEnumOptions(reader, typeDefinition).ToArray()
                : Array.Empty<EnumOptionMetadata>(),
        };
    }

    private static IEnumerable<EnumOptionMetadata> ReadEnumOptions(MetadataReader reader, TypeDefinition typeDefinition)
    {
        foreach (var fieldHandle in typeDefinition.GetFields())
        {
            var field = reader.GetFieldDefinition(fieldHandle);
            if ((field.Attributes & System.Reflection.FieldAttributes.SpecialName) != 0)
                continue;

            var name = reader.GetString(field.Name);
            if (string.IsNullOrWhiteSpace(name) || name == "value__")
                continue;

            yield return new EnumOptionMetadata
            {
                Name = name,
                Value = ReadConstantValue(reader, field.GetDefaultValue()),
            };
        }
    }

    private static string ReadConstantValue(MetadataReader reader, ConstantHandle handle)
    {
        if (handle.IsNil)
            return "";

        var constant = reader.GetConstant(handle);
        return constant.Value.IsNil
            ? ""
            : constant.TypeCode switch
            {
                ConstantTypeCode.Boolean => reader.GetBlobReader(constant.Value).ReadBoolean().ToString(),
                ConstantTypeCode.Byte => reader.GetBlobReader(constant.Value).ReadByte().ToString(),
                ConstantTypeCode.SByte => reader.GetBlobReader(constant.Value).ReadSByte().ToString(),
                ConstantTypeCode.Int16 => reader.GetBlobReader(constant.Value).ReadInt16().ToString(),
                ConstantTypeCode.UInt16 => reader.GetBlobReader(constant.Value).ReadUInt16().ToString(),
                ConstantTypeCode.Int32 => reader.GetBlobReader(constant.Value).ReadInt32().ToString(),
                ConstantTypeCode.UInt32 => reader.GetBlobReader(constant.Value).ReadUInt32().ToString(),
                ConstantTypeCode.Int64 => reader.GetBlobReader(constant.Value).ReadInt64().ToString(),
                ConstantTypeCode.UInt64 => reader.GetBlobReader(constant.Value).ReadUInt64().ToString(),
                ConstantTypeCode.String => ReadConstantString(reader.GetBlobReader(constant.Value)),
                _ => "",
            };
    }

    private static string ReadConstantString(BlobReader blobReader)
    {
        return blobReader.Length == 0
            ? ""
            : blobReader.ReadUTF16(blobReader.Length);
    }

    private static bool HasAttribute(
        MetadataReader reader,
        CustomAttributeHandleCollection handles,
        string expectedAttributeFullName)
    {
        foreach (var handle in handles)
        {
            var attribute = reader.GetCustomAttribute(handle);
            var attributeTypeName = ResolveAttributeTypeFullName(reader, attribute);
            if (string.Equals(attributeTypeName, expectedAttributeFullName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string ResolveAttributeTypeFullName(MetadataReader reader, CustomAttribute attribute)
    {
        return attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => ResolveMemberReferenceParentFullName(reader, (MemberReferenceHandle)attribute.Constructor),
            HandleKind.MethodDefinition => ResolveMethodDefinitionDeclaringTypeFullName(reader, (MethodDefinitionHandle)attribute.Constructor),
            _ => "",
        };
    }

    private static string ResolveMethodDefinitionDeclaringTypeFullName(MetadataReader reader, MethodDefinitionHandle handle)
    {
        var methodDefinition = reader.GetMethodDefinition(handle);
        return ResolveTypeFullName(reader, methodDefinition.GetDeclaringType());
    }

    private static string ResolveMemberReferenceParentFullName(MetadataReader reader, MemberReferenceHandle handle)
    {
        var memberReference = reader.GetMemberReference(handle);
        return ResolveTypeFullName(reader, memberReference.Parent);
    }

    private static string ResolveTypeFullName(MetadataReader reader, EntityHandle handle)
    {
        if (handle.IsNil)
            return "";

        return handle.Kind switch
        {
            HandleKind.TypeDefinition => BuildFullName(
                reader,
                reader.GetTypeDefinition((TypeDefinitionHandle)handle),
                (TypeDefinitionHandle)handle),
            HandleKind.TypeReference => BuildFullName(reader, reader.GetTypeReference((TypeReferenceHandle)handle)),
            HandleKind.TypeSpecification => ResolveTypeSpecificationFullName(reader, (TypeSpecificationHandle)handle),
            _ => "",
        };
    }

    private static string ResolveTypeSpecificationFullName(MetadataReader reader, TypeSpecificationHandle handle)
    {
        var specification = reader.GetTypeSpecification(handle);
        var blobReader = reader.GetBlobReader(specification.Signature);
        return ResolveTypeFromSignature(reader, ref blobReader);
    }

    private static string ResolveTypeFromSignature(MetadataReader reader, ref BlobReader blobReader)
    {
        var signatureTypeCode = blobReader.ReadSignatureTypeCode();
        return signatureTypeCode switch
        {
            SignatureTypeCode.TypeHandle => ResolveTypeFullName(reader, MetadataTokens.EntityHandle(blobReader.ReadCompressedInteger())),
            SignatureTypeCode.SZArray => ResolveTypeFromSignature(reader, ref blobReader),
            SignatureTypeCode.ByReference => ResolveTypeFromSignature(reader, ref blobReader),
            SignatureTypeCode.Pointer => ResolveTypeFromSignature(reader, ref blobReader),
            SignatureTypeCode.GenericTypeInstance =>
                ResolveGenericTypeInstance(reader, ref blobReader),
            SignatureTypeCode.Boolean => typeof(bool).FullName ?? "System.Boolean",
            SignatureTypeCode.Byte => typeof(byte).FullName ?? "System.Byte",
            SignatureTypeCode.SByte => typeof(sbyte).FullName ?? "System.SByte",
            SignatureTypeCode.Int16 => typeof(short).FullName ?? "System.Int16",
            SignatureTypeCode.UInt16 => typeof(ushort).FullName ?? "System.UInt16",
            SignatureTypeCode.Int32 => typeof(int).FullName ?? "System.Int32",
            SignatureTypeCode.UInt32 => typeof(uint).FullName ?? "System.UInt32",
            SignatureTypeCode.Int64 => typeof(long).FullName ?? "System.Int64",
            SignatureTypeCode.UInt64 => typeof(ulong).FullName ?? "System.UInt64",
            SignatureTypeCode.Single => typeof(float).FullName ?? "System.Single",
            SignatureTypeCode.Double => typeof(double).FullName ?? "System.Double",
            SignatureTypeCode.String => typeof(string).FullName ?? "System.String",
            SignatureTypeCode.Object => typeof(object).FullName ?? "System.Object",
            _ => "",
        };
    }

    private static string ResolveGenericTypeInstance(MetadataReader reader, ref BlobReader blobReader)
    {
        var genericKind = blobReader.ReadSignatureTypeCode();
        if (genericKind != SignatureTypeCode.TypeHandle)
            return "";

        var typeHandle = MetadataTokens.EntityHandle(blobReader.ReadCompressedInteger());
        var genericTypeFullName = ResolveTypeFullName(reader, typeHandle);
        _ = blobReader.ReadCompressedInteger();
        return genericTypeFullName;
    }

    private static string BuildFullName(MetadataReader reader, TypeDefinition definition)
    {
        return BuildFullName(reader, definition, default);
    }

    private static string BuildFullName(
        MetadataReader reader,
        TypeDefinition definition,
        TypeDefinitionHandle handle)
    {
        if (!handle.IsNil)
        {
            var declaringType = definition.GetDeclaringType();
            if (!declaringType.IsNil)
            {
                var parent = reader.GetTypeDefinition(declaringType);
                return $"{BuildFullName(reader, parent, declaringType)}.{reader.GetString(definition.Name)}";
            }
        }

        var ns = reader.GetString(definition.Namespace);
        var name = reader.GetString(definition.Name);
        return string.IsNullOrWhiteSpace(ns) ? name : $"{ns}.{name}";
    }

    private static string BuildFullName(MetadataReader reader, TypeReference reference)
    {
        var ns = reader.GetString(reference.Namespace);
        var name = reader.GetString(reference.Name);
        return string.IsNullOrWhiteSpace(ns) ? name : $"{ns}.{name}";
    }

    private static TypeMetadata CreateBuiltInMetadata(string alias, string fullName)
    {
        return new TypeMetadata
        {
            TypeName = alias,
            TypeFullName = fullName,
            IsNumeric = alias is "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or "float" or "double" or "decimal",
            IsBool = alias == "bool",
            IsString = alias == "string",
        };
    }

    private void IndexConstants(MetadataReader reader, TypeDefinition definition, string fullTypeName)
    {
        foreach (var fieldHandle in definition.GetFields())
        {
            var field = reader.GetFieldDefinition(fieldHandle);
            if ((field.Attributes & System.Reflection.FieldAttributes.Literal) == 0)
                continue;

            var constantValue = ReadConstantValue(reader, field.GetDefaultValue());
            if (string.IsNullOrEmpty(constantValue))
                continue;

            var fieldName = reader.GetString(field.Name);
            if (string.IsNullOrWhiteSpace(fieldName))
                continue;

            foreach (var candidateTypeName in GetTypeNameSuffixes(fullTypeName))
                AddConstant(candidateTypeName, fieldName, constantValue);
        }
    }

    private void AddConstant(string typeName, string fieldName, string value)
    {
        _constantValuesByExpression[$"{typeName}.{fieldName}"] = value;
    }

    private static IEnumerable<string> GetTypeNameSuffixes(string fullTypeName)
    {
        var parts = fullTypeName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
            yield return string.Join('.', parts.Skip(i));
    }

    private bool TryResolveRuntimeStaticField(string expression, out string value)
    {
        value = "";
        var lastDotIndex = expression.LastIndexOf('.');
        if (lastDotIndex <= 0 || lastDotIndex >= expression.Length - 1)
            return false;

        var typeExpression = expression[..lastDotIndex];
        var fieldName = expression[(lastDotIndex + 1)..];

        foreach (var assemblyPath in _indexedAssemblyPaths)
        {
            try
            {
                var assembly = System.Reflection.Assembly.LoadFrom(assemblyPath);
                var field = assembly
                    .GetTypes()
                    .Select(type => new
                    {
                        Type = type,
                        NormalizedName = (type.FullName ?? type.Name).Replace('+', '.'),
                    })
                    .Where(item =>
                        string.Equals(item.NormalizedName, typeExpression, StringComparison.Ordinal) ||
                        item.NormalizedName.EndsWith($".{typeExpression}", StringComparison.Ordinal))
                    .Select(item => item.Type.GetField(
                        fieldName,
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.FlattenHierarchy))
                    .FirstOrDefault(fieldInfo => fieldInfo is not null);

                if (field is null)
                    continue;

                var fieldValue = field.GetValue(null);
                if (fieldValue is null)
                    return false;

                value = fieldValue is string text ? text : fieldValue.ToString() ?? "";
                return !string.IsNullOrEmpty(value);
            }
            catch
            {
                // Some game assemblies may require runtime dependencies. Metadata parsing remains the safe fallback.
            }
        }

        return false;
    }

    private static string? FindProjectRoot(string startDirectory)
    {
        var directory = startDirectory;
        for (var i = 0; i < 10 && !string.IsNullOrWhiteSpace(directory); i++)
        {
            if (Directory.GetFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0)
                return directory;

            directory = Directory.GetParent(directory)?.FullName ?? "";
        }

        return null;
    }
}
