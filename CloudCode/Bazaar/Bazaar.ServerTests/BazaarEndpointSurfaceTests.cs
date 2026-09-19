using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace MyriadOfDragons.CloudCode.Bazaar.Tests;

/// <summary>
/// Pins the module's deployed callable surface. The reconciliation sweep is a LIBRARY class on purpose:
/// how/when/by-whom it is invoked (endpoint, scheduler, support tool) is an open owner decision, so no
/// entry point for it may exist in the real module. The nonprod validation harness
/// (tools/bazaar_nonprod_validation) deploys a temporary endpoint from a scratch COPY only; this test
/// fails the build if anything like it (or any other unreviewed CloudCodeFunction) lands in the repo module.
/// Adding a new endpoint must be a deliberate change to this list.
/// </summary>
public sealed class BazaarEndpointSurfaceTests
{
    private static readonly string[] ApprovedEndpoints =
    {
        "BuyBazaarItem", "CancelBazaarListing", "GetBazaarWallet", "ListBazaarItem", "QueryBazaarListings",
    };

    private static string[] DeclaredEndpoints() =>
        typeof(BazaarOperations).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.CustomAttributes.Where(a => a.AttributeType.Name == "CloudCodeFunctionAttribute"))
            .Select(a => (string)a.ConstructorArguments[0].Value!)
            .OrderBy(n => n)
            .ToArray();

    [Test]
    public void Module_ExposesExactlyTheApprovedEndpoints()
    {
        Assert.That(DeclaredEndpoints(), Is.EqualTo(ApprovedEndpoints),
            "An unreviewed CloudCodeFunction was added to (or removed from) the Bazaar module. New entry points - especially for the reconciliation sweep - need an owner decision first.");
    }

    [Test]
    public void NoEndpointIsNamedLikeATemporaryValidationHook()
    {
        Assert.That(DeclaredEndpoints().Any(n => n.StartsWith("Temp") || n.Contains("Validate")), Is.False);
    }

    [Test]
    public void ReconciliationSweep_IsALibraryClass_NotACloudCodeEntryPoint()
    {
        bool anyAttribute = typeof(BazaarReconciliationSweep).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(m => m.CustomAttributes.Any(a => a.AttributeType.Name == "CloudCodeFunctionAttribute"));
        Assert.That(anyAttribute, Is.False);
    }
}
