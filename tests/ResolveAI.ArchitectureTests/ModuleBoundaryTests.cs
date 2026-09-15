using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.EntityFrameworkCore.Migrations;
using ResolveAI.Audit.Contracts;
using ResolveAI.CaseManagement.Contracts;
using ResolveAI.Platform;
using ResolveAI.Execution.Contracts;
using ResolveAI.Integrations.DependencyInjection;
using ResolveAI.AI.Contracts;

namespace ResolveAI.ArchitectureTests;

public sealed class ModuleBoundaryTests
{
    private static readonly Assembly[] Modules =
        [typeof(ActorContext).Assembly, typeof(ICaseService).Assembly, typeof(IAuditWriter).Assembly,
            typeof(IBusinessReads).Assembly, typeof(SimulationModule).Assembly, typeof(IProposals).Assembly];

    [Fact]
    public void Module_references_follow_the_allowed_acyclic_graph()
    {
        var allowed = new Dictionary<string, string[]>
        {
            ["ResolveAI.Platform"] = [],
            ["ResolveAI.Audit"] = ["ResolveAI.Platform"],
            ["ResolveAI.Execution"] = ["ResolveAI.Platform"],
            ["ResolveAI.AI"] = ["ResolveAI.Platform", "ResolveAI.Execution", "ResolveAI.Audit"],
            ["ResolveAI.Integrations"] = ["ResolveAI.Execution", "ResolveAI.Platform"],
            ["ResolveAI.CaseManagement"] = ["ResolveAI.Platform", "ResolveAI.Audit"]
        };
        foreach (var assembly in Modules)
            foreach (var dependency in assembly.GetReferencedAssemblies().Where(x => x.Name!.StartsWith("ResolveAI.", StringComparison.Ordinal)))
                Assert.Contains(dependency.Name!, allowed[assembly.GetName().Name!]);
    }

    [Fact]
    public void Modules_consume_only_foreign_contracts_and_never_HTTP_or_provider_types()
    {
        foreach (var assembly in Modules)
        {
            using var stream = File.OpenRead(assembly.Location);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            foreach (var handle in metadata.TypeReferences)
            {
                var reference = metadata.GetTypeReference(handle);
                var ns = metadata.GetString(reference.Namespace);
                Assert.False(ns.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal), $"HTTP dependency in {assembly.GetName().Name}: {ns}");
                if (assembly != typeof(IProposals).Assembly)
                    Assert.False(ns.StartsWith("OpenAI", StringComparison.Ordinal), $"Provider dependency: {ns}");
                if (reference.ResolutionScope.Kind != HandleKind.AssemblyReference) continue;
                var owner = metadata.GetString(metadata.GetAssemblyReference((AssemblyReferenceHandle)reference.ResolutionScope).Name);
                if (owner.StartsWith("ResolveAI.", StringComparison.Ordinal) && owner != "ResolveAI.Platform")
                    Assert.Equal(owner + ".Contracts", ns);
            }
        }
    }

    [Fact]
    public void Module_implementation_and_persistence_are_internal()
    {
        // EF-generated migration entry points are public for tooling; runtime implementation is private.
        foreach (var type in Modules.SelectMany(x => x.GetExportedTypes()).Where(x => !typeof(Migration).IsAssignableFrom(x)))
            Assert.DoesNotContain(type.Namespace?.Split('.') ?? [], x => x is "Application" or "Persistence" or "Domain" or "Infrastructure");
    }

    [Fact]
    public void AI_public_surface_does_not_expose_provider_SDK_types()
    {
        foreach (var type in typeof(IProposals).Assembly.GetExportedTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                Check(method.ReturnType);
                foreach (var parameter in method.GetParameters()) Check(parameter.ParameterType);
            }
            foreach (var constructor in type.GetConstructors())
                foreach (var parameter in constructor.GetParameters()) Check(parameter.ParameterType);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)) Check(field.FieldType);
        }

        static void Check(Type type)
        {
            Assert.False(type.Namespace?.StartsWith("OpenAI", StringComparison.Ordinal) == true ||
                type.Namespace?.StartsWith("System.ClientModel", StringComparison.Ordinal) == true, type.FullName);
            if (type.HasElementType) Check(type.GetElementType()!);
            foreach (var argument in type.GetGenericArguments()) Check(argument);
        }
    }
}
